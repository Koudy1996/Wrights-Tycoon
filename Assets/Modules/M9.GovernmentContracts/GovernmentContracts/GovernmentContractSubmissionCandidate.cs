using System;

namespace WrightTycoon.Core.GovernmentContracts;

public sealed class GovernmentContractSubmissionCandidate
{
    public GovernmentContractSubmissionCandidate(
        string designId,
        string displayName,
        AircraftLifecycleStage lifecycleStage,
        GovernmentContractAircraftMetrics metrics)
    {
        DesignId = string.IsNullOrWhiteSpace(designId)
            ? throw new ArgumentException("Design id cannot be empty.", nameof(designId))
            : designId.Trim();
        DisplayName = string.IsNullOrWhiteSpace(displayName)
            ? throw new ArgumentException("Display name cannot be empty.", nameof(displayName))
            : displayName.Trim();
        LifecycleStage = lifecycleStage;
        Metrics = metrics;

        Validate();
    }

    public string DesignId { get; }

    public string DisplayName { get; }

    public AircraftLifecycleStage LifecycleStage { get; }

    public GovernmentContractAircraftMetrics Metrics { get; }

    public void Validate() => Metrics.Validate();
}
