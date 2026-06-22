using System;
using System.Collections.Generic;
using WrightTycoon.Core.Categories;

namespace WrightTycoon.Core.Balance;

// Legacy compatibility surface.
// v0.2 active module balance files are in:
// - M1.Core/balance/CoreBalance.cs
// - M2.EngineDesigner/balance/EngineDesignerBalance.cs
// - M3.PlaneDesigner/balance/PlaneDesignerBalance.cs
// - M5.Factory/balance/FactoryBalance.cs
// - M6.Contracts/balance/ContractBalance.cs
public static class WrightTycoonTuning
{
    public static class Atmosphere
    {
        public const double SeaLevelDensityKilogramsPerCubicMeter = 1.225d;
        public const double DensityGradient = 0.0000225577d;
        public const double DensityExponent = 4.256d;
        public static readonly double[] EvaluationAltitudesMeters = { 0d, 5000d, 10000d };
    }

    public static class Engine
    {
        public const double MinimumReliability = 0.3d;
        public const double MaximumReliability = 0.995d;

        public const int PistonCylinderMin = 2;
        public const int PistonCylinderMax = 48;
        public const double PistonCompressionMin = 3d;
        public const double PistonCompressionMax = 16d;
        public const double PistonOptimizationMin = 1000d;
        public const double PistonOptimizationMax = 10000d;
        public const double PistonSpeedMinRpm = 500d;
        public const double PistonSpeedMaxRpm = 7000d;

        public const double JetCompressionMin = 10d;
        public const double JetCompressionMax = 100d;
        public const double JetTurbineDiameterMinMeters = 0.05d;
        public const double JetTurbineDiameterMaxMeters = 4.0d;
        public const double JetBypassRatioMin = 0.1d;
        public const double JetBypassRatioMax = 50d;
        public const double JetSpeedMinRpm = 2000d;
        public const double JetSpeedMaxRpm = 200000d;

        public const double JetAfterburnerEnabledPowerFactor = 1.20d;
        public const double JetAfterburnerDisabledPowerFactor = 0.80d;
        public const double JetAfterburnerWeightMultiplier = 1.35d;
        public const double JetAfterburnerCostMultiplier = 1.55d;
        public const double JetAfterburnerProductionManDaysMultiplier = 1.40d;

        public const double OptimizationNormalizedDeltaDivisor = 10000d;
        public const double OptimizationNormalizedDeltaMax = 1.45d;
        public const double OptimizationProximityBonusScale = 0.12d;
        public const double OptimizationMismatchPenaltyScale = 0.2d;
        public const double OptimizationTurboPenaltyMultiplier = 0.55d;
        public const double OptimizationSuperchargerPenaltyMultiplier = 0.65d;
        public const double OptimizationTurboBonus = 0.015d;
        public const double OptimizationSuperchargerBonus = 0.01d;
        public const double OptimizationFactorMin = 0.82d;
        public const double OptimizationFactorMax = 1.16d;

        public const double JetAltitudeDensityExponentBase = 0.62d;
        public const double JetAltitudeBypassExponentSlope = 0.015d;
        public const double JetAltitudeBypassExponentMaxBypass = 15d;
        public const double JetAltitudeCompressionExponentSlope = 0.0018d;
        public const double JetAltitudeCompressionExponentReference = 20d;
        public const double JetAltitudeCoolingExponentSlope = 0.05d;
        public const double JetAltitudeCoolingExponentReference = 0.7d;
        public const double JetAltitudeTurbojetExponentDelta = 0.05d;
        public const double JetAltitudeTurbofanExponentDelta = -0.02d;
        public const double JetAltitudeExponentMin = 0.32d;
        public const double JetAltitudeExponentMax = 0.66d;
        public const double JetAltitudeDensityRatioMin = 0.05d;

        public const double TurboBoostPowerFactor = 1.28d;
        public const double SuperchargerBoostPowerFactor = 1.2d;
        public const double TurboBoostFuelFactor = 0.98d;
        public const double SuperchargerBoostFuelFactor = 1.08d;
        public const double TurboBoostJetFactor = 1.05d;
        public const double SuperchargerBoostJetFactor = 1.03d;
        public const double TurboBoostJetReliabilityPenalty = 0.03d;
        public const double SuperchargerBoostJetReliabilityPenalty = 0.02d;
        public const double TurboBoostPistonReliabilityPenalty = 0.06d;
        public const double SuperchargerBoostPistonReliabilityPenalty = 0.045d;

