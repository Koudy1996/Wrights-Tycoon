using System;
using System.Collections.Generic;
using WrightTycoon.Core.Simulation;

namespace WrightTycoon.Core.Campaign;

public static class HistoricalCampaignValidator
{
    public const int ArticleWordLimit = 100;

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
}
