using System;
using System.Collections.Generic;
using WrightTycoon.Core.Airframes;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Categories;
using WrightTycoon.Core.Engines;
using WrightTycoon.Core.Math;
using WrightTycoon.Core.Physics;
using WrightTycoon.Core.Units;

namespace WrightTycoon.Core.Aircraft;

public sealed class AircraftPerformanceCalculator
{
    private readonly IAtmosphereModel _atmosphereModel;
    private readonly IAerodynamicsCalculator _aerodynamicsCalculator;
    private static readonly double[] EvaluationAltitudes = CoreBalance.Atmosphere.EvaluationAltitudesMeters;

    public AircraftPerformanceCalculator(IAtmosphereModel atmosphereModel, IAerodynamicsCalculator aerodynamicsCalculator)
    {
        _atmosphereModel = atmosphereModel ?? throw new ArgumentNullException(nameof(atmosphereModel));
        _aerodynamicsCalculator = aerodynamicsCalculator ?? throw new ArgumentNullException(nameof(aerodynamicsCalculator));
    }

    public AircraftPerformanceReport Calculate(Engine engine, Airframe airframe)
    {
        return Calculate(engine, airframe, AircraftCategory.LightSportAndRecreational);
    }

    public AircraftPerformanceReport Calculate(Engine engine, Airframe airframe, AircraftCategory category)
    {
        if (engine is null)
        {
            throw new ArgumentNullException(nameof(engine));
        }

        if (airframe is null)
        {
            throw new ArgumentNullException(nameof(airframe));
        }

        var categoryProfile = AircraftCategoryCatalog.GetProfile(category);
        var engineCount = airframe.Parameters.EngineCount;
        var wingAreaSquareMeters = System.Math.Max(airframe.Parameters.WingAreaSquareMeters, Numeric.Epsilon);
        var baseDragCoefficient = System.Math.Max(
            airframe.Performance.DragCoefficient,
            PlaneDesignerBalance.Performance.MinimumDragCoefficient);
        var wingPositionDragFactor = GetWingPositionSpeedDragFactor(airframe.Parameters.WingPosition);
        var dragCoefficient = Numeric.Clamp(
            baseDragCoefficient * wingPositionDragFactor,
            PlaneDesignerBalance.Performance.MinimumDragCoefficient,
            PlaneDesignerBalance.Performance.SpeedCalculationMaximumDragCoefficient);
        var hasDeltaWingDragModifier = airframe.Parameters.WingShape == WingShape.Delta;
        var wingProfile = airframe.Parameters.WingProfile;
        var liftCoefficient = Numeric.Clamp(
            airframe.Performance.LiftCoefficient,
            PlaneDesignerBalance.Performance.MinimumLiftCoefficient,
            PlaneDesignerBalance.Performance.MaximumLiftCoefficient);

        var fuelMassKilograms = airframe.Parameters.FuelAmountLiters * PlaneDesignerBalance.Performance.FuelDensityKilogramsPerLiter;
        var baseStructuralWithoutFuelKilograms = System.Math.Max(
            PlaneDesignerBalance.Performance.MinimumEmptyWeightKilograms,
            airframe.Performance.TotalWeight.Kilograms - fuelMassKilograms);
        var engineMassKilograms = engine.Performance.Weight.Kilograms * engineCount;
        var systemsMassKilograms = PlaneDesignerBalance.Performance.EmptyWeightBaseSystemsKilograms
            + (engineCount * PlaneDesignerBalance.Performance.EmptyWeightPerEngineSystemsKilograms)
            + (airframe.Parameters.CrewCount * PlaneDesignerBalance.Performance.EmptyWeightPerCrewSystemsKilograms);
        systemsMassKilograms *= CalculateUltralightSystemsMassFactor(engine, airframe);

        // Category weight modifiers can be negative (for very light categories), so do not clamp.
        var categoryStructuralMassKilograms = categoryProfile.WeightModifierKilograms;
        var emptyWeightKilograms = baseStructuralWithoutFuelKilograms + engineMassKilograms + systemsMassKilograms + categoryStructuralMassKilograms;

        var speedPoints = new AircraftPerformancePoint[EvaluationAltitudes.Length];
        for (var index = 0; index < EvaluationAltitudes.Length; index++)
        {
            var altitude = EvaluationAltitudes[index];
            var density = _atmosphereModel.GetDensityAtAltitude(altitude).KilogramsPerCubicMeter;
            var thrust = GetThrustAtAltitude(engine, altitude) * engineCount;
            var maxSpeed = CalculateMaximumSpeedMetersPerSecond(
                thrust,
                density,
                wingAreaSquareMeters,
                dragCoefficient,
                hasDeltaWingDragModifier,
                wingProfile);
            maxSpeed += categoryProfile.SpeedModifierMetersPerSecond;
            maxSpeed = ApplyJetSpeedEnvelope(maxSpeed, engine, category);
            speedPoints[index] = new AircraftPerformancePoint(altitude, System.Math.Max(0d, maxSpeed));
        }

        var maximumSpeedKilometersPerHour = GetMaximumSpeedKilometersPerHour(speedPoints);
        var speedWeightFactor = CalculateSpeedWeightFactor(maximumSpeedKilometersPerHour);
        emptyWeightKilograms *= speedWeightFactor;

        var wetWeightKilograms = (emptyWeightKilograms * (1d + PlaneDesignerBalance.Performance.WetWeightMarginRatio))
            + fuelMassKilograms;

        var seaLevelDensity = _atmosphereModel.GetDensityAtAltitude(0d).KilogramsPerCubicMeter;
        var seaLevelThrustPerEngine = GetThrustAtAltitude(engine, 0d);
        var totalSeaLevelThrust = seaLevelThrustPerEngine * engineCount;
        var runwayLengthMeters = GetRunwayLengthMeters(airframe.Parameters.RunwayType);
        var takeoffLiftCoefficient = CalculateTakeoffLiftCoefficient(liftCoefficient);
        var maximumWeightKilograms = CalculateMaximumWeightKilograms(
            runwayLengthMeters,
            seaLevelDensity,
            wingAreaSquareMeters,
            totalSeaLevelThrust,
            takeoffLiftCoefficient);
        maximumWeightKilograms = System.Math.Max(maximumWeightKilograms, wetWeightKilograms);
        var usefulWeightKilograms = System.Math.Max(0d, maximumWeightKilograms - wetWeightKilograms);

        var cruiseAltitudeMeters = engine.Definition.EngineType is EngineType.Jet or EngineType.Turbojet or EngineType.Turbofan
            ? PlaneDesignerBalance.Performance.JetCruiseAltitudeMeters
            : PlaneDesignerBalance.Performance.PropCruiseAltitudeMeters;
        var cruiseDensity = _atmosphereModel.GetDensityAtAltitude(cruiseAltitudeMeters).KilogramsPerCubicMeter;
        var maximumWeightNewtons = maximumWeightKilograms * PlaneDesignerBalance.Performance.GravityMetersPerSecondSquared;
        var optimalCruiseSpeedMetersPerSecond = CalculateCruiseSpeedMetersPerSecond(
            maximumWeightNewtons,
            cruiseDensity,
            wingAreaSquareMeters,
            liftCoefficient);
        var maxSpeedAtCruise = System.Math.Max(0d, GetSpeedAtAltitude(speedPoints, cruiseAltitudeMeters));
        var minimumCruiseSpeedAtAltitude = System.Math.Min(
            PlaneDesignerBalance.Performance.MinimumCruiseSpeedMetersPerSecond,
            maxSpeedAtCruise);
        optimalCruiseSpeedMetersPerSecond = Numeric.Clamp(
            optimalCruiseSpeedMetersPerSecond,
            minimumCruiseSpeedAtAltitude,
            maxSpeedAtCruise);

        var cruiseDragCoefficient = ApplySpeedDependentWingDragModifiers(
            dragCoefficient,
            hasDeltaWingDragModifier,
            wingProfile,
            optimalCruiseSpeedMetersPerSecond);
        var requiredCruiseThrust = 0.5d
            * cruiseDensity
            * optimalCruiseSpeedMetersPerSecond
            * optimalCruiseSpeedMetersPerSecond
            * wingAreaSquareMeters
            * cruiseDragCoefficient;
        var availableCruiseThrust = System.Math.Max(GetThrustAtAltitude(engine, cruiseAltitudeMeters) * engineCount, Numeric.Epsilon);
        var thrustRatio = Numeric.Clamp(requiredCruiseThrust / availableCruiseThrust, 0d, 1d);
        var fullThrottleCruiseFuelBurn = engine.Performance.GetPerformanceAtAltitude(cruiseAltitudeMeters).FuelConsumptionKilogramsPerHour
            * engineCount
            * categoryProfile.FuelConsumptionMultiplier;
        var cruiseFuelBurnKilogramsPerHour = System.Math.Max(
            PlaneDesignerBalance.Performance.CruiseMinimumFuelBurnKilogramsPerHour,
            fullThrottleCruiseFuelBurn * thrustRatio);

        var rangeKilometers = (fuelMassKilograms / cruiseFuelBurnKilogramsPerHour)
            * (optimalCruiseSpeedMetersPerSecond * 3.6d);

        var aircraftLengthMeters = CalculateAircraftLengthMeters(maximumWeightKilograms, wingAreaSquareMeters);
        var bodyFrontalAreaSquareMeters = CalculateBodyFrontalAreaSquareMeters(maximumWeightKilograms, wingAreaSquareMeters);
        var totalFrontalAreaSquareMeters = (engine.Performance.EngineFrontalAreaSquareMeters * engineCount)
            + bodyFrontalAreaSquareMeters;
        var totalDragAreaSquareMeters = cruiseDragCoefficient * totalFrontalAreaSquareMeters;

        var maneuverability = Numeric.Clamp(
            airframe.Performance.Maneuverability - (maximumWeightKilograms * PlaneDesignerBalance.Performance.ManeuverabilityWeightPenaltyPerKilogram),
            1d,
            100d);

        var overallReliability = CalculateOverallReliability(
            engine.Performance.Reliability,
            airframe.Performance.ReliabilityModifier,
            engineCount,
            categoryProfile.ReliabilityModifierDelta);

        var totalProductionCost = airframe.Performance.ProductionCost + (engine.Performance.Cost * engineCount);
        var totalProductionManDays = airframe.Performance.ProductionManDays + (engine.Performance.ProductionManDays * engineCount);

        return new AircraftPerformanceReport(
            Array.AsReadOnly(speedPoints),
            rangeKilometers,
            Mass.FromKilograms(usefulWeightKilograms),
            totalProductionCost,
            totalProductionManDays,
            overallReliability,
            Mass.FromKilograms(wetWeightKilograms),
            maximumWeightKilograms,
            wetWeightKilograms,
            emptyWeightKilograms,
            aircraftLengthMeters,
            totalFrontalAreaSquareMeters,
            totalDragAreaSquareMeters,
            optimalCruiseSpeedMetersPerSecond,
            cruiseAltitudeMeters,
            cruiseFuelBurnKilogramsPerHour,
            cruiseFuelBurnKilogramsPerHour,
            cruiseFuelBurnKilogramsPerHour);
    }

