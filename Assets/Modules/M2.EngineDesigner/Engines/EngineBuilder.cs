using System;
using WrightTycoon.Core.Campaign;
using WrightTycoon.Core.Data;
using WrightTycoon.Core.Physics;
using WrightTycoon.Core.Aircraft;
using WrightTycoon.Core.Research;
using WrightTycoon.Core.Production;
using System.Linq;

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
        => Build(definition, parameters, technologyModifiers, null);

    public Engine Build(
        EngineDefinition definition,
        EngineParameters parameters,
        TypeResearchModifierPoints researchModifiers)
        => Build(definition, parameters, _technologyModifierSource?.GetCurrentModifiers(), researchModifiers);

    public Engine Build(
        EngineDefinition definition,
        EngineParameters parameters,
        HistoricalCampaignTechnologyModifiers? technologyModifiers,
        TypeResearchModifierPoints? researchModifiers)
    {
        var report = _calculator.Calculate(definition, parameters, _atmosphereModel, technologyModifiers);
        var effects = TypeResearchModifierCalculator.Calculate(researchModifiers);
        // Always build from raw parameters, never from a previously modified report.
        report = new EnginePerformanceReport(
            report.OutputKind,
            report.AltitudePerformance,
            System.Math.Max(0d, report.Reliability + effects.ReliabilityDelta),
            report.Weight,
            report.Cost * effects.ProductionCostMultiplier,
            report.ProductionManDays * effects.ProductionManDaysMultiplier,
            report.EngineLengthMeters,
            report.EngineFrontalAreaSquareMeters,
            ScaleBreakdown(report.CostBreakdown, effects.ProductionCostMultiplier),
            ScaleBreakdown(report.ProductionManDaysBreakdown, effects.ProductionManDaysMultiplier));
        return new Engine(definition, parameters, report);
    }

    private static ProductionBreakdown ScaleBreakdown(ProductionBreakdown breakdown, double multiplier) =>
        new(breakdown.Items.Select(item => new ProductionBreakdownItem(item.Label, item.Amount * multiplier)));

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
