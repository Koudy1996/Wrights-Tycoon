using WrightTycoon.Core.Math;

namespace WrightTycoon.Core.Units;

public readonly record struct Force
{
    public Force(double newtons)
    {
        Numeric.ThrowIfNotFinite(newtons, nameof(newtons));
        Newtons = newtons;
    }

    public double Newtons { get; }

    public static Force Zero => new(0d);

    public static Force FromNewtons(double newtons) => new(newtons);

    public static Force operator +(Force left, Force right) => new(left.Newtons + right.Newtons);

    public static Force operator -(Force left, Force right) => new(left.Newtons - right.Newtons);

    public static Force operator *(Force force, double scalar)
    {
        Numeric.ThrowIfNotFinite(scalar, nameof(scalar));
        return new Force(force.Newtons * scalar);
    }

    public static Force operator *(double scalar, Force force) => force * scalar;

    public override string ToString() => $"{Newtons:0.###} N";
}
