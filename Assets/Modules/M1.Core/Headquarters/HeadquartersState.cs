using System;
using System.Collections.Generic;
using System.Linq;

namespace WrightTycoon.Core.Headquarters;

public sealed record HeadquartersMonthlyUpdate(
    double OperatingCost,
    int GeneratedSalesContracts,
    bool CompanyQualityLevelChanged,
    HeadquartersFacility? CompletedFacility,
    int CompletedFacilityLevel);

public sealed class HeadquartersState
{
    private readonly HashSet<string> _windTunnelResearchIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> _engineTestCellResearchIds = new(StringComparer.Ordinal);

    public CompanyResearchFundingLevel ResearchFundingLevel { get; private set; }

    public int CompanyResearchPoints { get; private set; }

    public int CompanyQualityLevel => HeadquartersBalance.GetCompanyQualityLevel(CompanyResearchPoints);

    public int CommercialDepartmentLevel { get; private set; }

    public double CommercialSalesProgress { get; private set; }

    public int CommercialGeneratedSequence { get; private set; }

    public int WindTunnelLevel { get; private set; }

    public int EngineTestCellLevel { get; private set; }

    public HeadquartersConstructionSnapshot? ActiveConstruction { get; private set; }

    public IReadOnlyCollection<string> WindTunnelResearchIds => _windTunnelResearchIds;

    public IReadOnlyCollection<string> EngineTestCellResearchIds => _engineTestCellResearchIds;

    public void SetResearchFundingLevel(CompanyResearchFundingLevel level)
    {
        if (!Enum.IsDefined(typeof(CompanyResearchFundingLevel), level))
        {
            throw new ArgumentOutOfRangeException(nameof(level));
        }

        ResearchFundingLevel = CompanyQualityLevel >= HeadquartersBalance.MaximumCompanyQualityLevel
            ? CompanyResearchFundingLevel.Paused
            : level;
    }

    public bool TryStartNextUpgrade(
        HeadquartersFacility facility,
        double availableCash,
        out HeadquartersUpgradeDefinition upgrade,
        out string message)
    {
        upgrade = default;
        if (ActiveConstruction is not null)
        {
            message = "Another headquarters upgrade is already under construction.";
            return false;
        }

        var currentLevel = GetFacilityLevel(facility);
        if (currentLevel >= HeadquartersBalance.GetMaximumFacilityLevel(facility))
        {
            message = $"{FormatFacilityName(facility)} is already at maximum level.";
            return false;
        }

        upgrade = HeadquartersBalance.GetNextUpgrade(facility, currentLevel);
        if (availableCash < upgrade.CapitalCost)
        {
            message = $"Insufficient cash for {FormatFacilityName(facility)} level {upgrade.TargetLevel}.";
            return false;
        }

        ActiveConstruction = new HeadquartersConstructionSnapshot
        {
            Facility = facility,
            TargetLevel = upgrade.TargetLevel,
            RemainingMonths = upgrade.ConstructionMonths,
        };
        message = $"Construction started: {FormatFacilityName(facility)} level {upgrade.TargetLevel}.";
        return true;
    }

    public HeadquartersMonthlyUpdate AdvanceMonth(IEnumerable<string>? completedResearchIds = null)
    {
        var operatingCost = GetMonthlyOperatingCost();
        if (completedResearchIds is not null)
        {
            foreach (var researchId in completedResearchIds)
            {
                if (string.IsNullOrWhiteSpace(researchId))
                {
                    continue;
                }

                _windTunnelResearchIds.Remove(researchId);
                _engineTestCellResearchIds.Remove(researchId);
            }
        }

        var previousQualityLevel = CompanyQualityLevel;
        if (previousQualityLevel < HeadquartersBalance.MaximumCompanyQualityLevel)
        {
            CompanyResearchPoints += HeadquartersBalance.GetResearchPointsPerMonth(ResearchFundingLevel);
            var maximumPoints = HeadquartersBalance.GetCompanyQualityPointThreshold(
                HeadquartersBalance.MaximumCompanyQualityLevel);
            CompanyResearchPoints = System.Math.Min(CompanyResearchPoints, maximumPoints);
            if (CompanyQualityLevel >= HeadquartersBalance.MaximumCompanyQualityLevel)
            {
                ResearchFundingLevel = CompanyResearchFundingLevel.Paused;
            }
        }

        CommercialSalesProgress += HeadquartersBalance.GetCommercialSalesProgressPerMonth(
            CommercialDepartmentLevel);
        var generatedSalesContracts = (int)System.Math.Floor(CommercialSalesProgress + 1e-9d);
        CommercialSalesProgress -= generatedSalesContracts;

        HeadquartersFacility? completedFacility = null;
        var completedFacilityLevel = 0;
        if (ActiveConstruction is not null)
        {
            ActiveConstruction.RemainingMonths = System.Math.Max(0, ActiveConstruction.RemainingMonths - 1);
            if (ActiveConstruction.RemainingMonths == 0)
            {
                SetFacilityLevel(ActiveConstruction.Facility, ActiveConstruction.TargetLevel);
                completedFacility = ActiveConstruction.Facility;
                completedFacilityLevel = ActiveConstruction.TargetLevel;
                ActiveConstruction = null;
            }
        }

        return new HeadquartersMonthlyUpdate(
            operatingCost,
            generatedSalesContracts,
            previousQualityLevel != CompanyQualityLevel,
            completedFacility,
            completedFacilityLevel);
    }

