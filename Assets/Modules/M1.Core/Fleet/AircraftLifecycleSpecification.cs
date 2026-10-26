using System;
using WrightTycoon.Core.Categories;
using WrightTycoon.Core.Headquarters;

namespace WrightTycoon.Core.Fleet;

/// <summary>Value-only specification suitable for freezing at delivery; contains no live design links.</summary>
public sealed record AircraftLifecycleSpecification(
    AircraftCategory Category,
    double Reliability,
    int ConstructionDurabilityLevel,
    int ServiceIntervalHours,
    double ExpectedLifeHours,
    int GuaranteedSupportMonths)
{
    public static AircraftLifecycleSpecification FromAircraft(Aircraft.Aircraft aircraft, int companyQualityLevel = 0)
    {
        if (aircraft is null) throw new ArgumentNullException(nameof(aircraft));
        var type = aircraft.TypeDefinition;
        var reliability = HeadquartersBalance.ApplyCompanyReliabilityBonus(aircraft.Performance.OverallReliability, companyQualityLevel);
        var result = new AircraftLifecycleSpecification(aircraft.FamilyDefinition.Category,
            reliability, type.ConstructionDurabilityLevel,
            type.ServiceIntervalHours, LifecycleCalculator.CalculateExpectedLifeHours(aircraft.FamilyDefinition.Category,
                reliability, type.ServiceIntervalHours, type.ConstructionDurabilityLevel), type.GuaranteedSupportMonths);
        result.Validate();
        return result;
    }

    public void Validate()
    {
        LifecycleBalance.ReferenceLifeHours(Category);
        LifecycleCalculator.ValidateReliability(Reliability);
        LifecycleCalculator.ValidateDurability(ConstructionDurabilityLevel);
        LifecycleCalculator.ValidateServiceInterval(ServiceIntervalHours);
        LifecycleCalculator.RequireFiniteNonNegative(ExpectedLifeHours, nameof(ExpectedLifeHours));
        if (ExpectedLifeHours <= 0d) throw new ArgumentOutOfRangeException(nameof(ExpectedLifeHours));
        if (GuaranteedSupportMonths <= 0) throw new ArgumentOutOfRangeException(nameof(GuaranteedSupportMonths));
    }
}

public sealed record OperatingProfile(string Id, double UtilizationHoursPerMonth, double AverageFlightHours)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id)) throw new ArgumentException("Profile id is required.", nameof(Id));
        LifecycleCalculator.RequireFiniteNonNegative(UtilizationHoursPerMonth, nameof(UtilizationHoursPerMonth));
        LifecycleCalculator.RequireFiniteNonNegative(AverageFlightHours, nameof(AverageFlightHours));
        if (AverageFlightHours <= 0d) throw new ArgumentOutOfRangeException(nameof(AverageFlightHours));
    }
}

/// <summary>Frozen customer terms for airframe parts, engine parts, and exact-version replacement engines.</summary>
public sealed record ServiceOfferSnapshot(
    int ServiceIntervalHours,
    int GuaranteedSupportMonths,
    double ServicePricePerEvent,
    double ExpectedRepairCostPerHour,
    double DowntimeCostPerHour)
{
    public int MaintenanceModelVersion { get; init; } = 2;
    public int SparePartsPerService { get; init; }
    public double SparePartUnitPrice { get; init; }
    public string AirframePartsGroupId { get; init; } = string.Empty;
    public EngineMaintenanceSpecification? EngineMaintenance { get; init; }
    public int EngineCount { get; init; } = 1;

    public void Validate()
    {
        LifecycleCalculator.ValidateServiceInterval(ServiceIntervalHours);
        if (GuaranteedSupportMonths <= 0) throw new ArgumentOutOfRangeException(nameof(GuaranteedSupportMonths));
        LifecycleCalculator.RequireFiniteNonNegative(ServicePricePerEvent, nameof(ServicePricePerEvent));
        LifecycleCalculator.RequireFiniteNonNegative(ExpectedRepairCostPerHour, nameof(ExpectedRepairCostPerHour));
        LifecycleCalculator.RequireFiniteNonNegative(DowntimeCostPerHour, nameof(DowntimeCostPerHour));
        if (MaintenanceModelVersion != 2)
            throw new ArgumentOutOfRangeException(nameof(MaintenanceModelVersion));
        if (SparePartsPerService < 1 || SparePartsPerService > 11)
            throw new ArgumentOutOfRangeException(nameof(SparePartsPerService));
        LifecycleCalculator.RequireFiniteNonNegative(SparePartUnitPrice, nameof(SparePartUnitPrice));
        var expectedPrice = SparePartsPerService * SparePartUnitPrice;
        LifecycleCalculator.RequireFiniteNonNegative(expectedPrice, nameof(SparePartUnitPrice));
        if (System.Math.Abs(ServicePricePerEvent - expectedPrice) > System.Math.Max(1d, expectedPrice) * 1e-9d)
            throw new ArgumentException("Service price must equal the frozen spare-parts quantity times unit price.", nameof(ServicePricePerEvent));
        if (EngineCount < 1 || EngineCount > 6) throw new ArgumentOutOfRangeException(nameof(EngineCount));
        if (string.IsNullOrWhiteSpace(AirframePartsGroupId)) throw new ArgumentException("An airframe parts group is required.");
        if (EngineMaintenance == null) throw new ArgumentException("Frozen installed-engine maintenance terms are required.");
        EngineMaintenance.Validate();
    }
}
