using System;
using System.Collections.Generic;
using System.Linq;

namespace WrightTycoon.Game.Launch;

public sealed class MonthlyCompletionSummarySnapshot
{
    public int MonthIndex { get; set; } = -1;
    public string[] Entries { get; set; } = Array.Empty<string>();
    public bool Acknowledged { get; set; } = true;
}

/// <summary>One aggregate notification per simulation month, including across save/load.</summary>
public sealed class MonthlyCompletionSummary
{
    private readonly List<string> entries = new();
    public int MonthIndex { get; private set; } = -1;
    public bool Acknowledged { get; private set; } = true;
    public IReadOnlyList<string> Entries => entries.AsReadOnly();
    public bool HasPending => entries.Count > 0 && !Acknowledged;

    public void Publish(int monthIndex, IEnumerable<string> completedEvents)
    {
        if (monthIndex < MonthIndex) return;
        if (monthIndex != MonthIndex)
        {
            MonthIndex = monthIndex;
            entries.Clear();
            Acknowledged = false;
        }
        foreach (var entry in completedEvents)
            if (!string.IsNullOrWhiteSpace(entry) && !entries.Contains(entry)) entries.Add(entry);
    }

    public void Dismiss() => Acknowledged = true;
    public MonthlyCompletionSummarySnapshot CreateSnapshot() => new()
        { MonthIndex = MonthIndex, Entries = entries.ToArray(), Acknowledged = Acknowledged };

    public void RestoreSnapshot(MonthlyCompletionSummarySnapshot? snapshot)
    {
        MonthIndex = snapshot?.MonthIndex ?? -1;
        entries.Clear();
        entries.AddRange((snapshot?.Entries ?? Array.Empty<string>()).Where(e => !string.IsNullOrWhiteSpace(e)).Distinct());
        Acknowledged = snapshot?.Acknowledged ?? true;
    }
}
