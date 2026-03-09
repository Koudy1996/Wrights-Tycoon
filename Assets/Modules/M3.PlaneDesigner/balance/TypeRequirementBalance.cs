using System;
using System.Collections.Generic;
using WrightTycoon.Core.Categories;
using WrightTycoon.Core.Math;

namespace WrightTycoon.Core.Balance;

// Shared year-based scaling for category demand thresholds used by aircraft type categories.
public static class TypeRequirementBalance
{
    public const int RequirementYearOffset = 0;

    public static readonly IReadOnlyList<YearlyBalancePoint> MinimumRequirementMultiplierCurve = Array.AsReadOnly(new[]
    {
        new YearlyBalancePoint(1946, 0.60d),
        new YearlyBalancePoint(2020, 1.00d),
    });

    public static double ResolveMinimumRequirementMultiplier(int year, int playerYearOffset = 0)
    {
        if (year < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(year), "Year must be greater than or equal to 1.");
        }

        var effectiveYearLong = (long)year + RequirementYearOffset + playerYearOffset;
        if (effectiveYearLong < int.MinValue)
        {
            effectiveYearLong = int.MinValue;
        }
        else if (effectiveYearLong > int.MaxValue)
        {
            effectiveYearLong = int.MaxValue;
        }

        var clampedYear = ClampYear((int)effectiveYearLong);
        return ResolveCurveMultiplier(MinimumRequirementMultiplierCurve, clampedYear, nameof(MinimumRequirementMultiplierCurve));
    }

    public static double ScaleMinimumReliability(double value, int year, int playerYearOffset = 0)
    {
        Numeric.ThrowIfNotFinite(value, nameof(value));
        if (value < 0d || value > 1d)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Reliability must be within [0, 1].");
        }

        var multiplier = ResolveMinimumRequirementMultiplier(year, playerYearOffset);
        return Numeric.Clamp(value * multiplier, 0d, 1d);
    }

    public static double ScaleMaximumReliability(double value, int year, int playerYearOffset = 0)
    {
        Numeric.ThrowIfNotFinite(value, nameof(value));
        if (value < 0d || value > 1d)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Reliability must be within [0, 1].");
        }

        var multiplier = ResolveMinimumRequirementMultiplier(year, playerYearOffset);
        return Numeric.Clamp(value * multiplier, 0d, 1d);
    }

    public static double ScaleMinimumRangeKilometers(double value, int year, int playerYearOffset = 0)
    {
        Numeric.ThrowIfNotFinite(value, nameof(value));
        if (value < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Range must be greater than or equal to zero.");
        }

        var multiplier = ResolveMinimumRequirementMultiplier(year, playerYearOffset);
        return System.Math.Max(0d, value * multiplier);
    }

    public static double ScaleMaximumRangeKilometers(double value, int year, int playerYearOffset = 0)
    {
        Numeric.ThrowIfNotFinite(value, nameof(value));
        if (value < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Range must be greater than or equal to zero.");
        }

        var multiplier = ResolveMinimumRequirementMultiplier(year, playerYearOffset);
        return System.Math.Max(0d, value * multiplier);
    }

    public static double ScaleMinimumSpeedKilometersPerHour(double value, int year, int playerYearOffset = 0)
    {
        Numeric.ThrowIfNotFinite(value, nameof(value));
        if (value < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Speed must be greater than or equal to zero.");
        }

        var multiplier = ResolveMinimumRequirementMultiplier(year, playerYearOffset);
        return System.Math.Max(0d, value * multiplier);
    }

    public static double ScaleMaximumSpeedKilometersPerHour(double value, int year, int playerYearOffset = 0)
    {
        Numeric.ThrowIfNotFinite(value, nameof(value));
        if (value < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Speed must be greater than or equal to zero.");
        }

        var multiplier = ResolveMinimumRequirementMultiplier(year, playerYearOffset);
        return System.Math.Max(0d, value * multiplier);
    }

    public static double ScaleMinimumPayloadKilograms(double value, int year, int playerYearOffset = 0)
    {
        Numeric.ThrowIfNotFinite(value, nameof(value));
        if (value < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Payload must be greater than or equal to zero.");
        }

        var multiplier = ResolveMinimumRequirementMultiplier(year, playerYearOffset);
        return System.Math.Max(0d, value * multiplier);
    }

    public static double ScaleMaximumPayloadKilograms(double value, int year, int playerYearOffset = 0)
    {
        Numeric.ThrowIfNotFinite(value, nameof(value));
        if (value < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Payload must be greater than or equal to zero.");
        }

        var multiplier = ResolveMinimumRequirementMultiplier(year, playerYearOffset);
        return System.Math.Max(0d, value * multiplier);
    }

    public static AircraftCategoryProfile CreateYearAdjustedProfile(
        AircraftCategoryProfile baseProfile,
        int year,
        int playerYearOffset = 0)
    {
        if (baseProfile is null)
        {
            throw new ArgumentNullException(nameof(baseProfile));
        }

        var minSpeed = ScaleMinimumSpeedKilometersPerHour(
            baseProfile.MinRequiredSpeedKilometersPerHour,
            year,
            playerYearOffset);
        var minRange = ScaleMinimumRangeKilometers(
            baseProfile.MinRequiredRangeKilometers,
            year,
            playerYearOffset);
        var minPayload = ScaleMinimumPayloadKilograms(
            baseProfile.MinRequiredPayloadKilograms,
            year,
            playerYearOffset);
        var maxSpeed = ScaleMaximumSpeedKilometersPerHour(
            baseProfile.MaxRequiredSpeedKilometersPerHour,
            year,
            playerYearOffset);
        var maxRange = ScaleMaximumRangeKilometers(
            baseProfile.MaxRequiredRangeKilometers,
            year,
            playerYearOffset);
        var maxPayload = ScaleMaximumPayloadKilograms(
            baseProfile.MaxRequiredPayloadKilograms,
            year,
            playerYearOffset);

        if (minSpeed > maxSpeed)
        {
            minSpeed = maxSpeed;
        }

        if (minRange > maxRange)
        {
            minRange = maxRange;
        }

        if (minPayload > maxPayload)
        {
            minPayload = maxPayload;
        }

        return new AircraftCategoryProfile(
            baseProfile.Category,
            baseProfile.DisplayName,
            baseProfile.MinCrew,
            baseProfile.MaxCrew,
            baseProfile.MinMaximumWeightKilograms,
            baseProfile.MaxMaximumWeightKilograms,
            minSpeed,
            maxSpeed,
            minRange,
            maxRange,
            minPayload,
            maxPayload,
            baseProfile.WeightModifierKilograms,
            baseProfile.SpeedModifierMetersPerSecond,
            baseProfile.ReliabilityModifierDelta,
            baseProfile.FuelConsumptionMultiplier);
    }

    private static int ClampYear(int year)
    {
        if (year < CoreBalance.HistoricalCampaignTechnology.StartYear)
        {
            return CoreBalance.HistoricalCampaignTechnology.StartYear;
        }

        if (year > CoreBalance.HistoricalCampaignTechnology.EndYear)
        {
            return CoreBalance.HistoricalCampaignTechnology.EndYear;
        }

        return year;
    }

    private static double ResolveCurveMultiplier(
        IReadOnlyList<YearlyBalancePoint> curve,
        int clampedYear,
        string curveName)
    {
        if (curve is null)
        {
            throw new ArgumentNullException(nameof(curve));
        }

        if (curve.Count == 0)
        {
            throw new InvalidOperationException($"Curve '{curveName}' is empty.");
        }

        ValidateCurve(curve, curveName);

        if (clampedYear <= curve[0].Year)
        {
            return curve[0].Multiplier;
        }

        var lastIndex = curve.Count - 1;
        if (clampedYear >= curve[lastIndex].Year)
        {
            return curve[lastIndex].Multiplier;
        }

        for (var index = 1; index < curve.Count; index++)
        {
            var upper = curve[index];
            if (clampedYear > upper.Year)
            {
                continue;
            }

            var lower = curve[index - 1];
            var yearRange = upper.Year - lower.Year;
            if (yearRange <= 0)
            {
                throw new InvalidOperationException(
                    $"Curve '{curveName}' has invalid year range at index {index}.");
            }

            var blend = (clampedYear - lower.Year) / (double)yearRange;
            return lower.Multiplier + ((upper.Multiplier - lower.Multiplier) * blend);
        }

        return curve[lastIndex].Multiplier;
    }

    private static void ValidateCurve(IReadOnlyList<YearlyBalancePoint> curve, string curveName)
    {
        var previousYear = int.MinValue;
        for (var index = 0; index < curve.Count; index++)
        {
            var point = curve[index];
            if (point.Year < CoreBalance.HistoricalCampaignTechnology.StartYear
                || point.Year > CoreBalance.HistoricalCampaignTechnology.EndYear)
            {
                throw new InvalidOperationException(
                    $"Curve '{curveName}' has year {point.Year} outside supported range [{CoreBalance.HistoricalCampaignTechnology.StartYear}, {CoreBalance.HistoricalCampaignTechnology.EndYear}].");
            }

            if (point.Year <= previousYear)
            {
                throw new InvalidOperationException($"Curve '{curveName}' is not sorted by ascending year.");
            }

            if (double.IsNaN(point.Multiplier)
                || double.IsInfinity(point.Multiplier)
                || point.Multiplier <= 0d)
            {
                throw new InvalidOperationException(
                    $"Curve '{curveName}' has invalid multiplier at year {point.Year}.");
            }

            previousYear = point.Year;
        }
    }
}
