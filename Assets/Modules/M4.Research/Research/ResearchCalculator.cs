using System;
using WrightTycoon.Core.Aircraft;
using AircraftModel = WrightTycoon.Core.Aircraft.Aircraft;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Engines;
using WrightTycoon.Core.Math;

namespace WrightTycoon.Core.Research;

public sealed class ResearchCalculator
{
    public ResearchQuote CalculateEngineQuote(Engine engine, TypeResearchModifierPoints? modifierPoints = null)
    {
        if (engine is null)
        {
            throw new ArgumentNullException(nameof(engine));
        }

        var complexity = CalculateEngineComplexity(engine);

        var multiplier = Lerp(ResearchBalance.Engine.CostMultiplierMin, ResearchBalance.Engine.CostMultiplierMax, complexity);
        var months = RoundToInt(Lerp(ResearchBalance.Engine.TimeMonthsMin, ResearchBalance.Engine.TimeMonthsMax, complexity));
        var cost = RoundCurrency(engine.Performance.CompleteProductionCost * multiplier);
        var baseQuote = new ResearchQuote(cost, months, complexity);
        return ApplyResearchModifierEffects(baseQuote, modifierPoints);
    }

    public ResearchQuote CalculateFamilyQuote(AircraftModel aircraft, TypeResearchModifierPoints? modifierPoints = null)
    {
        if (aircraft is null)
        {
            throw new ArgumentNullException(nameof(aircraft));
        }

        var complexity = CalculateFamilyComplexity(aircraft);
        var complexityCostMultiplier = Lerp(
            ResearchBalance.Family.ComplexityCostMultiplierMin,
            ResearchBalance.Family.ComplexityCostMultiplierMax,
            complexity);
        var cost = RoundCurrency(
            aircraft.Performance.AirframeCompleteProductionCost
            * ResearchBalance.Family.CostMultiplier
            * complexityCostMultiplier);
        var months = RoundToInt(Lerp(ResearchBalance.Family.TimeMonthsMin, ResearchBalance.Family.TimeMonthsMax, complexity));
        var baseQuote = new ResearchQuote(cost, months, complexity);
        return ApplyResearchModifierEffects(baseQuote, modifierPoints);
    }

    public ResearchQuote CalculateTypeQuote(AircraftModel aircraft, TypeResearchModifierPoints? modifierPoints = null)
    {
        if (aircraft is null)
        {
            throw new ArgumentNullException(nameof(aircraft));
        }

        var maxSpeedKmh = 0d;
        if (aircraft.Performance.MaxSpeedByAltitude.Count > 0)
        {
            maxSpeedKmh = aircraft.Performance.MaxSpeedByAltitude[0].MaxSpeedMetersPerSecond * 3.6d;
        }

        var magnitudeNormalized = Normalize(
            aircraft.Development.ChangeMagnitudeScore,
            ResearchBalance.Type.ChangeMagnitudeMin,
            ResearchBalance.Type.ChangeMagnitudeMax);
        var changedCountNormalized = Normalize(
            aircraft.Development.ChangedParameterCount,
            ResearchBalance.Type.ChangedCountMin,
            ResearchBalance.Type.ChangedCountMax);
        var weightNormalized = Normalize(
            aircraft.Performance.TotalWeight.Kilograms,
            ResearchBalance.Type.WeightMinKilograms,
            ResearchBalance.Type.WeightMaxKilograms);
        var speedNormalized = Normalize(
            maxSpeedKmh,
            ResearchBalance.Type.SpeedMinKilometersPerHour,
            ResearchBalance.Type.SpeedMaxKilometersPerHour);

        var complexity = (magnitudeNormalized * ResearchBalance.Type.ComplexityWeightMagnitude)
            + (changedCountNormalized * ResearchBalance.Type.ComplexityWeightChangedCount)
            + (weightNormalized * ResearchBalance.Type.ComplexityWeightWeight)
            + (speedNormalized * ResearchBalance.Type.ComplexityWeightSpeed);
        complexity = Numeric.Clamp(complexity, 0d, 1d);

        var baseMultiplier = Lerp(ResearchBalance.Type.CostMultiplierMin, ResearchBalance.Type.CostMultiplierMax, complexity);
        var baseMonths = RoundToInt(Lerp(ResearchBalance.Type.TimeMonthsMin, ResearchBalance.Type.TimeMonthsMax, complexity));
        var baseCost = RoundCurrency(aircraft.Performance.CompleteProductionCost * baseMultiplier);
        var baseQuote = new ResearchQuote(baseCost, baseMonths, complexity);
        var effectiveModifierPoints = modifierPoints ?? aircraft.TypeDefinition.ResearchModifiers;
        return ApplyResearchModifierEffects(baseQuote, effectiveModifierPoints);
    }

