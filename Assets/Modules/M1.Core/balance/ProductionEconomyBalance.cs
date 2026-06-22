using System;
using WrightTycoon.Core.Aircraft;
using WrightTycoon.Core.Airframes;
using WrightTycoon.Core.Engines;

namespace WrightTycoon.Core.Balance;

public static class ProductionEconomyBalance
{
    public const double LaborCostPerManDay = 80d;

    public static class Engine
    {
        public const double PistonBaseParts = 200d;
        public const double PistonCylinderParts = 115d;
        public const double PistonReferenceCylinderVolumeLiters = 0.6d;
        public const double PistonCylinderPartsExponent = 0.55d;
        public const double PistonDisplacementPartsPerLiter = 140d;
        public const double PistonBaseManDays = 14d;
        public const double PistonCylinderManDays = 5.5d;
        public const double PistonCylinderManDaysExponent = 0.45d;
        public const double PistonDisplacementManDaysPerLiter = 5.83d;

        public const double JetReferenceDiameterMeters = 1.2d;
        public const double JetSizeExponent = 2.1d;
        public const double TurbojetReferenceParts = 30000d;
        public const double TurbojetReferenceManDays = 200d;
        public const double TurbofanReferenceParts = 55000d;
        public const double TurbofanReferenceManDays = 340d;

        public const double DirectInjectionPartsMultiplier = 1.08d;
        public const double DirectInjectionManDaysMultiplier = 1.06d;
        public const double TurboPartsMultiplier = 1.22d;
        public const double TurboManDaysMultiplier = 1.20d;
        public const double SuperchargerPartsMultiplier = 1.16d;
        public const double SuperchargerManDaysMultiplier = 1.14d;
        public const double AfterburnerPartsMultiplier = 1.35d;
        public const double AfterburnerManDaysMultiplier = 1.45d;
        public const double BypassPartsIncreasePerPoint = 0.025d;
        public const double BypassManDaysIncreasePerPoint = 0.035d;
        public const double BypassPricingCap = 12d;
        public const double PistonReferenceRpm = 2500d;
        public const double JetReferenceRpm = 50000d;
        public const double RpmStepRatio = 0.25d;
        public const double RpmPartsIncreasePerStep = 0.08d;
        public const double RpmManDaysIncreasePerStep = 0.10d;
        public const double CoolingReference = 0.75d;
        public const double CoolingStep = 0.10d;
        public const double CoolingPartsIncreasePerStep = 0.04d;
        public const double CoolingManDaysIncreasePerStep = 0.03d;
        public const double AltitudeReferenceMeters = 1500d;
        public const double AltitudeStepMeters = 1000d;
        public const double AltitudePartsIncreasePerStep = 0.005d;
        public const double AltitudeManDaysIncreasePerStep = 0.008d;
        public const double EfficiencyOptimizationMaximumPercent = 30d;
        public const double EfficiencyOptimizationMaximumPartsIncrease = 0.12d;
        public const double EfficiencyOptimizationMaximumManDaysIncrease = 0.20d;
    }

    public static class Airframe
    {
        public const double ReferenceStructuralIndex = 15.6d;
        public const double ReferenceParts = 51500d;
        public const double PartsExponent = 1.31d;
        public const double ReferenceManDays = 245d;
        public const double ManDaysExponent = 1.18d;
        public const double PayloadIndexDivisor = 100d;
        public const double FuelIndexDivisor = 500d;
        public const double DefaultPayloadKilograms = 300d;
        public const double BaseWeightPayloadFactor = 0.35d;

        public const double WoodPartsMultiplier = 0.70d;
        public const double WoodManDaysMultiplier = 0.85d;
        public const double AluminumPartsMultiplier = 1d;
        public const double AluminumManDaysMultiplier = 1d;
        public const double CompositePartsMultiplier = 1.45d;
        public const double CompositeManDaysMultiplier = 1.30d;
        public const double TrussPartsMultiplier = 0.85d;
        public const double TrussManDaysMultiplier = 0.90d;
        public const double MonocoquePartsMultiplier = 1d;
        public const double MonocoqueManDaysMultiplier = 1d;

        public const double SweptWingPartsRate = 0.12d;
        public const double SweptWingManDaysRate = 0.15d;
        public const double DeltaWingPartsRate = 0.18d;
        public const double DeltaWingManDaysRate = 0.20d;
        public const double HighWingPartsRate = 0.04d;
        public const double HighWingManDaysRate = 0.03d;
        public const double BiplanePartsRate = -0.35d;
        public const double BiplaneManDaysRate = -0.25d;
        public const double LightProfilePartsRate = -0.05d;
        public const double LightProfileManDaysRate = -0.03d;
        public const double LaminarProfilePartsRate = 0.12d;
        public const double LaminarProfileManDaysRate = 0.18d;
        public const double AcrobaticProfilePartsRate = 0.08d;
        public const double AcrobaticProfileManDaysRate = 0.10d;
        public const double TTailPartsRate = 0.04d;
        public const double TTailManDaysRate = 0.06d;
        public const double VTailPartsRate = 0.05d;
        public const double VTailManDaysRate = 0.08d;
        public const double TwinTailPartsRate = 0.07d;
        public const double TwinTailManDaysRate = 0.08d;
        public const double NoFlapsPartsRate = -0.04d;
        public const double NoFlapsManDaysRate = -0.06d;
        public const double FowlerFlapsPartsRate = 0.08d;
        public const double FowlerFlapsManDaysRate = 0.12d;
        public const double LeadingEdgeFlapsPartsRate = 0.10d;
        public const double LeadingEdgeFlapsManDaysRate = 0.14d;
        public const double SemiRetractableGearPartsRate = 0.05d;
        public const double SemiRetractableGearManDaysRate = 0.08d;
        public const double RetractableGearPartsRate = 0.12d;
        public const double RetractableGearManDaysRate = 0.18d;
        public const double BackupSystemsPartsRate = 0.08d;
        public const double BackupSystemsManDaysRate = 0.12d;
        public const double MaximumCleanlinessPartsRate = 0.08d;
        public const double MaximumCleanlinessManDaysRate = 0.25d;
        public const double AdditionalEngineInstallationPartsRate = 0.015d;
        public const double AdditionalEngineInstallationManDaysRate = 0.02d;

