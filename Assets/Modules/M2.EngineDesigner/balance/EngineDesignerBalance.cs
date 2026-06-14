using System;
using System.Collections.Generic;

namespace WrightTycoon.Core.Balance;

// M2 tuning surface: all engine designer constants in one editable place.
public static class EngineDesignerBalance
{
    public static class Engine
    {
        public const double MinimumReliability = 0.3d;
        public const double MaximumReliability = 0.9d;

        public const int PistonCylinderMin = 2;
        public const int PistonCylinderMax = 48;
        public const int InlineCylinderMax = 8;
        public const double PistonCompressionMin = 3d;
        public const double PistonCompressionMax = 12d;
        public const double PistonCompressionPowerFactorAtMax = 2d;
        public const double PistonCompressionFuelSpecificConsumptionFactorAtMax = 0.80d;
        public const double PistonVolumetricEfficiencyMin = 0.52d;
        public const double PistonVolumetricEfficiencyMax = 2.2d;
        public const double PistonOptimizationMin = 0d;
        public const double PistonOptimizationMax = 10000d;
        public const double PistonSpeedMinRpm = 500d;
        public const double PistonSpeedMaxRpm = 7000d;

        public const double EfficiencyOptimizationMinPercent = 0d;
        public const double EfficiencyOptimizationMaxPercent = 30d;
        public const double EfficiencyOptimizationPowerPenaltyAtMax = 0.20d;
        public const double EfficiencyOptimizationCoolingBonusAtMax = 0.10d;
        public const double EfficiencyOptimizationFuelBurnReductionAtMax = 0.30d;

        public const double JetCompressionMin = 5d;
        public const double JetCompressionMax = 50d;
        public const double JetTurbineDiameterMinMeters = 0.05d;
        public const double JetTurbineDiameterMaxMeters = 4.0d;
        public const double JetBypassRatioMin = 0.1d;
        public const double JetBypassRatioMax = 20d;
        public const double TurbofanLowBypassPenaltyBypassThreshold = 5d;
        public const double TurbofanLowBypassPenaltyStartThrustNewtons = 150000d;
        public const double TurbofanLowBypassPenaltyPer100kNewtons = 0.12d;
        public const double TurbofanLowBypassPenaltyMax = 0.5d;
        public const double JetSpeedMinRpm = 2000d;
        public const double JetSpeedMaxRpm = 100000d;

        public const double JetAfterburnerEnabledPowerFactor = 1.20d;
        public const double JetAfterburnerDisabledPowerFactor = 0.80d;
        public const double JetAfterburnerWeightMultiplier = 1.35d;
        public const double JetAfterburnerCostMultiplier = 1.55d;
        public const double JetAfterburnerProductionManDaysMultiplier = 1.40d;

        public const double OptimizationNormalizedDeltaDivisor = 10000d;
        public const double OptimizationNormalizedDeltaMax = 1.45d;
        public const double OptimizationProximityBonusScale = 0.15d;
        public const double OptimizationMismatchPenaltyScale = 0.2d;
        public const double OptimizationTurboPenaltyMultiplier = 0.55d;
        public const double OptimizationSuperchargerPenaltyMultiplier = 0.65d;
        public const double OptimizationTurboBonus = 0.015d;
        public const double OptimizationSuperchargerBonus = 0.01d;
        public const double OptimizationFactorMin = 0.82d;
        public const double OptimizationFactorMax = 1.16d;
        public const double TurboAltitudeBonusStartMeters = 1200d;
        public const double TurboAltitudeBonusRangeMeters = 8000d;
        public const double TurboAltitudeBonusMax = 0.51d;
        public const double TurboLowAltitudePenaltyCeilingMeters = 1200d;
        public const double TurboLowAltitudePenaltyMax = 0.08d;
        public const double TurboOptimizationSynergyAltitudeRangeMeters = 7000d;
        public const double TurboOptimizationSynergyBonusMax = 0.11d;
        public const double TurboBoostAltitudeFactorMin = 0.99d;
        public const double TurboBoostAltitudeFactorMax = 1.81d;
        public const double SuperchargerOptimizedAltitudeRangeMeters = 5000d;
        public const double SuperchargerOptimizedAltitudeBonusMax = 0.29d;
        public const double SuperchargerOffDesignPenaltyMax = 0.09d;
        public const double SuperchargerAltitudeSupportAtOptimizedAltitude = 0.39d;
        public const double SuperchargerAltitudeSupportMax = 0.44d;
        public const double SuperchargerBoostAltitudeFactorMin = 0.82d;
        public const double SuperchargerBoostAltitudeFactorMax = 1.0d;

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
        public const double TurboBoostFuelFactor = 1.18d;
        public const double SuperchargerBoostFuelFactor = 1.12d;
        public const double TurbojetFuelConsumptionMultiplier = 1.2d;
        public const double TurboBoostJetFactor = 1.05d;
        public const double SuperchargerBoostJetFactor = 1.03d;
        public const double TurboBoostJetReliabilityPenalty = 0.03d;
        public const double SuperchargerBoostJetReliabilityPenalty = 0.02d;
        public const double TurboBoostPistonReliabilityPenalty = 0.06d;
        public const double SuperchargerBoostPistonReliabilityPenalty = 0.045d;

