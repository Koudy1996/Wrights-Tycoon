using System;

namespace WrightTycoon.Core.Math;

public static class Numeric
{
    public const double Epsilon = 1e-9;

    public static double Clamp(double value, double min, double max)
    {
        ThrowIfNotFinite(value, nameof(value));
        ThrowIfNotFinite(min, nameof(min));
        ThrowIfNotFinite(max, nameof(max));

        if (min > max)
        {
            throw new ArgumentException("Minimum must be lower than or equal to maximum.");
        }

        if (value < min)
        {
            return min;
        }

        return value > max ? max : value;
    }

    public static bool AlmostEqual(double left, double right, double tolerance = 1e-6)
    {
        ThrowIfNotFinite(left, nameof(left));
        ThrowIfNotFinite(right, nameof(right));
        ThrowIfNegative(tolerance, nameof(tolerance));

        return System.Math.Abs(left - right) <= tolerance;
    }

    public static void ThrowIfNotFinite(double value, string paramName)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            throw new ArgumentOutOfRangeException(paramName, "Value must be finite.");
        }
    }

    public static void ThrowIfNegative(double value, string paramName)
    {
        ThrowIfNotFinite(value, paramName);

        if (value < 0d)
        {
            throw new ArgumentOutOfRangeException(paramName, "Value must be greater than or equal to zero.");
        }
    }
}
