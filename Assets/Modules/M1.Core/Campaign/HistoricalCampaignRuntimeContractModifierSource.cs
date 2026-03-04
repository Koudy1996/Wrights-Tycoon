using System;

namespace WrightTycoon.Core.Campaign;

public sealed class HistoricalCampaignRuntimeContractModifierSource : IHistoricalCampaignContractModifierSource
{
    private readonly HistoricalCampaignService _service;
    private readonly HistoricalCampaignState _state;

    public HistoricalCampaignRuntimeContractModifierSource(
        HistoricalCampaignService service,
        HistoricalCampaignState state)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _state = state ?? throw new ArgumentNullException(nameof(state));
    }

    public HistoricalCampaignContractModifiers GetCurrentModifiers() => _service.BuildActiveContractModifiers(_state);
}
