using WrightTycoon.Core.Aircraft;

namespace WrightTycoon.Core.Balance;

public static class AircraftConstructionStandardBalance
{
    public static AircraftConstructionStandardEffects GetEffects(AircraftConstructionStandard constructionStandard) =>
        constructionStandard switch
        {
            AircraftConstructionStandard.Ultralight => new AircraftConstructionStandardEffects(
                WeightMultiplier: 0.88d,
                ProductionCostMultiplier: 0.75d,
                ProductionManDaysMultiplier: 0.80d,
                DevelopmentCostMultiplier: 0.80d,
                DevelopmentManDaysMultiplier: 0.85d,
                ReliabilityDelta: -0.035d,
                ManeuverabilityMultiplier: 1.06d,
                DragMultiplier: 1.015d),
            AircraftConstructionStandard.Civil => new AircraftConstructionStandardEffects(
                WeightMultiplier: 1.00d,
                ProductionCostMultiplier: 1.00d,
                ProductionManDaysMultiplier: 1.00d,
                DevelopmentCostMultiplier: 1.00d,
                DevelopmentManDaysMultiplier: 1.00d,
                ReliabilityDelta: 0.000d,
                ManeuverabilityMultiplier: 1.00d,
                DragMultiplier: 1.000d),
            AircraftConstructionStandard.ArmyGrade => new AircraftConstructionStandardEffects(
                WeightMultiplier: 1.16d,
                ProductionCostMultiplier: 1.35d,
                ProductionManDaysMultiplier: 1.30d,
                DevelopmentCostMultiplier: 1.35d,
                DevelopmentManDaysMultiplier: 1.25d,
                ReliabilityDelta: 0.055d,
                ManeuverabilityMultiplier: 0.96d,
                DragMultiplier: 1.010d),
            _ => GetEffects(AircraftConstructionStandard.Civil),
        };
}

public readonly record struct AircraftConstructionStandardEffects(
    double WeightMultiplier,
    double ProductionCostMultiplier,
    double ProductionManDaysMultiplier,
    double DevelopmentCostMultiplier,
    double DevelopmentManDaysMultiplier,
    double ReliabilityDelta,
    double ManeuverabilityMultiplier,
    double DragMultiplier);
