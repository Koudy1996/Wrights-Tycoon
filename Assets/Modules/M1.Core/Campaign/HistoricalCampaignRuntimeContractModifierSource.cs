using System;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Simulation;

namespace WrightTycoon.Core.Campaign;

public sealed class HistoricalCampaignRuntimeContractModifierSource : IHistoricalCampaignContractModifierSource
{
    private readonly HistoricalCampaignService _service;
    private readonly HistoricalCampaignState _state;
    private readonly Func<SimulationDate> _currentDateProvider;

    public HistoricalCampaignRuntimeContractModifierSource(
        HistoricalCampaignService service,
        HistoricalCampaignState state,
        Func<SimulationDate> currentDateProvider)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _currentDateProvider = currentDateProvider ?? throw new ArgumentNullException(nameof(currentDateProvider));
    }

    public HistoricalCampaignContractModifiers GetCurrentModifiers()
    {
        var modifiers = _service.BuildActiveContractModifiers(_state);
        var currentDate = _currentDateProvider();
        var requirementDifficultyMultiplier = ContractBalance.ResolveHistoricalCampaignRequirementDifficultyMultiplier(currentDate.Year);
        modifiers.SetContractRequirementDifficultyMultiplier(requirementDifficultyMultiplier);
        return modifiers;
    }
}
