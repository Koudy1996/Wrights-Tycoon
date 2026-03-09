using System;
using WrightTycoon.Core.Simulation;

namespace WrightTycoon.Core.Campaign;

public sealed class HistoricalCampaignRuntimeContractModifierSource : IHistoricalCampaignContractModifierSource
{
    private readonly HistoricalCampaignService _service;
    private readonly HistoricalCampaignState _state;

    public HistoricalCampaignRuntimeContractModifierSource(
        HistoricalCampaignService service,
        HistoricalCampaignState state,
        Func<SimulationDate> currentDateProvider)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _ = currentDateProvider ?? throw new ArgumentNullException(nameof(currentDateProvider));
    }

    public HistoricalCampaignContractModifiers GetCurrentModifiers()
    {
        return _service.BuildActiveContractModifiers(_state);
    }
}
