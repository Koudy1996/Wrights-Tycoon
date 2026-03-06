using System;
using System.Collections.Generic;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Simulation;

namespace WrightTycoon.Core.Campaign;

public sealed class HistoricalCampaignTechnologyProgression
{
    public HistoricalCampaignTechnologyModifiers ResolveForDate(SimulationDate currentDate) =>
        ResolveForYear(currentDate.Year);

    public HistoricalCampaignTechnologyModifiers ResolveForYear(int year)
    {
        if (year < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(year), "Year must be greater than or equal to 1.");
        }

        var clampedYear = ClampYear(year);
        var enginePerformanceMultiplier = ResolveCurveMultiplier(
            EngineDesignerBalance.HistoricalCampaignTechnology.EnginePerformanceCurve,
            clampedYear,
            nameof(EngineDesignerBalance.HistoricalCampaignTechnology.EnginePerformanceCurve));
        var airframeEfficiencyMultiplier = ResolveCurveMultiplier(
            PlaneDesignerBalance.HistoricalCampaignTechnology.AirframeEfficiencyCurve,
            clampedYear,
            nameof(PlaneDesignerBalance.HistoricalCampaignTechnology.AirframeEfficiencyCurve));
        var aerodynamicsEfficiencyMultiplier = ResolveCurveMultiplier(
            PlaneDesignerBalance.HistoricalCampaignTechnology.AerodynamicsEfficiencyCurve,
            clampedYear,
            nameof(PlaneDesignerBalance.HistoricalCampaignTechnology.AerodynamicsEfficiencyCurve));
        var fuelEfficiencyMultiplier = ResolveCurveMultiplier(
            EngineDesignerBalance.HistoricalCampaignTechnology.FuelEfficiencyCurve,
            clampedYear,
            nameof(EngineDesignerBalance.HistoricalCampaignTechnology.FuelEfficiencyCurve));

        return new HistoricalCampaignTechnologyModifiers(
            enginePerformanceMultiplier,
            airframeEfficiencyMultiplier,
            aerodynamicsEfficiencyMultiplier,
            fuelEfficiencyMultiplier);
    }

    internal static double ResolveCurveMultiplier(
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
            throw new InvalidOperationException($"Historical campaign technology curve '{curveName}' is empty.");
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
                    $"Historical campaign technology curve '{curveName}' has invalid year range at index {index}.");
            }

            var blend = (clampedYear - lower.Year) / (double)yearRange;
            return lower.Multiplier + ((upper.Multiplier - lower.Multiplier) * blend);
        }

        return curve[lastIndex].Multiplier;
    }

    private static int ClampYear(int year)
    {
        if (year < CoreBalance.HistoricalCampaignTechnology.StartYear)
        {
            return CoreBalance.HistoricalCampaignTechnology.StartYear;
        }

        return year > CoreBalance.HistoricalCampaignTechnology.EndYear
            ? CoreBalance.HistoricalCampaignTechnology.EndYear
            : year;
    }

    private static void ValidateCurve(IReadOnlyList<YearlyBalancePoint> curve, string curveName)
    {
        var previousYear = int.MinValue;
        for (var index = 0; index < curve.Count; index++)
        {
            var point = curve[index];
            if (point.Year < CoreBalance.HistoricalCampaignTechnology.StartYear ||
                point.Year > CoreBalance.HistoricalCampaignTechnology.EndYear)
            {
                throw new InvalidOperationException(
                    $"Historical campaign technology curve '{curveName}' has year {point.Year} outside supported range [{CoreBalance.HistoricalCampaignTechnology.StartYear}, {CoreBalance.HistoricalCampaignTechnology.EndYear}].");
            }

            if (point.Year <= previousYear)
            {
                throw new InvalidOperationException(
                    $"Historical campaign technology curve '{curveName}' is not sorted by ascending year.");
            }

            if (double.IsNaN(point.Multiplier) ||
                double.IsInfinity(point.Multiplier) ||
                point.Multiplier <= 0d)
            {
                throw new InvalidOperationException(
                    $"Historical campaign technology curve '{curveName}' contains an invalid multiplier at year {point.Year}.");
            }

            previousYear = point.Year;
        }
    }
}
