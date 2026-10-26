using System;
using System.Collections.Generic;
using System.Linq;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Campaign;
using WrightTycoon.Core.Math;
using WrightTycoon.Core.Physics;
using WrightTycoon.Core.Production;
using WrightTycoon.Core.Units;
using WrightTycoon.Core.Fleet;

namespace WrightTycoon.Core.Engines;

public sealed class EngineCalculator : IEngineCalculator
{
    private const double MinimumReliability = EngineDesignerBalance.Engine.MinimumReliability;
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
        var precisionEffects = PrecisionEngineeringBalance.GetEffects(effectiveParameters.PrecisionEngineeringLevel);

        var isJet = IsJetFamily(definition.EngineType);
        var outputKind = isJet ? EngineOutputKind.ThrustNewtons : EngineOutputKind.PowerKilowatts;
        var baseSeaLevelOutput = outputKind == EngineOutputKind.PowerKilowatts
            ? CalculateSeaLevelPower(definition, effectiveParameters)
            : CalculateSeaLevelThrust(definition, effectiveParameters);
        var enginePerformanceMultiplier = isJet
            ? effectiveTechnologyModifiers.EnginePerformanceMultiplier
            : effectiveTechnologyModifiers.PistonEnginePerformanceMultiplier;
        baseSeaLevelOutput *= enginePerformanceMultiplier;
        var fuelReferenceSeaLevelOutput = baseSeaLevelOutput;
        baseSeaLevelOutput *= precisionEffects.OutputMultiplier;
        var seaLevelOutput = ApplyJetAfterburnerOutputFactor(baseSeaLevelOutput, outputKind, effectiveParameters);

        var altitudePerformance = BuildAltitudePerformance(
            definition,
            effectiveParameters,
            atmosphereModel,
            outputKind,
            seaLevelOutput,
            fuelReferenceSeaLevelOutput,
            effectiveTechnologyModifiers);