    private static double GetThrustAtAltitude(Engine engine, double altitudeMeters)
    {
        var thrust = engine.Performance.GetThrustNewtonsAtAltitude(altitudeMeters);
        return System.Math.Max(0d, thrust);
    }

    private static double CalculateMaximumSpeedMetersPerSecond(
        double totalThrustNewtons,
        double densityKilogramsPerCubicMeter,
        double wingAreaSquareMeters,
        double dragCoefficient,
        bool hasDeltaWingDragModifier,
        WingProfileType wingProfile)
    {
        var hasSpeedDependentDragModifier = hasDeltaWingDragModifier || wingProfile == WingProfileType.Naca4DigitLight;
        if (!hasSpeedDependentDragModifier)
        {
            return CalculateSpeedFromDragCoefficient(totalThrustNewtons, densityKilogramsPerCubicMeter, wingAreaSquareMeters, dragCoefficient);
        }

        // Fixed-point iteration resolves speed-dependent drag modifiers (delta and wing profile).
        var effectiveDragCoefficient = dragCoefficient;
        var maximumSpeedMetersPerSecond = 0d;
        for (var iteration = 0; iteration < 4; iteration++)
        {
            maximumSpeedMetersPerSecond = CalculateSpeedFromDragCoefficient(
                totalThrustNewtons,
                densityKilogramsPerCubicMeter,
                wingAreaSquareMeters,
                effectiveDragCoefficient);
            effectiveDragCoefficient = ApplySpeedDependentWingDragModifiers(
                dragCoefficient,
                hasDeltaWingDragModifier,
                wingProfile,
                maximumSpeedMetersPerSecond);
        }

        return maximumSpeedMetersPerSecond;
    }

