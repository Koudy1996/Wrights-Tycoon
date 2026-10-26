using System;
using WrightTycoon.Core.Campaign;
using WrightTycoon.Core.Data;

namespace WrightTycoon.Core.Airframes;

public sealed class AirframeBuilder
{
    private readonly IAirframeCalculator _calculator;
    private readonly IHistoricalCampaignTechnologyModifierSource? _technologyModifierSource;

    public AirframeBuilder(
        IAirframeCalculator calculator,
        IHistoricalCampaignTechnologyModifierSource? technologyModifierSource = null)
    {
        _calculator = calculator ?? throw new ArgumentNullException(nameof(calculator));
        _technologyModifierSource = technologyModifierSource;
    }

    public Airframe Build(AirframeDefinition definition, AirframeParameters parameters)
    {
        var technologyModifiers = _technologyModifierSource?.GetCurrentModifiers();
        var report = _calculator.Calculate(definition, parameters, technologyModifiers);
        return new Airframe(definition, parameters, report);
    }

    public Airframe BuildFromRepository(string definitionId, AirframeParameters parameters, IAirframeDefinitionRepository repository)
    {
        if (repository is null)
        {
            throw new ArgumentNullException(nameof(repository));
        }

        var definition = repository.GetById(definitionId);
        return Build(definition, parameters);
    }

    public Airframe Build(AirframeDefinition definition, AirframeParameters parameters, AirframeTechnologyEffects effects)
    {
        if (effects is null) throw new ArgumentNullException(nameof(effects));
        return new Airframe(definition, parameters, _calculator.Calculate(definition, parameters, null, effects));
    }
}
