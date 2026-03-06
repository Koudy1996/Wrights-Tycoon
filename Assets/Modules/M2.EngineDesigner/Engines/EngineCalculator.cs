using System;
using System.Collections.Generic;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Campaign;
using WrightTycoon.Core.Math;
using WrightTycoon.Core.Physics;
using WrightTycoon.Core.Units;

namespace WrightTycoon.Core.Engines;

public sealed class EngineCalculator : IEngineCalculator
{
    private const double MinimumReliability = EngineDesignerBalance.Engine.MinimumReliability;
    private const double MaximumReliability = EngineDesignerBalance.Engine.MaximumReliability;
    private const int PistonCylinderMin = EngineDesignerBalance.Engine.PistonCylinderMin;
    private const int PistonCylinderMax = EngineDesignerBalance.Engine.PistonCylinderMax;
    private const double PistonCompressionMin = EngineDesignerBalance.Engine.PistonCompressionMin;
    private const double PistonCompressionMax = EngineDesignerBalance.Engine.PistonCompressionMax;
    private const double PistonOptimizationMin = EngineDesignerBalance.Engine.PistonOptimizationMin;
    private const double PistonOptimizationMax = EngineDesignerBalance.Engine.PistonOptimizationMax;
    private const double PistonSpeedMin = EngineDesignerBalance.Engine.PistonSpeedMinRpm;
    private const double PistonSpeedMax = EngineDesignerBalance.Engine.PistonSpeedMaxRpm;

    private const double JetCompressionMin = EngineDesignerBalance.Engine.JetCompressionMin;
    private const double JetCompressionMax = EngineDesignerBalance.Engine.JetCompressionMax;
    private const double JetTurbineDiameterMinMeters = EngineDesignerBalance.Engine.JetTurbineDiameterMinMeters;
    private const double JetTurbineDiameterMaxMeters = EngineDesignerBalance.Engine.JetTurbineDiameterMaxMeters;
    private const double JetBypassRatioMin = EngineDesignerBalance.Engine.JetBypassRatioMin;
    private const double JetBypassRatioMax = EngineDesignerBalance.Engine.JetBypassRatioMax;
    private const double JetSpeedMin = EngineDesignerBalance.Engine.JetSpeedMinRpm;
    private const double JetSpeedMax = EngineDesignerBalance.Engine.JetSpeedMaxRpm;

    private static readonly double[] EvaluationAltitudes = CoreBalance.Atmosphere.EvaluationAltitudesMeters;

    public EnginePerformanceReport Calculate(
        EngineDefinition definition,
        EngineParameters parameters,
        IAtmosphereModel atmosphereModel,
        HistoricalCampaignTechnologyModifiers? technologyModifiers = null)
    {
        if (definition is null)
        {
            throw new ArgumentNullException(nameof(definition));
        }

        if (parameters is null)
        {
            throw new ArgumentNullException(nameof(parameters));
        }

        if (atmosphereModel is null)
        {
            throw new ArgumentNullException(nameof(atmosphereModel));
        }

        definition.Validate();
        parameters.Validate();
        ValidateContextualInputs(definition.EngineType, parameters);
        var effectiveParameters = NormalizeParametersForEngineType(definition.EngineType, parameters);
        var effectiveTechnologyModifiers = HistoricalCampaignTechnologyModifiers.Sanitize(technologyModifiers);

        var outputKind = IsJetFamily(definition.EngineType) ? EngineOutputKind.ThrustNewtons : EngineOutputKind.PowerKilowatts;
        var baseSeaLevelOutput = outputKind == EngineOutputKind.PowerKilowatts
            ? CalculateSeaLevelPower(definition, effectiveParameters)
            : CalculateSeaLevelThrust(definition, effectiveParameters);
        baseSeaLevelOutput *= effectiveTechnologyModifiers.EnginePerformanceMultiplier;
        var seaLevelOutput = ApplyJetAfterburnerOutputFactor(baseSeaLevelOutput, outputKind, effectiveParameters);

        var altitudePerformance = BuildAltitudePerformance(
            definition,
            effectiveParameters,
            atmosphereModel,
            outputKind,
            seaLevelOutput,
            baseSeaLevelOutput,
            effectiveTechnologyModifiers);

        var reliability = CalculateReliability(definition, effectiveParameters);
        var weight = CalculateWeight(definition, effectiveParameters, seaLevelOutput, outputKind);
        var cost = CalculateCost(definition, effectiveParameters, seaLevelOutput, outputKind, weight);
        var productionMd = CalculateProductionManDays(definition, effectiveParameters, weight);
        var engineCoreDiameterMeters = CalculateEngineCoreDiameterMeters(definition.EngineType, effectiveParameters);
        var engineLengthMeters = CalculateEngineLengthMeters(definition.EngineType, effectiveParameters, engineCoreDiameterMeters);
        var engineFrontalAreaSquareMeters = CalculateEngineFrontalAreaSquareMeters(effectiveParameters, engineCoreDiameterMeters);

        return new EnginePerformanceReport(
            outputKind,
            altitudePerformance,
            reliability,
            weight,
            cost,
            productionMd,
            engineLengthMeters,
            engineFrontalAreaSquareMeters);
    }

