namespace WrightTycoon.Core.Campaign;

public sealed class HistoricalCampaignEffectDefinition
{
    public string Type { get; init; } = string.Empty;

    public string? BalanceKey { get; init; }

    public string? Target { get; init; }

    public string? ValueId { get; init; }
}
