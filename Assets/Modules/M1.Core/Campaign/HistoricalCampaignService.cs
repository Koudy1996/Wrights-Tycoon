using System;
using System.Collections.Generic;
using System.Linq;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Categories;
using WrightTycoon.Core.Simulation;

namespace WrightTycoon.Core.Campaign;

public sealed class HistoricalCampaignService
{
    private const string ContractMixModifierEffectType = "contract_mix_modifier";
    private const string ContractPriceModifierEffectType = "contract_price_modifier";
    private const string UnlockCategoryEffectType = "unlock_category";
    private const string UnlockContractParameterEffectType = "unlock_contract_parameter";

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

    public HistoricalCampaignContractModifiers BuildActiveContractModifiers(HistoricalCampaignState state)
    {
        if (state is null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        var modifiers = new HistoricalCampaignContractModifiers();
        for (var categoryIndex = 0; categoryIndex < ContractBalance.HistoricalCampaignInitiallyLockedCategories.Length; categoryIndex++)
        {
            modifiers.LockCategory(ContractBalance.HistoricalCampaignInitiallyLockedCategories[categoryIndex]);
        }

        for (var eventIndex = 0; eventIndex < _scheduledEvents.Count; eventIndex++)
        {
            var definition = _scheduledEvents[eventIndex];
            if (!state.HasFired(definition.Id))
            {
                continue;
            }

            for (var effectIndex = 0; effectIndex < definition.Effects.Count; effectIndex++)
            {
                var effect = definition.Effects[effectIndex];
                if (effect is null)
                {
                    continue;
                }

                if (string.Equals(effect.Type, ContractMixModifierEffectType, StringComparison.OrdinalIgnoreCase))
                {
                    if (ContractBalance.TryResolveHistoricalCampaignMixWeightMultiplier(effect.BalanceKey, out var mixMultiplier))
                    {
                        modifiers.ApplyCategoryMixMultiplier(effect.Target, mixMultiplier);
                    }

                    continue;
                }

                if (string.Equals(effect.Type, ContractPriceModifierEffectType, StringComparison.OrdinalIgnoreCase) &&
                    ContractBalance.TryResolveHistoricalCampaignPriceMultiplier(effect.BalanceKey, out var priceMultiplier))
                {
                    modifiers.ApplyPriceMultiplier(effect.Target, priceMultiplier);
                    continue;
                }

                if (!string.Equals(effect.Type, UnlockCategoryEffectType, StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.Equals(effect.Type, UnlockContractParameterEffectType, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    modifiers.UnlockContractParameter(effect.Target);
                    continue;
                }

                if (TryResolveAircraftCategory(effect.Target, out var category))
                {
                    modifiers.UnlockCategory(category);
                }
            }
        }

        return modifiers;
    }

    private static bool TryResolveAircraftCategory(string? rawCategory, out AircraftCategory category)
    {
        category = default;
        if (string.IsNullOrWhiteSpace(rawCategory))
        {
            return false;
        }

        return Enum.TryParse(rawCategory.Trim(), ignoreCase: true, out category);
    }
}
