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

        var outputValue = ResolveSeaLevelOutputForComplexity(engine);
        var outputNormalized = engine.Performance.OutputKind == EngineOutputKind.PowerKilowatts
            ? Normalize(outputValue, ResearchBalance.Engine.OutputPowerMinKilowatts, ResearchBalance.Engine.OutputPowerMaxKilowatts)
            : Normalize(outputValue, ResearchBalance.Engine.OutputThrustMinKiloNewtons, ResearchBalance.Engine.OutputThrustMaxKiloNewtons);
        var weightNormalized = Normalize(engine.Performance.Weight.Kilograms, ResearchBalance.Engine.WeightMinKilograms, ResearchBalance.Engine.WeightMaxKilograms);
        var costNormalized = Normalize(engine.Performance.Cost, ResearchBalance.Engine.CostMin, ResearchBalance.Engine.CostMax);
        var manDaysNormalized = Normalize(engine.Performance.ProductionManDays, ResearchBalance.Engine.ManDaysMin, ResearchBalance.Engine.ManDaysMax);

        var complexity = GetEngineTypeComplexityBase(engine.Definition.EngineType)
            + (outputNormalized * ResearchBalance.Engine.ComplexityWeightOutput)
            + (weightNormalized * ResearchBalance.Engine.ComplexityWeightWeight)
            + (costNormalized * ResearchBalance.Engine.ComplexityWeightCost)
            + (manDaysNormalized * ResearchBalance.Engine.ComplexityWeightManDays);
        complexity = Numeric.Clamp(complexity, 0d, 1d);

        var multiplier = Lerp(ResearchBalance.Engine.CostMultiplierMin, ResearchBalance.Engine.CostMultiplierMax, complexity);
        var months = RoundToInt(Lerp(ResearchBalance.Engine.TimeMonthsMin, ResearchBalance.Engine.TimeMonthsMax, complexity));
        var cost = RoundCurrency(engine.Performance.Cost * multiplier);
        var baseQuote = new ResearchQuote(cost, months, complexity);
        return ApplyResearchModifierEffects(baseQuote, modifierPoints);
    }

    public ResearchQuote CalculateFamilyQuote(AircraftModel aircraft, TypeResearchModifierPoints? modifierPoints = null)
    {
        if (aircraft is null)
        {
            throw new ArgumentNullException(nameof(aircraft));
        }

        var cost = RoundCurrency(aircraft.Performance.TotalProductionCost * ResearchBalance.Family.CostMultiplier);
        var baseQuote = new ResearchQuote(cost, ResearchBalance.Family.TimeMonths, ResearchBalance.Family.ComplexityReference);
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
        var baseCost = RoundCurrency(aircraft.Performance.TotalProductionCost * baseMultiplier);
        var baseQuote = new ResearchQuote(baseCost, baseMonths, complexity);
        var effectiveModifierPoints = modifierPoints ?? aircraft.TypeDefinition.ResearchModifiers;
        return ApplyResearchModifierEffects(baseQuote, effectiveModifierPoints);
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
            EngineType.Jet => ResearchBalance.Engine.ComplexityTypeBaseJet,
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