        public const double CarburetorPowerFactor = 0.88d;
        public const double CarburetorCostDiscount = 6200d;
        public const double CarburetorProductionManDayDiscount = 6d;

        public const double PistonDerivedDiameterBaseMeters = 0.22d;
        public const double PistonDerivedDiameterPerLiter = 0.042d;
        public const double PistonDerivedDiameterCylinderFactor = 0.004d;
        public const double PistonDerivedDiameterMinMeters = 0.25d;
        public const double PistonDerivedDiameterMaxMeters = 2.3d;
        public const double PistonCoreDiameterInlineFactor = 0.9d;
        public const double PistonCoreDiameterVFactor = 1d;
        public const double PistonCoreDiameterRadialFactor = 1.45d;
        public const double PistonCoreDiameterGenericFactor = 0.95d;

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

        public const double EngineFrontalAreaCompressionScale = 0.009d;
        public const double EngineFrontalAreaCoolingScale = 0.22d;
        public const double EngineFrontalAreaTurboBoostFactor = 0.12d;
        public const double EngineFrontalAreaSuperchargerBoostFactor = 0.08d;
        public const double EngineFrontalAreaMinSquareMeters = 0.04d;
        public const double EngineFrontalAreaMaxSquareMeters = 14d;

        public const double PistonWeightArrangementFactorInline = 1.02d;
        public const double PistonWeightArrangementFactorV = 1.12d;
        public const double PistonWeightArrangementFactorRadial = 0.72d;
        public const double PistonWeightArrangementFactorGeneric = 0.9d;
        public const double PistonCylinderMassInline = 4.8d;
        public const double PistonCylinderMassV = 5.1d;
        public const double PistonCylinderMassRadial = 3.2d;
        public const double PistonCylinderMassGeneric = 4.6d;
        public const double PistonPackagingMassPerLiterInline = 3.2d;
        public const double PistonPackagingMassPerLiterV = 4.8d;
        public const double PistonPackagingMassPerLiterRadial = 2.2d;
        public const double PistonPackagingMassPerLiterGeneric = 2.8d;

        public const double ComplexityPenaltyPiston = 0.02d;
        public const double ComplexityPenaltyInline = 0.03d;
        public const double ComplexityPenaltyV = 0.04d;
        public const double ComplexityPenaltyRadial = 0.05d;
        public const double ComplexityPenaltyJet = 0.08d;
        public const double ComplexityPenaltyTurbojet = 0.1d;
        public const double ComplexityPenaltyTurbofan = 0.12d;
    }

    public static class HistoricalCampaignTechnology
    {
        public static readonly IReadOnlyList<YearlyBalancePoint> PistonEnginePerformanceCurve = Array.AsReadOnly(new[]
        {
            new YearlyBalancePoint(1946, 0.85d),
            new YearlyBalancePoint(1955, 0.88d),
            new YearlyBalancePoint(1965, 0.91d),
            new YearlyBalancePoint(1975, 0.93d),
            new YearlyBalancePoint(1985, 0.94d),
            new YearlyBalancePoint(1995, 0.96d),
            new YearlyBalancePoint(2005, 0.975d),
            new YearlyBalancePoint(2015, 0.995d),
            new YearlyBalancePoint(2020, 1.0d),
        });

        public static readonly IReadOnlyList<YearlyBalancePoint> PistonFuelEfficiencyCurve = Array.AsReadOnly(new[]
        {
            new YearlyBalancePoint(1946, 0.80d),
            new YearlyBalancePoint(1955, 0.83d),
            new YearlyBalancePoint(1965, 0.86d),
            new YearlyBalancePoint(1975, 0.91d),
            new YearlyBalancePoint(1985, 0.93d),
            new YearlyBalancePoint(1995, 0.94d),
            new YearlyBalancePoint(2005, 0.975d),
            new YearlyBalancePoint(2015, 0.995d),
            new YearlyBalancePoint(2020, 1.0d),
        });

        public static readonly IReadOnlyList<YearlyBalancePoint> EnginePerformanceCurve = Array.AsReadOnly(new[]
        {
            new YearlyBalancePoint(1946, 0.60d),
            new YearlyBalancePoint(1955, 0.68d),
            new YearlyBalancePoint(1965, 0.76d),
            new YearlyBalancePoint(1975, 0.83d),
            new YearlyBalancePoint(1985, 0.89d),
            new YearlyBalancePoint(1995, 0.94d),
            new YearlyBalancePoint(2005, 0.975d),
            new YearlyBalancePoint(2015, 0.995d),
            new YearlyBalancePoint(2020, 1.0d),
        });

        public static readonly IReadOnlyList<YearlyBalancePoint> FuelEfficiencyCurve = Array.AsReadOnly(new[]
        {
            new YearlyBalancePoint(1946, 0.60d),
            new YearlyBalancePoint(1955, 0.664d),
            new YearlyBalancePoint(1965, 0.744d),
            new YearlyBalancePoint(1975, 0.816d),
            new YearlyBalancePoint(1985, 0.88d),
            new YearlyBalancePoint(1995, 0.928d),
            new YearlyBalancePoint(2005, 0.968d),
            new YearlyBalancePoint(2015, 0.992d),
            new YearlyBalancePoint(2020, 1.0d),
        });
    }
}
