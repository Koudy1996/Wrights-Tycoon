using System;
using WrightTycoon.Core.Airframes;
using WrightTycoon.Core.Engines;
using WrightTycoon.Core.Math;

namespace WrightTycoon.Core.Aircraft;

public sealed class DevelopmentCostCalculator
{
    public DevelopmentCostReport Calculate(
        AircraftFamilyDefinition familyDefinition,
        EngineParameters baseEngine,
        EngineParameters variantEngine,
        AirframeParameters baseAirframe,
        AirframeParameters variantAirframe)
    {
        if (familyDefinition is null)
        {
            throw new ArgumentNullException(nameof(familyDefinition));
        }

        if (baseEngine is null)
        {
            throw new ArgumentNullException(nameof(baseEngine));
        }

        if (variantEngine is null)
        {
            throw new ArgumentNullException(nameof(variantEngine));
        }

        if (baseAirframe is null)
        {
            throw new ArgumentNullException(nameof(baseAirframe));
        }

        if (variantAirframe is null)
        {
            throw new ArgumentNullException(nameof(variantAirframe));
        }

        var changedCount = 0;
        var magnitude = 0d;

        AddNumericChange(baseEngine.DisplacementLiters, variantEngine.DisplacementLiters, 20d, ref changedCount, ref magnitude);
        AddNumericChange(baseEngine.CompressionRatio, variantEngine.CompressionRatio, 8d, ref changedCount, ref magnitude);
        AddNumericChange(baseEngine.OptimizationAltitudeMeters, variantEngine.OptimizationAltitudeMeters, 10000d, ref changedCount, ref magnitude);
        AddNumericChange(baseEngine.TurbineDiameterMeters, variantEngine.TurbineDiameterMeters, 2.5d, ref changedCount, ref magnitude);
        AddNumericChange(baseEngine.BypassRatio, variantEngine.BypassRatio, 12d, ref changedCount, ref magnitude);
        AddNumericChange(baseEngine.CoolingEfficiency, variantEngine.CoolingEfficiency, 1d, ref changedCount, ref magnitude);
        AddNumericChange(baseEngine.PrecisionEngineeringLevel, variantEngine.PrecisionEngineeringLevel, 10d, ref changedCount, ref magnitude);
        AddEnumChange(baseEngine.BoostType, variantEngine.BoostType, 1.1d, ref changedCount, ref magnitude);

        AddNumericChange(baseAirframe.WingAreaSquareMeters, variantAirframe.WingAreaSquareMeters, 70d, ref changedCount, ref magnitude);
        AddNumericChange(baseAirframe.FuelAmountLiters, variantAirframe.FuelAmountLiters, 2500d, ref changedCount, ref magnitude);
        AddNumericChange(baseAirframe.PrecisionEngineeringLevel, variantAirframe.PrecisionEngineeringLevel, 10d, ref changedCount, ref magnitude);
        AddNumericChange(
            baseAirframe.PayloadLimitKilograms ?? 0d,
            variantAirframe.PayloadLimitKilograms ?? 0d,
            10000d,
            ref changedCount,
            ref magnitude);
        AddNumericChange(baseAirframe.CrewCount, variantAirframe.CrewCount, 6d, ref changedCount, ref magnitude);
        AddNumericChange(baseAirframe.EngineCowling, variantAirframe.EngineCowling, 1d, ref changedCount, ref magnitude);
        AddNumericChange(baseAirframe.EngineCount, variantAirframe.EngineCount, 6d, ref changedCount, ref magnitude);
        AddEnumChange(baseAirframe.WingPosition, variantAirframe.WingPosition, 0.7d, ref changedCount, ref magnitude);
        AddEnumChange(baseAirframe.WingShape, variantAirframe.WingShape, 0.9d, ref changedCount, ref magnitude);
        AddEnumChange(baseAirframe.WingProfile, variantAirframe.WingProfile, 0.8d, ref changedCount, ref magnitude);
        AddEnumChange(baseAirframe.TailType, variantAirframe.TailType, 0.75d, ref changedCount, ref magnitude);
        AddEnumChange(baseAirframe.FlapType, variantAirframe.FlapType, 0.65d, ref changedCount, ref magnitude);
        AddBooleanChange(baseAirframe.HasLeadingEdgeFlaps, variantAirframe.HasLeadingEdgeFlaps, 0.45d, ref changedCount, ref magnitude);
        AddBooleanChange(baseAirframe.HasBackupSystems, variantAirframe.HasBackupSystems, 0.8d, ref changedCount, ref magnitude);
        AddEnumChange(baseAirframe.LandingGearType, variantAirframe.LandingGearType, 0.95d, ref changedCount, ref magnitude);

        // v0.2: aircraft type development cost/MD is intentionally ignored.
        var developmentCost = 0d;
        var developmentManDays = 0d;

        return new DevelopmentCostReport(changedCount, magnitude, developmentCost, developmentManDays);
    }

    private static void AddNumericChange(
        double baseline,
        double variant,
        double normalizationRange,
        ref int changedCount,
        ref double magnitudeScore)
    {
        if (System.Math.Abs(variant - baseline) <= Numeric.Epsilon)
        {
            return;
        }

        changedCount++;
        var normalizedDelta = System.Math.Abs(variant - baseline) / System.Math.Max(normalizationRange, Numeric.Epsilon);
        magnitudeScore += Numeric.Clamp(normalizedDelta, 0.05d, 2.4d);
    }

    private static void AddEnumChange<T>(
        T baseline,
        T variant,
        double magnitudeContribution,
        ref int changedCount,
        ref double magnitudeScore)
        where T : struct, Enum
    {
        if (baseline.Equals(variant))
        {
            return;
        }

        changedCount++;
        magnitudeScore += magnitudeContribution;
    }

    private static void AddBooleanChange(
        bool baseline,
        bool variant,
        double magnitudeContribution,
        ref int changedCount,
        ref double magnitudeScore)
    {
        if (baseline == variant)
        {
            return;
        }

        changedCount++;
        magnitudeScore += magnitudeContribution;
    }
}
