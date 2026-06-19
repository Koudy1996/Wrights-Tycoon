using System;
using System.Collections.Generic;
using WrightTycoon.Core.Math;

namespace WrightTycoon.Core.GovernmentContracts;

public sealed class GovernmentContractComplianceEvaluator
{
    public GovernmentContractComplianceReport Evaluate(
        GovernmentContractRequirements requirements,
        GovernmentContractSubmissionCandidate candidate)
    {
        if (requirements is null)
        {
            throw new ArgumentNullException(nameof(requirements));
        }

        if (candidate is null)
        {
            throw new ArgumentNullException(nameof(candidate));
        }

        requirements.Validate();
        candidate.Validate();

        var unmetRequirements = new List<string>();
        var stageAllowed = candidate.LifecycleStage is AircraftLifecycleStage.InDevelopment or AircraftLifecycleStage.Completed;
        if (!stageAllowed)
        {
            unmetRequirements.Add("Aircraft in Concept stage cannot be submitted.");
        }

        var metrics = candidate.Metrics;

        var actualSpeed = metrics.GetMaximumSpeedForBand(requirements.SpeedAltitudeBand);
        if (actualSpeed + Numeric.Epsilon < requirements.MinimumMaximumSpeedMetersPerSecond)
        {
            unmetRequirements.Add(
                $"Max speed at {requirements.SpeedAltitudeBand} altitude ({actualSpeed:0.0} m/s) is below required {requirements.MinimumMaximumSpeedMetersPerSecond:0.0} m/s.");
        }

        if (metrics.RangeKilometers + Numeric.Epsilon < requirements.MinimumRangeKilometers)
        {
            unmetRequirements.Add($"Range {metrics.RangeKilometers:0.0} km is below required {requirements.MinimumRangeKilometers:0.0} km.");
        }

        if (metrics.PayloadKilograms + Numeric.Epsilon < requirements.MinimumPayloadKilograms)
        {
            unmetRequirements.Add($"Payload {metrics.PayloadKilograms:0.0} kg is below required {requirements.MinimumPayloadKilograms:0.0} kg.");
        }

        if (metrics.Reliability + Numeric.Epsilon < requirements.MinimumReliability)
        {
            unmetRequirements.Add($"Reliability {metrics.Reliability:0.000} is below required {requirements.MinimumReliability:0.000}.");
        }

        if (metrics.Maneuverability + Numeric.Epsilon < requirements.MinimumManeuverability)
        {
            unmetRequirements.Add(
                $"Maneuverability {metrics.Maneuverability:0.0} is below required {requirements.MinimumManeuverability:0.0}.");
        }

        if (metrics.CrewSize < requirements.MinimumCrewSize)
        {
            unmetRequirements.Add($"Crew size {metrics.CrewSize} is below required {requirements.MinimumCrewSize}.");
        }

        if (metrics.RequiredRunwayLengthMeters - Numeric.Epsilon > requirements.MaximumRequiredRunwayLengthMeters)
        {
            unmetRequirements.Add(
                $"Required runway length {metrics.RequiredRunwayLengthMeters:0.0} m exceeds allowed {requirements.MaximumRequiredRunwayLengthMeters:0.0} m.");
        }

        var bonuses = CalculateBonuses(requirements, metrics);
        return new GovernmentContractComplianceReport(stageAllowed, unmetRequirements, bonuses);
    }

    public IReadOnlyList<GovernmentContractParameterBonus> CalculateBonuses(
        GovernmentContractRequirements requirements,
        GovernmentContractAircraftMetrics metrics)
    {
        if (requirements is null)
        {
            throw new ArgumentNullException(nameof(requirements));
        }

        requirements.Validate();
        metrics.Validate();

        var bonuses = new List<GovernmentContractParameterBonus>(capacity: 7)
        {
            GovernmentContractProbabilityCalculator.CreateHigherIsBetterBonus(
                GovernmentContractParameter.MaximumSpeed,
                requirements.MinimumMaximumSpeedMetersPerSecond,
                metrics.GetMaximumSpeedForBand(requirements.SpeedAltitudeBand)),

            GovernmentContractProbabilityCalculator.CreateHigherIsBetterBonus(
                GovernmentContractParameter.Range,
                requirements.MinimumRangeKilometers,
                metrics.RangeKilometers),

            GovernmentContractProbabilityCalculator.CreateHigherIsBetterBonus(
                GovernmentContractParameter.Payload,
                requirements.MinimumPayloadKilograms,
                metrics.PayloadKilograms),

            GovernmentContractProbabilityCalculator.CreateHigherIsBetterBonus(
                GovernmentContractParameter.Reliability,
                requirements.MinimumReliability,
                metrics.Reliability),

            GovernmentContractProbabilityCalculator.CreateHigherIsBetterBonus(
                GovernmentContractParameter.Maneuverability,
                requirements.MinimumManeuverability,
                metrics.Maneuverability),

            GovernmentContractProbabilityCalculator.CreateHigherIsBetterBonus(
                GovernmentContractParameter.CrewSize,
                requirements.MinimumCrewSize,
                metrics.CrewSize),

            GovernmentContractProbabilityCalculator.CreateLowerIsBetterBonus(
                GovernmentContractParameter.RunwayLength,
                requirements.MaximumRequiredRunwayLengthMeters,
                metrics.RequiredRunwayLengthMeters),
        };

        return bonuses;
    }
}
