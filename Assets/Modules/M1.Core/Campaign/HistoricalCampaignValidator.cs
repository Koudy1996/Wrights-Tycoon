using System;
using System.Collections.Generic;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Categories;
using WrightTycoon.Core.Simulation;

namespace WrightTycoon.Core.Campaign;

public static class HistoricalCampaignValidator
{
    public const int ArticleWordLimit = 100;
    public const int MinimumSupportedYear = CoreBalance.HistoricalCampaignTechnology.StartYear;
    public const int MaximumSupportedYear = CoreBalance.HistoricalCampaignTechnology.EndYear;

    private const string ContractMixModifierEffectType = "contract_mix_modifier";
    private const string ContractPriceModifierEffectType = "contract_price_modifier";
    private const string BonusContractBatchEffectType = "bonus_contract_batch";
    private const string UnlockCategoryEffectType = "unlock_category";
    private const string UnlockContractParameterEffectType = "unlock_contract_parameter";
    private const string TechnologyShiftEffectType = "technology_shift";
    private const string CivilTarget = "civil";
    private const string MilitaryTarget = "military";

    public static void ValidateDefinitions(IReadOnlyList<HistoricalCampaignEventDefinition> definitions)
    {
        if (definitions is null)
        {
            throw new ArgumentNullException(nameof(definitions));
        }

        if (definitions.Count == 0)
        {
            throw new InvalidOperationException("Historical campaign data did not contain any events.");
        }

        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        for (var eventIndex = 0; eventIndex < definitions.Count; eventIndex++)
        {
            var definition = definitions[eventIndex]
                ?? throw new InvalidOperationException($"Historical campaign event at index {eventIndex} is null.");

            ValidateRequiredText(definition.Id, $"events[{eventIndex}].id");
            ValidateRequiredText(definition.Title, $"events[{eventIndex}].title");
            ValidateRequiredText(definition.ArticleText, $"events[{eventIndex}].articleText");

            _ = new SimulationDate(definition.Year, definition.Month);
            ValidateSupportedYearRange(definition.Year, $"events[{eventIndex}].year");

            var normalizedId = definition.Id.Trim();
            if (!seenIds.Add(normalizedId))
            {
                throw new InvalidOperationException($"Historical campaign event id '{normalizedId}' is duplicated.");
            }

            var wordCount = CountWords(definition.ArticleText);
            if (wordCount > ArticleWordLimit)
            {
                throw new InvalidOperationException(
                    $"Historical campaign article for event '{normalizedId}' contains {wordCount} words. The limit is {ArticleWordLimit}.");
            }

            if (definition.Effects is null)
            {
                throw new InvalidOperationException($"Historical campaign event '{normalizedId}' has a null effects array.");
            }

            for (var effectIndex = 0; effectIndex < definition.Effects.Count; effectIndex++)
            {
                var effect = definition.Effects[effectIndex]
                    ?? throw new InvalidOperationException($"Historical campaign effect at events[{eventIndex}].effects[{effectIndex}] is null.");
                ValidateRequiredText(effect.Type, $"events[{eventIndex}].effects[{effectIndex}].type");

                var effectType = effect.Type.Trim();
                if (string.Equals(effectType, ContractMixModifierEffectType, StringComparison.OrdinalIgnoreCase))
                {
                    ValidateCivilOrMilitaryTarget(effect.Target, $"events[{eventIndex}].effects[{effectIndex}].target");
                    ValidateRequiredText(effect.BalanceKey, $"events[{eventIndex}].effects[{effectIndex}].balanceKey");
                    if (!ContractBalance.TryResolveHistoricalCampaignMixWeightMultiplier(effect.BalanceKey, out _))
                    {
                        throw new InvalidOperationException(
                            $"Historical campaign effect 'contract_mix_modifier' at events[{eventIndex}].effects[{effectIndex}] has unknown balanceKey '{effect.BalanceKey}'.");
                    }
                }
                else if (string.Equals(effectType, ContractPriceModifierEffectType, StringComparison.OrdinalIgnoreCase))
                {
                    ValidateCivilOrMilitaryTarget(effect.Target, $"events[{eventIndex}].effects[{effectIndex}].target");
                    ValidateRequiredText(effect.BalanceKey, $"events[{eventIndex}].effects[{effectIndex}].balanceKey");
                    if (!ContractBalance.TryResolveHistoricalCampaignPriceMultiplier(effect.BalanceKey, out _))
                    {
                        throw new InvalidOperationException(
                            $"Historical campaign effect 'contract_price_modifier' at events[{eventIndex}].effects[{effectIndex}] has unknown balanceKey '{effect.BalanceKey}'.");
                    }
                }
                else if (string.Equals(effectType, BonusContractBatchEffectType, StringComparison.OrdinalIgnoreCase))
                {
                    ValidateCivilOrMilitaryTarget(effect.Target, $"events[{eventIndex}].effects[{effectIndex}].target");
                    ValidateRequiredText(effect.BalanceKey, $"events[{eventIndex}].effects[{effectIndex}].balanceKey");
                    if (!ContractBalance.TryResolveHistoricalCampaignBonusBatchRange(effect.BalanceKey, out _, out _))
                    {
                        throw new InvalidOperationException(
                            $"Historical campaign effect 'bonus_contract_batch' at events[{eventIndex}].effects[{effectIndex}] has unknown balanceKey '{effect.BalanceKey}'.");
                    }
                }
                else if (string.Equals(effectType, UnlockCategoryEffectType, StringComparison.OrdinalIgnoreCase))
                {
                    ValidateRequiredText(effect.Target, $"events[{eventIndex}].effects[{effectIndex}].target");
                    if (!Enum.TryParse<AircraftCategory>(effect.Target!.Trim(), ignoreCase: true, out _))
                    {
                        throw new InvalidOperationException(
                            $"Historical campaign effect 'unlock_category' at events[{eventIndex}].effects[{effectIndex}] has unknown category '{effect.Target}'.");
                    }
                }
                else if (string.Equals(effect.Type.Trim(), UnlockContractParameterEffectType, StringComparison.OrdinalIgnoreCase))
                {
                    ValidateRequiredText(effect.Target, $"events[{eventIndex}].effects[{effectIndex}].target");
                    if (!ContractBalance.TryNormalizeHistoricalCampaignContractParameter(effect.Target, out _))
                    {
                        throw new InvalidOperationException(
                            $"Historical campaign effect 'unlock_contract_parameter' at events[{eventIndex}].effects[{effectIndex}] has unknown parameter '{effect.Target}'.");
                    }
                }
                else if (string.Equals(effectType, TechnologyShiftEffectType, StringComparison.OrdinalIgnoreCase))
                {
                    ValidateRequiredText(effect.Target, $"events[{eventIndex}].effects[{effectIndex}].target");
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Historical campaign effect at events[{eventIndex}].effects[{effectIndex}] has unsupported type '{effect.Type}'.");
                }
            }
        }
    }

