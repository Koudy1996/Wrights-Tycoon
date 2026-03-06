using System;
using WrightTycoon.Core.Simulation;

namespace WrightTycoon.Core.Campaign;

public sealed class HistoricalCampaignRuntimeTechnologyModifierSource : IHistoricalCampaignTechnologyModifierSource
{
    private readonly HistoricalCampaignTechnologyProgression _progression;
    private readonly Func<SimulationDate> _currentDateProvider;

    public HistoricalCampaignRuntimeTechnologyModifierSource(
        HistoricalCampaignTechnologyProgression progression,
        Func<SimulationDate> currentDateProvider)
    {
        _progression = progression ?? throw new ArgumentNullException(nameof(progression));
        _currentDateProvider = currentDateProvider ?? throw new ArgumentNullException(nameof(currentDateProvider));
    }

    public HistoricalCampaignTechnologyModifiers GetCurrentModifiers() =>
        _progression.ResolveForDate(_currentDateProvider());
}