    private static double CalculateSpeedFromDragCoefficient(
        double totalThrustNewtons,
        double densityKilogramsPerCubicMeter,
        double wingAreaSquareMeters,
        double dragCoefficient)
    {
        var denominator = System.Math.Max(
            densityKilogramsPerCubicMeter * wingAreaSquareMeters * dragCoefficient,
            Numeric.Epsilon);
        var speedSquared = (2d * totalThrustNewtons) / denominator;
        return System.Math.Sqrt(System.Math.Max(0d, speedSquared));
    }

    private static double ApplyDeltaWingHighSpeedDragModifier(
        double baseDragCoefficient,
        bool hasDeltaWingDragModifier,
        double speedMetersPerSecond)
    {
        if (!hasDeltaWingDragModifier)
        {
            return baseDragCoefficient;
        }

        var speedKilometersPerHour = speedMetersPerSecond * 3.6d;
        var reductionRatio = CalculateDeltaWingHighSpeedDragReductionRatio(speedKilometersPerHour);
        return Numeric.Clamp(
            baseDragCoefficient * (1d - reductionRatio),
            PlaneDesignerBalance.Performance.MinimumDragCoefficient,
            PlaneDesignerBalance.Performance.SpeedCalculationMaximumDragCoefficient);
    }

    private static double ApplySpeedDependentWingDragModifiers(
        double baseDragCoefficient,
        bool hasDeltaWingDragModifier,
        WingProfileType wingProfile,
        double speedMetersPerSecond)
    {
        var dragCoefficient = ApplyDeltaWingHighSpeedDragModifier(
            baseDragCoefficient,
            hasDeltaWingDragModifier,
            speedMetersPerSecond);

        if (wingProfile != WingProfileType.Naca4DigitLight)
        {
            return dragCoefficient;
        }

        var speedKilometersPerHour = speedMetersPerSecond * 3.6d;
        var increaseRatio = CalculateNaca4DigitLightDragIncreaseRatio(speedKilometersPerHour);
        return Numeric.Clamp(
            dragCoefficient * (1d + increaseRatio),
            PlaneDesignerBalance.Performance.MinimumDragCoefficient,
            PlaneDesignerBalance.Performance.SpeedCalculationMaximumDragCoefficient);
    }