        public const double CarburetorPowerFactor = 0.93d;
        public const double CarburetorCostDiscount = 4200d;
        public const double CarburetorProductionManDayDiscount = 6d;

        public const double PistonDerivedDiameterBaseMeters = 0.22d;
        public const double PistonDerivedDiameterPerLiter = 0.042d;
        public const double PistonDerivedDiameterCylinderFactor = 0.004d;
        public const double PistonDerivedDiameterMinMeters = 0.25d;
        public const double PistonDerivedDiameterMaxMeters = 2.3d;

        public const double PistonThrustReferenceSpeedBaseMetersPerSecond = 25d;
        public const double PistonThrustReferenceSpeedPowerExponent = 0.52d;
        public const double PistonThrustReferenceSpeedPowerScale = 0.9d;
        public const double PistonThrustReferenceSpeedRpmScale = 0.45d;
        public const double PistonThrustReferenceSpeedMinMetersPerSecond = 30d;
        public const double PistonThrustReferenceSpeedMaxMetersPerSecond = 130d;

        public const double EngineLengthBaseMeters = 0.42d;
        public const double EngineLengthDiameterScale = 1.25d;
        public const double EngineLengthCompressionScale = 0.028d;
        public const double EngineLengthPistonDisplacementScale = 0.018d;
        public const double EngineLengthMinMeters = 0.4d;
        public const double EngineLengthMaxMeters = 12d;

        public const double EngineFrontalAreaCompressionScale = 0.006d;
        public const double EngineFrontalAreaCoolingScale = 0.2d;
        public const double EngineFrontalAreaMinSquareMeters = 0.04d;
        public const double EngineFrontalAreaMaxSquareMeters = 14d;

        public const double ComplexityPenaltyPiston = 0.02d;
        public const double ComplexityPenaltyInline = 0.03d;
        public const double ComplexityPenaltyV = 0.04d;
        public const double ComplexityPenaltyRadial = 0.05d;
        public const double ComplexityPenaltyJet = 0.08d;
        public const double ComplexityPenaltyTurbojet = 0.1d;
        public const double ComplexityPenaltyTurbofan = 0.12d;
    }

    public static class Airframe
    {
        public const int CrewMin = 1;
        public const int CrewMax = 20;
        public const double FuelMinLiters = 50d;
        public const double FuelMaxLiters = 50000d;
        public const double WoodFabricMaximumTotalWeightKilograms = 15000d;
        public const double SmallWoodTrussWeightReductionThresholdKilograms = 3000d;
        public const double SmallWoodTrussMaxWeightReductionRatio = 0.3d;
        public const double AerodynamicCleanlinessMinPercent = 0d;
        public const double AerodynamicCleanlinessMaxPercent = 20d;
        public const double RunwayShortLengthMeters = 500d;
        public const double RunwayMediumLengthMeters = 1000d;
        public const double RunwayLongLengthMeters = 2000d;

        public const double StructuralAreaScaleMinRatio = 0.2d;
        public const double StructuralAreaScaleExponent = 1.28d;
        public const double StructuralAreaScaleMin = 0.3d;
        public const double StructuralAreaScaleMax = 40d;

        public const double WingMassExponent = 1.44d;
        public const double WingMassCoefficient = 4.9d;
        public const double WingReinforcementMassBaseShare = 0.24d;
        public const double WingReinforcementMassExponent = 1.6d;
        public const double WingSystemsMassBasePerSquareMeter = 1.35d;
        public const double WingSystemsMassPerEnginePerSquareMeter = 0.18d;

        public const double AerodynamicCleanlinessMaxProductionCostIncreaseRatio = 0.05d;
        public const double AerodynamicCleanlinessMaxProductionManDaysIncreaseRatio = 0.20d;

