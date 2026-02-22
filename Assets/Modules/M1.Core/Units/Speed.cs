using WrightTycoon.Core.Math;

namespace WrightTycoon.Core.Units;

public readonly record struct Speed
{
    public Speed(double metersPerSecond)
    {
        Numeric.ThrowIfNegative(metersPerSecond, nameof(metersPerSecond));
        MetersPerSecond = metersPerSecond;
    }

    public double MetersPerSecond { get; }

    public static Speed Zero => new(0d);

    public static Speed FromMetersPerSecond(double metersPerSecond) => new(metersPerSecond);

    public override string ToString() => $"{MetersPerSecond:0.###} m/s";
}
