using System;
using System.Collections.Generic;
using System.Linq;

namespace WrightTycoon.Core.Campaign;

public sealed class HistoricalCampaignState
{
    private readonly HashSet<string> _firedEventIds = new(StringComparer.Ordinal);

    public IReadOnlyCollection<string> FiredEventIds => _firedEventIds;

    public bool HasFired(string eventId)
    {
        if (string.IsNullOrWhiteSpace(eventId))
        {
            return false;
        }

        return _firedEventIds.Contains(eventId.Trim());
    }

    public bool TryMarkFired(string eventId)
    {
        if (string.IsNullOrWhiteSpace(eventId))
        {
            throw new ArgumentException("Event id cannot be empty.", nameof(eventId));
        }

        return _firedEventIds.Add(eventId.Trim());
    }

    public void ReplaceFiredEventIds(IEnumerable<string>? firedEventIds)
    {
        _firedEventIds.Clear();
        if (firedEventIds is null)
        {
            return;
        }

        foreach (var eventId in firedEventIds)
        {
            if (string.IsNullOrWhiteSpace(eventId))
            {
                continue;
            }

            _firedEventIds.Add(eventId.Trim());
        }
    }

    public IReadOnlyList<string> CreateSnapshot() =>
        _firedEventIds
            .OrderBy(eventId => eventId, StringComparer.Ordinal)
            .ToArray();
}