        public const double AircraftLengthBaseMeters = 3.2d;
        public const double AircraftLengthWingAreaScale = 0.60d;
        public const double AircraftLengthWingAreaExponent = 0.36d;
        public const double AircraftLengthWeightScale = 1.25d;
        public const double AircraftLengthWeightExponent = 0.31d;
        public const double AircraftLengthMinMeters = 4d;
        public const double AircraftLengthMaxMeters = 95d;
        public const double AircraftLengthWingStretchReferenceArea = 30d;
        public const double AircraftLengthWingStretchExponent = 0.35d;
        public const double AircraftLengthWingStretchScale = 0.5d;
        public const double AircraftLengthWingStretchMaxBonus = 1.7d;
        public const double AircraftLengthWeightStretchReferenceKilograms = 10000d;
        public const double AircraftLengthWeightStretchExponent = 0.3d;
        public const double AircraftLengthWeightStretchScale = 0.25d;
        public const double AircraftLengthWeightStretchMaxBonus = 0.6d;

        public const double FuselageFrontalAreaPerLength = 0.17d;
        public const double FuselageFrontalAreaWeightSqrtScale = 0.0065d;
        public const double FuselageFrontalAreaMinSquareMeters = 0.6d;
        public const double FuselageFrontalAreaMaxSquareMeters = 140d;
    }

    public static class AircraftPerformance
    {
        public const double PropulsivePowerEfficiency = 0.82d;
        public const double FuelDensityKilogramsPerLiter = 0.72d;
        public const double GravityMetersPerSecondSquared = 9.80665d;
        public const double MinimumDragCoefficient = 0.012d;
        public const double MaximumDragCoefficient = 0.12d;
        public const double MinimumLiftCoefficient = 0.35d;
        public const double MaximumLiftCoefficient = 2.2d;
        public const double MinimumEmptyWeightKilograms = 180d;
        public const double EmptyWeightBaseSystemsKilograms = 210d;
        public const double EmptyWeightPerEngineSystemsKilograms = 90d;
        public const double EmptyWeightPerCrewSystemsKilograms = 36d;
        public const double WetWeightMarginRatio = 0.05d;
        public const double MaximumLiftCoefficientTakeoff = 2.1d;
        public const double JetCruiseAltitudeMeters = 10000d;
        public const double PropCruiseAltitudeMeters = 5000d;
        public const double MinimumCruiseSpeedMetersPerSecond = 35d;
        public const double ManeuverabilityWeightPenaltyPerKilogram = 0.00035d;
        public const double BodyFrontalAreaBaseSquareMeters = 0.8d;
        public const double BodyFrontalAreaWeightExponent = 0.55d;
        public const double BodyFrontalAreaWeightScale = 0.3d;
        public const double BodyFrontalAreaWingExponent = 0.45d;
        public const double BodyFrontalAreaWingScale = 0.05d;
        public const double BodyFrontalAreaMinSquareMeters = 0.5d;
        public const double BodyFrontalAreaMaxSquareMeters = 180d;
        public const double MultiEngineReliabilityPenaltyFactor = 0.55d;
        public const double SpeedWeightReductionMinSpeedKilometersPerHour = 100d;
        public const double SpeedWeightReductionMaxSpeedKilometersPerHour = 300d;
        public const double SpeedWeightReductionAtMinSpeed = 0.4d;
        public const double SpeedWeightReductionAtMaxSpeed = 0.05d;
        public const double SpeedWeightIncreaseMinSpeedKilometersPerHour = 1000d;
        public const double SpeedWeightIncreaseMaxSpeedKilometersPerHour = 5000d;
        public const double SpeedWeightIncreaseAtMinSpeed = 0.05d;
        public const double SpeedWeightIncreaseAtMaxSpeed = 0.4d;
        public const double TakeoffSpeedMinMetersPerSecond = 52d;
        public const double TakeoffSpeedMaxSpeedFraction = 0.42d;
        public const double LiftMarginMultiplier = 1.18d;
        public const double TakeoffRollingResistanceCoefficient = 0.03d;
        public const double TakeoffAverageDragFactor = 0.35d;
        public const double TakeoffDistanceSafetyFactor = 1.12d;
        public const double TakeoffPropellerMinimumSpeedMetersPerSecond = 28d;
        public const double AircraftWeightWithFuelSafetyMultiplier = 1.1d;
        public const double PayloadToEmptyWeightTransferRatio = 0.15d;