    public int ConsumeNextCommercialGeneratedSequence()
    {
        var sequence = CommercialGeneratedSequence;
        CommercialGeneratedSequence = checked(CommercialGeneratedSequence + 1);
        return sequence;
    }

    public double GetMonthlyOperatingCost()
    {
        var windTunnel = HeadquartersBalance.GetDevelopmentFacilityEffects(
            DevelopmentFacility.WindTunnel,
            WindTunnelLevel);
        var engineTestCell = HeadquartersBalance.GetDevelopmentFacilityEffects(
            DevelopmentFacility.EngineTestCell,
            EngineTestCellLevel);
        return HeadquartersBalance.GetResearchFundingMonthlyCost(ResearchFundingLevel)
            + HeadquartersBalance.GetCommercialMonthlyOperatingCost(CommercialDepartmentLevel)
            + windTunnel.MonthlyOperatingCost
            + engineTestCell.MonthlyOperatingCost;
    }

    public DevelopmentFacilityEffects GetDevelopmentFacilityEffects(DevelopmentFacility facility) =>
        HeadquartersBalance.GetDevelopmentFacilityEffects(
            facility,
            facility == DevelopmentFacility.WindTunnel ? WindTunnelLevel : EngineTestCellLevel);

    public bool CanAssignDevelopmentFacility(DevelopmentFacility facility, string? excludingResearchId = null)
    {
        var effects = GetDevelopmentFacilityEffects(facility);
        var assignments = GetAssignments(facility);
        var occupiedSlots = assignments.Count;
        if (!string.IsNullOrWhiteSpace(excludingResearchId) && assignments.Contains(excludingResearchId))
        {
            occupiedSlots--;
        }

        return effects.ProjectSlots > occupiedSlots;
    }

    public bool TryAssignDevelopmentFacility(
        DevelopmentFacility facility,
        string researchId,
        out string message)
    {
        if (string.IsNullOrWhiteSpace(researchId))
        {
            throw new ArgumentException("Research id cannot be empty.", nameof(researchId));
        }

        var normalizedResearchId = researchId.Trim();
        var assignments = GetAssignments(facility);
        if (assignments.Contains(normalizedResearchId))
        {
            message = "Research is already assigned to this facility.";
            return true;
        }

        if (!CanAssignDevelopmentFacility(facility))
        {
            message = $"No free {FormatDevelopmentFacilityName(facility)} slot is available.";
            return false;
        }

        assignments.Add(normalizedResearchId);
        message = $"Assigned to {FormatDevelopmentFacilityName(facility)}.";
        return true;
    }

    public void ReleaseDevelopmentFacility(string researchId)
    {
        if (string.IsNullOrWhiteSpace(researchId))
        {
            return;
        }

        _windTunnelResearchIds.Remove(researchId);
        _engineTestCellResearchIds.Remove(researchId);
    }

    public HeadquartersSnapshot CreateSnapshot() => new()
    {
        ResearchFundingLevel = ResearchFundingLevel,
        CompanyResearchPoints = CompanyResearchPoints,
        CommercialDepartmentLevel = CommercialDepartmentLevel,
        CommercialSalesProgress = CommercialSalesProgress,
        CommercialGeneratedSequence = CommercialGeneratedSequence,
        WindTunnelLevel = WindTunnelLevel,
        EngineTestCellLevel = EngineTestCellLevel,
        ActiveConstruction = ActiveConstruction is null
            ? null
            : new HeadquartersConstructionSnapshot
            {
                Facility = ActiveConstruction.Facility,
                TargetLevel = ActiveConstruction.TargetLevel,
                RemainingMonths = ActiveConstruction.RemainingMonths,
            },
        WindTunnelResearchIds = _windTunnelResearchIds.OrderBy(id => id, StringComparer.Ordinal).ToList(),
        EngineTestCellResearchIds = _engineTestCellResearchIds.OrderBy(id => id, StringComparer.Ordinal).ToList(),
    };

