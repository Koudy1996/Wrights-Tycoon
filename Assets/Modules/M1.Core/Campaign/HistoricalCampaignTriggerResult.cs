namespace WrightTycoon.Core.Campaign;

public sealed class HistoricalCampaignTriggerResult
{
    public HistoricalCampaignTriggerResult(HistoricalCampaignEventDefinition? triggeredEvent)
    {
        TriggeredEvent = triggeredEvent;
    }

    public HistoricalCampaignEventDefinition? TriggeredEvent { get; }

    public bool HasTriggeredEvent => TriggeredEvent is not null;
}