        public const double CruiseLiftUsableCoefficient = 0.86d;
        public const double CruiseLiftSafetySpeedMultiplier = 1.08d;
        public const double CruiseAerodynamicReferenceLiftToDrag = 15d;
        public const double CruiseAerodynamicSpeedGainPerLiftToDrag = 0.010d;
        public const double CruiseAerodynamicSpeedFactorMin = 0.88d;
        public const double CruiseAerodynamicSpeedFactorMax = 1.16d;
        public const double CruiseLiftSpeedBlendMin = 0.18d;
        public const double CruiseLiftSpeedBlendMax = 0.65d;
        public const double CruiseFuelIdleFraction = 0.3d;
        public const double CruiseFuelThrottleExponent = 1.08d;
        public const double CruiseMinimumThrottleFraction = 0.05d;
        public const double CruiseMinimumFuelBurnKilogramsPerHour = 0.1d;
        public const double CruiseInducedDragFactor = 0.005d;
        public const double JetSpeedEnvelopeBypassHighThreshold = 8d;
        public const double JetSpeedEnvelopeBypassMediumThreshold = 4d;
        public const double JetSpeedEnvelopeBypassLowThreshold = 1d;
        public const double JetSpeedEnvelopeBypassVeryLowThreshold = 0.35d;
        public const double JetSpeedEnvelopeHighBypassLimitMetersPerSecond = 320d;
        public const double JetSpeedEnvelopeMediumBypassLimitMetersPerSecond = 360d;
        public const double JetSpeedEnvelopeLowBypassLimitMetersPerSecond = 520d;
        public const double JetSpeedEnvelopeVeryLowBypassLimitMetersPerSecond = 760d;
        public const double JetSpeedEnvelopeTurbojetLikeLimitMetersPerSecond = 1020d;
        public const double JetSpeedEnvelopeCategoryDefaultFactor = 1d;
        public const double JetSpeedEnvelopeCategoryRegionalJetFactor = 0.92d;
        public const double JetSpeedEnvelopeCategoryBusinessJetFactor = 0.95d;
        public const double JetSpeedEnvelopeCategoryMediumTransportFactor = 0.9d;
        public const double JetSpeedEnvelopeCategoryHeavyTransportFactor = 0.88d;
        public const double JetSpeedEnvelopeCategorySpecialCivilFactor = 0.86d;
        public const double JetSpeedEnvelopeCategoryMilitaryStrategicFactor = 0.9d;
        public const double JetSpeedEnvelopeCategoryMilitaryTacticalFactor = 1d;
        public const double JetAltitudeGainVeryLowBypassAtTenKilometers = 0.35d;
        public const double JetAltitudeGainLowBypassAtTenKilometers = 0.22d;
        public const double JetAltitudeGainMediumBypassAtTenKilometers = 0.12d;
        public const double JetAltitudeGainHighBypassAtTenKilometers = 0.06d;
        public const double JetAltitudeGainVeryHighBypassAtTenKilometers = 0.02d;
        public const double JetAltitudeGainMilitaryTacticalBonus = 0.08d;
        public const double JetAltitudeGainBusinessJetBonus = 0.03d;
        public const double JetAltitudeGainRegionalJetBonus = 0.02d;
    }

    public static class Categories
    {
        public const double PayloadKilogramsPerPassenger = 250d;
    }