    private static double CalculateDeltaWingHighSpeedDragReductionRatio(double speedKilometersPerHour)
    {
        var start = PlaneDesignerBalance.Performance.DeltaWingHighSpeedDragReductionStartKilometersPerHour;
        var end = PlaneDesignerBalance.Performance.DeltaWingHighSpeedDragReductionEndKilometersPerHour;
        if (speedKilometersPerHour <= start)
        {
            return 0d;
        }

        var range = System.Math.Max(end - start, Numeric.Epsilon);
        var blend = Numeric.Clamp((speedKilometersPerHour - start) / range, 0d, 1d);
        return PlaneDesignerBalance.Performance.DeltaWingHighSpeedDragReductionMaxRatio * blend;
    }

    private static double CalculateNaca4DigitLightDragIncreaseRatio(double speedKilometersPerHour)
    {
        var start = PlaneDesignerBalance.Performance.Naca4DigitLightProfileDragIncreaseStartKilometersPerHour;
        var end = PlaneDesignerBalance.Performance.Naca4DigitLightProfileDragIncreaseEndKilometersPerHour;
        if (speedKilometersPerHour <= start)
        {
            return 0d;
        }

        var range = System.Math.Max(end - start, Numeric.Epsilon);
        var blend = Numeric.Clamp((speedKilometersPerHour - start) / range, 0d, 1d);
        return PlaneDesignerBalance.Performance.Naca4DigitLightProfileDragIncreaseMaxRatio * blend;
    }