    private static IReadOnlyList<EngineAltitudePerformance> BuildAltitudePerformance(
        EngineDefinition definition,
        EngineParameters parameters,
        IAtmosphereModel atmosphereModel,
        EngineOutputKind outputKind,
        double seaLevelOutput,
        double fuelReferenceSeaLevelOutput,
        HistoricalCampaignTechnologyModifiers technologyModifiers)
    {
        var isJet = IsJetFamily(definition.EngineType);
        var points = new EngineAltitudePerformance[EvaluationAltitudes.Length];
        var effectiveCoolingEfficiency = isJet
            ? parameters.CoolingEfficiency
            : GetCoolingEfficiencyWithOptimizations(parameters);
        var efficiencyFuelSpecificFactor = isJet
            ? 1d
            : GetEfficiencyFuelSpecificConsumptionFactor(parameters);
        var compressionFuelFactor = isJet
            ? 1d + (System.Math.Max(0d, parameters.CompressionRatio - JetCompressionMin) * 0.015d)
            : 1d + (System.Math.Max(0d, parameters.CompressionRatio - PistonCompressionMin) * 0.03d);
        var speedFuelFactor = isJet
            ? Numeric.Clamp(System.Math.Pow(parameters.MaximumSpeed / 50000d, 0.24d), 0.75d, 1.65d)
            : Numeric.Clamp(System.Math.Pow(parameters.MaximumSpeed / 2500d, 0.2d), 0.8d, 1.55d);
        var boostFuelFactor = GetBoostFuelFactor(parameters.BoostType);
        var coolingFuelFactor = 1d + ((1d - effectiveCoolingEfficiency) * 0.1d);
        var jetAfterburnerFuelFactor = outputKind == EngineOutputKind.ThrustNewtons
            ? GetJetAfterburnerFuelFlowFactor(parameters)
            : 1d;

        for (var index = 0; index < EvaluationAltitudes.Length; index++)
        {
            var altitude = EvaluationAltitudes[index];
            var densityRatio = atmosphereModel.GetDensityRatioAtAltitude(altitude);
            var optimizationFactor = isJet
                ? 1d
                : CalculateOptimizationFactor(altitude, parameters.OptimizationAltitudeMeters, parameters.BoostType);
            var outputScale = isJet
                ? CalculateJetAltitudeOutputScale(densityRatio, definition.EngineType, parameters)
                : densityRatio;
            var boostAltitudeFactor = isJet
                ? 1d
                : CalculatePistonBoostAltitudeFactor(altitude, parameters.OptimizationAltitudeMeters, parameters.BoostType);
            var outputAtAltitude = seaLevelOutput * outputScale * optimizationFactor * boostAltitudeFactor;
            var fuelReferenceOutputAtAltitude = fuelReferenceSeaLevelOutput * outputScale * optimizationFactor * boostAltitudeFactor;
            var thrustAtAltitude = outputKind == EngineOutputKind.ThrustNewtons
                ? outputAtAltitude
                : CalculatePistonThrustNewtons(outputAtAltitude, parameters);

            double fuelPerHour;
            if (outputKind == EngineOutputKind.PowerKilowatts)
            {
                // Piston output is in kW, so prop fuel flow uses kg / (kW * h).
                fuelPerHour = fuelReferenceOutputAtAltitude
                    * definition.PropFuelConsumptionPerKilowattHour
                    * compressionFuelFactor
                    * boostFuelFactor
                    * coolingFuelFactor
                    * speedFuelFactor
                    * (1d + (System.Math.Max(0d, parameters.CylinderCount - 12) * 0.006d));
            }
            else
            {
                var bypassFuelFactor = definition.EngineType == EngineType.Turbofan
                    ? Numeric.Clamp(1.15d - (parameters.BypassRatio * 0.009d), 0.7d, 1.18d)
                    : 1d;
                var turbojetFuelFactor = definition.EngineType == EngineType.Turbojet
                    ? EngineDesignerBalance.Engine.TurbojetFuelConsumptionMultiplier
                    : 1d;
                var thrustKiloNewtons = fuelReferenceOutputAtAltitude / 1000d;
                // Jet output is in kN, so jet fuel flow uses kg / (kN * h).
                fuelPerHour = thrustKiloNewtons
                    * definition.JetFuelConsumptionPerKiloNewtonHour
                    * compressionFuelFactor
                    * boostFuelFactor
                    * coolingFuelFactor
                    * bypassFuelFactor
                    * turbojetFuelFactor
                    * speedFuelFactor;
            }

            fuelPerHour *= jetAfterburnerFuelFactor;
            fuelPerHour *= efficiencyFuelSpecificFactor;
            fuelPerHour /= technologyModifiers.FuelEfficiencyMultiplier;
            points[index] = new EngineAltitudePerformance(altitude, outputAtAltitude, fuelPerHour, thrustAtAltitude);
        }

        return Array.AsReadOnly(points);
    }
    private static double CalculatePistonThrustNewtons(double outputKilowatts, EngineParameters parameters)
    {
        var referenceSpeed = Numeric.Clamp(
            EngineDesignerBalance.Engine.PistonThrustReferenceSpeedBaseMetersPerSecond
            + (System.Math.Pow(System.Math.Max(outputKilowatts, 1d), EngineDesignerBalance.Engine.PistonThrustReferenceSpeedPowerExponent)
               * EngineDesignerBalance.Engine.PistonThrustReferenceSpeedPowerScale)
            + (System.Math.Sqrt(System.Math.Max(parameters.MaximumSpeed, 1d))
               * EngineDesignerBalance.Engine.PistonThrustReferenceSpeedRpmScale),
            EngineDesignerBalance.Engine.PistonThrustReferenceSpeedMinMetersPerSecond,
            EngineDesignerBalance.Engine.PistonThrustReferenceSpeedMaxMetersPerSecond);
        var thrust = (outputKilowatts * 1000d * PlaneDesignerBalance.Performance.PropulsivePowerEfficiency)
            / System.Math.Max(referenceSpeed, Numeric.Epsilon);
        return System.Math.Max(0d, thrust);
    }

