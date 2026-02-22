using System.Collections.Generic;

namespace WrightTycoon.Core.Simulation;

public sealed record MonthlySimulationMonthReport(
    SimulationDate Date,
    IReadOnlyList<string> ExecutedSystemIds,
    IReadOnlyList<string> Events);