    private static double CalculateMaximumWeightKilograms(
        double runwayLengthMeters,
        double seaLevelDensity,
        double wingAreaSquareMeters,
        double totalSeaLevelThrustNewtons,
        double takeoffLiftCoefficient)
    {
        var numerator = runwayLengthMeters
            * seaLevelDensity
            * wingAreaSquareMeters
            * takeoffLiftCoefficient
            * totalSeaLevelThrustNewtons;
        var denominator = PlaneDesignerBalance.Performance.GravityMetersPerSecondSquared;
        if (numerator <= 0d || denominator <= 0d)
        {
            return PlaneDesignerBalance.Performance.MinimumEmptyWeightKilograms;
        }

        return System.Math.Sqrt(numerator / denominator);
    }

    private static double CalculateTakeoffLiftCoefficient(double liftCoefficient)
    {
        var referenceLiftCoefficient = System.Math.Max(
            PlaneDesignerBalance.Performance.TakeoffLiftReferenceCoefficient,
            Numeric.Epsilon);
        var liftScaling = Numeric.Clamp(
            liftCoefficient / referenceLiftCoefficient,
            PlaneDesignerBalance.Performance.TakeoffLiftScalingMin,
            PlaneDesignerBalance.Performance.TakeoffLiftScalingMax);
        return PlaneDesignerBalance.Performance.MaximumLiftCoefficientTakeoff * liftScaling;
    }

    private static double CalculateCruiseSpeedMetersPerSecond(
        double weightNewtons,
        double densityKilogramsPerCubicMeter,
        double wingAreaSquareMeters,
        double liftCoefficient)
    {
        var denominator = System.Math.Max(
            densityKilogramsPerCubicMeter * wingAreaSquareMeters * liftCoefficient,
            Numeric.Epsilon);
        var speedSquared = (2d * weightNewtons) / denominator;
        return System.Math.Sqrt(System.Math.Max(0d, speedSquared));
    }

    private static double CalculateAircraftLengthMeters(double maximumWeightKilograms, double wingAreaSquareMeters)
    {
        var lengthMeters = PlaneDesignerBalance.Airframe.AircraftLengthBaseMeters
            + (System.Math.Pow(System.Math.Max(wingAreaSquareMeters, 1d), PlaneDesignerBalance.Airframe.AircraftLengthWingAreaExponent)
               * PlaneDesignerBalance.Airframe.AircraftLengthWingAreaScale)
            + (System.Math.Pow(
                   System.Math.Max(maximumWeightKilograms / PlaneDesignerBalance.Airframe.AircraftLengthWeightStretchReferenceKilograms, 0.05d),
                   PlaneDesignerBalance.Airframe.AircraftLengthWeightStretchExponent)
               * PlaneDesignerBalance.Airframe.AircraftLengthWeightStretchScale
               * PlaneDesignerBalance.Airframe.AircraftLengthWeightStretchReferenceKilograms / 1000d);

        return Numeric.Clamp(
            lengthMeters,
            PlaneDesignerBalance.Airframe.AircraftLengthMinMeters,
            PlaneDesignerBalance.Airframe.AircraftLengthMaxMeters);
    }

    private static double CalculateBodyFrontalAreaSquareMeters(double maximumWeightKilograms, double wingAreaSquareMeters)
    {
        var area = PlaneDesignerBalance.Performance.BodyFrontalAreaBaseSquareMeters
            + (System.Math.Pow(System.Math.Max(maximumWeightKilograms / 1000d, 0.05d), PlaneDesignerBalance.Performance.BodyFrontalAreaWeightExponent)
               * PlaneDesignerBalance.Performance.BodyFrontalAreaWeightScale)
            + (System.Math.Pow(System.Math.Max(wingAreaSquareMeters, 1d), PlaneDesignerBalance.Performance.BodyFrontalAreaWingExponent)
               * PlaneDesignerBalance.Performance.BodyFrontalAreaWingScale);
        return Numeric.Clamp(
            area,
            PlaneDesignerBalance.Performance.BodyFrontalAreaMinSquareMeters,
            PlaneDesignerBalance.Performance.BodyFrontalAreaMaxSquareMeters);
    }

