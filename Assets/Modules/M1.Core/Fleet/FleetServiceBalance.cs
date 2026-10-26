using System;
using WrightTycoon.Core.Aircraft;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Engines;
using WrightTycoon.Core.Research;

namespace WrightTycoon.Core.Fleet;

/// <summary>Physical airframe parts and their target contribution.</summary>
public static class FleetServiceBalance
{
    public const double TargetContributionFraction = .50d;
    public const double AirframeSparePartFraction = .01d;

    /// <summary>Full utilization of one smallest hand-built site; capital expenditure is not amortized.</summary>
    public static double ReferenceManufacturingCost(double materials, double manDays, bool engineFactory = false)
    {
        LifecycleCalculator.RequireFiniteNonNegative(materials, nameof(materials));
        LifecycleCalculator.RequireFiniteNonNegative(manDays, nameof(manDays));
        var monthlyMaintenance = FactoryBalance.GetBuildingMonthlyMaintenance(FactoryBuildingSize.Small) * (engineFactory ? .5d : 1d);
        var equipmentFee = FactoryBalance.GetEquipmentPurchaseCost(FactoryAutomationTier.HandBuilt, FactoryBuildingSize.Small)
            * FactoryBalance.GetMonthlyActiveEquipmentCostShare(FactoryAutomationTier.HandBuilt);
        var overheadPerManDay = (monthlyMaintenance + equipmentFee) / FactoryBalance.GetBuildingCapacityManDays(FactoryBuildingSize.Small);
        return materials + manDays * (ProductionEconomyBalance.LaborCostPerManDay + overheadPerManDay);
    }

    public static double SellingPrice(double referenceManufacturingCost)
    {
        LifecycleCalculator.RequireFiniteNonNegative(referenceManufacturingCost, nameof(referenceManufacturingCost));
        return referenceManufacturingCost / (1d - TargetContributionFraction);
    }

    public static double ReferenceAirframeManufacturingCost(Aircraft.Aircraft aircraft)
    {
        if (aircraft is null) throw new ArgumentNullException(nameof(aircraft));
        var research = TypeResearchModifierCalculator.CalculateCombined(aircraft.FamilyDefinition.ResearchModifiers,
            aircraft.TypeDefinition.ResearchModifiers);
        return ReferenceManufacturingCost(aircraft.Performance.AirframeProductionCost / research.ProductionCostMultiplier,
            aircraft.Performance.AirframeProductionManDays / research.ProductionManDaysMultiplier);
    }

    public static double ReferenceEngineManufacturingCost(Engine engine, TypeResearchModifierPoints? researchModifiers = null)
    {
        if (engine is null) throw new ArgumentNullException(nameof(engine));
        var research = TypeResearchModifierCalculator.Calculate(researchModifiers);
        return ReferenceManufacturingCost(engine.Performance.Cost / research.ProductionCostMultiplier,
            engine.Performance.ProductionManDays / research.ProductionManDaysMultiplier, engineFactory: true);
    }

    public static double AirframePartUnitPrice(Aircraft.Aircraft aircraft) =>
        SellingPrice(ReferenceAirframeManufacturingCost(aircraft) * AirframeSparePartFraction);

    public static double EnginePartUnitPrice(Engine engine, TypeResearchModifierPoints? researchModifiers = null) =>
        SellingPrice(ReferenceEngineManufacturingCost(engine, researchModifiers) * EngineMaintenancePolicy.EnginePartProductionCostFraction);

    public static double ReplacementEngineUnitPrice(Engine engine, TypeResearchModifierPoints? researchModifiers = null) =>
        SellingPrice(ReferenceEngineManufacturingCost(engine, researchModifiers));

    public static int RequiredSpareParts(double reliability)
    {
        LifecycleCalculator.ValidateReliability(reliability);
        return System.Math.Max(1, System.Math.Min(11,
            1 + (int)System.Math.Ceiling(10d * System.Math.Max(0d, 1d - reliability) - 1e-9d)));
    }
}
