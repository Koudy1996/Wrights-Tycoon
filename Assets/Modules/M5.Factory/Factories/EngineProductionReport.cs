namespace WrightTycoon.Core.Factories;

public sealed record EngineProductionReport(
    string LineId,
    string EngineDisplayName,
    int RequiredEngines,
    int ProducedEngines,
    int DiscardedEngines,
    int AutoAssignedEmployees,
    double RequiredManDays,
    double CapacityManDays);