    private static double CalculateOverallReliability(
        double engineReliability,
        double airframeReliabilityModifier,
        int engineCount,
        double categoryReliabilityModifierDelta)
    {
        var multiEnginePenalty = 1d - ((1d - engineReliability) * engineCount * PlaneDesignerBalance.Performance.MultiEngineReliabilityPenaltyFactor);
        var overall = (multiEnginePenalty * airframeReliabilityModifier) + categoryReliabilityModifierDelta;
        return Numeric.Clamp(overall, 0.2d, 0.995d);
    }

    private static double CalculateUltralightSystemsMassFactor(Engine engine, Airframe airframe)
    {
        if (engine.Performance.OutputKind != EngineOutputKind.PowerKilowatts)
        {
            return 1d;
        }

        if (airframe.Parameters.EngineCount != 1
            || airframe.Parameters.MaterialType != MaterialType.WoodFabric
            || airframe.Parameters.StructureType != StructureType.Truss)
        {
            return 1d;
        }

        var seaLevelPowerKilowatts = engine.Performance.GetPerformanceAtAltitude(0d).Output;
        if (seaLevelPowerKilowatts > PlaneDesignerBalance.Performance.UltralightSystemsMassMaxPowerKilowatts)
        {
            return 1d;
        }

        if (airframe.Parameters.WingAreaSquareMeters > PlaneDesignerBalance.Performance.UltralightSystemsMassMaxWingAreaSquareMeters)
        {
            return 1d;
        }

        if (airframe.Parameters.CrewCount > PlaneDesignerBalance.Performance.UltralightSystemsMassMaxCrew)
        {
            return 1d;
        }

        return Numeric.Clamp(PlaneDesignerBalance.Performance.UltralightSystemsMassFactorAt50Kilowatts, 0.05d, 1d);
    }

    private static double GetMaximumSpeedKilometersPerHour(IReadOnlyList<AircraftPerformancePoint> speedPoints)
    {
        var maximumSpeedMetersPerSecond = 0d;
        for (var index = 0; index < speedPoints.Count; index++)
        {
            maximumSpeedMetersPerSecond = System.Math.Max(maximumSpeedMetersPerSecond, speedPoints[index].MaxSpeedMetersPerSecond);
        }

        return maximumSpeedMetersPerSecond * 3.6d;
    }

    private static double CalculateSpeedWeightFactor(double maximumSpeedKilometersPerHour)
    {
        var curve = PlaneDesignerBalance.Performance.SpeedWeightFactorCurve;
        if (curve.Count == 0)
        {
            return 1d;
        }

        if (maximumSpeedKilometersPerHour <= curve[0].SpeedKilometersPerHour)
        {
            return ClampSpeedWeightFactor(curve[0].WeightFactor);
        }

        for (var index = 1; index < curve.Count; index++)
        {
            var previous = curve[index - 1];
            var current = curve[index];
            if (maximumSpeedKilometersPerHour > current.SpeedKilometersPerHour)
            {
                continue;
            }

            var segmentRange = System.Math.Max(
                current.SpeedKilometersPerHour - previous.SpeedKilometersPerHour,
                Numeric.Epsilon);
            var blend = Numeric.Clamp(
                (maximumSpeedKilometersPerHour - previous.SpeedKilometersPerHour) / segmentRange,
                0d,
                1d);
            var interpolated = Lerp(previous.WeightFactor, current.WeightFactor, blend);
            return ClampSpeedWeightFactor(interpolated);
        }

        return ClampSpeedWeightFactor(curve[curve.Count - 1].WeightFactor);
    }

    private static double Lerp(double fromValue, double toValue, double blend) =>
        fromValue + ((toValue - fromValue) * Numeric.Clamp(blend, 0d, 1d));

    private static double ClampSpeedWeightFactor(double factor) =>
        Numeric.Clamp(factor, 0.05d, 5d);

