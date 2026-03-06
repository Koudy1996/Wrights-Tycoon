using System;
using System.Collections.Generic;
using WrightTycoon.Core.Categories;

namespace WrightTycoon.Core.Balance;

// M3 tuning surface: aircraft/airframe/category gameplay constants.
public static class PlaneDesignerBalance
{
    public static class Airframe
    {
        public const int CrewMin = 1;
        public const int CrewMax = 20;
        public const double FuelMinLiters = 50d;
        public const double FuelMaxLiters = 50000d;
        public const double WoodFabricMaximumTotalWeightKilograms = 15000d;
        public const double WoodFabricEqualWeightThresholdKilograms = 800d;
        public const double WoodFabricHeavyWeightReferenceKilograms = 12000d;
        public const double WoodFabricHeavyWeightFactor = 1.22d;
        public const double SmallWoodTrussWeightReductionThresholdKilograms = 3000d;
        public const double SmallWoodTrussMaxWeightReductionRatio = 0.3d;
        public const double BackupSystemsWeightMultiplier = 1.1d;
        public const double BackupSystemsDragMultiplier = 1.03d;
        public const double BackupSystemsReliabilityDelta = 0.2d;
        public const double DeltaWingLiftMultiplier = 0.80d;
        public const double LaminarProfileFullEffectMaxWingAreaSquareMeters = 50d;
        public const double LaminarProfileZeroEffectMinWingAreaSquareMeters = 500d;
        public const double LaminarProfileLiftBonusMaxRatio = 0.05d;
        public const double LaminarProfileDragReductionMaxRatio = 0.05d;
        public const double Naca4DigitLightProfileWeightReductionRatio = 0.10d;
        public const double Naca4DigitLightProfileAirframeCostReductionRatio = 0.10d;
        public const double SymmetricalAcrobaticProfileLiftPenaltyRatio = 0.10d;
        public const double SymmetricalAcrobaticProfileManeuverabilityBonusRatio = 0.10d;
        public const double SymmetricalAcrobaticProfileCostIncreaseRatio = 0.20d;
        public const double WingPositionMassFactorLow = 1d;
        public const double WingPositionMassFactorHigh = 0.96d;
        public const double WingPositionMassFactorBiplane = 0.83d;
        public const double WingPositionDragDeltaLow = 0d;
        public const double WingPositionDragDeltaHigh = 0.003d;
        public const double WingPositionDragDeltaBiplane = 0.03d;
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
        public const double WingMassCoefficient = 5.2d;
        public const double WingReinforcementMassBaseShare = 0.07d;
        public const double WingReinforcementMassExponent = 1.7d;
        public const double WingSystemsMassBasePerSquareMeter = 1.18d;
        public const double WingSystemsMassPerEnginePerSquareMeter = 0.18d;

        public const double AerodynamicCleanlinessManDaysPerPercent = 1.2d;
        public const double AerodynamicCleanlinessManDaysBaseShare = 0.65d;

        public const double AircraftLengthBaseMeters = 2.5d;
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

    public static class Performance
    {
        public const double PropulsivePowerEfficiency = 0.82d;
        public const double FuelDensityKilogramsPerLiter = 0.72d;
        public const double GravityMetersPerSecondSquared = 9.80665d;
        public const double MinimumDragCoefficient = 0.012d;
        public const double MaximumDragCoefficient = 0.12d;
        public const double SpeedCalculationMaximumDragCoefficient = 0.24d;
        public const double WingPositionSpeedDragFactorLow = 1d;
        public const double WingPositionSpeedDragFactorHigh = 1.1d;
        public const double WingPositionSpeedDragFactorBiplane = 1.35d;
        public const double DeltaWingHighSpeedDragReductionStartKilometersPerHour = 500d;
        public const double DeltaWingHighSpeedDragReductionEndKilometersPerHour = 2000d;
        public const double DeltaWingHighSpeedDragReductionMaxRatio = 0.30d;
        public const double Naca4DigitLightProfileDragIncreaseStartKilometersPerHour = 250d;
        public const double Naca4DigitLightProfileDragIncreaseEndKilometersPerHour = 1000d;
        public const double Naca4DigitLightProfileDragIncreaseMaxRatio = 0.80d;
        public const double MinimumLiftCoefficient = 0.35d;
        public const double MaximumLiftCoefficient = 2.2d;
        public const double MinimumEmptyWeightKilograms = 180d;
        public const double EmptyWeightBaseSystemsKilograms = 210d;
        public const double EmptyWeightPerEngineSystemsKilograms = 50d;
        public const double EmptyWeightPerCrewSystemsKilograms = 250d;
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
        public const double BodyFrontalAreaWingScale = 0.08d;
        public const double BodyFrontalAreaMinSquareMeters = 0.5d;
        public const double BodyFrontalAreaMaxSquareMeters = 180d;
        public const double MultiEngineReliabilityPenaltyFactor = 0.55d;
        public const double UltralightSystemsMassFactorAt50Kilowatts = 0.33d;
        public const double UltralightSystemsMassMaxPowerKilowatts = 5d;
        public const double UltralightSystemsMassMaxWingAreaSquareMeters = 3d;
        public const int UltralightSystemsMassMaxCrew = 1;