    private static double CalculateSeaLevelPower(EngineDefinition definition, EngineParameters parameters)
    {
        // Piston family: output scales with displacement and volumetric efficiency.
        var effectiveCoolingEfficiency = GetCoolingEfficiencyWithOptimizations(parameters);
        var outputRetentionFactor = GetEfficiencyPowerRetentionFactor(parameters);
        var compressionPowerFactor = 1d + (System.Math.Max(0d, parameters.CompressionRatio - PistonCompressionMin) * 0.032d);
        var coolingPowerFactor = 0.9d + (effectiveCoolingEfficiency * 0.2d);
        var boostPowerFactor = GetBoostPowerFactor(parameters.BoostType);
        var arrangementFactor = definition.EngineType switch
        {
            EngineType.Inline => 1d,
            EngineType.V => 1.08d,
            EngineType.Radial => 1.05d,
            EngineType.Piston => 0.97d,
            _ => 1d,
        };
        var pistonCylinderMaxForType = definition.EngineType == EngineType.Inline
            ? EngineDesignerBalance.Engine.InlineCylinderMax
            : PistonCylinderMax;
        var cylinderFactor = Numeric.Clamp(0.68d + (((double)parameters.CylinderCount - PistonCylinderMin) / (pistonCylinderMaxForType - PistonCylinderMin) * 0.62d), 0.6d, 1.35d);
        var speedFactor = Numeric.Clamp(System.Math.Pow(parameters.MaximumSpeed / 2500d, 0.55d), 0.55d, 1.65d);
        var injectionPowerFactor = parameters.PistonInjectionType == PistonInjectionType.Carburetor
            ? EngineDesignerBalance.Engine.CarburetorPowerFactor
            : 1d;
        var volumetricEfficiency = Numeric.Clamp(
            definition.BaseVolumetricEfficiency * compressionPowerFactor * coolingPowerFactor,
            0.52d,
            1.55d);

        return parameters.DisplacementLiters
            * definition.BasePowerPerLiterKilowatts
            * volumetricEfficiency
            * boostPowerFactor
            * arrangementFactor
            * cylinderFactor
            * speedFactor
            * injectionPowerFactor
            * outputRetentionFactor;
    }
    private static double CalculateSeaLevelThrust(EngineDefinition definition, EngineParameters parameters)
    {
        // Jet family: thrust scales with turbine frontal area (diameter^2).
        var turbineAreaFactor = parameters.TurbineDiameterMeters * parameters.TurbineDiameterMeters;
        var bypassModifier = definition.EngineType == EngineType.Turbofan
            ? 1d + (parameters.BypassRatio * 0.03d)
            : 1d;
        var coolingFactor = 0.9d + (parameters.CoolingEfficiency * 0.16d);
        var compressionFactor = 1d + (System.Math.Max(0d, parameters.CompressionRatio - JetCompressionMin) * 0.012d);
        var speedFactor = Numeric.Clamp(System.Math.Pow(parameters.MaximumSpeed / 50000d, 0.4d), 0.6d, 1.9d);
        var turbineTypeFactor = definition.EngineType switch
        {
            EngineType.Turbojet => 1.12d,
            EngineType.Turbofan => 1d,
            EngineType.Jet => 1.06d,
            _ => 1d,
        };
        var boostFactor = GetJetBoostFactor(parameters.BoostType);

        var baseThrust = definition.BaseJetThrustPerSquareMeterNewtons
            * turbineAreaFactor
            * bypassModifier
            * coolingFactor
            * compressionFactor
            * speedFactor
            * turbineTypeFactor
            * boostFactor;

        return definition.EngineType == EngineType.Turbofan
            ? ApplyLowBypassHighThrustScalingPenalty(baseThrust, parameters.BypassRatio)
            : baseThrust;
    }

    private static double ApplyLowBypassHighThrustScalingPenalty(double thrustNewtons, double bypassRatio)
    {
        if (thrustNewtons <= EngineDesignerBalance.Engine.TurbofanLowBypassPenaltyStartThrustNewtons
            || bypassRatio >= EngineDesignerBalance.Engine.TurbofanLowBypassPenaltyBypassThreshold)
        {
            return thrustNewtons;
        }

        var lowBypassSeverity = Numeric.Clamp(
            (EngineDesignerBalance.Engine.TurbofanLowBypassPenaltyBypassThreshold - bypassRatio)
            / EngineDesignerBalance.Engine.TurbofanLowBypassPenaltyBypassThreshold,
            0d,
            1d);
        var thrustOverloadUnits = System.Math.Max(0d,
            thrustNewtons - EngineDesignerBalance.Engine.TurbofanLowBypassPenaltyStartThrustNewtons) / 100000d;
        var penalty = Numeric.Clamp(
            lowBypassSeverity * thrustOverloadUnits * EngineDesignerBalance.Engine.TurbofanLowBypassPenaltyPer100kNewtons,
            0d,
            EngineDesignerBalance.Engine.TurbofanLowBypassPenaltyMax);

        return thrustNewtons * (1d - penalty);
    }

    private static double CalculateReliability(EngineDefinition definition, EngineParameters parameters)
    {
        var isJet = IsJetFamily(definition.EngineType);
        var effectiveCoolingEfficiency = isJet
            ? parameters.CoolingEfficiency
            : GetCoolingEfficiencyWithOptimizations(parameters);
        var compressionPenalty = isJet
            ? System.Math.Max(0d, parameters.CompressionRatio - JetCompressionMin) * 0.005d
            : System.Math.Max(0d, parameters.CompressionRatio - PistonCompressionMin) * 0.015d;

        var boostPenalty = GetBoostPenalty(parameters.BoostType, isJet);

        var coolingBonus = (effectiveCoolingEfficiency - 0.5d) * 0.14d;
        var bypassPenalty = definition.EngineType == EngineType.Turbofan
            ? System.Math.Max(0d, parameters.BypassRatio - 18d) * 0.002d
            : 0d;
        var speedPenalty = isJet
            ? (System.Math.Max(0d, parameters.MaximumSpeed - 50000d) / 50000d) * 0.075d
            : (System.Math.Max(0d, parameters.MaximumSpeed - 2500d) / 2500d) * 0.06d;
        var cylinderPenalty = isJet ? 0d : System.Math.Max(0d, parameters.CylinderCount - 12) * 0.0035d;

        var complexityPenalty = GetComplexityPenalty(definition.EngineType) * definition.ComplexityFactor;
        var reliability = definition.BaseReliability
            - compressionPenalty
            - boostPenalty
            - bypassPenalty
            - speedPenalty
            - cylinderPenalty
            - complexityPenalty
            + coolingBonus;
        return Numeric.Clamp(reliability, MinimumReliability, MaximumReliability);
    }
    private static Mass CalculateWeight(
        EngineDefinition definition,
        EngineParameters parameters,
        double seaLevelOutput,
        EngineOutputKind outputKind)
    {
        var kilograms = outputKind == EngineOutputKind.ThrustNewtons
            ? CalculateJetWeightKilograms(definition, parameters)
            : CalculatePistonWeightKilograms(definition, parameters, seaLevelOutput);

        return Mass.FromKilograms(System.Math.Max(10d, kilograms));
    }