        var reliability = System.Math.Max(
            MinimumReliability,
            CalculateReliability(definition, effectiveParameters) * precisionEffects.ReliabilityMultiplier);
        var unadjustedWeight = CalculateWeight(definition, effectiveParameters, seaLevelOutput, outputKind);
        var referenceWeight = Mass.FromKilograms(unadjustedWeight.Kilograms * precisionEffects.WeightMultiplier);
        var weight = Mass.FromKilograms(referenceWeight.Kilograms
            * (1d + effectiveParameters.DurabilityLevel * EngineMaintenancePolicy.DurabilityMassPerLevel));
        var costBreakdown = CalculateCostBreakdown(
            definition,
            effectiveParameters,
            seaLevelOutput,
            outputKind,
            referenceWeight,
            precisionEffects);
        var productionManDaysBreakdown = CalculateProductionManDaysBreakdown(
            definition,
            effectiveParameters,
            referenceWeight,
            precisionEffects);
        if (effectiveParameters.DurabilityLevel > 0)
        {
            costBreakdown = new ProductionBreakdown(costBreakdown.Items.Concat(new[]
            {
                new ProductionBreakdownItem("Engine durability", costBreakdown.Total
                    * effectiveParameters.DurabilityLevel * EngineMaintenancePolicy.DurabilityProductionPerLevel),
            }));
            productionManDaysBreakdown = new ProductionBreakdown(productionManDaysBreakdown.Items.Concat(new[]
            {
                new ProductionBreakdownItem("Engine durability", productionManDaysBreakdown.Total
                    * effectiveParameters.DurabilityLevel * EngineMaintenancePolicy.DurabilityProductionPerLevel),
            }));
        }
        var cost = costBreakdown.Total;
        var productionMd = productionManDaysBreakdown.Total;
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
            engineFrontalAreaSquareMeters,
            costBreakdown,
            productionManDaysBreakdown);
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
            : GetPistonCompressionFuelSpecificConsumptionFactor(parameters);
        var speedFuelFactor = isJet
            ? Numeric.Clamp(System.Math.Pow(parameters.MaximumSpeed / 50000d, 0.24d), 0.75d, 1.65d)
            : Numeric.Clamp(System.Math.Pow(parameters.MaximumSpeed / 2500d, 0.2d), 0.8d, 1.55d);
        var boostFuelFactor = GetBoostFuelFactor(parameters.BoostType);
        var injectionFuelFactor = !isJet && parameters.PistonInjectionType == PistonInjectionType.DirectInjection
            ? EngineDesignerBalance.Engine.DirectInjectionFuelConsumptionFactor
            : 1d;
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
                    * injectionFuelFactor
                    * coolingFuelFactor
                    * speedFuelFactor
                    * (1d + (System.Math.Max(0d, parameters.CylinderCount - 12) * 0.006d));
            }
            else
            {
                var bypassFuelFactor = definition.EngineType == EngineType.Turbofan
                    ? CalculateTurbofanBypassSpecificFuelFactor(parameters.BypassRatio)
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
            var fuelEfficiencyMultiplier = isJet
                ? technologyModifiers.FuelEfficiencyMultiplier
                : technologyModifiers.PistonFuelEfficiencyMultiplier;
            fuelPerHour /= fuelEfficiencyMultiplier;
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
        var compressionPowerFactor = GetPistonCompressionPowerFactor(parameters);
        // Better cooling hardware adds thermal headroom reliability, but costs output.
        var coolingPowerFactor = 1.1d - (effectiveCoolingEfficiency * 0.2d);
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
            EngineDesignerBalance.Engine.PistonVolumetricEfficiencyMin,
            EngineDesignerBalance.Engine.PistonVolumetricEfficiencyMax);

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
            ? CalculateTurbofanBypassThrustFactor(parameters.BypassRatio)
            : 1d;
        // Better cooling hardware adds drag/mass complexity and trims peak thrust.
        var coolingFactor = 1.06d - (parameters.CoolingEfficiency * 0.16d);
        var compressionFactor = 1d + (System.Math.Max(0d, parameters.CompressionRatio - JetCompressionMin) * 0.012d);
        var speedFactor = Numeric.Clamp(System.Math.Pow(parameters.MaximumSpeed / 50000d, 0.4d), 0.6d, 1.9d);
        var turbineTypeFactor = definition.EngineType switch
        {
            EngineType.Turbojet => 1.12d,
            EngineType.Turbofan => 1d,
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

    private static double CalculateTurbofanBypassThrustFactor(double bypassRatio) =>
        1d + (InterpolateLogBypassCorrectionPercent(
            bypassRatio,
            EngineDesignerBalance.Engine.TurbofanBypassThrustCorrectionPercent) / 100d);

    private static double CalculateTurbofanBypassSpecificFuelFactor(double bypassRatio) =>
        1d + (InterpolateLogBypassCorrectionPercent(
            bypassRatio,
            EngineDesignerBalance.Engine.TurbofanBypassSfcCorrectionPercent) / 100d);

    private static double InterpolateLogBypassCorrectionPercent(double bypassRatio, IReadOnlyList<double> correctionPercent)
    {
        var nodes = EngineDesignerBalance.Engine.TurbofanBypassRatioCorrectionNodes;
        if (correctionPercent.Count != nodes.Length)
        {
            throw new InvalidOperationException("Turbofan bypass correction table must match the bypass node count.");
        }

        if (bypassRatio <= nodes[0])
        {
            return correctionPercent[0];
        }

        var lastIndex = nodes.Length - 1;
        if (bypassRatio >= nodes[lastIndex])
        {
            return correctionPercent[lastIndex];
        }

        for (var index = 0; index < lastIndex; index++)
        {
            var lowerNode = nodes[index];
            var upperNode = nodes[index + 1];
            if (bypassRatio > upperNode)
            {
                continue;
            }

            var t = (System.Math.Log(bypassRatio) - System.Math.Log(lowerNode))
                / (System.Math.Log(upperNode) - System.Math.Log(lowerNode));
            return correctionPercent[index] + (t * (correctionPercent[index + 1] - correctionPercent[index]));
        }

        return correctionPercent[lastIndex];
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
        return System.Math.Max(MinimumReliability, reliability);
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
            bypassHardware = (bypass * 6d) + (System.Math.Max(0d, 1d - bypass) * 20d);
            var highBypassRpmFactor = 1.65d + (0.15d * bypass);
            rpmHardware = System.Math.Max(0d, parameters.MaximumSpeed - 5000d) * 0.022d * highBypassRpmFactor;
        }
        else
        {
            fanBypassFactor = 0.52d;
            coreScale = 0.68d;
            bypassHardware = 0d;
            rpmHardware = System.Math.Max(0d, parameters.MaximumSpeed - 5000d) * 0.018d;
        }

        // Split jet mass into fan casing + core hardware. Turbofan spool and fan
        // hardware now become heavier as bypass ratio rises at otherwise fixed inputs.
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
        var techFactor = Numeric.Clamp(1.06d + ((parameters.CoolingEfficiency - 0.75d) * 0.45d), 0.8d, 1.22d);

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
            + ((1d - parameters.CoolingEfficiency) * 0.26d)
            + GetPistonSpecificPowerBoost(parameters.BoostType),
            0.4d,
            2.2d);
        var outputMass = (seaLevelPowerKilowatts / specificPowerTarget) * 0.18d;
        var techFactor = Numeric.Clamp(1.08d + ((parameters.CoolingEfficiency - 0.6d) * 0.28d), 0.82d, 1.22d);

        return (definition.BaseWeightKilograms * baseWeightScale)
            + ((displacementMass
                + cylinderMass
                + arrangementPackagingMass
                + boostMass
                + speedHardwareMass
                + compressionHardwareMass
                + outputMass) * techFactor);
    }

    private static ProductionBreakdown CalculateCostBreakdown(
        EngineDefinition definition,
        EngineParameters parameters,
        double seaLevelOutput,
        EngineOutputKind outputKind,
        Mass weight,
        PrecisionEngineeringEffects precisionEffects)
    {
        var isJet = outputKind == EngineOutputKind.ThrustNewtons;
        var items = new List<ProductionBreakdownItem>();
        double totalCost;
        if (isJet)
        {
            var referenceParts = definition.EngineType == EngineType.Turbofan
                ? ProductionEconomyBalance.Engine.TurbofanReferenceParts
                : ProductionEconomyBalance.Engine.TurbojetReferenceParts;
            var diameterRatio = System.Math.Max(parameters.TurbineDiameterMeters, Numeric.Epsilon)
                / ProductionEconomyBalance.Engine.JetReferenceDiameterMeters;
            totalCost = referenceParts * System.Math.Pow(diameterRatio, ProductionEconomyBalance.Engine.JetSizeExponent);
            items.Add(new ProductionBreakdownItem("Turbine circumference and architecture", totalCost));
            AddMultiplierAdjustment(
                items,
                "Compressor pressure ratio",
                ref totalCost,
                ProductionEconomyBalance.GetJetCompressionPartsMultiplier(parameters.CompressionRatio));
            if (definition.EngineType == EngineType.Turbofan)
            {
                var pricedBypass = Numeric.Clamp(
                    parameters.BypassRatio,
                    0d,
                    ProductionEconomyBalance.Engine.BypassPricingCap);
                AddMultiplierAdjustment(
                    items,
                    "Bypass hardware",
                    ref totalCost,
                    1d + (pricedBypass * ProductionEconomyBalance.Engine.BypassPartsIncreasePerPoint));
            }
        }
        else
        {
            var cylinders = System.Math.Max(1, parameters.CylinderCount);
            var cylinderVolume = System.Math.Max(parameters.DisplacementLiters / cylinders, Numeric.Epsilon);
            var cylinderScale = System.Math.Pow(
                cylinderVolume / ProductionEconomyBalance.Engine.PistonReferenceCylinderVolumeLiters,
                ProductionEconomyBalance.Engine.PistonCylinderPartsExponent);
            var crankcaseParts = ProductionEconomyBalance.Engine.PistonBaseParts;
            var cylinderParts = cylinders * ProductionEconomyBalance.Engine.PistonCylinderParts * cylinderScale;
            var displacementParts = parameters.DisplacementLiters * ProductionEconomyBalance.Engine.PistonDisplacementPartsPerLiter;
            items.Add(new ProductionBreakdownItem("Crankcase", crankcaseParts));
            items.Add(new ProductionBreakdownItem("Cylinders and cylinder volume", cylinderParts));
            items.Add(new ProductionBreakdownItem("Displacement hardware", displacementParts));
            totalCost = crankcaseParts + cylinderParts + displacementParts;
            AddMultiplierAdjustment(
                items,
                $"Engine architecture ({definition.EngineType})",
                ref totalCost,
                ProductionEconomyBalance.GetEngineArchitecturePartsMultiplier(definition.EngineType));
            AddMultiplierAdjustment(
                items,
                "Compression ratio",
                ref totalCost,
                ProductionEconomyBalance.GetPistonCompressionPartsMultiplier(parameters.CompressionRatio));
            if (parameters.PistonInjectionType == PistonInjectionType.DirectInjection)
            {
                AddMultiplierAdjustment(
                    items,
                    "Direct injection",
                    ref totalCost,
                    ProductionEconomyBalance.Engine.DirectInjectionPartsMultiplier);
            }
        }

        AddEngineProductionOptionMultipliers(items, ref totalCost, parameters, isJet, parts: true);
        if (isJet && parameters.HasAfterburner)
        {
            AddMultiplierAdjustment(
                items,
                "Afterburner",
                ref totalCost,
                ProductionEconomyBalance.Engine.AfterburnerPartsMultiplier);
        }

        AddMultiplierAdjustment(
            items,
            $"Precision engineering (level {precisionEffects.Level})",
            ref totalCost,
            precisionEffects.ProductionCostMultiplier);

        AddMinimumAdjustment(items, "Minimum engine cost", ref totalCost, 1d);
        return new ProductionBreakdown(items);
    }

    private static ProductionBreakdown CalculateProductionManDaysBreakdown(
        EngineDefinition definition,
        EngineParameters parameters,
        Mass weight,
        PrecisionEngineeringEffects precisionEffects)
    {
        var isJet = IsJetFamily(definition.EngineType);
        var items = new List<ProductionBreakdownItem>();
        double productionManDays;
        if (isJet)
        {
            var referenceManDays = definition.EngineType == EngineType.Turbofan
                ? ProductionEconomyBalance.Engine.TurbofanReferenceManDays
                : ProductionEconomyBalance.Engine.TurbojetReferenceManDays;
            var diameterRatio = System.Math.Max(parameters.TurbineDiameterMeters, Numeric.Epsilon)
                / ProductionEconomyBalance.Engine.JetReferenceDiameterMeters;
            productionManDays = referenceManDays * System.Math.Pow(diameterRatio, ProductionEconomyBalance.Engine.JetSizeExponent);
            items.Add(new ProductionBreakdownItem("Turbine circumference and architecture", productionManDays));
            AddMultiplierAdjustment(
                items,
                "Compressor pressure ratio",
                ref productionManDays,
                ProductionEconomyBalance.GetJetCompressionManDaysMultiplier(parameters.CompressionRatio));
            if (definition.EngineType == EngineType.Turbofan)
            {
                var pricedBypass = Numeric.Clamp(
                    parameters.BypassRatio,
                    0d,
                    ProductionEconomyBalance.Engine.BypassPricingCap);
                AddMultiplierAdjustment(
                    items,
                    "Bypass hardware",
                    ref productionManDays,
                    1d + (pricedBypass * ProductionEconomyBalance.Engine.BypassManDaysIncreasePerPoint));
            }
        }
        else
        {
            var cylinders = System.Math.Max(1, parameters.CylinderCount);
            var cylinderVolume = System.Math.Max(parameters.DisplacementLiters / cylinders, Numeric.Epsilon);
            var cylinderScale = System.Math.Pow(
                cylinderVolume / ProductionEconomyBalance.Engine.PistonReferenceCylinderVolumeLiters,
                ProductionEconomyBalance.Engine.PistonCylinderManDaysExponent);
            var baseManDays = ProductionEconomyBalance.Engine.PistonBaseManDays;
            var cylinderManDays = cylinders * ProductionEconomyBalance.Engine.PistonCylinderManDays * cylinderScale;
            var displacementManDays = parameters.DisplacementLiters * ProductionEconomyBalance.Engine.PistonDisplacementManDaysPerLiter;
            items.Add(new ProductionBreakdownItem("Base assembly", baseManDays));
            items.Add(new ProductionBreakdownItem("Cylinders and cylinder volume", cylinderManDays));
            items.Add(new ProductionBreakdownItem("Displacement assembly", displacementManDays));
            productionManDays = baseManDays + cylinderManDays + displacementManDays;
            AddMultiplierAdjustment(
                items,
                $"Engine architecture ({definition.EngineType})",
                ref productionManDays,
                ProductionEconomyBalance.GetEngineArchitectureManDaysMultiplier(definition.EngineType));
            AddMultiplierAdjustment(
                items,
                "Compression ratio",
                ref productionManDays,
                ProductionEconomyBalance.GetPistonCompressionManDaysMultiplier(parameters.CompressionRatio));
            if (parameters.PistonInjectionType == PistonInjectionType.DirectInjection)
            {
                AddMultiplierAdjustment(
                    items,
                    "Direct injection",
                    ref productionManDays,
                    ProductionEconomyBalance.Engine.DirectInjectionManDaysMultiplier);
            }
        }

        AddEngineProductionOptionMultipliers(items, ref productionManDays, parameters, isJet, parts: false);
        if (isJet && parameters.HasAfterburner)
        {
            AddMultiplierAdjustment(
                items,
                "Afterburner",
                ref productionManDays,
                ProductionEconomyBalance.Engine.AfterburnerManDaysMultiplier);
        }

        AddMultiplierAdjustment(
            items,
            $"Precision engineering (level {precisionEffects.Level})",
            ref productionManDays,
            precisionEffects.ProductionManDaysMultiplier);

        AddMinimumAdjustment(
            items,
            "Minimum engine production time",
            ref productionManDays,
            1d);
        return new ProductionBreakdown(items);
    }

    private static void AddEngineProductionOptionMultipliers(
        List<ProductionBreakdownItem> items,
        ref double total,
        EngineParameters parameters,
        bool isJet,
        bool parts)
    {
        var boostMultiplier = parameters.BoostType switch
        {
            BoostType.Turbo => parts
                ? ProductionEconomyBalance.Engine.TurboPartsMultiplier
                : ProductionEconomyBalance.Engine.TurboManDaysMultiplier,
            BoostType.Supercharger => parts
                ? ProductionEconomyBalance.Engine.SuperchargerPartsMultiplier
                : ProductionEconomyBalance.Engine.SuperchargerManDaysMultiplier,
            _ => 1d,
        };
        AddMultiplierAdjustment(items, "Boost system", ref total, boostMultiplier);

        var referenceRpm = isJet
            ? ProductionEconomyBalance.Engine.JetReferenceRpm
            : ProductionEconomyBalance.Engine.PistonReferenceRpm;
        var rpmSteps = System.Math.Max(0d, (parameters.MaximumSpeed / referenceRpm) - 1d)
            / ProductionEconomyBalance.Engine.RpmStepRatio;
        var rpmRate = parts
            ? ProductionEconomyBalance.Engine.RpmPartsIncreasePerStep
            : ProductionEconomyBalance.Engine.RpmManDaysIncreasePerStep;
        AddMultiplierAdjustment(items, "High-speed hardware", ref total, 1d + (rpmSteps * rpmRate));

        var coolingSteps = System.Math.Max(0d, parameters.CoolingEfficiency - ProductionEconomyBalance.Engine.CoolingReference)
            / ProductionEconomyBalance.Engine.CoolingStep;
        var coolingRate = parts
            ? ProductionEconomyBalance.Engine.CoolingPartsIncreasePerStep
            : ProductionEconomyBalance.Engine.CoolingManDaysIncreasePerStep;
        AddMultiplierAdjustment(items, "Cooling system", ref total, 1d + (coolingSteps * coolingRate));

        var altitudeSteps = System.Math.Max(0d, parameters.OptimizationAltitudeMeters - ProductionEconomyBalance.Engine.AltitudeReferenceMeters)
            / ProductionEconomyBalance.Engine.AltitudeStepMeters;
        var altitudeRate = parts
            ? ProductionEconomyBalance.Engine.AltitudePartsIncreasePerStep
            : ProductionEconomyBalance.Engine.AltitudeManDaysIncreasePerStep;
        AddMultiplierAdjustment(items, "Altitude optimization", ref total, 1d + (altitudeSteps * altitudeRate));

        var optimizationRatio = Numeric.Clamp(
            parameters.EfficiencyOptimizationsPercent / ProductionEconomyBalance.Engine.EfficiencyOptimizationMaximumPercent,
            0d,
            1d);
        var optimizationRate = parts
            ? ProductionEconomyBalance.Engine.EfficiencyOptimizationMaximumPartsIncrease
            : ProductionEconomyBalance.Engine.EfficiencyOptimizationMaximumManDaysIncrease;
        AddMultiplierAdjustment(items, "Efficiency optimization", ref total, 1d + (optimizationRatio * optimizationRate));
    }

    private static void AddMultiplierAdjustment(
        List<ProductionBreakdownItem> items,
        string label,
        ref double total,
        double multiplier)
    {
        var adjustment = total * (multiplier - 1d);
        items.Add(new ProductionBreakdownItem(label, adjustment));
        total += adjustment;
    }

    private static void AddMinimumAdjustment(
        List<ProductionBreakdownItem> items,
        string label,
        ref double total,
        double minimum)
    {
        if (total >= minimum)
        {
            return;
        }

        items.Add(new ProductionBreakdownItem(label, minimum - total));
        total = minimum;
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
        // Compression/cooling reduce the effective degradation exponent; BPR is already handled by the calibrated
        // turbofan bypass correction tables, so it is intentionally not counted again here.
        var exponent = EngineDesignerBalance.Engine.JetAltitudeDensityExponentBase
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

    private static double GetPistonCompressionNormalized(EngineParameters parameters)
    {
        var range = PistonCompressionMax - PistonCompressionMin;
        if (range <= Numeric.Epsilon)
        {
            return 0d;
        }

        return Numeric.Clamp(
            (parameters.CompressionRatio - PistonCompressionMin) / range,
            0d,
            1d);
    }

    private static double GetPistonCompressionPowerFactor(EngineParameters parameters)
    {
        var normalized = GetPistonCompressionNormalized(parameters);
        return 1d + (normalized * (EngineDesignerBalance.Engine.PistonCompressionPowerFactorAtMax - 1d));
    }

    private static double GetPistonCompressionFuelSpecificConsumptionFactor(EngineParameters parameters)
    {
        var normalized = GetPistonCompressionNormalized(parameters);
        return 1d - (normalized * (1d - EngineDesignerBalance.Engine.PistonCompressionFuelSpecificConsumptionFactorAtMax));
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
        var coolingFactor = 1d + ((parameters.CoolingEfficiency - 0.5d) * EngineDesignerBalance.Engine.EngineFrontalAreaCoolingScale);
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
        engineType is EngineType.Turbojet or EngineType.Turbofan;

    private static bool IsPistonFamily(EngineType engineType) =>
        !IsJetFamily(engineType);

    private static double GetComplexityPenalty(EngineType engineType) =>
        engineType switch
        {
            EngineType.Piston => EngineDesignerBalance.Engine.ComplexityPenaltyPiston,
            EngineType.Inline => EngineDesignerBalance.Engine.ComplexityPenaltyInline,
            EngineType.V => EngineDesignerBalance.Engine.ComplexityPenaltyV,
            EngineType.Radial => EngineDesignerBalance.Engine.ComplexityPenaltyRadial,
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
