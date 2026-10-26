using System;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Campaign;
using WrightTycoon.Core.Categories;
using WrightTycoon.Core.Fleet;
using WrightTycoon.Core.Simulation;

namespace WrightTycoon.Core.Contracts;

public enum OperationalCostPriority { PurchasePrice, Balanced, OperationalCosts }

public static class ContractPricingBalance
{
    public const double OverheadMultiplier = 1.15d;
    public const double MarkupMultiplier = 1.25d;
    public static double QuantityMultiplier(int quantity) => quantity >= 150 ? .94d : quantity >= 50 ? .97d : 1d;
    public static double OperatingTolerance(OperationalCostPriority priority) => priority switch
    {
        OperationalCostPriority.PurchasePrice => 1.35d,
        OperationalCostPriority.Balanced => 1.20d,
        OperationalCostPriority.OperationalCosts => 1.10d,
        _ => throw new ArgumentOutOfRangeException(nameof(priority)),
    };
    public static OperationalCostPriority Priority(ProcurementPreference preference) =>
        preference == ProcurementPreference.PurchasePrice ? OperationalCostPriority.PurchasePrice : OperationalCostPriority.OperationalCosts;
    public static double RoundCurrency(double value) => System.Math.Round(value, 2, MidpointRounding.AwayFromZero);
    public static double PurchasePrice(double directCost, int quantity, double reputation = 1d, double historicalDemand = 1d)
    {
        foreach (var value in new[] { directCost, reputation, historicalDemand })
        {
            LifecycleCalculator.RequireFiniteNonNegative(value, nameof(PurchasePrice));
            if (value <= 0d) throw new ArgumentOutOfRangeException(nameof(PurchasePrice));
        }
        if (quantity < 1) throw new ArgumentOutOfRangeException(nameof(quantity));
        return RoundCurrency(directCost * OverheadMultiplier * MarkupMultiplier * QuantityMultiplier(quantity) * reputation * historicalDemand);
    }
}

public sealed record ContractReferenceEstimate(double AirframeMaterials, double AirframeManDays, int EngineCount,
    double EngineMaterialsPerEngine, double EngineManDaysPerEngine, bool UsesAviationGasoline,
    double FuelBurnKilogramsPerHour, double Reliability)
{
    public double AirframeDirectCost => ProductionEconomyBalance.CalculateCompleteCost(AirframeMaterials, AirframeManDays);
    public double EngineDirectCost => ProductionEconomyBalance.CalculateCompleteCost(EngineMaterialsPerEngine, EngineManDaysPerEngine);
    public double DirectUnitCost => AirframeDirectCost + EngineCount * EngineDirectCost;

    public ServiceOfferSnapshot ServiceOffer(int interval, int supportMonths)
    {
        var quantity = FleetServiceBalance.RequiredSpareParts(Reliability);
        var partPrice = AirframeDirectCost * FleetServiceBalance.AirframeSparePartFraction / (1d - FleetServiceBalance.TargetContributionFraction);
        var engineLife = (int)(System.Math.Round(EngineMaintenancePolicy.ReferenceReplacementLifeHours
            * (EngineMaintenancePolicy.MinimumReliabilityLifetimeFactor + EngineMaintenancePolicy.ReliabilityLifetimeFactor * Reliability)
            / EngineMaintenancePolicy.ReplacementLifeStepHours, MidpointRounding.AwayFromZero) * EngineMaintenancePolicy.ReplacementLifeStepHours);
        return new ServiceOfferSnapshot(interval, supportMonths, quantity * partPrice, 0d, 0d)
        {
            SparePartsPerService = quantity, SparePartUnitPrice = partPrice, AirframePartsGroupId = "reference-airframe",
            EngineCount = EngineCount,
            EngineMaintenance = new EngineMaintenanceSpecification("reference-engine", "reference-engine", Reliability,
                EngineMaintenancePolicy.DefaultServiceIntervalHours, engineLife, quantity,
                EngineDirectCost * EngineMaintenancePolicy.EnginePartProductionCostFraction / (1d - EngineMaintenancePolicy.EnginePartContributionFraction),
                EngineDirectCost * EngineMaintenancePolicy.ReplacementPriceMultiplier),
        };
    }

    public void Validate()
    {
        foreach (var value in new[] { AirframeMaterials, AirframeManDays, EngineMaterialsPerEngine,
            EngineManDaysPerEngine, FuelBurnKilogramsPerHour, Reliability })
            LifecycleCalculator.RequireFiniteNonNegative(value, nameof(ContractReferenceEstimate));
        if (EngineCount < 1 || EngineCount > 6 || DirectUnitCost <= 0d || Reliability <= 0d)
            throw new ArgumentOutOfRangeException(nameof(ContractReferenceEstimate));
    }
}

