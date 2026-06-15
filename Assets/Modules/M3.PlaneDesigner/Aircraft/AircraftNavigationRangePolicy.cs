using System;
using WrightTycoon.Core.Math;

namespace WrightTycoon.Core.Aircraft;

public static class AircraftNavigationRangePolicy
{
    public const double NoNavigationMaximumCertifiedRangeKilometers = 1500d;
    public const double BasicNavigationMaximumCertifiedRangeKilometers = 4000d;

    public static double GetMaximumCertifiedRangeKilometers(AircraftNavigationEquipment navigationEquipment)
    {
        return navigationEquipment switch
        {
            AircraftNavigationEquipment.None => NoNavigationMaximumCertifiedRangeKilometers,
            AircraftNavigationEquipment.BasicNavigationSuite => BasicNavigationMaximumCertifiedRangeKilometers,
            AircraftNavigationEquipment.AdvancedNavigationSuite => double.PositiveInfinity,
            _ => throw new ArgumentOutOfRangeException(nameof(navigationEquipment), "Navigation equipment must be a defined value."),
        };
    }

    public static double GetCertifiedRangeKilometers(
        AircraftNavigationEquipment navigationEquipment,
        double physicalRangeKilometers)
    {
        Numeric.ThrowIfNotFinite(physicalRangeKilometers, nameof(physicalRangeKilometers));
        if (physicalRangeKilometers < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(physicalRangeKilometers), "Physical range must be >= 0.");
        }

        var maximumCertifiedRange = GetMaximumCertifiedRangeKilometers(navigationEquipment);
        return double.IsPositiveInfinity(maximumCertifiedRange)
            ? physicalRangeKilometers
            : System.Math.Min(physicalRangeKilometers, maximumCertifiedRange);
    }

    public static bool IsRangeCapped(
        AircraftNavigationEquipment navigationEquipment,
        double physicalRangeKilometers)
    {
        return GetCertifiedRangeKilometers(navigationEquipment, physicalRangeKilometers)
            < physicalRangeKilometers - Numeric.Epsilon;
    }
}