    public static IReadOnlyList<AircraftCategoryTuningEntry> CategoryEntries { get; } = Array.AsReadOnly(new[]
    {
        new AircraftCategoryTuningEntry(
            AircraftCategory.LightSportAndRecreational,
            "Light Sport and Recreational",
            MinCrew: 1,
            MaxCrew: 2,
            MinMaximumWeightKilograms: 0d,
            MaxMaximumWeightKilograms: 1200d,
            MinRequiredSpeedKilometersPerHour: 60d,
            MaxRequiredSpeedKilometersPerHour: 150d,
            MinRequiredRangeKilometers: 200d,
            MaxRequiredRangeKilometers: 1000d,
            MinRequiredPayloadKilograms: 150d,
            MaxRequiredPayloadKilograms: 350d,
            WeightModifierKilograms: -150d,
            SpeedModifierMetersPerSecond: -14d,
            ReliabilityModifierDelta: 0d,
            FuelConsumptionMultiplier: 1.02d),
        new AircraftCategoryTuningEntry(
            AircraftCategory.LightMultiRolePiston,
            "Light Multi-role Piston",
            MinCrew: 1,
            MaxCrew: 2,
            MinMaximumWeightKilograms: 0d,
            MaxMaximumWeightKilograms: 6000d,
            MinRequiredSpeedKilometersPerHour: 180d,
            MaxRequiredSpeedKilometersPerHour: 300d,
            MinRequiredRangeKilometers: 800d,
            MaxRequiredRangeKilometers: 1500d,
            MinRequiredPayloadKilograms: 800d,
            MaxRequiredPayloadKilograms: 1500d,
            WeightModifierKilograms: 75d,
            SpeedModifierMetersPerSecond: -6d,
            ReliabilityModifierDelta: 0d,
            FuelConsumptionMultiplier: 0.98d),
        new AircraftCategoryTuningEntry(
            AircraftCategory.TurbopropRegional,
            "Turboprop Regional",
            MinCrew: 1,
            MaxCrew: 2,
            MinMaximumWeightKilograms: 2500d,
            MaxMaximumWeightKilograms: 23000d,
            MinRequiredSpeedKilometersPerHour: 300d,
            MaxRequiredSpeedKilometersPerHour: 550d,
            MinRequiredRangeKilometers: 1000d,
            MaxRequiredRangeKilometers: 2500d,
            MinRequiredPayloadKilograms: 1200d,
            MaxRequiredPayloadKilograms: 7000d,
            WeightModifierKilograms: 500d,
            SpeedModifierMetersPerSecond: 0d,
            ReliabilityModifierDelta: 0d,
            FuelConsumptionMultiplier: 0.9d),
        new AircraftCategoryTuningEntry(
            AircraftCategory.RegionalJet,
            "Regional Jet",
            MinCrew: 2,
            MaxCrew: 2,
            MinMaximumWeightKilograms: 5000d,
            MaxMaximumWeightKilograms: 45000d,
            MinRequiredSpeedKilometersPerHour: 600d,
            MaxRequiredSpeedKilometersPerHour: 800d,
            MinRequiredRangeKilometers: 2000d,
            MaxRequiredRangeKilometers: 6000d,
            MinRequiredPayloadKilograms: 8000d,
            MaxRequiredPayloadKilograms: 20000d,
            WeightModifierKilograms: 1500d,
            SpeedModifierMetersPerSecond: 0d,
            ReliabilityModifierDelta: 0d,
            FuelConsumptionMultiplier: 0.88d),
        new AircraftCategoryTuningEntry(
            AircraftCategory.MediumTransportNarrowBody,
            "Medium Transport (Narrow-body)",
            MinCrew: 2,
            MaxCrew: 4,
            MinMaximumWeightKilograms: 15000d,
            MaxMaximumWeightKilograms: 90000d,
            MinRequiredSpeedKilometersPerHour: 700d,
            MaxRequiredSpeedKilometersPerHour: 900d,
            MinRequiredRangeKilometers: 3000d,
            MaxRequiredRangeKilometers: 10000d,
            MinRequiredPayloadKilograms: 10000d,
            MaxRequiredPayloadKilograms: 50000d,
            WeightModifierKilograms: 3000d,
            SpeedModifierMetersPerSecond: 6d,
            ReliabilityModifierDelta: 0.1d,
            FuelConsumptionMultiplier: 0.84d),
        new AircraftCategoryTuningEntry(
            AircraftCategory.HeavyTransportWideBody,
            "Heavy Transport and Cargo (Wide-body)",
            MinCrew: 2,
            MaxCrew: 6,
            MinMaximumWeightKilograms: 20000d,
            MaxMaximumWeightKilograms: 300000d,
            MinRequiredSpeedKilometersPerHour: 700d,
            MaxRequiredSpeedKilometersPerHour: 1000d,
            MinRequiredRangeKilometers: 8000d,
            MaxRequiredRangeKilometers: 15000d,
            MinRequiredPayloadKilograms: 30000d,
            MaxRequiredPayloadKilograms: 300000d,
            WeightModifierKilograms: 5000d,
            SpeedModifierMetersPerSecond: 8d,
            ReliabilityModifierDelta: 0.12d,
            FuelConsumptionMultiplier: 0.72d),
        new AircraftCategoryTuningEntry(
            AircraftCategory.SpecialCivil,
            "Special Civil",
            MinCrew: 1,
            MaxCrew: 2,
            MinMaximumWeightKilograms: 4000d,
            MaxMaximumWeightKilograms: 60000d,
            MinRequiredSpeedKilometersPerHour: 150d,
            MaxRequiredSpeedKilometersPerHour: 600d,
            MinRequiredRangeKilometers: 500d,
            MaxRequiredRangeKilometers: 2500d,
            MinRequiredPayloadKilograms: 1500d,
            MaxRequiredPayloadKilograms: 8000d,
            WeightModifierKilograms: 3000d,
            SpeedModifierMetersPerSecond: -8d,
            ReliabilityModifierDelta: 0d,
            FuelConsumptionMultiplier: 0.92d),
        new AircraftCategoryTuningEntry(
            AircraftCategory.BusinessVipJet,
            "Business and VIP Jet",
            MinCrew: 1,
            MaxCrew: 4,
            MinMaximumWeightKilograms: 5000d,
            MaxMaximumWeightKilograms: 45000d,
            MinRequiredSpeedKilometersPerHour: 600d,
            MaxRequiredSpeedKilometersPerHour: 950d,
            MinRequiredRangeKilometers: 2500d,
            MaxRequiredRangeKilometers: 9000d,
            MinRequiredPayloadKilograms: 1500d,
            MaxRequiredPayloadKilograms: 15000d,
            WeightModifierKilograms: 2000d,
            SpeedModifierMetersPerSecond: 3d,
            ReliabilityModifierDelta: 0.1d,
            FuelConsumptionMultiplier: 0.8d),
        new AircraftCategoryTuningEntry(
            AircraftCategory.MilitaryTactical,
            "Military Tactical",
            MinCrew: 1,
            MaxCrew: 2,
            MinMaximumWeightKilograms: 5000d,
            MaxMaximumWeightKilograms: 40000d,
            MinRequiredSpeedKilometersPerHour: 900d,
            MaxRequiredSpeedKilometersPerHour: 3000d,
            MinRequiredRangeKilometers: 1000d,
            MaxRequiredRangeKilometers: 5000d,
            MinRequiredPayloadKilograms: 2000d,
            MaxRequiredPayloadKilograms: 15000d,
            WeightModifierKilograms: 5000d,
            SpeedModifierMetersPerSecond: 35d,
            ReliabilityModifierDelta: -0.1d,
            FuelConsumptionMultiplier: 1.08d),
        new AircraftCategoryTuningEntry(
            AircraftCategory.MilitaryStrategicSupport,
            "Military Strategic and Support",
            MinCrew: 3,
            MaxCrew: 20,
            MinMaximumWeightKilograms: 20000d,
            MaxMaximumWeightKilograms: 200000d,
            MinRequiredSpeedKilometersPerHour: 500d,
            MaxRequiredSpeedKilometersPerHour: 900d,
            MinRequiredRangeKilometers: 3000d,
            MaxRequiredRangeKilometers: 10000d,
            MinRequiredPayloadKilograms: 9000d,
            MaxRequiredPayloadKilograms: 200000d,
            WeightModifierKilograms: 4000d,
            SpeedModifierMetersPerSecond: -5d,
            ReliabilityModifierDelta: 0.12d,
            FuelConsumptionMultiplier: 0.78d),
    });
}

