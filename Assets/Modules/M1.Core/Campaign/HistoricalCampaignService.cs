using System;
using System.Collections.Generic;
using System.Linq;
using WrightTycoon.Core.Simulation;

namespace WrightTycoon.Core.Campaign;

public sealed class HistoricalCampaignService
{
    private readonly IReadOnlyList<HistoricalCampaignEventDefinition> _allEvents;
    private readonly IReadOnlyList<HistoricalCampaignEventDefinition> _scheduledEvents;
    private readonly IReadOnlyDictionary<int, HistoricalCampaignEventDefinition> _scheduledEventsByMonthKey;
    private readonly IReadOnlyList<string> _loadWarnings;

    public HistoricalCampaignService(IReadOnlyList<HistoricalCampaignEventDefinition> definitions)
    {
        if (definitions is null)
        {
            throw new ArgumentNullException(nameof(definitions));
        }

        HistoricalCampaignValidator.ValidateDefinitions(definitions);

        _allEvents = definitions.ToArray();

        var scheduledEvents = new List<HistoricalCampaignEventDefinition>(_allEvents.Count);
        var scheduledEventsByMonthKey = new Dictionary<int, HistoricalCampaignEventDefinition>();
        var loadWarnings = new List<string>();

        for (var index = 0; index < _allEvents.Count; index++)
        {
            var definition = _allEvents[index];
            var monthKey = HistoricalCampaignValidator.CreateMonthKey(definition.Year, definition.Month);
            if (scheduledEventsByMonthKey.TryGetValue(monthKey, out var existing))
            {
                loadWarnings.Add(
                    $"Duplicate historical campaign month {definition.Year:D4}-{definition.Month:D2}: keeping '{existing.Id}', skipping '{definition.Id}'.");
                continue;
            }

            scheduledEventsByMonthKey.Add(monthKey, definition);
            scheduledEvents.Add(definition);
        }

        _scheduledEvents = scheduledEvents.AsReadOnly();
        _scheduledEventsByMonthKey = scheduledEventsByMonthKey;
        _loadWarnings = loadWarnings.AsReadOnly();
    }

    public IReadOnlyList<HistoricalCampaignEventDefinition> AllEvents => _allEvents;

    public IReadOnlyList<HistoricalCampaignEventDefinition> ScheduledEvents => _scheduledEvents;

    public IReadOnlyList<string> LoadWarnings => _loadWarnings;

    public HistoricalCampaignTriggerResult TryTrigger(SimulationDate currentDate, HistoricalCampaignState state)
    {
        if (state is null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        var monthKey = HistoricalCampaignValidator.CreateMonthKey(currentDate.Year, currentDate.Month);
        if (!_scheduledEventsByMonthKey.TryGetValue(monthKey, out var definition))
        {
            return new HistoricalCampaignTriggerResult(null);
        }

        if (!state.TryMarkFired(definition.Id))
        {
            return new HistoricalCampaignTriggerResult(null);
        }

        return new HistoricalCampaignTriggerResult(definition);
    }

    public HistoricalCampaignEventDefinition? GetNextUnfiredEvent(HistoricalCampaignState state)
    {
        if (state is null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        for (var index = 0; index < _scheduledEvents.Count; index++)
        {
            var definition = _scheduledEvents[index];
            if (!state.HasFired(definition.Id))
            {
                return definition;
            }
        }

        return null;
    }

    public HistoricalCampaignEventDefinition? GetNextUpcomingUnfiredEvent(
        SimulationDate currentDate,
        HistoricalCampaignState state)
    {
        if (state is null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        var currentMonthKey = HistoricalCampaignValidator.CreateMonthKey(currentDate.Year, currentDate.Month);
        for (var index = 0; index < _scheduledEvents.Count; index++)
        {
            var definition = _scheduledEvents[index];
            if (state.HasFired(definition.Id))
            {
                continue;
            }

            var definitionMonthKey = HistoricalCampaignValidator.CreateMonthKey(definition.Year, definition.Month);
            if (definitionMonthKey < currentMonthKey)
            {
                continue;
            }

            return definition;
        }

        return null;
    }
}
