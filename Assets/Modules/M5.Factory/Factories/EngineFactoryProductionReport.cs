using System.Collections.Generic;

namespace WrightTycoon.Core.Factories;

public sealed record EngineFactoryProductionReport(
    IReadOnlyList<EngineProductionReport> Entries,
    int TotalRequiredEngines,
    int TotalProducedEngines,
    int TotalDiscardedEngines,
    int TotalAutoAssignedEmployees,
    double TotalRequiredManDays,
    double TotalCapacityManDays);
