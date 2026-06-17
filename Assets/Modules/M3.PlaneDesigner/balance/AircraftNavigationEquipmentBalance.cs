using WrightTycoon.Core.Aircraft;

namespace WrightTycoon.Core.Balance;

public static class AircraftNavigationEquipmentBalance
{
    public static AircraftNavigationEquipmentEffects GetEffects(AircraftNavigationEquipment navigationEquipment) =>
        navigationEquipment switch
        {
            AircraftNavigationEquipment.None => new AircraftNavigationEquipmentEffects(
                ExtraWeightKilograms: 0d,
                DragCoefficientDelta: 0.0000d,
                ReliabilityDelta: 0.000d,
                ProductionCostAdd: 0d,
                ProductionManDaysAdd: 0d),
            AircraftNavigationEquipment.BasicNavigationSuite => new AircraftNavigationEquipmentEffects(
                ExtraWeightKilograms: 45d,
                DragCoefficientDelta: 0.0004d,
                ReliabilityDelta: 0.010d,
                ProductionCostAdd: 18000d,
                ProductionManDaysAdd: 8d),
            AircraftNavigationEquipment.AdvancedNavigationSuite => new AircraftNavigationEquipmentEffects(
                ExtraWeightKilograms: 140d,
                DragCoefficientDelta: 0.0010d,
                ReliabilityDelta: 0.025d,
                ProductionCostAdd: 85000d,
                ProductionManDaysAdd: 24d),
            _ => GetEffects(AircraftNavigationEquipment.None),
        };
}

public readonly record struct AircraftNavigationEquipmentEffects(
    double ExtraWeightKilograms,
    double DragCoefficientDelta,
    double ReliabilityDelta,
    double ProductionCostAdd,
    double ProductionManDaysAdd);
