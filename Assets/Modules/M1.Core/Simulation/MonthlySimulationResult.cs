using System.Collections.Generic;

namespace WrightTycoon.Core.Simulation;

public sealed record MonthlySimulationResult<TState>(
    SimulationDate StartDate,
    SimulationDate EndDate,
    TState FinalState,
    IReadOnlyList<MonthlySimulationMonthReport> MonthReports)
    where TState : class;
