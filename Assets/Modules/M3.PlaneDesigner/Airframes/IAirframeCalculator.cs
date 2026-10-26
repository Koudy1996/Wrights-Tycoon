using WrightTycoon.Core.Campaign;

namespace WrightTycoon.Core.Airframes;

public interface IAirframeCalculator
{
    AirframePerformanceReport Calculate(
        AirframeDefinition definition,
        AirframeParameters parameters,
        HistoricalCampaignTechnologyModifiers? technologyModifiers = null,
        AirframeTechnologyEffects? technologyEffects = null);
}
