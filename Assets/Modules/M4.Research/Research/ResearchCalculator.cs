using System;
using AircraftModel = WrightTycoon.Core.Aircraft.Aircraft;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Engines;
using WrightTycoon.Core.Math;

namespace WrightTycoon.Core.Research;

public sealed class ResearchCalculator
{
    public ResearchQuote CalculateEngineQuote(Engine engine)
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

        return new ResearchQuote(cost, months, complexity);
    }

    public ResearchQuote CalculateFamilyQuote(AircraftModel aircraft)
    {
        if (aircraft is null)
        {
            throw new ArgumentNullException(nameof(aircraft));
        }

        var cost = RoundCurrency(aircraft.Performance.TotalProductionCost * ResearchBalance.Family.CostMultiplier);
        return new ResearchQuote(cost, ResearchBalance.Family.TimeMonths, ResearchBalance.Family.ComplexityReference);
    }

    public ResearchQuote CalculateTypeQuote(AircraftModel aircraft)
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

        var multiplier = Lerp(ResearchBalance.Type.CostMultiplierMin, ResearchBalance.Type.CostMultiplierMax, complexity);
        var months = RoundToInt(Lerp(ResearchBalance.Type.TimeMonthsMin, ResearchBalance.Type.TimeMonthsMax, complexity));
        var cost = RoundCurrency(aircraft.Performance.TotalProductionCost * multiplier);

        return new ResearchQuote(cost, months, complexity);
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