    private static double CalculateJetWeightKilograms(EngineDefinition definition, EngineParameters parameters)
    {
        var diameter = parameters.TurbineDiameterMeters;
        var turbineAreaFactor = diameter * diameter;
        var compressionFactor = Numeric.Clamp(0.92d + ((parameters.CompressionRatio - JetCompressionMin) * 0.009d), 0.85d, 1.6d);
        var speedFactor = Numeric.Clamp(0.86d + ((parameters.MaximumSpeed / 12000d) * 0.32d), 0.75d, 1.45d);
        var sizeFactor = Numeric.Clamp(System.Math.Pow(System.Math.Max(diameter, 0.1d) / 1.2d, 0.9d), 0.45d, 2.4d);

        double fanBypassFactor;
        double coreScale;
        double bypassHardware;
        double rpmHardware;

        if (definition.EngineType == EngineType.Turbofan)
        {
            var bypass = parameters.BypassRatio;
            fanBypassFactor = 0.58d + (0.03d * System.Math.Sqrt(System.Math.Max(bypass, JetBypassRatioMin)));
            coreScale = 0.23d + (0.42d / (bypass + 0.8d));

            if (bypass < 2d)
            {
                coreScale *= 1d + (((2d - bypass) / 2d) * 0.7d);
            }

            bypassHardware = bypass * 6d;
            var lowBypassRpmFactor = 1d + (1.2d / (bypass + 0.5d));
            rpmHardware = System.Math.Max(0d, parameters.MaximumSpeed - 5000d) * 0.022d * lowBypassRpmFactor;
        }
        else
        {
            fanBypassFactor = 0.52d;
            coreScale = 0.68d;
            bypassHardware = 0d;
            rpmHardware = System.Math.Max(0d, parameters.MaximumSpeed - 5000d) * 0.018d;
        }

        // Split jet mass into fan casing + core hardware so low-bypass high-RPM designs
        // can stay heavy even with smaller fan diameters.
        var fanMass = turbineAreaFactor
            * definition.WeightPerTurbineSquareMeter
            * fanBypassFactor
            * sizeFactor;

        var coreMass = turbineAreaFactor
            * definition.WeightPerTurbineSquareMeter
            * coreScale
            * compressionFactor
            * speedFactor;

        var compressionHardware = System.Math.Max(0d, parameters.CompressionRatio - 25d) * 8d;
        var techFactor = Numeric.Clamp(1.06d - ((parameters.CoolingEfficiency - 0.75d) * 0.45d), 0.8d, 1.12d);

        var weight = definition.BaseWeightKilograms
            + ((fanMass + coreMass + bypassHardware + compressionHardware + rpmHardware) * techFactor);

        if (parameters.HasAfterburner)
        {
            weight *= EngineDesignerBalance.Engine.JetAfterburnerWeightMultiplier;
        }

        return weight;
    }

    private static double CalculatePistonWeightKilograms(
        EngineDefinition definition,
        EngineParameters parameters,
        double seaLevelPowerKilowatts)
    {
        var displacement = System.Math.Max(parameters.DisplacementLiters, 1d);
        var arrangementFactor = definition.EngineType switch
        {
            EngineType.Radial => EngineDesignerBalance.Engine.PistonWeightArrangementFactorRadial,
            EngineType.V => EngineDesignerBalance.Engine.PistonWeightArrangementFactorV,
            EngineType.Inline => EngineDesignerBalance.Engine.PistonWeightArrangementFactorInline,
            EngineType.Piston => EngineDesignerBalance.Engine.PistonWeightArrangementFactorGeneric,
            _ => EngineDesignerBalance.Engine.PistonWeightArrangementFactorInline,
        };

        var baseWeightScale = Numeric.Clamp(0.28d + (displacement / 60d), 0.35d, 1.25d);
        var displacementMass = System.Math.Pow(displacement, 1.24d)
            * definition.WeightPerDisplacementLiter
            * 0.45d
            * arrangementFactor;
        var cylinderMass = parameters.CylinderCount * (definition.EngineType switch
        {
            EngineType.Radial => EngineDesignerBalance.Engine.PistonCylinderMassRadial,
            EngineType.V => EngineDesignerBalance.Engine.PistonCylinderMassV,
            EngineType.Inline => EngineDesignerBalance.Engine.PistonCylinderMassInline,
            EngineType.Piston => EngineDesignerBalance.Engine.PistonCylinderMassGeneric,
            _ => EngineDesignerBalance.Engine.PistonCylinderMassInline,
        });
        var arrangementPackagingMass = displacement * (definition.EngineType switch
        {
            EngineType.Radial => EngineDesignerBalance.Engine.PistonPackagingMassPerLiterRadial,
            EngineType.V => EngineDesignerBalance.Engine.PistonPackagingMassPerLiterV,
            EngineType.Inline => EngineDesignerBalance.Engine.PistonPackagingMassPerLiterInline,
            EngineType.Piston => EngineDesignerBalance.Engine.PistonPackagingMassPerLiterGeneric,
            _ => EngineDesignerBalance.Engine.PistonPackagingMassPerLiterInline,
        });

        var boostMass = parameters.BoostType switch
        {
            BoostType.None => 0d,
            BoostType.Turbo => 18d + (displacement * 1.1d),
            BoostType.Supercharger => 12d + (displacement * 0.8d),
            _ => 0d,
        };

        var speedHardwareMass = System.Math.Max(0d, parameters.MaximumSpeed - 2200d) * 0.008d;
        var compressionHardwareMass = System.Math.Max(0d, parameters.CompressionRatio - 6.5d) * displacement * 0.45d;

        var specificPowerTarget = Numeric.Clamp(
            0.42d
            + (parameters.CompressionRatio * 0.05d)
            + ((parameters.MaximumSpeed / 3000d) * 0.24d)
            + (parameters.CoolingEfficiency * 0.26d)
            + GetPistonSpecificPowerBoost(parameters.BoostType),
            0.4d,
            2.2d);
        var outputMass = (seaLevelPowerKilowatts / specificPowerTarget) * 0.18d;
        var techFactor = Numeric.Clamp(1.08d - ((parameters.CoolingEfficiency - 0.6d) * 0.28d), 0.82d, 1.12d);

        return (definition.BaseWeightKilograms * baseWeightScale)
            + ((displacementMass
                + cylinderMass
                + arrangementPackagingMass
                + boostMass
                + speedHardwareMass
                + compressionHardwareMass
                + outputMass) * techFactor);
    }

