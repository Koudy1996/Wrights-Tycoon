using System;
using System.Collections.Generic;

namespace WrightTycoon.Core.Simulation;

public sealed class MonthlySimulationContext<TState>
    where TState : class
{
    private readonly List<string> _events = new();
    private readonly Dictionary<string, object?> _scratchpad = new(StringComparer.Ordinal);

    internal MonthlySimulationContext(SimulationDate date, int monthIndex, TState state)
    {
        Date = date;
        MonthIndex = monthIndex;
        State = state ?? throw new ArgumentNullException(nameof(state));
    }

    public SimulationDate Date { get; }

    public int MonthIndex { get; }

    public TState State { get; }

    public IDictionary<string, object?> Scratchpad => _scratchpad;

    public void AddEvent(string eventText)
    {
        if (string.IsNullOrWhiteSpace(eventText))
        {
            throw new ArgumentException("Event text cannot be empty.", nameof(eventText));
        }

        _events.Add(eventText);
    }

    internal IReadOnlyList<string> CaptureEvents() => _events.ToArray();
}
