using System;
using WrightTycoon.Core.Math;

namespace WrightTycoon.Core.Units;

public readonly record struct Area
{
    public Area(double squareMeters)
    {
        Numeric.ThrowIfNegative(squareMeters, nameof(squareMeters));
        SquareMeters = squareMeters;
    }

    public double SquareMeters { get; }

    public static Area Zero => new(0d);

    public static Area FromSquareMeters(double squareMeters) => new(squareMeters);

    public static Area operator +(Area left, Area right) => new(left.SquareMeters + right.SquareMeters);

    public static Area operator -(Area left, Area right)
    {
        var result = left.SquareMeters - right.SquareMeters;
        Numeric.ThrowIfNegative(result, nameof(result));
        return new Area(result);
    }

    public static Area operator *(Area area, double scalar)
    {
        Numeric.ThrowIfNegative(scalar, nameof(scalar));
        return new Area(area.SquareMeters * scalar);
    }

    public static Area operator *(double scalar, Area area) => area * scalar;

    public override string ToString() => $"{SquareMeters:0.###} m^2";
}