    private static double CalculateCost(
        EngineDefinition definition,
        EngineParameters parameters,
        double seaLevelOutput,
        EngineOutputKind outputKind,
        Mass weight)
    {
        var normalizedOutput = outputKind == EngineOutputKind.PowerKilowatts
            ? seaLevelOutput
            : seaLevelOutput / 1000d;
        var isJet = outputKind == EngineOutputKind.ThrustNewtons;
        var boostCost = parameters.BoostType switch
        {
            BoostType.None => 0d,
            BoostType.Turbo => 3500d,
            BoostType.Supercharger => 2800d,
            _ => 0d,
        };
        var speedCost = isJet
            ? System.Math.Max(0d, parameters.MaximumSpeed - 50000d) * 0.09d
            : System.Math.Max(0d, parameters.MaximumSpeed - 2500d) * 2.2d;
        var cylinderCost = isJet ? 0d : parameters.CylinderCount * 120d;
        var bypassCost = definition.EngineType == EngineType.Turbofan ? parameters.BypassRatio * 420d : 0d;
        var injectionCost = !isJet && parameters.PistonInjectionType == PistonInjectionType.Carburetor
            ? -EngineDesignerBalance.Engine.CarburetorCostDiscount
            : 0d;

        var complexityCost = GetComplexityPenalty(definition.EngineType) * 24000d * definition.ComplexityFactor;
        var totalCost = definition.BaseCost
            + (normalizedOutput * definition.CostPerOutputUnit)
            + (weight.Kilograms * 5.5d)
            + speedCost
            + cylinderCost
            + bypassCost
            + boostCost
            + complexityCost
            + injectionCost;
        if (isJet && parameters.HasAfterburner)
        {
            totalCost *= EngineDesignerBalance.Engine.JetAfterburnerCostMultiplier;
        }

        return System.Math.Max(totalCost, definition.BaseCost * 0.2d);
    }

    private static double CalculateProductionManDays(EngineDefinition definition, EngineParameters parameters, Mass weight)
    {
        var isJet = IsJetFamily(definition.EngineType);
        var boostMd = parameters.BoostType switch
        {
            BoostType.None => 0d,
            BoostType.Turbo => 4.5d,
            BoostType.Supercharger => 3.8d,
            _ => 0d,
        };
        var speedMd = isJet
            ? System.Math.Max(0d, parameters.MaximumSpeed - 50000d) * 0.00055d
            : System.Math.Max(0d, parameters.MaximumSpeed - 2500d) * 0.0045d;
        var cylinderMd = isJet ? 0d : parameters.CylinderCount * 0.35d;
        var bypassMd = definition.EngineType == EngineType.Turbofan ? parameters.BypassRatio * 0.55d : 0d;
        var injectionMd = !isJet && parameters.PistonInjectionType == PistonInjectionType.Carburetor
            ? -EngineDesignerBalance.Engine.CarburetorProductionManDayDiscount
            : 0d;
        var complexityMd = GetComplexityPenalty(definition.EngineType) * 65d * definition.ComplexityFactor;
        var productionManDays = definition.BaseProductionManDays + (weight.Kilograms * 0.03d) + boostMd + speedMd + cylinderMd + bypassMd + complexityMd + injectionMd;
        if (isJet && parameters.HasAfterburner)
        {
            productionManDays *= EngineDesignerBalance.Engine.JetAfterburnerProductionManDaysMultiplier;
        }

        return System.Math.Max(productionManDays, definition.BaseProductionManDays * 0.3d);
    }

    private static double CalculateOptimizationFactor(double altitudeMeters, double optimizationAltitudeMeters, BoostType boostType)
    {
        var altitudeDelta = System.Math.Abs(altitudeMeters - optimizationAltitudeMeters);
        var normalizedDelta = Numeric.Clamp(
            altitudeDelta / EngineDesignerBalance.Engine.OptimizationNormalizedDeltaDivisor,
            0d,
            EngineDesignerBalance.Engine.OptimizationNormalizedDeltaMax);

        // Stronger optimization impact for gameplay feedback:
        // near the tuned altitude the engine gains more output, while far from it it loses more.
        var proximityBonus = EngineDesignerBalance.Engine.OptimizationProximityBonusScale
            * (1d - Numeric.Clamp(normalizedDelta, 0d, 1d));
        var mismatchPenalty = normalizedDelta * EngineDesignerBalance.Engine.OptimizationMismatchPenaltyScale;

        // Boost hardware mitigates altitude mismatch penalties.
        var penaltyMultiplier = boostType switch
        {
            BoostType.Turbo => EngineDesignerBalance.Engine.OptimizationTurboPenaltyMultiplier,
            BoostType.Supercharger => EngineDesignerBalance.Engine.OptimizationSuperchargerPenaltyMultiplier,
            _ => 1d,
        };
        var boostMarginBonus = boostType switch
        {
            BoostType.Turbo => EngineDesignerBalance.Engine.OptimizationTurboBonus,
            BoostType.Supercharger => EngineDesignerBalance.Engine.OptimizationSuperchargerBonus,
            _ => 0d,
        };

        var factor = 1d + proximityBonus + boostMarginBonus - (mismatchPenalty * penaltyMultiplier);
        return Numeric.Clamp(
            factor,
            EngineDesignerBalance.Engine.OptimizationFactorMin,
            EngineDesignerBalance.Engine.OptimizationFactorMax);
    }