    private static double CalculateEngineComplexity(Engine engine)
    {
        var parameters = engine.Parameters;
        var isJet = engine.Performance.OutputKind == EngineOutputKind.ThrustNewtons;
        var size = isJet
            ? Normalize(parameters.TurbineDiameterMeters, 0.8d, 3d)
            : Numeric.Clamp(
                (Normalize(parameters.CylinderCount, 4d, 18d)
                 + Normalize(parameters.DisplacementLiters, 2.4d, 100d)) * 0.5d,
                0d,
                1d);
        var compression = isJet
            ? Normalize(parameters.CompressionRatio, 6d, 50d)
            : Normalize(parameters.CompressionRatio, 6d, 20d);
        var optionComplexity = 0d;
        if (parameters.BoostType != BoostType.None)
        {
            optionComplexity += 0.08d;
        }

        if (parameters.PistonInjectionType == PistonInjectionType.DirectInjection && !isJet)
        {
            optionComplexity += 0.04d;
        }

        if (parameters.HasAfterburner)
        {
            optionComplexity += 0.12d;
        }

        if (isJet && engine.Definition.EngineType == EngineType.Turbofan)
        {
            optionComplexity += Numeric.Clamp(parameters.BypassRatio / 20d, 0d, 1d) * 0.12d;
        }

        var rpmReference = isJet
            ? ProductionEconomyBalance.Engine.JetReferenceRpm
            : ProductionEconomyBalance.Engine.PistonReferenceRpm;
        var rpmComplexity = Numeric.Clamp((parameters.MaximumSpeed / rpmReference) - 1d, 0d, 1d);
        var optimization = Numeric.Clamp(parameters.EfficiencyOptimizationsPercent / 30d, 0d, 1d);
        var precision = Numeric.Clamp(parameters.PrecisionEngineeringLevel / 10d, 0d, 1d);
        return Numeric.Clamp(
            GetEngineTypeComplexityBase(engine.Definition.EngineType)
            + (size * 0.15d)
            + (compression * 0.15d)
            + optionComplexity
            + (rpmComplexity * 0.08d)
            + (optimization * 0.10d)
            + (precision * 0.15d),
            0d,
            1d);
    }

    private static double CalculateFamilyComplexity(AircraftModel aircraft)
    {
        var parameters = aircraft.Airframe.Parameters;
        var payload = parameters.PayloadLimitKilograms
            ?? System.Math.Max(
                ProductionEconomyBalance.Airframe.DefaultPayloadKilograms,
                aircraft.Airframe.Definition.BaseStructuralWeightKilograms * ProductionEconomyBalance.Airframe.BaseWeightPayloadFactor);
        var structuralIndex = ProductionEconomyBalance.CalculateStructuralIndex(
            parameters.WingAreaSquareMeters,
            payload,
            parameters.FuelAmountLiters);
        var size = NormalizeLogarithmic(
            structuralIndex,
            ProductionEconomyBalance.Airframe.ReferenceStructuralIndex,
            2000d);
        var complexity = 0.20d + (size * 0.50d);
        if (parameters.MaterialType == WrightTycoon.Core.Airframes.MaterialType.Composite)
        {
            complexity += 0.10d;
        }

        if (parameters.StructureType == WrightTycoon.Core.Airframes.StructureType.Monocoque)
        {
            complexity += 0.05d;
        }

        if (parameters.WingShape != WrightTycoon.Core.Airframes.WingShape.Straight)
        {
            complexity += 0.08d;
        }

        complexity += Numeric.Clamp(parameters.PrecisionEngineeringLevel / 10d, 0d, 1d) * 0.17d;
        return Numeric.Clamp(complexity, 0d, 1d);
    }

    private static double NormalizeLogarithmic(double value, double minValue, double maxValue)
    {
        var clamped = Numeric.Clamp(value, minValue, maxValue);
        return Normalize(System.Math.Log(clamped), System.Math.Log(minValue), System.Math.Log(maxValue));
    }

    private static ResearchQuote ApplyPrecisionEngineeringDevelopmentEffects(ResearchQuote quote, int level)
    {
        var effects = PrecisionEngineeringBalance.GetEffects(level);
        var cost = RoundCurrency(quote.ResearchCost * effects.DevelopmentCostMultiplier);
        var months = System.Math.Max(1, RoundToInt(quote.ResearchTimeMonths * effects.DevelopmentTimeMultiplier));
        return new ResearchQuote(cost, months, quote.ComplexityNormalized);
    }

    private static ResearchQuote ApplyResearchModifierEffects(ResearchQuote baseQuote, TypeResearchModifierPoints? modifierPoints)
    {
        var effects = TypeResearchModifierCalculator.Calculate(modifierPoints);
        var months = System.Math.Max(1, RoundToInt(baseQuote.ResearchTimeMonths * effects.ResearchTimeMultiplier));
        var cost = RoundCurrency(baseQuote.ResearchCost * effects.ResearchCostMultiplier);
        return new ResearchQuote(cost, months, baseQuote.ComplexityNormalized);
    }

