using System;
using WrightTycoon.Core.Airframes;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Data;
using WrightTycoon.Core.Engines;
using WrightTycoon.Core.Math;
using WrightTycoon.Core.Research;

namespace WrightTycoon.Core.Aircraft;

public sealed class AircraftFactory
{
    private readonly EngineBuilder _engineBuilder;
    private readonly AirframeBuilder _airframeBuilder;
    private readonly AircraftPerformanceCalculator _performanceCalculator;
    private readonly DevelopmentCostCalculator _developmentCostCalculator;
    private readonly IEngineDefinitionRepository _engineRepository;
    private readonly IAirframeDefinitionRepository _airframeRepository;

    public AircraftFactory(
        EngineBuilder engineBuilder,
        AirframeBuilder airframeBuilder,
        AircraftPerformanceCalculator performanceCalculator,
        DevelopmentCostCalculator developmentCostCalculator,
        IEngineDefinitionRepository engineRepository,
        IAirframeDefinitionRepository airframeRepository)
    {
        _engineBuilder = engineBuilder ?? throw new ArgumentNullException(nameof(engineBuilder));
        _airframeBuilder = airframeBuilder ?? throw new ArgumentNullException(nameof(airframeBuilder));
        _performanceCalculator = performanceCalculator ?? throw new ArgumentNullException(nameof(performanceCalculator));
        _developmentCostCalculator = developmentCostCalculator ?? throw new ArgumentNullException(nameof(developmentCostCalculator));
        _engineRepository = engineRepository ?? throw new ArgumentNullException(nameof(engineRepository));
        _airframeRepository = airframeRepository ?? throw new ArgumentNullException(nameof(airframeRepository));
    }

    public Aircraft CreateAircraft(AircraftFamilyDefinition familyDefinition, AircraftTypeDefinition typeDefinition)
    {
        if (familyDefinition is null)
        {
            throw new ArgumentNullException(nameof(familyDefinition));
        }

        if (typeDefinition is null)
        {
            throw new ArgumentNullException(nameof(typeDefinition));
        }

        familyDefinition.Validate();
        typeDefinition.Validate();

        if (!string.Equals(familyDefinition.Id, typeDefinition.FamilyId, StringComparison.Ordinal))
        {
            throw new ArgumentException("Aircraft type family id must match the provided family.", nameof(typeDefinition));
        }

        var engineDefinition = _engineRepository.GetById(familyDefinition.EngineDefinitionId);
        if (engineDefinition.EngineType != familyDefinition.LockedEngineType)
        {
            throw new InvalidOperationException(
                $"Family lock mismatch. Engine definition '{engineDefinition.Id}' is '{engineDefinition.EngineType}' but family requires '{familyDefinition.LockedEngineType}'.");
        }

        if (!AircraftEngineFamilyCompatibility.IsEngineTypeAllowed(familyDefinition.AllowedEngineFamily, engineDefinition.EngineType))
        {
            throw new InvalidOperationException(
                $"Family engine policy mismatch. Engine definition '{engineDefinition.Id}' is '{engineDefinition.EngineType}' but family allows '{familyDefinition.AllowedEngineFamily}'.");
        }

        var airframeDefinition = _airframeRepository.GetById(familyDefinition.AirframeDefinitionId);
        var baseEngine = familyDefinition.BaseEngineParameters.Clone();
        var baseAirframe = familyDefinition.BaseAirframeParameters.Clone();

        var variantEngine = ApplyEngineDelta(baseEngine, typeDefinition.EngineDelta);
        var variantAirframe = ApplyAirframeDelta(baseAirframe, typeDefinition.AirframeDelta);

        // Family-level locks keep variants inside one product line.
        variantAirframe.StructureType = familyDefinition.LockedStructureType;
        variantAirframe.MaterialType = familyDefinition.LockedMaterialType;
        variantAirframe.EngineCount = familyDefinition.LockedEngineCount;

        variantEngine.Validate();
        variantAirframe.Validate();

        var engine = _engineBuilder.Build(engineDefinition, variantEngine);
        var airframe = _airframeBuilder.Build(airframeDefinition, variantAirframe);
        var performance = _performanceCalculator.Calculate(engine, airframe, familyDefinition.Category);
        var researchModifierEffects = TypeResearchModifierCalculator.Calculate(typeDefinition.ResearchModifiers);
        performance = ApplyTypeResearchModifierEffects(performance, researchModifierEffects);
        var development = _developmentCostCalculator.Calculate(familyDefinition, baseEngine, variantEngine, baseAirframe, variantAirframe);

        return new Aircraft(familyDefinition, typeDefinition, engine, airframe, performance, development);
    }

