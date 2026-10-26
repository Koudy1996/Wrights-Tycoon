using System;

namespace WrightTycoon.Core.Airframes;

/// <summary>Game design coefficients, inspired by real airfoil families, not measured airfoil polars.</summary>
public sealed record ModularWingProfile(
    string Name,
    string Inspiration,
    double MaximumLiftCoefficient,
    double ProfileDragCoefficient,
    double WingMassMultiplier,
    double WingCostMultiplier,
    double WingWorkMultiplier,
    double DesignLoadFactor,
    double ThicknessRatio);

public static class WingProfileCalculator
{
    private sealed record SpeedPoint(double Speed, string Name, string Inspiration, double Lift, double Drag,
        double Thickness, double Mass, double Cost);

    private static readonly SpeedPoint[] SpeedPoints =
    {
        new(100, "STOL", "NACA 4418", 2.10, .0200, .18, 1.10, 1.05),
        new(200, "Utility", "NACA 4415", 1.85, .0140, .15, .95, .95),
        new(350, "Universal", "NACA 23012", 1.60, .0100, .12, 1.00, 1.00),
        new(500, "Cruising", "NACA 63-412", 1.40, .0080, .12, 1.05, 1.15),
        new(750, "High-speed", "NACA 65-210", 1.20, .0075, .10, 1.15, 1.35),
        new(1000, "Transonic", "NASA SC(2)-0410", 1.05, .0085, .08, 1.30, 1.65),
        new(1400, "Supersonic", "NACA 64A204", .85, .0100, .05, 1.55, 2.10),
        new(2000, "Very-high-speed", "NACA 64A003", .65, .0120, .03, 1.90, 2.80),
    };

    // Columns: lift, drag, structural mass, materials/work, design load.
    private static readonly double[][] Orientation =
    {
        new[] { 1.25, 1.15, .90, .95, 2.5 },
        new[] { 1.12, 1.07, .95, .97, 3.0 },
        new[] { 1.00, 1.00, 1.00, 1.00, 3.8 },
        new[] { .94, .98, 1.10, 1.12, 4.8 },
        new[] { .88, 1.00, 1.25, 1.30, 6.0 },
    };

    // Columns: profile drag, structural mass, materials/work, lift, design load.
    // Cheaper manufacture must never provide a mass or performance advantage.
    private static readonly double[][] Efficiency =
    {
        new[] { 1.20, 1.12, .75, .90, .90 }, new[] { 1.10, 1.06, .87, .95, .95 },
        new[] { 1.00, 1.00, 1.00, 1.00, 1.00 },
        new[] { .90, .96, 1.25, 1.025, 1.025 }, new[] { .80, .92, 1.60, 1.05, 1.05 },
    };

    public static ModularWingProfile Calculate(AirframeParameters parameters)
    {
        if (parameters is null) throw new ArgumentNullException(nameof(parameters));
        ValidateInputs(parameters);
        var speed = parameters.WingDesignSpeedKph;
        var upper = 1;
        while (upper < SpeedPoints.Length - 1 && speed > SpeedPoints[upper].Speed) upper++;
        var a = SpeedPoints[upper - 1];
        var b = SpeedPoints[upper];
        var t = (speed - a.Speed) / (b.Speed - a.Speed);
        var label = t <= .5 ? a : b;
        var orientation = (parameters.WingPayloadAgility + 100d) / 50d;
        var efficiency = parameters.WingProfileEfficiency / 25d;
        // The reference is a real designation, not a claim that interpolated game
        // coefficients reproduce the exact section geometry or measured polars.
        var reference = parameters.WingPayloadAgility >= 75d && speed <= 600d
            ? "NACA 0012" : label.Inspiration;
        var name = $"{reference} — modified";
        var cost = Lerp(a.Cost, b.Cost, t) * Sample(Orientation, orientation, 3) * Sample(Efficiency, efficiency, 2);
        return new ModularWingProfile(name, reference,
            Lerp(a.Lift, b.Lift, t) * Sample(Orientation, orientation, 0) * Sample(Efficiency, efficiency, 3),
            Lerp(a.Drag, b.Drag, t) * Sample(Orientation, orientation, 1) * Sample(Efficiency, efficiency, 0),
            Lerp(a.Mass, b.Mass, t) * Sample(Orientation, orientation, 2) * Sample(Efficiency, efficiency, 1),
            cost, cost, Sample(Orientation, orientation, 4) * Sample(Efficiency, efficiency, 4),
            Lerp(a.Thickness, b.Thickness, t));
    }

    public static double CalculateSpeedDragMultiplier(double speedKph, double designSpeedKph)
    {
        if (!IsFinite(speedKph) || speedKph < 0) throw new ArgumentOutOfRangeException(nameof(speedKph));
        if (!IsFinite(designSpeedKph) || designSpeedKph < 100 || designSpeedKph > 2000)
            throw new ArgumentOutOfRangeException(nameof(designSpeedKph));
        var excess = System.Math.Max(0d, speedKph / designSpeedKph - 1d);
        return 1d + 4d * excess * excess;
    }

    public static void ValidateInputs(AirframeParameters parameters)
    {
        if (!IsFinite(parameters.WingDesignSpeedKph) || parameters.WingDesignSpeedKph < 100 || parameters.WingDesignSpeedKph > 2000)
            throw new ArgumentOutOfRangeException(nameof(parameters.WingDesignSpeedKph));
        if (!IsFinite(parameters.WingPayloadAgility) || parameters.WingPayloadAgility < -100 || parameters.WingPayloadAgility > 100)
            throw new ArgumentOutOfRangeException(nameof(parameters.WingPayloadAgility));
        if (!IsFinite(parameters.WingProfileEfficiency) || parameters.WingProfileEfficiency < 0 || parameters.WingProfileEfficiency > 100)
            throw new ArgumentOutOfRangeException(nameof(parameters.WingProfileEfficiency));
    }

    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    private static double Lerp(double a, double b, double t) => a + (b - a) * t;
    private static double Sample(double[][] table, double position, int column)
    {
        var lower = System.Math.Min(3, (int)System.Math.Floor(position));
        return Lerp(table[lower][column], table[lower + 1][column], position - lower);
    }
}
