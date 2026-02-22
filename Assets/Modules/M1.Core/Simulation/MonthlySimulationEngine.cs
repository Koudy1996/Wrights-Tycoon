using System;
using System.Collections.Generic;
using System.Linq;

namespace WrightTycoon.Core.Simulation;

public sealed class MonthlySimulationEngine<TState>
    where TState : class
{
    private readonly IReadOnlyList<IMonthlySimulationSystem<TState>> _orderedSystems;

    public MonthlySimulationEngine(IEnumerable<IMonthlySimulationSystem<TState>> systems)
    {
        if (systems is null)
        {
            throw new ArgumentNullException(nameof(systems));
        }

        _orderedSystems = systems
            .OrderBy(system => system.ExecutionOrder)
            .ThenBy(system => system.SystemId, StringComparer.Ordinal)
            .ToArray();

        if (_orderedSystems.Count == 0)
        {
            throw new ArgumentException("At least one simulation system is required.", nameof(systems));
        }

        if (_orderedSystems.Any(system => string.IsNullOrWhiteSpace(system.SystemId)))
        {
            throw new ArgumentException("Each simulation system must have a non-empty system id.", nameof(systems));
        }

        var duplicateId = _orderedSystems
            .GroupBy(system => system.SystemId, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateId is not null)
        {
            throw new ArgumentException($"Duplicate system id detected: '{duplicateId.Key}'.", nameof(systems));
        }
    }

    public MonthlySimulationResult<TState> Run(TState state, SimulationDate startDate, int monthCount)
    {
        if (state is null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (monthCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(monthCount), "Month count must be greater than zero.");
        }

        var reports = new List<MonthlySimulationMonthReport>(monthCount);
        var currentDate = startDate;

        for (var monthIndex = 0; monthIndex < monthCount; monthIndex++)
        {
            var context = new MonthlySimulationContext<TState>(currentDate, monthIndex, state);
            var executedSystemIds = new string[_orderedSystems.Count];

            for (var systemIndex = 0; systemIndex < _orderedSystems.Count; systemIndex++)
            {
                var system = _orderedSystems[systemIndex];
                // Systems mutate shared state in deterministic order once per month.
                system.Execute(context);
                executedSystemIds[systemIndex] = system.SystemId;
            }

            reports.Add(new MonthlySimulationMonthReport(currentDate, executedSystemIds, context.CaptureEvents()));
            currentDate = currentDate.NextMonth();
        }

        return new MonthlySimulationResult<TState>(startDate, currentDate, state, reports);
    }
}
