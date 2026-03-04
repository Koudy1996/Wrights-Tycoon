using WrightTycoon.Core.Simulation;

namespace WrightTycoon.Core.Campaign;

public sealed class HistoricalCampaignArchiveEntry
{
    public string Title { get; set; } = string.Empty;

    public int Year { get; set; }

    public int Month { get; set; }

    public SimulationDate GetDate() => new(Year, Month);

    public static HistoricalCampaignArchiveEntry FromEvent(HistoricalCampaignEventDefinition definition)
    {
        return new HistoricalCampaignArchiveEntry
        {
            Title = definition.Title,
            Year = definition.Year,
            Month = definition.Month,
        };
    }
}
