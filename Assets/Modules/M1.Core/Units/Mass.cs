using WrightTycoon.Core.Math;

namespace WrightTycoon.Core.Units;

public readonly record struct Mass
{
    public Mass(double kilograms)
    {
        Numeric.ThrowIfNegative(kilograms, nameof(kilograms));
        Kilograms = kilograms;
    }

    public double Kilograms { get; }

    public static Mass Zero => new(0d);

    public static Mass FromKilograms(double kilograms) => new(kilograms);

    public static Mass operator +(Mass left, Mass right) => new(left.Kilograms + right.Kilograms);

    public static Mass operator -(Mass left, Mass right)
    {
        var result = left.Kilograms - right.Kilograms;
        Numeric.ThrowIfNegative(result, nameof(result));
        return new Mass(result);
    }

    public static Mass operator *(Mass mass, double scalar)
    {
        Numeric.ThrowIfNegative(scalar, nameof(scalar));
        return new Mass(mass.Kilograms * scalar);
    }

    public static Mass operator *(double scalar, Mass mass) => mass * scalar;

    public override string ToString() => $"{Kilograms:0.###} kg";
}
