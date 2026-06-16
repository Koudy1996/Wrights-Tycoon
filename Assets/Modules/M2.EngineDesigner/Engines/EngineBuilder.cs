using System;
using WrightTycoon.Core.Campaign;
using WrightTycoon.Core.Data;
using WrightTycoon.Core.Physics;

namespace WrightTycoon.Core.Engines;

public sealed class EngineBuilder
{
    private readonly IEngineCalculator _calculator;
    private readonly IAtmosphereModel _atmosphereModel;
    private readonly IHistoricalCampaignTechnologyModifierSource? _technologyModifierSource;

    public EngineBuilder(
        IEngineCalculator calculator,
        IAtmosphereModel atmosphereModel,
        IHistoricalCampaignTechnologyModifierSource? technologyModifierSource = null)
    {
        _calculator = calculator ?? throw new ArgumentNullException(nameof(calculator));
        _atmosphereModel = atmosphereModel ?? throw new ArgumentNullException(nameof(atmosphereModel));
        _technologyModifierSource = technologyModifierSource;
    }

    public Engine Build(EngineDefinition definition, EngineParameters parameters)
    {
        var technologyModifiers = _technologyModifierSource?.GetCurrentModifiers();
        return Build(definition, parameters, technologyModifiers);
    }

    public Engine Build(
        EngineDefinition definition,
        EngineParameters parameters,
        HistoricalCampaignTechnologyModifiers? technologyModifiers)
    {
        var report = _calculator.Calculate(definition, parameters, _atmosphereModel, technologyModifiers);
        return new Engine(definition, parameters, report);
    }

    public Engine BuildFromRepository(string definitionId, EngineParameters parameters, IEngineDefinitionRepository repository)
    {
        if (repository is null)
        {
            throw new ArgumentNullException(nameof(repository));
        }

        var definition = repository.GetById(definitionId);
        return Build(definition, parameters);
    }
}