        public const double PassengerRolePartsRate = 0.12d;
        public const double PassengerRoleManDaysRate = 0.12d;
        public const double CargoRolePartsRate = 0.08d;
        public const double CargoRoleManDaysRate = 0.07d;
        public const double SpecialRolePartsRate = 0.25d;
        public const double SpecialRoleManDaysRate = 0.30d;
        public const double CombatRolePartsRate = 0.35d;
        public const double CombatRoleManDaysRate = 0.45d;

        public const double BasicNavigationMinimumParts = 4000d;
        public const double BasicNavigationPartsRate = 0.02d;
        public const double BasicNavigationMinimumManDays = 20d;
        public const double BasicNavigationManDaysRate = 0.02d;
        public const double AdvancedNavigationMinimumParts = 15000d;
        public const double AdvancedNavigationPartsRate = 0.05d;
        public const double AdvancedNavigationMinimumManDays = 60d;
        public const double AdvancedNavigationManDaysRate = 0.05d;
    }

    public static double CalculateLaborCost(double manDays) => System.Math.Max(0d, manDays) * LaborCostPerManDay;

    public static double CalculateCompleteCost(double partsCost, double manDays) =>
        System.Math.Max(0d, partsCost) + CalculateLaborCost(manDays);

    public static double GetEngineArchitecturePartsMultiplier(EngineType type) => type switch
    {
        EngineType.V => 1.08d,
        EngineType.Radial => 0.90d,
        _ => 1d,
    };

    public static double GetEngineArchitectureManDaysMultiplier(EngineType type) => type switch
    {
        EngineType.V => 1.10d,
        EngineType.Radial => 0.95d,
        _ => 1d,
    };

    public static double GetPistonCompressionPartsMultiplier(double ratio) => ratio switch
    {
        <= 6d => 0.90d,
        <= 8d => 1d,
        <= 10d => 1.06d,
        <= 12d => 1.14d,
        <= 16d => 1.28d,
        _ => 1.45d,
    };

    public static double GetPistonCompressionManDaysMultiplier(double ratio) => ratio switch
    {
        <= 6d => 0.92d,
        <= 8d => 1d,
        <= 10d => 1.04d,
        <= 12d => 1.10d,
        <= 16d => 1.20d,
        _ => 1.32d,
    };

    public static double GetJetCompressionPartsMultiplier(double ratio) => ratio switch
    {
        <= 6d => 0.85d,
        <= 12d => 1d,
        <= 20d => 1.12d,
        <= 30d => 1.28d,
        <= 40d => 1.50d,
        _ => 1.80d,
    };

    public static double GetJetCompressionManDaysMultiplier(double ratio) => ratio switch
    {
        <= 6d => 0.90d,
        <= 12d => 1d,
        <= 20d => 1.10d,
        <= 30d => 1.22d,
        <= 40d => 1.40d,
        _ => 1.65d,
    };

    public static double CalculateStructuralIndex(double wingArea, double payload, double fuelLiters) =>
        System.Math.Max(1d, wingArea)
        + (System.Math.Max(0d, payload) / Airframe.PayloadIndexDivisor)
        + (System.Math.Max(0d, fuelLiters) / Airframe.FuelIndexDivisor);

    public static double CalculateAirframeBaseParts(double structuralIndex) =>
        Airframe.ReferenceParts * System.Math.Pow(System.Math.Max(structuralIndex, 1d) / Airframe.ReferenceStructuralIndex, Airframe.PartsExponent);

    public static double CalculateAirframeBaseManDays(double structuralIndex) =>
        Airframe.ReferenceManDays * System.Math.Pow(System.Math.Max(structuralIndex, 1d) / Airframe.ReferenceStructuralIndex, Airframe.ManDaysExponent);

    public static double GetRolePartsRate(AircraftCategoryRoleRequirement role) => role switch
    {
        AircraftCategoryRoleRequirement.PassengerCabin => Airframe.PassengerRolePartsRate,
        AircraftCategoryRoleRequirement.CargoSpace => Airframe.CargoRolePartsRate,
        AircraftCategoryRoleRequirement.SpecialEquipment => Airframe.SpecialRolePartsRate,
        AircraftCategoryRoleRequirement.CombatEquipment => Airframe.CombatRolePartsRate,
        _ => 0d,
    };

    public static double GetRoleManDaysRate(AircraftCategoryRoleRequirement role) => role switch
    {
        AircraftCategoryRoleRequirement.PassengerCabin => Airframe.PassengerRoleManDaysRate,
        AircraftCategoryRoleRequirement.CargoSpace => Airframe.CargoRoleManDaysRate,
        AircraftCategoryRoleRequirement.SpecialEquipment => Airframe.SpecialRoleManDaysRate,
        AircraftCategoryRoleRequirement.CombatEquipment => Airframe.CombatRoleManDaysRate,
        _ => 0d,
    };
}