    private static double ApplyJetSpeedEnvelope(double speedMetersPerSecond, Engine engine, AircraftCategory category)
    {
        if (engine.Performance.OutputKind != EngineOutputKind.ThrustNewtons)
        {
            return speedMetersPerSecond;
        }

        var envelopeLimit = GetJetSpeedEnvelopeLimitMetersPerSecond(engine.Parameters.BypassRatio);
        var categoryFactor = category switch
        {
            AircraftCategory.RegionalJet => PlaneDesignerBalance.Performance.JetSpeedEnvelopeCategoryRegionalJetFactor,
            AircraftCategory.BusinessVipJet => PlaneDesignerBalance.Performance.JetSpeedEnvelopeCategoryBusinessJetFactor,
            AircraftCategory.MediumTransportNarrowBody => PlaneDesignerBalance.Performance.JetSpeedEnvelopeCategoryMediumTransportFactor,
            AircraftCategory.HeavyTransportWideBody => PlaneDesignerBalance.Performance.JetSpeedEnvelopeCategoryHeavyTransportFactor,
            AircraftCategory.SpecialCivil => PlaneDesignerBalance.Performance.JetSpeedEnvelopeCategorySpecialCivilFactor,
            AircraftCategory.MilitaryStrategicSupport => PlaneDesignerBalance.Performance.JetSpeedEnvelopeCategoryMilitaryStrategicFactor,
            AircraftCategory.MilitaryTactical => PlaneDesignerBalance.Performance.JetSpeedEnvelopeCategoryMilitaryTacticalFactor,
            _ => PlaneDesignerBalance.Performance.JetSpeedEnvelopeCategoryDefaultFactor,
        };

        return System.Math.Min(speedMetersPerSecond, envelopeLimit * categoryFactor);
    }

    private static double GetJetSpeedEnvelopeLimitMetersPerSecond(double bypassRatio)
    {
        if (bypassRatio >= PlaneDesignerBalance.Performance.JetSpeedEnvelopeBypassHighThreshold)
        {
            return PlaneDesignerBalance.Performance.JetSpeedEnvelopeHighBypassLimitMetersPerSecond;
        }

        if (bypassRatio >= PlaneDesignerBalance.Performance.JetSpeedEnvelopeBypassMediumThreshold)
        {
            return PlaneDesignerBalance.Performance.JetSpeedEnvelopeMediumBypassLimitMetersPerSecond;
        }

        if (bypassRatio >= PlaneDesignerBalance.Performance.JetSpeedEnvelopeBypassLowThreshold)
        {
            return PlaneDesignerBalance.Performance.JetSpeedEnvelopeLowBypassLimitMetersPerSecond;
        }

        if (bypassRatio >= PlaneDesignerBalance.Performance.JetSpeedEnvelopeBypassVeryLowThreshold)
        {
            return PlaneDesignerBalance.Performance.JetSpeedEnvelopeVeryLowBypassLimitMetersPerSecond;
        }

        return PlaneDesignerBalance.Performance.JetSpeedEnvelopeTurbojetLikeLimitMetersPerSecond;
    }


    private static double GetWingPositionSpeedDragFactor(WingPosition wingPosition) =>
        wingPosition switch
        {
            WingPosition.Low => PlaneDesignerBalance.Performance.WingPositionSpeedDragFactorLow,
            WingPosition.High => PlaneDesignerBalance.Performance.WingPositionSpeedDragFactorHigh,
            WingPosition.Biplane => PlaneDesignerBalance.Performance.WingPositionSpeedDragFactorBiplane,
            _ => PlaneDesignerBalance.Performance.WingPositionSpeedDragFactorLow,
        };
    private static double GetRunwayLengthMeters(RunwayType runwayType) =>
        runwayType switch
        {
            RunwayType.Short => PlaneDesignerBalance.Airframe.RunwayShortLengthMeters,
            RunwayType.Medium => PlaneDesignerBalance.Airframe.RunwayMediumLengthMeters,
            RunwayType.Long => PlaneDesignerBalance.Airframe.RunwayLongLengthMeters,
            _ => PlaneDesignerBalance.Airframe.RunwayMediumLengthMeters,
        };

    private static double GetSpeedAtAltitude(IReadOnlyList<AircraftPerformancePoint> points, double altitudeMeters)
    {
        for (var index = 0; index < points.Count; index++)
        {
            if (System.Math.Abs(points[index].AltitudeMeters - altitudeMeters) < 1e-6)
            {
                return points[index].MaxSpeedMetersPerSecond;
            }
        }

        return points.Count == 0 ? 0d : points[0].MaxSpeedMetersPerSecond;
    }
}

