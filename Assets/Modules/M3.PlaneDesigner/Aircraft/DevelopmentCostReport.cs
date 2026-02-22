namespace WrightTycoon.Core.Aircraft;

public sealed record DevelopmentCostReport(
    int ChangedParameterCount,
    double ChangeMagnitudeScore,
    double DevelopmentCost,
    double DevelopmentManDays);
