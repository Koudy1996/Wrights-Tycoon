using System.Collections.Generic;
using WrightTycoon.Core.Simulation;

namespace WrightTycoon.Core.Campaign;

public sealed class HistoricalCampaignEventDefinition
{
    public string Id { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public int Year { get; init; }

    public int Month { get; init; }

    public string ArticleText { get; init; } = string.Empty;

    public List<HistoricalCampaignEffectDefinition> Effects { get; init; } = new();

    public SimulationDate GetDate() => new(Year, Month);

    public int GetArticleWordCount() => HistoricalCampaignValidator.CountWords(ArticleText);

    public override string ToString() => $"{Id} ({Year:D4}-{Month:D2})";
}