    private static double CalculatePistonBoostAltitudeFactor(double altitudeMeters, double optimizationAltitudeMeters, BoostType boostType)
    {
        if (boostType == BoostType.None)
        {
            return 1d;
        }

        if (boostType == BoostType.Turbo)
        {
            var highAltitudeBonus = Numeric.Clamp(
                (altitudeMeters - EngineDesignerBalance.Engine.TurboAltitudeBonusStartMeters)
                / EngineDesignerBalance.Engine.TurboAltitudeBonusRangeMeters,
                0d,
                1d) * EngineDesignerBalance.Engine.TurboAltitudeBonusMax;
            var lowAltitudePenalty = Numeric.Clamp(
                (EngineDesignerBalance.Engine.TurboLowAltitudePenaltyCeilingMeters - altitudeMeters)
                / EngineDesignerBalance.Engine.TurboLowAltitudePenaltyCeilingMeters,
                0d,
                1d) * EngineDesignerBalance.Engine.TurboLowAltitudePenaltyMax;
            var optimizationDelta = System.Math.Abs(altitudeMeters - optimizationAltitudeMeters);
            var optimizationSynergy = (1d - Numeric.Clamp(
                    optimizationDelta / EngineDesignerBalance.Engine.TurboOptimizationSynergyAltitudeRangeMeters,
                    0d,
                    1d)) * EngineDesignerBalance.Engine.TurboOptimizationSynergyBonusMax;
            return Numeric.Clamp(
                1d + highAltitudeBonus + optimizationSynergy - lowAltitudePenalty,
                EngineDesignerBalance.Engine.TurboBoostAltitudeFactorMin,
                EngineDesignerBalance.Engine.TurboBoostAltitudeFactorMax);
        }

        var optimizedDelta = System.Math.Abs(altitudeMeters - optimizationAltitudeMeters);
        var optimizedBlend = 1d - Numeric.Clamp(
            optimizedDelta / EngineDesignerBalance.Engine.SuperchargerOptimizedAltitudeRangeMeters,
            0d,
            1d);
        var optimizedBonus = optimizedBlend * EngineDesignerBalance.Engine.SuperchargerOptimizedAltitudeBonusMax;
        var offDesignPenalty = (1d - optimizedBlend) * EngineDesignerBalance.Engine.SuperchargerOffDesignPenaltyMax;

        var optimizationReferenceAltitude = System.Math.Max(optimizationAltitudeMeters, 1000d);
        var altitudeSupportAtOptimized = Numeric.Clamp(
            altitudeMeters / optimizationReferenceAltitude,
            0d,
            1d) * EngineDesignerBalance.Engine.SuperchargerAltitudeSupportAtOptimizedAltitude;
        var extraAltitudeSupport = Numeric.Clamp(
            (altitudeMeters - optimizationReferenceAltitude)
            / EngineDesignerBalance.Engine.SuperchargerOptimizedAltitudeRangeMeters,
            0d,
            1d) * (EngineDesignerBalance.Engine.SuperchargerAltitudeSupportMax
                    - EngineDesignerBalance.Engine.SuperchargerAltitudeSupportAtOptimizedAltitude);

        return Numeric.Clamp(
            1d + optimizedBonus - offDesignPenalty + altitudeSupportAtOptimized + extraAltitudeSupport,
            EngineDesignerBalance.Engine.SuperchargerBoostAltitudeFactorMin,
            EngineDesignerBalance.Engine.SuperchargerBoostAltitudeFactorMax);
    }

    private static double CalculateJetAltitudeOutputScale(
        double densityRatio,
        EngineType engineType,
        EngineParameters parameters)
    {
        // Jets retain thrust with altitude better than linear density scaling.
        // Higher bypass/compression/cooling reduce the effective degradation exponent.
        var effectiveBypassRatio = engineType == EngineType.Turbofan ? parameters.BypassRatio : 0d;
        var exponent = EngineDesignerBalance.Engine.JetAltitudeDensityExponentBase
            - (Numeric.Clamp(effectiveBypassRatio, 0d, EngineDesignerBalance.Engine.JetAltitudeBypassExponentMaxBypass)
               * EngineDesignerBalance.Engine.JetAltitudeBypassExponentSlope)
            - ((parameters.CompressionRatio - EngineDesignerBalance.Engine.JetAltitudeCompressionExponentReference)
               * EngineDesignerBalance.Engine.JetAltitudeCompressionExponentSlope)
            - ((parameters.CoolingEfficiency - EngineDesignerBalance.Engine.JetAltitudeCoolingExponentReference)
               * EngineDesignerBalance.Engine.JetAltitudeCoolingExponentSlope);

        exponent += engineType switch
        {
            EngineType.Turbojet => EngineDesignerBalance.Engine.JetAltitudeTurbojetExponentDelta,
            EngineType.Turbofan => EngineDesignerBalance.Engine.JetAltitudeTurbofanExponentDelta,
            _ => 0d,
        };

        exponent = Numeric.Clamp(
            exponent,
            EngineDesignerBalance.Engine.JetAltitudeExponentMin,
            EngineDesignerBalance.Engine.JetAltitudeExponentMax);
        var normalizedDensity = Numeric.Clamp(densityRatio, EngineDesignerBalance.Engine.JetAltitudeDensityRatioMin, 1d);
        return System.Math.Pow(normalizedDensity, exponent);
    }


    private static double GetEfficiencyOptimizationNormalized(EngineParameters parameters)
    {
        var range = EngineDesignerBalance.Engine.EfficiencyOptimizationMaxPercent
            - EngineDesignerBalance.Engine.EfficiencyOptimizationMinPercent;
        if (range <= Numeric.Epsilon)
        {
            return 0d;
        }

        return Numeric.Clamp(
            (parameters.EfficiencyOptimizationsPercent - EngineDesignerBalance.Engine.EfficiencyOptimizationMinPercent) / range,
            0d,
            1d);
    }