public sealed class ReferenceProductionCostEstimator
{
    public ContractReferenceEstimate Estimate(ContractRequirements requirements, SimulationDate date)
    {
        var profile = AircraftCategoryCatalog.GetProfileForYear(requirements.RequiredCategory ?? AircraftCategory.LightMultiRolePiston, date.Year);
        var difficulty = ContractPricingCalculator.CalculateDifficultyNormalized(requirements, profile);
        var range = ContractBalance.ResolveReferenceStructuralIndexRange(profile.Category);
        var reference = ContractPricingCalculator.CalculateReferenceEstimate(profile,
            range.Min + (range.Max - range.Min) * difficulty, difficulty);
        var tech = new HistoricalCampaignTechnologyProgression().ResolveForDate(date);
        var fuelRange = ContractBalance.ResolveFuelConsumptionRequirementRangeKilogramsPerHour(profile.Category);
        var fuel = (fuelRange.Item1 + (fuelRange.Item2 - fuelRange.Item1) * difficulty)
            / (reference.UsesAviationGasoline ? tech.PistonFuelEfficiencyMultiplier : tech.FuelEfficiencyMultiplier);
        reference = reference with
        {
            AirframeMaterials = reference.AirframeMaterials / tech.AirframeEfficiencyMultiplier,
            AirframeManDays = reference.AirframeManDays / tech.AirframeEfficiencyMultiplier,
            FuelBurnKilogramsPerHour = System.Math.Min(requirements.MaximumFuelConsumptionKilogramsPerHour ?? double.MaxValue, fuel),
            Reliability = System.Math.Max(.8d, requirements.MinimumReliability ?? .8d),
        };
        if (reference.DirectUnitCost < ContractBalance.PricingMinimumUnitCostFloor)
            reference = reference with { AirframeMaterials = reference.AirframeMaterials + ContractBalance.PricingMinimumUnitCostFloor - reference.DirectUnitCost };
        reference.Validate();
        return reference;
    }
}

public static class ContractOperatingBudgetCalculator
{
    public static double ReferenceCostPerHour(ContractReferenceEstimate reference, double purchasePrice,
        ContractRequirements requirements, double fuelPricePerKilogram) =>
        LifecycleCostCalculator.Calculate(requirements.MinimumExpectedLifeHours, purchasePrice,
            reference.FuelBurnKilogramsPerHour, fuelPricePerKilogram,
            reference.ServiceOffer(requirements.MinimumServiceIntervalHours, requirements.MinimumSupportMonths),
            requirements.UtilizationHoursPerMonth).OperationCostsPerHour;
}

/// <summary>Immutable offer-time data. Customer service expenditure never discounts the purchase price.</summary>
public sealed record ContractEconomicSnapshot(int ModelVersion, SimulationDate GeneratedOn, ContractReferenceEstimate Reference,
    double OverheadMultiplier, double MarkupMultiplier, double QuantityMultiplier, double ReputationMultiplier,
    double HistoricalDemandMultiplier, double OfferedUnitPrice, double ReferenceOperationCostPerHour,
    double AllowedOperationCostPerHour, OperationalCostPriority Priority, double ReferenceFuelPricePerKilogram)
{
    public double ReferenceDirectUnitCost => Reference.DirectUnitCost;
    public void Validate()
    {
        if (ModelVersion != 1 || Reference is null) throw new ArgumentOutOfRangeException(nameof(ModelVersion));
        Reference.Validate();
        foreach (var value in new[] { OverheadMultiplier, MarkupMultiplier, QuantityMultiplier, ReputationMultiplier,
            HistoricalDemandMultiplier, OfferedUnitPrice, ReferenceOperationCostPerHour, AllowedOperationCostPerHour, ReferenceFuelPricePerKilogram })
        {
            LifecycleCalculator.RequireFiniteNonNegative(value, nameof(ContractEconomicSnapshot));
            if (value <= 0d) throw new ArgumentOutOfRangeException(nameof(ContractEconomicSnapshot));
        }
        var expectedPrice = ContractPricingBalance.RoundCurrency(ReferenceDirectUnitCost * OverheadMultiplier * MarkupMultiplier
            * QuantityMultiplier * ReputationMultiplier * HistoricalDemandMultiplier);
        if (System.Math.Abs(expectedPrice - OfferedUnitPrice) > .005d
            || System.Math.Abs(AllowedOperationCostPerHour - ReferenceOperationCostPerHour * ContractPricingBalance.OperatingTolerance(Priority)) > .000001d)
            throw new ArgumentException("Contract economic snapshot is inconsistent.");
    }
}
