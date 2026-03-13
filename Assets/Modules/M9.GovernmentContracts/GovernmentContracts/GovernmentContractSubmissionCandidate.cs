using System;

namespace WrightTycoon.Core.GovernmentContracts;

public sealed class GovernmentContractSubmissionCandidate
{
    public GovernmentContractSubmissionCandidate(
        string designId,
        string displayName,
        AircraftLifecycleStage lifecycleStage,
        GovernmentContractAircraftMetrics metrics,
        GovernmentContractDevelopmentPriceBreakdown? developmentPriceBreakdown = null)
    {
        DesignId = string.IsNullOrWhiteSpace(designId)
            ? throw new ArgumentException("Design id cannot be empty.", nameof(designId))
            : designId.Trim();
        DisplayName = string.IsNullOrWhiteSpace(displayName)
            ? throw new ArgumentException("Display name cannot be empty.", nameof(displayName))
            : displayName.Trim();
        LifecycleStage = lifecycleStage;
        Metrics = metrics;
        DevelopmentPriceBreakdown = developmentPriceBreakdown
            ?? GovernmentContractDevelopmentPriceBreakdown.FromTotal(metrics.DevelopmentCost);

        Validate();
    }

    public string DesignId { get; }

    public string DisplayName { get; }

    public AircraftLifecycleStage LifecycleStage { get; }

    public GovernmentContractAircraftMetrics Metrics { get; }

    public GovernmentContractDevelopmentPriceBreakdown DevelopmentPriceBreakdown { get; }

    public void Validate()
    {
        Metrics.Validate();
        DevelopmentPriceBreakdown.Validate();
    }
}