public readonly record struct AircraftCategoryTuningEntry(
    AircraftCategory Category,
    string DisplayName,
    int MinCrew,
    int MaxCrew,
    double MinMaximumWeightKilograms,
    double MaxMaximumWeightKilograms,
    double MinRequiredSpeedKilometersPerHour,
    double MaxRequiredSpeedKilometersPerHour,
    double MinRequiredRangeKilometers,
    double MaxRequiredRangeKilometers,
    double MinRequiredPayloadKilograms,
    double MaxRequiredPayloadKilograms,
    double WeightModifierKilograms,
    double SpeedModifierMetersPerSecond,
    double ReliabilityModifierDelta,
    double FuelConsumptionMultiplier)
{
    public AircraftCategoryProfile ToProfile() =>
        new(
            Category,
            DisplayName,
            MinCrew,
            MaxCrew,
            MinMaximumWeightKilograms,
            MaxMaximumWeightKilograms,
            MinRequiredSpeedKilometersPerHour,
            MaxRequiredSpeedKilometersPerHour,
            MinRequiredRangeKilometers,
            MaxRequiredRangeKilometers,
            MinRequiredPayloadKilograms,
            MaxRequiredPayloadKilograms,
            WeightModifierKilograms,
            SpeedModifierMetersPerSecond,
            ReliabilityModifierDelta,
            FuelConsumptionMultiplier);
}
