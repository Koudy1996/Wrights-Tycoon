using System;
using WrightTycoon.Core.Categories;
using WrightTycoon.Core.Fleet;

namespace WrightTycoon.Core.Contracts;

public static class LifecycleContractPolicy
{
    public const int CurrentLifecycleModelVersion = 2;
    public static System.Collections.Generic.IReadOnlyList<int> SupportOptionsMonths { get; } = Array.AsReadOnly(new[] { 60, 120, 180, 240 });
    public static System.Collections.Generic.IReadOnlyList<int> ServiceIntervalsHours => LifecycleBalance.ServiceIntervalsHours;
    public static double ReferenceUnitProductionCost(AircraftCategory category) =>
        new ContractPricingCalculator().EstimateMinimumUnitProductionCost(new ContractRequirements { RequiredCategory = category, MinimumReliability = 0.5d });
    private static double _purchasePriceShare = 0.25d;
    public static double PurchasePriceShare
    {
        get => _purchasePriceShare;
        set
        {
            LifecycleCalculator.RequireFiniteNonNegative(value, nameof(value));
            if (value > 1d) throw new ArgumentOutOfRangeException(nameof(value));
            _purchasePriceShare = value;
        }
    }
    public static double RequiredLife(AircraftCategory category) => LifecycleBalance.ReferenceLifeHours(category) * 0.6d;
    public static double ReferenceFuelPrice(AircraftCategory category) => category is AircraftCategory.LightSportAndRecreational
        or AircraftCategory.LightMultiRolePiston or AircraftCategory.LightCargo or AircraftCategory.SpecialCivil
            ? LifecycleCostCalculator.AviationGasolinePricePerKilogram : LifecycleCostCalculator.DefaultFuelPricePerKilogram;
    public static OperatingProfile Profile(AircraftCategory category) => category switch
    {
        AircraftCategory.LightSportAndRecreational => new OperatingProfile("general-aviation", 30d, 1d),
        AircraftCategory.MilitaryTactical or AircraftCategory.MilitaryStrategicSupport or AircraftCategory.Bomber => new OperatingProfile("military", 80d, 1.5d),
        AircraftCategory.TurbopropRegional or AircraftCategory.RegionalJet => new OperatingProfile("airline", 220d, 1.5d),
        AircraftCategory.MediumTransportNarrowBody or AircraftCategory.HeavyTransportWideBody or AircraftCategory.LongRangeCargo => new OperatingProfile("airline", 300d, 4d),
        _ => new OperatingProfile("general-aviation", 100d, 2d),
    };
    public static double OperatingBudget(AircraftCategory category, double unitCost)
        => ReferenceOperationQuote(category, unitCost).OperationCosts;

    /// <summary>Stable category estimate, not a selected design: 70% airframe, 30% installed engines,
    /// two reference engines at 80% reliability and current parts/replacement recipes. Demand and generation share it.</summary>
    public static LifecycleCostQuote ReferenceOperationQuote(AircraftCategory category, double unitCost,
        double? lifeHours = null, int serviceIntervalHours = 1000, int supportMonths = 120,
        double? purchasePrice = null, double? fuelBurnKilogramsPerHour = null)
    {
        LifecycleCalculator.RequireFiniteNonNegative(unitCost, nameof(unitCost));
        var fuel = WrightTycoon.Core.Balance.ContractBalance.ResolveFuelConsumptionRequirementRangeKilogramsPerHour(category);
        var service = ReferenceServiceOffer(unitCost, serviceIntervalHours, supportMonths);
        return LifecycleCostCalculator.Calculate(lifeHours ?? RequiredLife(category), purchasePrice ?? unitCost * 2d,
            fuelBurnKilogramsPerHour ?? fuel.Item2, ReferenceFuelPrice(category), service, Profile(category).UtilizationHoursPerMonth);
    }

    /// <summary>Market-estimate terms only; delivered aircraft must use their actual installed engine and parts groups.</summary>
    public static ServiceOfferSnapshot ReferenceServiceOffer(double unitCost, int serviceIntervalHours = 1000, int supportMonths = 120)
    {
        LifecycleCalculator.RequireFiniteNonNegative(unitCost, nameof(unitCost));
        const double reliability = 0.8d;
        const int engineCount = 2;
        var engineCost = unitCost * 0.3d / engineCount;
        var engineLife = (int)(System.Math.Round(EngineMaintenancePolicy.ReferenceReplacementLifeHours
            * (EngineMaintenancePolicy.MinimumReliabilityLifetimeFactor + EngineMaintenancePolicy.ReliabilityLifetimeFactor * reliability)
            / EngineMaintenancePolicy.ReplacementLifeStepHours, MidpointRounding.AwayFromZero) * EngineMaintenancePolicy.ReplacementLifeStepHours);
        var engine = new EngineMaintenanceSpecification("reference-engine", "reference-engine", reliability,
            EngineMaintenancePolicy.DefaultServiceIntervalHours, engineLife, FleetServiceBalance.RequiredSpareParts(reliability),
            engineCost * EngineMaintenancePolicy.EnginePartProductionCostFraction / (1d - EngineMaintenancePolicy.EnginePartContributionFraction),
            engineCost * EngineMaintenancePolicy.ReplacementPriceMultiplier);
        var quantity = FleetServiceBalance.RequiredSpareParts(reliability);
        var unitPrice = unitCost * 0.7d * FleetServiceBalance.AirframeSparePartFraction / (1d - FleetServiceBalance.TargetContributionFraction);
        var service = new ServiceOfferSnapshot(serviceIntervalHours, supportMonths, quantity * unitPrice, 0d, 0d)
        {
            SparePartsPerService = quantity, SparePartUnitPrice = unitPrice,
            AirframePartsGroupId = "reference-airframe", EngineCount = engineCount, EngineMaintenance = engine,
        };
        service.Validate();
        return service;
    }
    public static void Validate(double life, int interval, int support, double utilization, double flight, string profile, double budget)
    {
        LifecycleCalculator.RequireFiniteNonNegative(life, nameof(life));
        LifecycleCalculator.RequireFiniteNonNegative(budget, nameof(budget));
        LifecycleCalculator.ValidateServiceInterval(interval);
        new OperatingProfile(profile, utilization, flight).Validate();
        if (life <= 0d || support <= 0 || utilization <= 0d || budget <= 0d) throw new ArgumentOutOfRangeException(nameof(life));
    }
}
