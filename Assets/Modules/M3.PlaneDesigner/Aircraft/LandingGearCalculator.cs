using System;
using WrightTycoon.Core.Airframes;

namespace WrightTycoon.Core.Aircraft;

public static class LandingGearBalance
{
    public const double ReferenceGrossToDryMass = 1.60d;
    public static double MassFraction(LandingGearType type) => type switch
    {
        LandingGearType.Fixed => .032d,
        LandingGearType.SemiRetractable => .036d,
        LandingGearType.Retractable => .042d,
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };
    public static double CruiseArea(LandingGearType type) => type switch
    {
        LandingGearType.Fixed => .050d,
        LandingGearType.SemiRetractable => .025d,
        LandingGearType.Retractable => .004d,
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };
}

public sealed record LandingGearReport(double ReferenceMassKilograms, double MassKilograms,
    double FixedMassKilograms, double CruiseDragAreaSquareMeters, double ExtendedDragAreaSquareMeters)
{
    public double ExtraMassOverFixedKilograms => MassKilograms - FixedMassKilograms;
}

public static class LandingGearCalculator
{
    public static LandingGearReport Calculate(double referenceDryMassKilograms, LandingGearType type,
        double systemsTechnology, double bodyTechnology, double precisionMassMultiplier,
        double dragAdjustmentMultiplier, double durabilityMassMultiplier)
    {
        foreach (var value in new[] { referenceDryMassKilograms, systemsTechnology, bodyTechnology,
                     precisionMassMultiplier, dragAdjustmentMultiplier, durabilityMassMultiplier })
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0d)
                throw new ArgumentOutOfRangeException(nameof(referenceDryMassKilograms), "Gear inputs must be finite and positive.");
        var referenceMass = LandingGearBalance.ReferenceGrossToDryMass * referenceDryMassKilograms;
        var massScale = referenceMass * precisionMassMultiplier * durabilityMassMultiplier / systemsTechnology;
        var areaScale = System.Math.Pow(referenceMass / 1000d, 2d / 3d) * dragAdjustmentMultiplier / bodyTechnology;
        var report = new LandingGearReport(referenceMass, massScale * LandingGearBalance.MassFraction(type),
            massScale * LandingGearBalance.MassFraction(LandingGearType.Fixed),
            areaScale * LandingGearBalance.CruiseArea(type),
            areaScale * LandingGearBalance.CruiseArea(LandingGearType.Fixed));
        foreach (var value in new[] { report.ReferenceMassKilograms, report.MassKilograms,
                     report.FixedMassKilograms, report.CruiseDragAreaSquareMeters, report.ExtendedDragAreaSquareMeters })
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0d)
                throw new ArgumentOutOfRangeException(nameof(referenceDryMassKilograms), "Gear calculation exceeds the supported numeric range.");
        return report;
    }
}