    public void RestoreSnapshot(HeadquartersSnapshot snapshot)
    {
        if (snapshot is null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        ResearchFundingLevel = Enum.IsDefined(typeof(CompanyResearchFundingLevel), snapshot.ResearchFundingLevel)
            ? snapshot.ResearchFundingLevel
            : CompanyResearchFundingLevel.Paused;
        CompanyResearchPoints = System.Math.Clamp(
            snapshot.CompanyResearchPoints,
            0,
            HeadquartersBalance.GetCompanyQualityPointThreshold(HeadquartersBalance.MaximumCompanyQualityLevel));
        CommercialDepartmentLevel = System.Math.Clamp(snapshot.CommercialDepartmentLevel, 0, 3);
        CommercialSalesProgress = double.IsFinite(snapshot.CommercialSalesProgress)
            ? System.Math.Clamp(snapshot.CommercialSalesProgress, 0d, 0.999999d)
            : 0d;
        CommercialGeneratedSequence = System.Math.Max(snapshot.CommercialGeneratedSequence, 0);
        WindTunnelLevel = System.Math.Clamp(snapshot.WindTunnelLevel, 0, 2);
        EngineTestCellLevel = System.Math.Clamp(snapshot.EngineTestCellLevel, 0, 2);
        ActiveConstruction = RestoreConstruction(snapshot.ActiveConstruction);

        _windTunnelResearchIds.Clear();
        RestoreAssignments(snapshot.WindTunnelResearchIds, _windTunnelResearchIds);
        _engineTestCellResearchIds.Clear();
        RestoreAssignments(snapshot.EngineTestCellResearchIds, _engineTestCellResearchIds);

        if (CompanyQualityLevel >= HeadquartersBalance.MaximumCompanyQualityLevel)
        {
            ResearchFundingLevel = CompanyResearchFundingLevel.Paused;
        }
    }

    private int GetFacilityLevel(HeadquartersFacility facility) => facility switch
    {
        HeadquartersFacility.CommercialDepartment => CommercialDepartmentLevel,
        HeadquartersFacility.WindTunnel => WindTunnelLevel,
        HeadquartersFacility.EngineTestCell => EngineTestCellLevel,
        _ => throw new ArgumentOutOfRangeException(nameof(facility)),
    };

    private void SetFacilityLevel(HeadquartersFacility facility, int level)
    {
        switch (facility)
        {
            case HeadquartersFacility.CommercialDepartment:
                CommercialDepartmentLevel = System.Math.Clamp(level, 0, 3);
                break;
            case HeadquartersFacility.WindTunnel:
                WindTunnelLevel = System.Math.Clamp(level, 0, 2);
                break;
            case HeadquartersFacility.EngineTestCell:
                EngineTestCellLevel = System.Math.Clamp(level, 0, 2);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(facility));
        }
    }

    private HashSet<string> GetAssignments(DevelopmentFacility facility) =>
        facility == DevelopmentFacility.WindTunnel
            ? _windTunnelResearchIds
            : _engineTestCellResearchIds;

    private static HeadquartersConstructionSnapshot? RestoreConstruction(HeadquartersConstructionSnapshot? snapshot)
    {
        if (snapshot is null
            || !Enum.IsDefined(typeof(HeadquartersFacility), snapshot.Facility)
            || snapshot.RemainingMonths <= 0)
        {
            return null;
        }

        var maximumLevel = HeadquartersBalance.GetMaximumFacilityLevel(snapshot.Facility);
        if (snapshot.TargetLevel < 1 || snapshot.TargetLevel > maximumLevel)
        {
            return null;
        }

        return new HeadquartersConstructionSnapshot
        {
            Facility = snapshot.Facility,
            TargetLevel = snapshot.TargetLevel,
            RemainingMonths = snapshot.RemainingMonths,
        };
    }

    private static void RestoreAssignments(IEnumerable<string>? source, HashSet<string> target)
    {
        if (source is null)
        {
            return;
        }

        foreach (var researchId in source)
        {
            if (!string.IsNullOrWhiteSpace(researchId))
            {
                target.Add(researchId.Trim());
            }
        }
    }

    private static string FormatFacilityName(HeadquartersFacility facility) => facility switch
    {
        HeadquartersFacility.CommercialDepartment => "Commercial Department",
        HeadquartersFacility.WindTunnel => "Wind Tunnel",
        HeadquartersFacility.EngineTestCell => "Engine Test Cell",
        _ => facility.ToString(),
    };

    private static string FormatDevelopmentFacilityName(DevelopmentFacility facility) =>
        facility == DevelopmentFacility.WindTunnel ? "Wind Tunnel" : "Engine Test Cell";
}