        public const double SpeedWeightReductionMinSpeedKilometersPerHour = 100d;
        public const double SpeedWeightReductionMaxSpeedKilometersPerHour = 300d;
        public const double SpeedWeightReductionAtMinSpeed = 0.4d;
        public const double SpeedWeightReductionAtMaxSpeed = 0.05d;

        public const double SpeedWeightIncreaseMinSpeedKilometersPerHour = 1000d;
        public const double SpeedWeightIncreaseMaxSpeedKilometersPerHour = 5000d;
        public const double SpeedWeightIncreaseAtMinSpeed = 0.05d;
        public const double SpeedWeightIncreaseAtMaxSpeed = 0.4d;

        public const double CruiseMinimumFuelBurnKilogramsPerHour = 0.12d;

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
    }

    public static class Categories
    {
        public const double PayloadKilogramsPerPassenger = 250d;
    }

    public static IReadOnlyList<AircraftCategoryBalanceEntry> CategoryEntries { get; } = Array.AsReadOnly(new[]
    {
        new AircraftCategoryBalanceEntry(
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
            WeightModifierKilograms: -350d,
            SpeedModifierMetersPerSecond: -14d,
            ReliabilityModifierDelta: 0d,
            FuelConsumptionMultiplier: 1.02d),
        new AircraftCategoryBalanceEntry(
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
        new AircraftCategoryBalanceEntry(
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
        new AircraftCategoryBalanceEntry(
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
        new AircraftCategoryBalanceEntry(
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
        new AircraftCategoryBalanceEntry(
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
        new AircraftCategoryBalanceEntry(
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
        new AircraftCategoryBalanceEntry(
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
        new AircraftCategoryBalanceEntry(
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
        new AircraftCategoryBalanceEntry(
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

    public static class HistoricalCampaignTechnology
    {
        public static readonly IReadOnlyList<YearlyBalancePoint> AirframeEfficiencyCurve = Array.AsReadOnly(new[]
        {
            new YearlyBalancePoint(1946, 0.62d),
            new YearlyBalancePoint(1955, 0.70d),
            new YearlyBalancePoint(1965, 0.78d),
            new YearlyBalancePoint(1975, 0.84d),
            new YearlyBalancePoint(1985, 0.89d),
            new YearlyBalancePoint(1995, 0.94d),
            new YearlyBalancePoint(2005, 0.97d),
            new YearlyBalancePoint(2015, 0.995d),
            new YearlyBalancePoint(2020, 1.0d),
        });

        public static readonly IReadOnlyList<YearlyBalancePoint> AerodynamicsEfficiencyCurve = Array.AsReadOnly(new[]
        {
            new YearlyBalancePoint(1946, 0.56d),
            new YearlyBalancePoint(1955, 0.64d),
            new YearlyBalancePoint(1965, 0.74d),
            new YearlyBalancePoint(1975, 0.82d),
            new YearlyBalancePoint(1985, 0.88d),
            new YearlyBalancePoint(1995, 0.93d),
            new YearlyBalancePoint(2005, 0.97d),
            new YearlyBalancePoint(2015, 0.995d),
            new YearlyBalancePoint(2020, 1.0d),
        });
    }
}

public readonly record struct AircraftCategoryBalanceEntry(
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

