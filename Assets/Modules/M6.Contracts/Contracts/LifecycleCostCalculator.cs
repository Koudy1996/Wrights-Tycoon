using System;
using WrightTycoon.Core.Fleet;

namespace WrightTycoon.Core.Contracts;

public enum ProcurementPreference { PurchasePrice, WholeLifeCost }

public sealed record LifecycleCostQuote(double PurchasePrice, double FuelCost, double ScheduledServiceCost,
    double RepairCost, double DowntimeCost, double SupportRiskCost, double ContractHours)
{
    public double OperatingCost => FuelCost + ScheduledServiceCost + RepairCost + DowntimeCost + SupportRiskCost;
    public double TotalCost => PurchasePrice + OperatingCost;
    /// <summary>Ownership amortization, fuel and physical aircraft/engine maintenance; excludes incidental risk and downtime.</summary>
    public double OperationCosts => PurchasePrice + FuelCost + ScheduledServiceCost;
    public double OperationCostsPerHour => ContractHours > 0d ? OperationCosts / ContractHours : 0d;
    public double OperatingCostPerHour => ContractHours > 0d ? OperatingCost / ContractHours : 0d;
}

/// <summary>Customer expenditure, never manufacturer revenue; all fuel amounts are kilograms.</summary>
public static class LifecycleCostCalculator
{
    public const double DefaultFuelPricePerKilogram = 0.65d;
    public const double AviationGasolinePricePerKilogram = 0.9d;
    public static double FuelPricePerKilogram(Aircraft.Aircraft aircraft) => aircraft.Engine.Definition.EngineType is
        WrightTycoon.Core.Engines.EngineType.Turbojet or WrightTycoon.Core.Engines.EngineType.Turbofan
            ? DefaultFuelPricePerKilogram : AviationGasolinePricePerKilogram;
    public static ServiceOfferSnapshot CreateServiceOffer(Aircraft.Aircraft aircraft, double? reliability = null) =>
        CreateEngineLifecycleServiceOffer(aircraft, reliability);

    public static ServiceOfferSnapshot CreateContractServiceOffer(Aircraft.Aircraft aircraft, ContractRequirements requirements,
        double? reliability = null) => CreateEngineLifecycleServiceOffer(aircraft, reliability) with
        { GuaranteedSupportMonths = requirements.LifecycleModelVersion > 0 ? requirements.MinimumSupportMonths : aircraft.TypeDefinition.GuaranteedSupportMonths };

    public static ServiceOfferSnapshot CreateEngineLifecycleServiceOffer(Aircraft.Aircraft aircraft, double? reliability = null,
        string? engineDesignId = null, string? airframeGroupId = null, string? engineGroupId = null)
    {
        var r = reliability ?? aircraft.Performance.OverallReliability;
        var engineId = engineDesignId ?? aircraft.Engine.Definition.Id;
        var quantity = FleetServiceBalance.RequiredSpareParts(r);
        var unitPrice = FleetServiceBalance.AirframePartUnitPrice(aircraft);
        var result = new ServiceOfferSnapshot(aircraft.TypeDefinition.ServiceIntervalHours, aircraft.TypeDefinition.GuaranteedSupportMonths,
            quantity * unitPrice, 0d, aircraft.Performance.CompleteProductionCost * System.Math.Max(0d, 1d - r) / 20000d)
        {
            MaintenanceModelVersion = 2, SparePartsPerService = quantity, SparePartUnitPrice = unitPrice,
            AirframePartsGroupId = airframeGroupId ?? aircraft.TypeDefinition.Id,
            EngineCount = aircraft.Airframe.Parameters.EngineCount,
            EngineMaintenance = EngineMaintenancePolicy.Create(aircraft.Engine, engineId, engineGroupId ?? engineId) with
            {
                PartUnitPrice = FleetServiceBalance.EnginePartUnitPrice(aircraft.Engine, aircraft.FamilyDefinition.InstalledEngineResearchModifiers),
                ReplacementUnitPrice = FleetServiceBalance.ReplacementEngineUnitPrice(aircraft.Engine, aircraft.FamilyDefinition.InstalledEngineResearchModifiers),
            },
        };
        result.Validate();
        return result;
    }

    public static double ExpectedOperatingLifeHours(double requestedLifeHours, double designLifeHours)
    {
        LifecycleCalculator.RequireFiniteNonNegative(requestedLifeHours, nameof(requestedLifeHours));
        LifecycleCalculator.RequireFiniteNonNegative(designLifeHours, nameof(designLifeHours));
        if (designLifeHours <= 0d) throw new ArgumentOutOfRangeException(nameof(designLifeHours));
        return requestedLifeHours > 0d ? requestedLifeHours * .5d + designLifeHours * .5d : designLifeHours;
    }

    public static LifecycleCostQuote QuoteForContract(Aircraft.Aircraft aircraft, ContractRequirements requirements,
        double purchasePrice, double? reliability = null) => Calculate(ExpectedOperatingLifeHours(requirements.MinimumExpectedLifeHours, LifecycleCalculator.CalculateExpectedLifeHours(aircraft.FamilyDefinition.Category,
                    reliability ?? aircraft.Performance.OverallReliability, aircraft.TypeDefinition.ServiceIntervalHours,
                    aircraft.TypeDefinition.ConstructionDurabilityLevel)),
            purchasePrice, aircraft.Performance.AverageCruiseFuelBurnKilogramsPerHour, FuelPricePerKilogram(aircraft),
            CreateContractServiceOffer(aircraft, requirements, reliability), requirements.UtilizationHoursPerMonth);

    public static LifecycleCostQuote Quote(Aircraft.Aircraft aircraft, double requiredLifeHours, double purchasePrice, double utilizationHoursPerMonth = 100d) =>
        Calculate(requiredLifeHours, purchasePrice, aircraft.Performance.AverageCruiseFuelBurnKilogramsPerHour,
            FuelPricePerKilogram(aircraft), CreateServiceOffer(aircraft), utilizationHoursPerMonth);

    public static LifecycleCostQuote Calculate(double hours, double purchasePrice, double fuelBurnKilogramsPerHour,
        double fuelPricePerKilogram, ServiceOfferSnapshot service, double utilizationHoursPerMonth = 100d)
    {
        foreach (var value in new[] { hours, purchasePrice, fuelBurnKilogramsPerHour, fuelPricePerKilogram, utilizationHoursPerMonth })
            LifecycleCalculator.RequireFiniteNonNegative(value, nameof(value));
        if (utilizationHoursPerMonth <= 0d) throw new ArgumentOutOfRangeException(nameof(utilizationHoursPerMonth));
        service.Validate();
        var engineCost = EngineOwnershipCostCalculator.Calculate(hours, service.EngineMaintenance!, service.EngineCount).TotalCost;
        return new LifecycleCostQuote(purchasePrice, hours * fuelBurnKilogramsPerHour * fuelPricePerKilogram,
            System.Math.Max(0d, System.Math.Ceiling(hours / service.ServiceIntervalHours) - 1d) * service.ServicePricePerEvent + engineCost,
            hours * service.ExpectedRepairCostPerHour, hours * service.DowntimeCostPerHour,
            0d,
            hours);
    }

    public static double ReputationMultiplier(Func<double>? provider)
    {
        var value = provider?.Invoke() ?? 1d;
        LifecycleCalculator.RequireFiniteNonNegative(value, nameof(provider));
        return System.Math.Max(0.85d, System.Math.Min(1.15d, value));
    }
}
