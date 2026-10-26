using System;
using System.Collections.Generic;
using WrightTycoon.Core.Math;

namespace WrightTycoon.Core.GovernmentContracts;

public sealed class GovernmentContractComplianceEvaluator
{
    private readonly IGovernmentContractPricingService _pricingService;
    public GovernmentContractComplianceEvaluator(IGovernmentContractPricingService? pricingService = null)
    {
        _pricingService = pricingService ?? new GovernmentContractPricingService();
    }
    public GovernmentContractComplianceReport Evaluate(
        GovernmentContractRequirements requirements,
        GovernmentContractSubmissionCandidate candidate,
        double? lockedUnitPrice = null,
        WrightTycoon.Core.Contracts.LifecycleCostQuote? lockedLifecycleCostQuote = null,
        bool serviceIntervalAlreadyAccepted = false)
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

        var qualifiesForRequestedCategory = false;
        for (var index = 0; index < metrics.QualifiedAircraftTypes.Count; index++)
        {
            if (metrics.QualifiedAircraftTypes[index] == requirements.RequiredAircraftType)
            {
                qualifiesForRequestedCategory = true;
                break;
            }
        }

        if (!qualifiesForRequestedCategory)
        {
            unmetRequirements.Add(
                $"Aircraft does not qualify for requested category '{requirements.RequiredAircraftType}'.");
        }

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

        if (requirements.LifecycleModelVersion > 0)
        {
            if (metrics.ExpectedLifeHours + Numeric.Epsilon < requirements.MinimumExpectedLifeHours) unmetRequirements.Add("Expected life is below contractual minimum.");
            if (!serviceIntervalAlreadyAccepted && metrics.ServiceIntervalHours < requirements.MinimumServiceIntervalHours)
                unmetRequirements.Add("Service interval is below contractual minimum.");
            if ((requirements.PricingModelVersion > 0 || requirements.ProcurementPreference == WrightTycoon.Core.Contracts.ProcurementPreference.WholeLifeCost)
                && EconomicCost(requirements, lockedLifecycleCostQuote ?? Quote(requirements, metrics, lockedUnitPrice)) > EconomicLimit(requirements) + Numeric.Epsilon)
                unmetRequirements.Add("Operational costs exceed customer budget.");
        }
        var bonuses = CalculateBonuses(requirements, metrics, lockedUnitPrice, lockedLifecycleCostQuote);
        return new GovernmentContractComplianceReport(stageAllowed, unmetRequirements, bonuses);
    }

    public IReadOnlyList<GovernmentContractParameterBonus> CalculateBonuses(
        GovernmentContractRequirements requirements,
        GovernmentContractAircraftMetrics metrics,
        double? lockedUnitPrice = null,
        WrightTycoon.Core.Contracts.LifecycleCostQuote? lockedLifecycleCostQuote = null)
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

        if (requirements.LifecycleModelVersion > 0 && requirements.ProcurementPreference == WrightTycoon.Core.Contracts.ProcurementPreference.WholeLifeCost)
            bonuses.Add(GovernmentContractProbabilityCalculator.CreateLowerIsBetterBonus(GovernmentContractParameter.WholeLifeCost,
                EconomicLimit(requirements), EconomicCost(requirements, lockedLifecycleCostQuote ?? Quote(requirements, metrics, lockedUnitPrice))));
        return bonuses;
    }
    private static double EconomicCost(GovernmentContractRequirements requirements, WrightTycoon.Core.Contracts.LifecycleCostQuote quote) =>
        quote.OperationCostsPerHour;
    private static double EconomicLimit(GovernmentContractRequirements requirements) =>
        requirements.MaximumOperationCostPerHour;
    private WrightTycoon.Core.Contracts.LifecycleCostQuote Quote(GovernmentContractRequirements requirements, GovernmentContractAircraftMetrics metrics, double? lockedUnitPrice) =>
        metrics.QuoteForContract(requirements, lockedUnitPrice ?? _pricingService.CalculateQuote(requirements, 1).UnitPrice);
}