    public static int CountWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        var count = 0;
        var inWord = false;
        for (var index = 0; index < text.Length; index++)
        {
            if (char.IsWhiteSpace(text[index]))
            {
                if (inWord)
                {
                    count++;
                    inWord = false;
                }

                continue;
            }

            inWord = true;
        }

        if (inWord)
        {
            count++;
        }

        return count;
    }

    public static int CreateMonthKey(int year, int month) =>
        checked(((year - 1) * 12) + (month - 1));

    private static void ValidateRequiredText(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Historical campaign field '{fieldName}' is required.");
        }
    }

    private static void ValidateSupportedYearRange(int year, string fieldName)
    {
        if (year < MinimumSupportedYear || year > MaximumSupportedYear)
        {
            throw new InvalidOperationException(
                $"Historical campaign field '{fieldName}' must be in range [{MinimumSupportedYear}, {MaximumSupportedYear}].");
        }
    }

    private static void ValidateCivilOrMilitaryTarget(string? target, string fieldName)
    {
        ValidateRequiredText(target, fieldName);
        var normalized = target!.Trim();
        if (string.Equals(normalized, CivilTarget, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, MilitaryTarget, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        throw new InvalidOperationException(
            $"Historical campaign field '{fieldName}' must be '{CivilTarget}' or '{MilitaryTarget}', got '{target}'.");
    }
}