    private static double GetEfficiencyPowerRetentionFactor(EngineParameters parameters)
    {
        var normalized = GetEfficiencyOptimizationNormalized(parameters);
        return Numeric.Clamp(
            1d - (normalized * EngineDesignerBalance.Engine.EfficiencyOptimizationPowerPenaltyAtMax),
            0.05d,
            1d);
    }

    private static double GetCoolingEfficiencyWithOptimizations(EngineParameters parameters)
    {
        var normalized = GetEfficiencyOptimizationNormalized(parameters);
        return Numeric.Clamp(
            parameters.CoolingEfficiency + (normalized * EngineDesignerBalance.Engine.EfficiencyOptimizationCoolingBonusAtMax),
            0d,
            1d);
    }

    private static double GetEfficiencyFuelSpecificConsumptionFactor(EngineParameters parameters)
    {
        var normalized = GetEfficiencyOptimizationNormalized(parameters);
        return Numeric.Clamp(
            1d - (normalized * EngineDesignerBalance.Engine.EfficiencyOptimizationFuelBurnReductionAtMax),
            0.05d,
            1d);
    }

    private static double ApplyJetAfterburnerOutputFactor(
        double baseSeaLevelOutput,
        EngineOutputKind outputKind,
        EngineParameters parameters)
    {
        if (outputKind != EngineOutputKind.ThrustNewtons)
        {
            return baseSeaLevelOutput;
        }

        var factor = parameters.HasAfterburner
            ? EngineDesignerBalance.Engine.JetAfterburnerEnabledPowerFactor
            : EngineDesignerBalance.Engine.JetAfterburnerDisabledPowerFactor;
        return baseSeaLevelOutput * factor;
    }
    private static double GetJetAfterburnerFuelFlowFactor(EngineParameters parameters)
    {
        return parameters.HasAfterburner
            ? EngineDesignerBalance.Engine.JetAfterburnerEnabledPowerFactor
            : EngineDesignerBalance.Engine.JetAfterburnerDisabledPowerFactor;
    }

    private static double GetBoostPowerFactor(BoostType boostType) =>
        boostType switch
        {
            BoostType.None => 1d,
            BoostType.Turbo => EngineDesignerBalance.Engine.TurboBoostPowerFactor,
            BoostType.Supercharger => EngineDesignerBalance.Engine.SuperchargerBoostPowerFactor,
            _ => 1d,
        };

    private static double GetBoostFuelFactor(BoostType boostType) =>
        boostType switch
        {
            BoostType.None => 1d,
            BoostType.Turbo => EngineDesignerBalance.Engine.TurboBoostFuelFactor,
            BoostType.Supercharger => EngineDesignerBalance.Engine.SuperchargerBoostFuelFactor,
            _ => 1d,
        };

    private static double GetPistonSpecificPowerBoost(BoostType boostType) =>
        boostType switch
        {
            BoostType.None => 0d,
            BoostType.Turbo => 0.24d,
            BoostType.Supercharger => 0.17d,
            _ => 0d,
        };

    private static double GetJetBoostFactor(BoostType boostType) =>
        boostType switch
        {
            BoostType.None => 1d,
            BoostType.Turbo => EngineDesignerBalance.Engine.TurboBoostJetFactor,
            BoostType.Supercharger => EngineDesignerBalance.Engine.SuperchargerBoostJetFactor,
            _ => 1d,
        };

    private static double GetBoostPenalty(BoostType boostType, bool isJet) =>
        boostType switch
        {
            BoostType.None => 0d,
            BoostType.Turbo => isJet
                ? EngineDesignerBalance.Engine.TurboBoostJetReliabilityPenalty
                : EngineDesignerBalance.Engine.TurboBoostPistonReliabilityPenalty,
            BoostType.Supercharger => isJet
                ? EngineDesignerBalance.Engine.SuperchargerBoostJetReliabilityPenalty
                : EngineDesignerBalance.Engine.SuperchargerBoostPistonReliabilityPenalty,
            _ => 0d,
        };

    private static double CalculateEngineCoreDiameterMeters(EngineType engineType, EngineParameters parameters)
    {
        if (IsJetFamily(engineType))
        {
            return Numeric.Clamp(
                parameters.TurbineDiameterMeters,
                EngineDesignerBalance.Engine.JetTurbineDiameterMinMeters,
                EngineDesignerBalance.Engine.JetTurbineDiameterMaxMeters);
        }

        var arrangementDiameterFactor = GetPistonCoreDiameterArrangementFactor(engineType);
        var pistonDerivedDiameterMeters = (EngineDesignerBalance.Engine.PistonDerivedDiameterBaseMeters
            + (System.Math.Sqrt(System.Math.Max(parameters.DisplacementLiters, 0d)) * EngineDesignerBalance.Engine.PistonDerivedDiameterPerLiter)
            + (parameters.CylinderCount * EngineDesignerBalance.Engine.PistonDerivedDiameterCylinderFactor))
            * arrangementDiameterFactor;
        return Numeric.Clamp(
            pistonDerivedDiameterMeters,
            EngineDesignerBalance.Engine.PistonDerivedDiameterMinMeters,
            EngineDesignerBalance.Engine.PistonDerivedDiameterMaxMeters);
    }

    private static double CalculateEngineLengthMeters(EngineType engineType, EngineParameters parameters, double coreDiameterMeters)
    {
        var isJet = IsJetFamily(engineType);
        var lengthMeters = EngineDesignerBalance.Engine.EngineLengthBaseMeters
            + (coreDiameterMeters * EngineDesignerBalance.Engine.EngineLengthDiameterScale)
            + (parameters.CompressionRatio * EngineDesignerBalance.Engine.EngineLengthCompressionScale);
        if (!isJet)
        {
            lengthMeters += parameters.DisplacementLiters * EngineDesignerBalance.Engine.EngineLengthPistonDisplacementScale;
        }

        return Numeric.Clamp(
            lengthMeters,
            EngineDesignerBalance.Engine.EngineLengthMinMeters,
            EngineDesignerBalance.Engine.EngineLengthMaxMeters);
    }

