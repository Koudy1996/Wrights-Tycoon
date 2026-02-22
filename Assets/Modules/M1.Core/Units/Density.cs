using WrightTycoon.Core.Math;

namespace WrightTycoon.Core.Units;

public readonly record struct Density
{
    public Density(double kilogramsPerCubicMeter)
    {
        Numeric.ThrowIfNegative(kilogramsPerCubicMeter, nameof(kilogramsPerCubicMeter));
        KilogramsPerCubicMeter = kilogramsPerCubicMeter;
    }

    public double KilogramsPerCubicMeter { get; }

    public static Density Zero => new(0d);

    public static Density FromKilogramsPerCubicMeter(double kilogramsPerCubicMeter) => new(kilogramsPerCubicMeter);

    public override string ToString() => $"{KilogramsPerCubicMeter:0.###} kg/m^3";
}
