using System;

namespace WrightTycoon.Core.Headquarters;

public enum CompanyResearchFundingLevel
{
    Paused = 0,
    Conservative = 1,
    Standard = 2,
    Intensive = 3,
}

public enum HeadquartersFacility
{
    CommercialDepartment = 0,
    WindTunnel = 1,
    EngineTestCell = 2,
}

public enum DevelopmentFacility
{
    WindTunnel = 0,
    EngineTestCell = 1,
}

public readonly record struct HeadquartersUpgradeDefinition(
    HeadquartersFacility Facility,
    int TargetLevel,
    double CapitalCost,
    int ConstructionMonths);

public readonly record struct DevelopmentFacilityEffects(
    int Level,
    int ProjectSlots,
    double ResearchCostMultiplier,
    double ResearchTimeMultiplier,
    double MonthlyOperatingCost);

public static class HeadquartersBalance
{
    public const int MaximumCompanyQualityLevel = 20;
    public const double ReliabilityBonusPerQualityLevel = 0.01d;
    public const double MaximumAircraftReliability = 0.995d;

    public static double GetResearchFundingMonthlyCost(CompanyResearchFundingLevel level) => level switch
    {
        CompanyResearchFundingLevel.Paused => 0d,
        CompanyResearchFundingLevel.Conservative => 500_000d,
        CompanyResearchFundingLevel.Standard => 1_250_000d,
        CompanyResearchFundingLevel.Intensive => 3_000_000d,
        _ => throw new ArgumentOutOfRangeException(nameof(level)),
    };

    public static int GetResearchPointsPerMonth(CompanyResearchFundingLevel level) => level switch
    {
        CompanyResearchFundingLevel.Paused => 0,
        CompanyResearchFundingLevel.Conservative => 1,
        CompanyResearchFundingLevel.Standard => 2,
        CompanyResearchFundingLevel.Intensive => 3,
        _ => throw new ArgumentOutOfRangeException(nameof(level)),
    };

    public static int GetCompanyQualityLevel(int researchPoints)
    {
        var normalizedPoints = System.Math.Max(researchPoints, 0);
        for (var level = MaximumCompanyQualityLevel; level >= 1; level--)
        {
            if (normalizedPoints >= GetCompanyQualityPointThreshold(level))
            {
                return level;
            }
        }

        return 0;
    }

    public static int GetCompanyQualityPointThreshold(int level)
    {
        if (level < 0 || level > MaximumCompanyQualityLevel)
        {
            throw new ArgumentOutOfRangeException(nameof(level));
        }

        // The original five-level curve was 3 * level * (level + 3).
        // Doubling it makes reaching 5% take twice as long while preserving
        // the same progressively slower cadence for all twenty levels.
        return checked(6 * level * (level + 3));
    }

    public static int MigrateLegacyCompanyResearchPoints(int legacyResearchPoints)
    {
        var maximumPoints = GetCompanyQualityPointThreshold(MaximumCompanyQualityLevel);
        return (int)System.Math.Clamp((long)System.Math.Max(legacyResearchPoints, 0) * 2L, 0L, maximumPoints);
    }

    public static double GetCompanyReliabilityBonus(int qualityLevel) =>
        System.Math.Clamp(qualityLevel, 0, MaximumCompanyQualityLevel) * ReliabilityBonusPerQualityLevel;

    public static double ApplyCompanyReliabilityBonus(double baseReliability, int qualityLevel) =>
        System.Math.Min(
            MaximumAircraftReliability,
            System.Math.Max(0d, baseReliability) + GetCompanyReliabilityBonus(qualityLevel));

    public static HeadquartersUpgradeDefinition GetNextUpgrade(HeadquartersFacility facility, int currentLevel)
    {
        var targetLevel = currentLevel + 1;
        return facility switch
        {
            HeadquartersFacility.CommercialDepartment => targetLevel switch
            {
                1 => new(facility, 1, 15_000_000d, 6),
                2 => new(facility, 2, 30_000_000d, 9),
                3 => new(facility, 3, 60_000_000d, 12),
                _ => throw new InvalidOperationException("Commercial Department is already at maximum level."),
            },
            HeadquartersFacility.WindTunnel => targetLevel switch
            {
                1 => new(facility, 1, 20_000_000d, 12),
                2 => new(facility, 2, 60_000_000d, 18),
                _ => throw new InvalidOperationException("Wind Tunnel is already at maximum level."),
            },
            HeadquartersFacility.EngineTestCell => targetLevel switch
            {
                1 => new(facility, 1, 10_000_000d, 12),
                2 => new(facility, 2, 30_000_000d, 18),
                _ => throw new InvalidOperationException("Engine Test Cell is already at maximum level."),
            },
            _ => throw new ArgumentOutOfRangeException(nameof(facility)),
        };
    }

    public static int GetMaximumFacilityLevel(HeadquartersFacility facility) =>
        facility == HeadquartersFacility.CommercialDepartment ? 3 : 2;

    public static double GetCommercialMonthlyOperatingCost(int level) => level switch
    {
        <= 0 => 0d,
        1 => 500_000d,
        2 => 1_250_000d,
        _ => 3_000_000d,
    };

    public static double GetCommercialSalesProgressPerMonth(int level) => level switch
    {
        <= 0 => 0d,
        1 => 0.25d,
        2 => 0.50d,
        _ => 1d,
    };

    public static DevelopmentFacilityEffects GetDevelopmentFacilityEffects(
        DevelopmentFacility facility,
        int level)
    {
        var normalizedLevel = System.Math.Clamp(level, 0, 2);
        if (normalizedLevel == 0)
        {
            return new DevelopmentFacilityEffects(0, 0, 1d, 1d, 0d);
        }

        var monthlyCostScale = facility == DevelopmentFacility.EngineTestCell ? 0.5d : 1d;
        return normalizedLevel == 1
            ? new DevelopmentFacilityEffects(1, 1, 0.92d, 0.88d, 150_000d * monthlyCostScale)
            : new DevelopmentFacilityEffects(2, 2, 0.85d, 0.75d, 450_000d * monthlyCostScale);
    }
}
