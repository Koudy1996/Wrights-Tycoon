using System;
using WrightTycoon.Core.Airframes;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Campaign;
using WrightTycoon.Core.Data;
using WrightTycoon.Core.Engines;
using WrightTycoon.Core.Math;
using WrightTycoon.Core.Research;
using WrightTycoon.Core.Fleet;
using WrightTycoon.Core.Production;
using System.Collections.Generic;

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

        if (!AircraftEngineClassificationCompatibility.IsEngineTypeAllowed(familyDefinition.EngineClassification, engineDefinition.EngineType))
        {
            throw new InvalidOperationException(
                $"Family engine classification mismatch. Engine definition '{engineDefinition.Id}' is '{engineDefinition.EngineType}' but family requires '{familyDefinition.EngineClassification}'.");
        }

        var airframeDefinition = _airframeRepository.GetById(familyDefinition.AirframeDefinitionId);
        var baseEngine = familyDefinition.BaseEngineParameters.Clone();
        var baseAirframe = familyDefinition.BaseAirframeParameters.Clone();

        var variantEngine = ApplyEngineDelta(baseEngine, typeDefinition.EngineDelta);
        var variantAirframe = ApplyAirframeDelta(baseAirframe, typeDefinition.AirframeDelta);

        // Family-level locks keep variants inside one product line.
        variantAirframe.StructureType = familyDefinition.LockedStructureType;
        variantAirframe.MaterialType = familyDefinition.LockedMaterialType;
        variantAirframe.ConstructionStandard = familyDefinition.ConstructionStandard;
        variantAirframe.EngineCount = familyDefinition.LockedEngineCount;
        ValidateTypeWingAreaWithinFamilyEnvelope(
            familyDefinition,
            baseAirframe.WingAreaSquareMeters,
            variantAirframe.WingAreaSquareMeters);

        variantEngine.Validate();
        variantAirframe.Validate();

        var engine = familyDefinition.InstalledEngineTechnologyModifiers.HasValue
            ? _engineBuilder.Build(
                engineDefinition,
                variantEngine,
                familyDefinition.InstalledEngineTechnologyModifiers.Value,
                familyDefinition.InstalledEngineResearchModifiers)
            : _engineBuilder.Build(engineDefinition, variantEngine, familyDefinition.InstalledEngineResearchModifiers);
        var technologyEffects = typeDefinition.TechnologyModelVersion == 2
            ? AirframeTechnologyEffects.Resolve(
                familyDefinition.TechnologySnapshot ?? throw new InvalidOperationException("Weighted technology requires a frozen family snapshot."),
                typeDefinition.TechnologySnapshot!)
            : null;
        var airframe = technologyEffects is null
            ? _airframeBuilder.Build(airframeDefinition, variantAirframe)
            : _airframeBuilder.Build(airframeDefinition, variantAirframe, technologyEffects);
        var performance = _performanceCalculator.Calculate(
            engine,
            airframe,
            familyDefinition.Category,
            typeDefinition.NavigationEquipment,
            ResolveRoleModule(typeDefinition),
            typeDefinition.ConstructionDurabilityLevel,
            typeDefinition.ServiceIntervalHours,
            technologyEffects);
        var researchModifierEffects = TypeResearchModifierCalculator.CalculateCombined(
            familyDefinition.ResearchModifiers, typeDefinition.ResearchModifiers);
        performance = ApplyTypeResearchModifierEffects(performance, researchModifierEffects);
        performance = performance with
        {
            ExpectedLifeHours = LifecycleCalculator.CalculateExpectedLifeHours(
                familyDefinition.Category, performance.OverallReliability,
                typeDefinition.ServiceIntervalHours, typeDefinition.ConstructionDurabilityLevel),
        };
        var development = _developmentCostCalculator.Calculate(familyDefinition, baseEngine, variantEngine, baseAirframe, variantAirframe);

        return new Aircraft(familyDefinition, typeDefinition, engine, airframe, performance, development);
    }

    private static AircraftCategoryRoleRequirement ResolveRoleModule(AircraftTypeDefinition typeDefinition)
    {
        if (typeDefinition.HasPassengerCabin)
        {
            return AircraftCategoryRoleRequirement.PassengerCabin;
        }

        if (typeDefinition.HasCargoSpace)
        {
            return AircraftCategoryRoleRequirement.CargoSpace;
        }

        if (typeDefinition.HasSpecialEquipment)
        {
            return AircraftCategoryRoleRequirement.SpecialEquipment;
        }

        if (typeDefinition.HasCombatEquipment)
        {
            return AircraftCategoryRoleRequirement.CombatEquipment;
        }

        return AircraftCategoryRoleRequirement.NoRoleModule;
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
        if (delta.PrecisionEngineeringLevelOverride.HasValue)
        {
            result.PrecisionEngineeringLevel = delta.PrecisionEngineeringLevelOverride.Value;
        }
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

    private static void ValidateTypeWingAreaWithinFamilyEnvelope(
        AircraftFamilyDefinition familyDefinition,
        double baseWingAreaSquareMeters,
        double variantWingAreaSquareMeters)
    {
        var referenceWingArea = familyDefinition.ReferenceWingAreaSquareMeters ?? baseWingAreaSquareMeters;
        var minimumWingArea = referenceWingArea * 0.8d;
        var maximumWingArea = referenceWingArea * 1.2d;
        if (variantWingAreaSquareMeters + Numeric.Epsilon < minimumWingArea
            || variantWingAreaSquareMeters - Numeric.Epsilon > maximumWingArea)
        {
            throw new InvalidOperationException(
                $"Type wing area {variantWingAreaSquareMeters:0.###} m2 is outside the family envelope [{minimumWingArea:0.###}, {maximumWingArea:0.###}] m2.");
        }
    }

    private static AircraftPerformanceReport ApplyTypeResearchModifierEffects(
        AircraftPerformanceReport performance,
        TypeResearchModifierEffects effects)
    {
        // Finished motors already carry their own research recipe. Aircraft
        // engineering optimizes only the airframe, systems and assembly share.
        var airframeCost = performance.AirframeProductionCost * effects.ProductionCostMultiplier;
        var airframeManDays = performance.AirframeProductionManDays * effects.ProductionManDaysMultiplier;
        var reliability = System.Math.Max(0.2d, performance.OverallReliability + effects.ReliabilityDelta);

        return performance with
        {
            TotalProductionCost = airframeCost + performance.EnginesProductionCost,
            TotalProductionManDays = airframeManDays + performance.EnginesProductionManDays,
            AirframeProductionCost = airframeCost,
            AirframeProductionManDays = airframeManDays,
            CostBreakdown = AddResearchAdjustment(performance.CostBreakdown,
                performance.TotalProductionCost, airframeCost - performance.AirframeProductionCost),
            ProductionManDaysBreakdown = AddResearchAdjustment(performance.ProductionManDaysBreakdown,
                performance.TotalProductionManDays, airframeManDays - performance.AirframeProductionManDays),
            OverallReliability = reliability,
        };
    }

    private static ProductionBreakdown AddResearchAdjustment(ProductionBreakdown? breakdown, double total, double adjustment)
    {
        var items = new List<ProductionBreakdownItem>();
        if (breakdown is null) items.Add(new ProductionBreakdownItem("Aircraft", total));
        else items.AddRange(breakdown.Items);
        items.Add(new ProductionBreakdownItem("Family + type production optimization (airframe/assembly)", adjustment));
        return new ProductionBreakdown(items);
    }
}

