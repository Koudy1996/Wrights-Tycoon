using System;
using WrightTycoon.Core.Data;

namespace WrightTycoon.Core.Airframes;

public sealed class AirframeBuilder
{
    private readonly IAirframeCalculator _calculator;

    public AirframeBuilder(IAirframeCalculator calculator)
    {
        _calculator = calculator ?? throw new ArgumentNullException(nameof(calculator));
    }

    public Airframe Build(AirframeDefinition definition, AirframeParameters parameters)
    {
        var report = _calculator.Calculate(definition, parameters);
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
}
