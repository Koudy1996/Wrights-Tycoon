using System;

namespace WrightTycoon.Core.Aircraft;

public sealed record WingAreaPayloadEvaluation(bool Success, double PayloadKilograms, string ErrorMessage = "");

public sealed record WingAreaPayloadOptimizationResult(
    bool Success,
    double WingAreaSquareMeters,
    double PayloadKilograms,
    string ErrorMessage);

public static class WingAreaPayloadOptimizer
{
    public static WingAreaPayloadOptimizationResult FindSmallestMeetingPayload(
        double minimumWingArea,
        double maximumWingArea,
        double targetPayloadKilograms,
        Func<double, WingAreaPayloadEvaluation> evaluate)
    {
        if (double.IsNaN(minimumWingArea) || double.IsInfinity(minimumWingArea)
            || double.IsNaN(maximumWingArea) || double.IsInfinity(maximumWingArea)
            || minimumWingArea > maximumWingArea)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumWingArea));
        }
        if (double.IsNaN(targetPayloadKilograms) || double.IsInfinity(targetPayloadKilograms) || targetPayloadKilograms < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(targetPayloadKilograms));
        }
        if (evaluate is null)
        {
            throw new ArgumentNullException(nameof(evaluate));
        }

        var bestArea = maximumWingArea;
        var bestPayload = 0d;
        var found = false;
        var lastError = string.Empty;
        Scan(minimumWingArea, maximumWingArea, 5d, targetPayloadKilograms, evaluate, ref found, ref bestArea, ref bestPayload, ref lastError);
        if (found)
        {
            Scan(minimumWingArea, System.Math.Min(maximumWingArea, bestArea + 5d), 1d, targetPayloadKilograms, evaluate, ref found, ref bestArea, ref bestPayload, ref lastError);
            Scan(minimumWingArea, System.Math.Min(maximumWingArea, bestArea + 1d), 0.1d, targetPayloadKilograms, evaluate, ref found, ref bestArea, ref bestPayload, ref lastError);
        }

        return new WingAreaPayloadOptimizationResult(found, bestArea, bestPayload, lastError);
    }

    private static void Scan(
        double minimumWingArea,
        double maximumWingArea,
        double step,
        double targetPayloadKilograms,
        Func<double, WingAreaPayloadEvaluation> evaluate,
        ref bool found,
        ref double bestArea,
        ref double bestPayload,
        ref string lastError)
    {
        var candidate = minimumWingArea;
        var guard = 0;
        while (candidate <= maximumWingArea + 1e-9d && guard < 20000)
        {
            var area = System.Math.Clamp(
                System.Math.Round(candidate, 4, MidpointRounding.AwayFromZero),
                minimumWingArea,
                maximumWingArea);
            var evaluation = evaluate(area);
            if (evaluation.Success
                && !double.IsNaN(evaluation.PayloadKilograms)
                && !double.IsInfinity(evaluation.PayloadKilograms)
                && evaluation.PayloadKilograms + 1e-6d >= targetPayloadKilograms
                && (!found || area < bestArea - 1e-6d))
            {
                found = true;
                bestArea = area;
                bestPayload = evaluation.PayloadKilograms;
            }
            else if (!evaluation.Success && !string.IsNullOrWhiteSpace(evaluation.ErrorMessage))
            {
                lastError = evaluation.ErrorMessage;
            }

            candidate += step;
            guard++;
        }
    }
}