    private static double CalculateEngineFrontalAreaSquareMeters(EngineParameters parameters, double coreDiameterMeters)
    {
        var baseArea = System.Math.PI * coreDiameterMeters * coreDiameterMeters * 0.25d;
        var compressionFactor = 1d + (parameters.CompressionRatio * EngineDesignerBalance.Engine.EngineFrontalAreaCompressionScale);
        var coolingFactor = 1d - ((parameters.CoolingEfficiency - 0.5d) * EngineDesignerBalance.Engine.EngineFrontalAreaCoolingScale);
        var boostAreaFactor = GetBoostFrontalAreaFactor(parameters.BoostType);
        var frontalArea = baseArea * compressionFactor * coolingFactor * boostAreaFactor;
        return Numeric.Clamp(
            frontalArea,
            EngineDesignerBalance.Engine.EngineFrontalAreaMinSquareMeters,
            EngineDesignerBalance.Engine.EngineFrontalAreaMaxSquareMeters);
    }

    private static double GetPistonCoreDiameterArrangementFactor(EngineType engineType) =>
        engineType switch
        {
            EngineType.Inline => EngineDesignerBalance.Engine.PistonCoreDiameterInlineFactor,
            EngineType.V => EngineDesignerBalance.Engine.PistonCoreDiameterVFactor,
            EngineType.Radial => EngineDesignerBalance.Engine.PistonCoreDiameterRadialFactor,
            EngineType.Piston => EngineDesignerBalance.Engine.PistonCoreDiameterGenericFactor,
            _ => EngineDesignerBalance.Engine.PistonCoreDiameterInlineFactor,
        };

    private static double GetBoostFrontalAreaFactor(BoostType boostType) =>
        boostType switch
        {
            BoostType.Turbo => 1d + EngineDesignerBalance.Engine.EngineFrontalAreaTurboBoostFactor,
            BoostType.Supercharger => 1d + EngineDesignerBalance.Engine.EngineFrontalAreaSuperchargerBoostFactor,
            _ => 1d,
        };

    private static bool IsJetFamily(EngineType engineType) =>
        engineType is EngineType.Jet or EngineType.Turbojet or EngineType.Turbofan;

    private static bool IsPistonFamily(EngineType engineType) =>
        !IsJetFamily(engineType);

    private static double GetComplexityPenalty(EngineType engineType) =>
        engineType switch
        {
            EngineType.Piston => EngineDesignerBalance.Engine.ComplexityPenaltyPiston,
            EngineType.Inline => EngineDesignerBalance.Engine.ComplexityPenaltyInline,
            EngineType.V => EngineDesignerBalance.Engine.ComplexityPenaltyV,
            EngineType.Radial => EngineDesignerBalance.Engine.ComplexityPenaltyRadial,
            EngineType.Jet => EngineDesignerBalance.Engine.ComplexityPenaltyJet,
            EngineType.Turbojet => EngineDesignerBalance.Engine.ComplexityPenaltyTurbojet,
            EngineType.Turbofan => EngineDesignerBalance.Engine.ComplexityPenaltyTurbofan,
            _ => EngineDesignerBalance.Engine.ComplexityPenaltyV,
        };

    private static EngineParameters NormalizeParametersForEngineType(EngineType engineType, EngineParameters parameters)
    {
        if (engineType == EngineType.Turbofan)
        {
            return parameters;
        }

        var normalized = parameters.Clone();
        normalized.BypassRatio = 0d;
        return normalized;
    }

    private static void ValidateContextualInputs(EngineType engineType, EngineParameters parameters)
    {
        if (IsPistonFamily(engineType))
        {
            var pistonCylinderMax = engineType == EngineType.Inline
                ? EngineDesignerBalance.Engine.InlineCylinderMax
                : PistonCylinderMax;
            ThrowOutOfRangeIfOutside(parameters.CylinderCount, PistonCylinderMin, pistonCylinderMax, nameof(parameters.CylinderCount));
            ThrowOutOfRangeIfOutside(parameters.CompressionRatio, PistonCompressionMin, PistonCompressionMax, nameof(parameters.CompressionRatio));
            ThrowOutOfRangeIfOutside(parameters.OptimizationAltitudeMeters, PistonOptimizationMin, PistonOptimizationMax, nameof(parameters.OptimizationAltitudeMeters));
            ThrowOutOfRangeIfOutside(parameters.MaximumSpeed, PistonSpeedMin, PistonSpeedMax, nameof(parameters.MaximumSpeed));
            return;
        }

        ThrowOutOfRangeIfOutside(parameters.CompressionRatio, JetCompressionMin, JetCompressionMax, nameof(parameters.CompressionRatio));
        ThrowOutOfRangeIfOutside(parameters.TurbineDiameterMeters, JetTurbineDiameterMinMeters, JetTurbineDiameterMaxMeters, nameof(parameters.TurbineDiameterMeters));
        if (engineType == EngineType.Turbofan)
        {
            ThrowOutOfRangeIfOutside(parameters.BypassRatio, JetBypassRatioMin, JetBypassRatioMax, nameof(parameters.BypassRatio));
        }
        else
        {
            Numeric.ThrowIfNegative(parameters.BypassRatio, nameof(parameters.BypassRatio));
        }

        ThrowOutOfRangeIfOutside(parameters.MaximumSpeed, JetSpeedMin, JetSpeedMax, nameof(parameters.MaximumSpeed));
    }

    private static void ThrowOutOfRangeIfOutside(double value, double minInclusive, double maxInclusive, string parameterName)
    {
        if (value < minInclusive || value > maxInclusive)
        {
            throw new ArgumentOutOfRangeException(parameterName, $"Value must be in range [{minInclusive}, {maxInclusive}].");
        }
    }
}