    private static EngineParameters ApplyEngineDelta(EngineParameters baseline, EngineParameterDelta? delta)
    {
        if (baseline is null)
        {
            throw new ArgumentNullException(nameof(baseline));
        }

        delta ??= new EngineParameterDelta();
        var result = baseline.Clone();
        result.DisplacementLiters += delta.DisplacementLitersDelta;
        result.CompressionRatio += delta.CompressionRatioDelta;
        result.OptimizationAltitudeMeters += delta.OptimizationAltitudeMetersDelta;
        result.TurbineDiameterMeters += delta.TurbineDiameterMetersDelta;
        result.BypassRatio += delta.BypassRatioDelta;
        result.CoolingEfficiency = Numeric.Clamp(result.CoolingEfficiency + delta.CoolingEfficiencyDelta, 0d, 1d);
        if (delta.BoostTypeOverride.HasValue)
        {
            result.BoostType = delta.BoostTypeOverride.Value;
        }

        return result;
    }

    private static AirframeParameters ApplyAirframeDelta(AirframeParameters baseline, AirframeParameterDelta? delta)
    {
        if (baseline is null)
        {
            throw new ArgumentNullException(nameof(baseline));
        }

        delta ??= new AirframeParameterDelta();
        var result = baseline.Clone();
        result.WingAreaSquareMeters += delta.WingAreaSquareMetersDelta;
        result.FuelAmountLiters += delta.FuelAmountLitersDelta;
        result.PayloadLimitKilograms = delta.PayloadLimitKilogramsOverride;
        result.CrewCount += delta.CrewCountDelta;
        result.EngineCowling = Numeric.Clamp(result.EngineCowling + delta.EngineCowlingDelta, 0.1d, 0.9d);
        result.AerodynamicCleanlinessPercent = Numeric.Clamp(
            result.AerodynamicCleanlinessPercent + delta.AerodynamicCleanlinessPercentDelta,
            PlaneDesignerBalance.Airframe.AerodynamicCleanlinessMinPercent,
            PlaneDesignerBalance.Airframe.AerodynamicCleanlinessMaxPercent);

        if (delta.EngineCountOverride.HasValue)
        {
            result.EngineCount = delta.EngineCountOverride.Value;
        }

        if (delta.WingPositionOverride.HasValue)
        {
            result.WingPosition = delta.WingPositionOverride.Value;
        }

        if (delta.WingShapeOverride.HasValue)
        {
            result.WingShape = delta.WingShapeOverride.Value;
        }

        if (delta.WingProfileOverride.HasValue)
        {
            result.WingProfile = delta.WingProfileOverride.Value;
        }

        if (delta.TailTypeOverride.HasValue)
        {
            result.TailType = delta.TailTypeOverride.Value;
        }

        if (delta.FlapTypeOverride.HasValue)
        {
            result.FlapType = delta.FlapTypeOverride.Value;
        }

        if (delta.HasLeadingEdgeFlapsOverride.HasValue)
        {
            result.HasLeadingEdgeFlaps = delta.HasLeadingEdgeFlapsOverride.Value;
        }

        if (delta.HasBackupSystemsOverride.HasValue)
        {
            result.HasBackupSystems = delta.HasBackupSystemsOverride.Value;
        }

        if (delta.LandingGearTypeOverride.HasValue)
        {
            result.LandingGearType = delta.LandingGearTypeOverride.Value;
        }

        if (delta.RunwayTypeOverride.HasValue)
        {
            result.RunwayType = delta.RunwayTypeOverride.Value;
        }

        return result;
    }

    private static AircraftPerformanceReport ApplyTypeResearchModifierEffects(
        AircraftPerformanceReport performance,
        TypeResearchModifierEffects effects)
    {
        var productionCost = System.Math.Max(0d, performance.TotalProductionCost * effects.ProductionCostMultiplier);
        var productionManDays = System.Math.Max(0d, performance.TotalProductionManDays * effects.ProductionManDaysMultiplier);
        var reliability = Numeric.Clamp(performance.OverallReliability + effects.ReliabilityDelta, 0.2d, 0.995d);

        return performance with
        {
            TotalProductionCost = productionCost,
            TotalProductionManDays = productionManDays,
            OverallReliability = reliability,
        };
    }
}