    private static double ResolveSeaLevelOutputForComplexity(Engine engine)
    {
        if (engine.Performance.AltitudePerformance.Count == 0)
        {
            return 0d;
        }

        var point = engine.Performance.AltitudePerformance[0];
        for (var index = 0; index < engine.Performance.AltitudePerformance.Count; index++)
        {
            var candidate = engine.Performance.AltitudePerformance[index];
            if (System.Math.Abs(candidate.AltitudeMeters) <= 0.0001d)
            {
                point = candidate;
                break;
            }
        }

        if (engine.Performance.OutputKind == EngineOutputKind.PowerKilowatts)
        {
            return point.Output;
        }

        return point.ThrustNewtons / 1000d;
    }

    private static double GetEngineTypeComplexityBase(EngineType engineType)
    {
        return engineType switch
        {
            EngineType.Piston => ResearchBalance.Engine.ComplexityTypeBasePiston,
            EngineType.Inline => ResearchBalance.Engine.ComplexityTypeBaseInline,
            EngineType.V => ResearchBalance.Engine.ComplexityTypeBaseV,
            EngineType.Radial => ResearchBalance.Engine.ComplexityTypeBaseRadial,
            EngineType.Turbojet => ResearchBalance.Engine.ComplexityTypeBaseTurbojet,
            EngineType.Turbofan => ResearchBalance.Engine.ComplexityTypeBaseTurbofan,
            _ => ResearchBalance.Engine.ComplexityTypeBasePiston,
        };
    }

    private static double Normalize(double value, double minValue, double maxValue)
    {
        if (maxValue - minValue <= Numeric.Epsilon)
        {
            return 0d;
        }

        return Numeric.Clamp((value - minValue) / (maxValue - minValue), 0d, 1d);
    }

    private static double Lerp(double a, double b, double t) => a + ((b - a) * t);

    private static int RoundToInt(double value) => (int)System.Math.Round(value, MidpointRounding.AwayFromZero);

    private static double RoundCurrency(double value) => System.Math.Round(value, 2, MidpointRounding.AwayFromZero);
}

public readonly record struct TypeResearchModifierEffects(
    double ReliabilityDelta,
    double ProductionCostMultiplier,
    double ProductionManDaysMultiplier,
    double ResearchCostMultiplier,
    double ResearchTimeMultiplier);

public static class TypeResearchModifierCalculator
{
    // Each project retains its own 0..10 limit; engineering effects add across
    // sources rather than clamping the combined total or multiplying discounts.
    public static TypeResearchModifierEffects CalculateCombined(
        TypeResearchModifierPoints? familyPoints,
        TypeResearchModifierPoints? typePoints)
    {
        var family = Calculate(familyPoints);
        var type = Calculate(typePoints);
        return new TypeResearchModifierEffects(
            family.ReliabilityDelta + type.ReliabilityDelta,
            family.ProductionCostMultiplier + type.ProductionCostMultiplier - 1d,
            family.ProductionManDaysMultiplier + type.ProductionManDaysMultiplier - 1d,
            1d,
            1d);
    }

    public static TypeResearchModifierEffects Calculate(TypeResearchModifierPoints? points)
    {
        if (points is null)
        {
            return new TypeResearchModifierEffects(0d, 1d, 1d, 1d, 1d);
        }

        var reliabilityPoints = TypeResearchModifierPoints.ClampPoints(points.ReliabilityPoints);
        var productionPoints = TypeResearchModifierPoints.ClampPoints(points.ProductionOptimizationPoints);
        var fastDevelopmentPoints = TypeResearchModifierPoints.ClampPoints(points.FastDevelopmentPoints);

        var reliabilityDelta = reliabilityPoints * ResearchBalance.Type.ReliabilityPerPoint;
        var productionCostMultiplier = System.Math.Max(
            0.05d,
            1d - (productionPoints * ResearchBalance.Type.ProductionCostReductionPerPoint));
        var productionManDaysMultiplier = System.Math.Max(
            0.05d,
            1d - (productionPoints * ResearchBalance.Type.ProductionManDaysReductionPerPoint));

        var researchCostMultiplier = 1d
            + ((reliabilityPoints + productionPoints) * ResearchBalance.Type.ResearchCostIncreasePerPoint)
            + (fastDevelopmentPoints * ResearchBalance.Type.FastDevelopmentCostIncreasePerPoint);
        var researchTimeMultiplier = 1d
            + ((reliabilityPoints + productionPoints) * ResearchBalance.Type.ResearchTimeIncreasePerPoint)
            - (fastDevelopmentPoints * ResearchBalance.Type.FastDevelopmentTimeReductionPerPoint);
        researchTimeMultiplier = System.Math.Max(0.1d, researchTimeMultiplier);

        return new TypeResearchModifierEffects(
            reliabilityDelta,
            productionCostMultiplier,
            productionManDaysMultiplier,
            researchCostMultiplier,
            researchTimeMultiplier);
    }
}
