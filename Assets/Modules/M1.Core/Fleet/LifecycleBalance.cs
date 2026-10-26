using System;
using System.Collections.Generic;
using WrightTycoon.Core.Categories;

namespace WrightTycoon.Core.Fleet;

/// <summary>Game calibration, not historical accident rates or certification limits.</summary>
public static class LifecycleBalance
{
    public const double StructuralMassPerDurabilityLevel = 0.02d;
    public const double StructuralProductionPerDurabilityLevel = 0.035d;
    public const double StructuralDragPerDurabilityLevel = 0.008d;
    public const double LifetimePerDurabilityLevel = 0.08d;
    public const double DesignReferenceLifeHours = 30000d;
    public const double ReliabilityReference = 0.8d;
    public const double ReliabilityExponent = 1.5d;
    public const double ServiceExponent = 0.2d;
    public static IReadOnlyList<int> ServiceIntervalsHours { get; } = Array.AsReadOnly(new[] { 200, 500, 1000, 2000, 4000 });

    // Market/accident references remain independent of the universal aircraft design baseline.
    public static double ReferenceReliability(AircraftCategory category) => category switch
    {
        AircraftCategory.LightSportAndRecreational or AircraftCategory.MilitaryTactical => 0.75d,
        AircraftCategory.LightMultiRolePiston or AircraftCategory.LightCargo or AircraftCategory.SpecialCivil => 0.8d,
        AircraftCategory.TurbopropRegional or AircraftCategory.RegionalJet or AircraftCategory.BusinessVipJet => 0.85d,
        AircraftCategory.MediumTransportNarrowBody or AircraftCategory.HeavyTransportWideBody
            or AircraftCategory.MediumCargo or AircraftCategory.HeavyCargo or AircraftCategory.LongRangeCargo
            or AircraftCategory.MilitaryStrategicSupport or AircraftCategory.Bomber => 0.9d,
        _ => throw new ArgumentOutOfRangeException(nameof(category)),
    };

    public static double ReferenceLifeHours(AircraftCategory category) => category switch
    {
        AircraftCategory.LightSportAndRecreational => 12000d,
        AircraftCategory.LightMultiRolePiston or AircraftCategory.LightCargo => 18000d,
        AircraftCategory.TurbopropRegional or AircraftCategory.RegionalJet => 40000d,
        AircraftCategory.MediumTransportNarrowBody or AircraftCategory.MediumCargo => 60000d,
        AircraftCategory.HeavyTransportWideBody or AircraftCategory.HeavyCargo or AircraftCategory.LongRangeCargo => 80000d,
        AircraftCategory.BusinessVipJet => 30000d,
        AircraftCategory.SpecialCivil => 25000d,
        AircraftCategory.MilitaryTactical => 12000d,
        AircraftCategory.MilitaryStrategicSupport or AircraftCategory.Bomber => 30000d,
        _ => throw new ArgumentOutOfRangeException(nameof(category)),
    };
}
