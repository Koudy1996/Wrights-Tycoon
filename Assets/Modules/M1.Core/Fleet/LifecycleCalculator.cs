using System;
using WrightTycoon.Core.Categories;

namespace WrightTycoon.Core.Fleet;

/// <summary>Shared deterministic design/offer calculation; no random survival or accident simulation.</summary>
public static class LifecycleCalculator
{
    public static double CalculateExpectedLifeHours(AircraftCategory category, double reliability, int intervalHours, int durability)
    {
        ValidateDurability(durability);
        ValidateServiceInterval(intervalHours);
        ValidateReliability(reliability);
        // Retain validation for existing callers; market classification no longer determines design life.
        _ = LifecycleBalance.ReferenceLifeHours(category);
        // Preserve the historical curve through 100%, then continue with its tangent.
        // Reliability is a rating, not a probability: surplus research must not hit a lifetime ceiling.
        var reliabilityAtOne = System.Math.Pow(1d / LifecycleBalance.ReliabilityReference,
            LifecycleBalance.ReliabilityExponent);
        var reliabilityFactor = reliability <= 1d
            ? System.Math.Max(0.5d, System.Math.Pow(reliability / LifecycleBalance.ReliabilityReference,
                LifecycleBalance.ReliabilityExponent))
            : reliabilityAtOne * (1d + LifecycleBalance.ReliabilityExponent * (reliability - 1d));
        var serviceFactor = Clamp(System.Math.Pow(1000d / intervalHours, LifecycleBalance.ServiceExponent), 0.65d, 1.2d);
        return LifecycleBalance.DesignReferenceLifeHours * reliabilityFactor * serviceFactor
            * (1d + LifecycleBalance.LifetimePerDurabilityLevel * durability);
    }

    /// <summary>Counts every threshold crossed; includes an event exactly at the ending hour.</summary>
    public static int CountServiceEvents(double previousHours, double flownHours, int intervalHours)
    {
        RequireFiniteNonNegative(previousHours, nameof(previousHours));
        RequireFiniteNonNegative(flownHours, nameof(flownHours));
        ValidateServiceInterval(intervalHours);
        var endHours = previousHours + flownHours;
        RequireFiniteNonNegative(endHours, nameof(flownHours));
        var count = System.Math.Floor(endHours / intervalHours) - System.Math.Floor(previousHours / intervalHours);
        if (count > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(flownHours));
        return (int)count;
    }

    public static void ValidateDurability(int durability)
    {
        if (durability < 0 || durability > 10) throw new ArgumentOutOfRangeException(nameof(durability));
    }

    public static void ValidateServiceInterval(int intervalHours)
    {
        // Historical saved designs and frozen delivered offers retain their original interval.
        if (intervalHours == 8000) return;
        foreach (var interval in LifecycleBalance.ServiceIntervalsHours)
            if (intervalHours == interval) return;
        throw new ArgumentOutOfRangeException(nameof(intervalHours), "Service interval must be a supported number of flight hours.");
    }

    public static void ValidateReliability(double reliability)
    {
        RequireFiniteNonNegative(reliability, nameof(reliability));
        // Reliability is an effective design rating; accumulated bonuses may exceed 100%.
    }

    public static void RequireFiniteNonNegative(double value, string name)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d) throw new ArgumentOutOfRangeException(name);
    }

    internal static double Clamp(double value, double min, double max) => System.Math.Max(min, System.Math.Min(max, value));
}
