using System;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Campaign;
using WrightTycoon.Core.Math;
using WrightTycoon.Core.Physics;
using WrightTycoon.Core.Units;

namespace WrightTycoon.Core.Airframes;

public sealed class AirframeCalculator : IAirframeCalculator
{
    private readonly IAerodynamicsCalculator _aerodynamicsCalculator;
    private readonly IAtmosphereModel _atmosphereModel;

    public AirframeCalculator(IAerodynamicsCalculator aerodynamicsCalculator, IAtmosphereModel atmosphereModel)
    {
        _aerodynamicsCalculator = aerodynamicsCalculator ?? throw new ArgumentNullException(nameof(aerodynamicsCalculator));
        _atmosphereModel = atmosphereModel ?? throw new ArgumentNullException(nameof(atmosphereModel));
    }

    public AirframePerformanceReport Calculate(
        AirframeDefinition definition,
        AirframeParameters parameters,
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

        definition.Validate();
        parameters.Validate();
        var effectiveTechnologyModifiers = HistoricalCampaignTechnologyModifiers.Sanitize(technologyModifiers);

        var materialCostFactor = parameters.MaterialType switch
        {
            MaterialType.WoodFabric => 0.65d,
            MaterialType.Aluminum => 1d,
            MaterialType.Composite => 1.46d,
            _ => 1d,
        };

        var materialReliability = parameters.MaterialType switch
        {
            MaterialType.WoodFabric => 0.93d,
            MaterialType.Aluminum => 1d,
            MaterialType.Composite => 1.08d,
            _ => 1d,
        };

        var structureWeightFactor = parameters.StructureType switch
        {
            StructureType.Truss => 1.12d,
            StructureType.Monocoque => 0.94d,
            _ => 1d,
        };

        var structureCostFactor = parameters.StructureType switch
        {
            StructureType.Truss => 0.75d,
            StructureType.Monocoque => 1.18d,
            _ => 1d,
        };

        var structureMdFactor = parameters.StructureType switch
        {
            StructureType.Truss => 0.92d,
            StructureType.Monocoque => 1.16d,
            _ => 1d,
        };

        var structureReliability = parameters.StructureType switch
        {
            StructureType.Truss => 0.95d,
            StructureType.Monocoque => 1.05d,
            _ => 1d,
        };

        var referenceAreaRatio = definition.ReferenceWingAreaSquareMeters > Numeric.Epsilon
            ? parameters.WingAreaSquareMeters / definition.ReferenceWingAreaSquareMeters
            : 1d;

        var structuralAreaScale = Numeric.Clamp(
            System.Math.Pow(
                System.Math.Max(referenceAreaRatio, PlaneDesignerBalance.Airframe.StructuralAreaScaleMinRatio),
                PlaneDesignerBalance.Airframe.StructuralAreaScaleExponent),
            PlaneDesignerBalance.Airframe.StructuralAreaScaleMin,
            PlaneDesignerBalance.Airframe.StructuralAreaScaleMax);
        var structuralMass = definition.BaseStructuralWeightKilograms * structuralAreaScale;

        var fuelMass = parameters.FuelAmountLiters * PlaneDesignerBalance.Performance.FuelDensityKilogramsPerLiter;
        var crewMass = parameters.CrewCount * 88d;
        var installationMass = parameters.EngineCount * (42d + (parameters.WingAreaSquareMeters * 0.012d));
        var wingMass = System.Math.Pow(parameters.WingAreaSquareMeters, PlaneDesignerBalance.Airframe.WingMassExponent)
            * PlaneDesignerBalance.Airframe.WingMassCoefficient;
        // Larger planforms need disproportionately more spar/rib reinforcement.
        var wingReinforcementMass = definition.BaseStructuralWeightKilograms
            * PlaneDesignerBalance.Airframe.WingReinforcementMassBaseShare
            * System.Math.Pow(
                System.Math.Max(referenceAreaRatio, PlaneDesignerBalance.Airframe.StructuralAreaScaleMinRatio),
                PlaneDesignerBalance.Airframe.WingReinforcementMassExponent);
        var wingSystemsMass = parameters.WingAreaSquareMeters
            * (PlaneDesignerBalance.Airframe.WingSystemsMassBasePerSquareMeter
               + (parameters.EngineCount * PlaneDesignerBalance.Airframe.WingSystemsMassPerEnginePerSquareMeter));
        var fuelTankAndSystemMass = (fuelMass * 0.11d) + (System.Math.Sqrt(System.Math.Max(fuelMass, 0d)) * 7.5d);
        var fuelLoadSupportMass = fuelMass * 0.34d;
        var systemsMass = (parameters.EngineCount * 52d) + (parameters.CrewCount * 26d);

        var wingPositionMassFactor = parameters.WingPosition switch
        {
            WingPosition.Low => PlaneDesignerBalance.Airframe.WingPositionMassFactorLow,
            WingPosition.High => PlaneDesignerBalance.Airframe.WingPositionMassFactorHigh,
            WingPosition.Biplane => PlaneDesignerBalance.Airframe.WingPositionMassFactorBiplane,
            _ => PlaneDesignerBalance.Airframe.WingPositionMassFactorLow,
        };

        var preMaterialWeightKg = (structuralMass
                                   + wingMass
                                   + wingReinforcementMass
                                   + wingSystemsMass
                                   + fuelMass
                                   + fuelTankAndSystemMass
                                   + fuelLoadSupportMass
                                   + crewMass
                                   + installationMass
                                   + systemsMass)
                                  * structureWeightFactor
                                  * wingPositionMassFactor;
        var materialWeightFactor = ResolveMaterialWeightFactor(parameters.MaterialType, preMaterialWeightKg);
        var totalWeightKg = preMaterialWeightKg * materialWeightFactor;
        if (parameters.HasBackupSystems)
        {
            totalWeightKg *= PlaneDesignerBalance.Airframe.BackupSystemsWeightMultiplier;
        }

        if (parameters.WingProfile == WingProfileType.Naca4DigitLight)
        {
            totalWeightKg *= 1d - PlaneDesignerBalance.Airframe.Naca4DigitLightProfileWeightReductionRatio;
        }

        totalWeightKg = ApplySmallWoodTrussWeightModifier(totalWeightKg, parameters);
        totalWeightKg /= effectiveTechnologyModifiers.AirframeEfficiencyMultiplier;
        var totalWeight = Mass.FromKilograms(totalWeightKg);

        var aerodynamicCleanlinessPercent = parameters.AerodynamicCleanlinessPercent;
        var aerodynamicCleanlinessRatio = Numeric.Clamp(
            aerodynamicCleanlinessPercent / PlaneDesignerBalance.Airframe.AerodynamicCleanlinessMaxPercent,
            0d,
            1d);
        var aerodynamicCleanlinessDragFactor = 1d - (aerodynamicCleanlinessPercent / 100d);

        var baseLengthMeters = PlaneDesignerBalance.Airframe.AircraftLengthBaseMeters
            + (System.Math.Pow(System.Math.Max(parameters.WingAreaSquareMeters, 1d), PlaneDesignerBalance.Airframe.AircraftLengthWingAreaExponent)
               * PlaneDesignerBalance.Airframe.AircraftLengthWingAreaScale)
            + (System.Math.Pow(System.Math.Max(totalWeightKg / 1000d, 0.1d), PlaneDesignerBalance.Airframe.AircraftLengthWeightExponent)
               * PlaneDesignerBalance.Airframe.AircraftLengthWeightScale);
        var wingStretchRatio = System.Math.Max(
            parameters.WingAreaSquareMeters / PlaneDesignerBalance.Airframe.AircraftLengthWingStretchReferenceArea,
            0.05d);
        var wingStretchBonus = System.Math.Min(
            PlaneDesignerBalance.Airframe.AircraftLengthWingStretchMaxBonus,
            System.Math.Pow(wingStretchRatio, PlaneDesignerBalance.Airframe.AircraftLengthWingStretchExponent)
            * PlaneDesignerBalance.Airframe.AircraftLengthWingStretchScale);
        var weightStretchRatio = System.Math.Max(
            totalWeightKg / PlaneDesignerBalance.Airframe.AircraftLengthWeightStretchReferenceKilograms,
            0.05d);
        var weightStretchBonus = System.Math.Min(
            PlaneDesignerBalance.Airframe.AircraftLengthWeightStretchMaxBonus,
            System.Math.Pow(weightStretchRatio, PlaneDesignerBalance.Airframe.AircraftLengthWeightStretchExponent)
            * PlaneDesignerBalance.Airframe.AircraftLengthWeightStretchScale);
        var aircraftLengthMeters = Numeric.Clamp(
            baseLengthMeters * (1d + wingStretchBonus + weightStretchBonus),
            PlaneDesignerBalance.Airframe.AircraftLengthMinMeters,
            PlaneDesignerBalance.Airframe.AircraftLengthMaxMeters);

        var airframeFrontalAreaSquareMeters = Numeric.Clamp(
            (aircraftLengthMeters * PlaneDesignerBalance.Airframe.FuselageFrontalAreaPerLength)
            + (System.Math.Sqrt(System.Math.Max(totalWeightKg, 0d)) * PlaneDesignerBalance.Airframe.FuselageFrontalAreaWeightSqrtScale),
            PlaneDesignerBalance.Airframe.FuselageFrontalAreaMinSquareMeters,
            PlaneDesignerBalance.Airframe.FuselageFrontalAreaMaxSquareMeters);

        var wingPositionLiftBonus = parameters.WingPosition switch
        {
            WingPosition.High => 0.08d,
            WingPosition.Low => 0.03d,
            WingPosition.Biplane => 0.30d,
            _ => 0d,
        };

        var wingShapeLiftFactor = parameters.WingShape switch
        {
            WingShape.Straight => 1d,
            WingShape.Swept => 0.93d,
            WingShape.Delta => 1d,
            _ => 1d,
        };

        var wingPositionDragDelta = parameters.WingPosition switch
        {
            WingPosition.Low => PlaneDesignerBalance.Airframe.WingPositionDragDeltaLow,
            WingPosition.High => PlaneDesignerBalance.Airframe.WingPositionDragDeltaHigh,
            WingPosition.Biplane => PlaneDesignerBalance.Airframe.WingPositionDragDeltaBiplane,
            _ => PlaneDesignerBalance.Airframe.WingPositionDragDeltaLow,
        };

        var wingShapeDragDelta = parameters.WingShape switch
        {
            WingShape.Straight => 0.002d,
            WingShape.Swept => -0.003d,
            WingShape.Delta => -0.001d,
            _ => 0d,
        };

        var hasLeadingEdgeFlaps = parameters.HasLeadingEdgeFlaps || parameters.FlapType == FlapType.LeadingEdge;
        var flapLiftBonus = parameters.FlapType switch
        {
            FlapType.None => 0d,
            FlapType.Plain => 0.09d,
            FlapType.Fowler => 0.16d,
            FlapType.LeadingEdge => 0d,
            _ => 0d,
        };

        var flapDragDelta = parameters.FlapType switch
        {
            FlapType.None => 0d,
            FlapType.Plain => 0.003d,
            FlapType.Fowler => 0.005d,
            FlapType.LeadingEdge => 0d,
            _ => 0d,
        };

        if (hasLeadingEdgeFlaps)
        {
            flapLiftBonus += 0.12d;
            flapDragDelta += 0.002d;
        }

        var gearDragDelta = parameters.LandingGearType switch
        {
            LandingGearType.Fixed => 0.025d,
            LandingGearType.SemiRetractable => 0.01d,
            LandingGearType.Retractable => 0.003d,
            _ => 0d,
        };

        var gearCostDelta = parameters.LandingGearType switch
        {
            LandingGearType.Fixed => 900d,
            LandingGearType.SemiRetractable => 3700d,
            LandingGearType.Retractable => 9100d,
            _ => 0d,
        };

        var gearMdDelta = parameters.LandingGearType switch
        {
            LandingGearType.Fixed => 2.5d,
            LandingGearType.SemiRetractable => 5.4d,
            LandingGearType.Retractable => 8.1d,
            _ => 0d,
        };

        var gearReliability = parameters.LandingGearType switch
        {
            LandingGearType.Fixed => 1.03d,
            LandingGearType.SemiRetractable => 1.01d,
            LandingGearType.Retractable => 0.97d,
            _ => 1d,
        };

        var tailDragDelta = parameters.TailType switch
        {
            TailType.Classic => 0d,
            TailType.TTail => -0.001d,
            TailType.VTail => -0.002d,
            TailType.Twin => 0.001d,
            _ => 0d,
        };

        var tailReliability = parameters.TailType switch
        {
            TailType.Classic => 1.02d,
            TailType.TTail => 0.98d,
            TailType.VTail => 0.97d,
            TailType.Twin => 1.01d,
            _ => 1d,
        };

        // Engine cowling slider trades drag versus cooling-driven durability.
        var cowlingDragDelta = (1d - parameters.EngineCowling) * 0.012d;
        var cowlingReliability = 0.9d + ((1d - parameters.EngineCowling) * 0.12d);

        var dragCoefficient = definition.BaseDragCoefficient
            + wingShapeDragDelta
            + wingPositionDragDelta
            + flapDragDelta
            + gearDragDelta
            + tailDragDelta
            + cowlingDragDelta
            + ((System.Math.Sqrt(System.Math.Max(referenceAreaRatio, 0.1d)) - 1d) * 0.0012d);
        dragCoefficient *= aerodynamicCleanlinessDragFactor;
        if (parameters.HasBackupSystems)
        {
            dragCoefficient *= PlaneDesignerBalance.Airframe.BackupSystemsDragMultiplier;
        }

        if (parameters.WingProfile == WingProfileType.Laminar6SeriesAerodynamic)
        {
            var laminarBlend = CalculateLaminarProfileAreaEffectBlend(parameters.WingAreaSquareMeters);
            dragCoefficient *= 1d - (PlaneDesignerBalance.Airframe.LaminarProfileDragReductionMaxRatio * laminarBlend);
        }

        dragCoefficient /= effectiveTechnologyModifiers.AerodynamicsEfficiencyMultiplier;
        dragCoefficient = Numeric.Clamp(dragCoefficient, 0.014d, PlaneDesignerBalance.Performance.MaximumDragCoefficient);
        var effectiveDragAreaSquareMeters = dragCoefficient * airframeFrontalAreaSquareMeters;

        var liftCoefficient = (definition.BaseLiftCoefficient * wingShapeLiftFactor)
            + wingPositionLiftBonus
            + flapLiftBonus
            + ((referenceAreaRatio - 1d) * 0.012d);
        if (parameters.WingShape == WingShape.Delta)
        {
            liftCoefficient *= PlaneDesignerBalance.Airframe.DeltaWingLiftMultiplier;
        }

        if (parameters.WingProfile == WingProfileType.Laminar6SeriesAerodynamic)
        {
            var laminarBlend = CalculateLaminarProfileAreaEffectBlend(parameters.WingAreaSquareMeters);
            liftCoefficient *= 1d + (PlaneDesignerBalance.Airframe.LaminarProfileLiftBonusMaxRatio * laminarBlend);
        }
        else if (parameters.WingProfile == WingProfileType.SymmetricalAcrobatic)
        {
            liftCoefficient *= 1d - PlaneDesignerBalance.Airframe.SymmetricalAcrobaticProfileLiftPenaltyRatio;
        }

        liftCoefficient *= effectiveTechnologyModifiers.AerodynamicsEfficiencyMultiplier;
        liftCoefficient = Numeric.Clamp(liftCoefficient, 0.5d, 1.8d);

        var wingLoading = totalWeightKg / System.Math.Max(parameters.WingAreaSquareMeters, 1d);
        var maneuverability = 92d - (wingLoading * 0.18d);
        maneuverability += parameters.WingPosition switch
        {
            WingPosition.High => 4d,
            WingPosition.Low => 2d,
            WingPosition.Biplane => 8d,
            _ => 0d,
        };
        maneuverability += parameters.FlapType switch
        {
            FlapType.None => -4d,
            FlapType.Plain => 2d,
            FlapType.Fowler => 6d,
            FlapType.LeadingEdge => 0d,
            _ => 0d,
        };
        if (hasLeadingEdgeFlaps)
        {
            maneuverability += 3d;
        }
        maneuverability += parameters.TailType switch
        {
            TailType.Classic => 0d,
            TailType.TTail => 4d,
            TailType.VTail => 5d,
            TailType.Twin => 3d,
            _ => 0d,
        };
        if (parameters.WingProfile == WingProfileType.SymmetricalAcrobatic)
        {
            maneuverability *= 1d + PlaneDesignerBalance.Airframe.SymmetricalAcrobaticProfileManeuverabilityBonusRatio;
        }

        maneuverability = Numeric.Clamp(maneuverability, 1d, 100d);

        var productionCost = (definition.BaseCost * materialCostFactor * structureCostFactor)
            + (parameters.WingAreaSquareMeters * 220d)
            + (parameters.EngineCount * 350d)
            + (parameters.CrewCount * 120d)
            + gearCostDelta
            + (hasLeadingEdgeFlaps ? 2600d : 0d);
        productionCost *= parameters.WingProfile switch
        {
            WingProfileType.Naca4DigitLight => 1d - PlaneDesignerBalance.Airframe.Naca4DigitLightProfileAirframeCostReductionRatio,
            WingProfileType.SymmetricalAcrobatic => 1d + PlaneDesignerBalance.Airframe.SymmetricalAcrobaticProfileCostIncreaseRatio,
            _ => 1d,
        };
        productionCost /= effectiveTechnologyModifiers.AirframeEfficiencyMultiplier;
        productionCost = System.Math.Max(0d, productionCost);

        var productionManDays = (definition.BaseProductionManDays * structureMdFactor)
            + (parameters.WingAreaSquareMeters * 0.55d)
            + (parameters.EngineCount * 1.8d)
            + (parameters.CrewCount * 0.3d)
            + gearMdDelta
            + (hasLeadingEdgeFlaps ? 2.4d : 0d)
            + (aerodynamicCleanlinessPercent * PlaneDesignerBalance.Airframe.AerodynamicCleanlinessManDaysPerPercent)
            + (definition.BaseProductionManDays
               * PlaneDesignerBalance.Airframe.AerodynamicCleanlinessManDaysBaseShare
               * aerodynamicCleanlinessRatio
               * aerodynamicCleanlinessRatio);
        productionManDays /= effectiveTechnologyModifiers.AirframeEfficiencyMultiplier;
        productionManDays = System.Math.Max(0d, productionManDays);

        var reliabilityModifier = definition.BaseReliabilityModifier
            * materialReliability
            * structureReliability
            * tailReliability
            * gearReliability
            * cowlingReliability
            * (hasLeadingEdgeFlaps ? 0.99d : 1d);
        if (parameters.HasBackupSystems)
        {
            reliabilityModifier += PlaneDesignerBalance.Airframe.BackupSystemsReliabilityDelta;
        }

        reliabilityModifier = Numeric.Clamp(reliabilityModifier, 0.65d, 1.2d);

        var seaLevelDensity = _atmosphereModel.GetDensityAtAltitude(0d);
        var referenceSpeed = Speed.FromMetersPerSecond(100d);
        var referenceArea = Area.FromSquareMeters(parameters.WingAreaSquareMeters);
        var referenceFrontalArea = Area.FromSquareMeters(airframeFrontalAreaSquareMeters);
        var referenceLift = _aerodynamicsCalculator.CalculateLift(seaLevelDensity, referenceSpeed, referenceArea, liftCoefficient);
        var referenceDrag = _aerodynamicsCalculator.CalculateDrag(seaLevelDensity, referenceSpeed, referenceFrontalArea, dragCoefficient);

        return new AirframePerformanceReport(
            totalWeight,
            dragCoefficient,
            liftCoefficient,
            maneuverability,
            productionCost,
            productionManDays,
            reliabilityModifier,
            referenceLift,
            referenceDrag,
            aircraftLengthMeters,
            airframeFrontalAreaSquareMeters,
            effectiveDragAreaSquareMeters,
            aerodynamicCleanlinessPercent);
    }

    private static double CalculateLaminarProfileAreaEffectBlend(double wingAreaSquareMeters)
    {
        var fullEffectArea = PlaneDesignerBalance.Airframe.LaminarProfileFullEffectMaxWingAreaSquareMeters;
        var noEffectArea = PlaneDesignerBalance.Airframe.LaminarProfileZeroEffectMinWingAreaSquareMeters;
        if (wingAreaSquareMeters <= fullEffectArea)
        {
            return 1d;
        }

        if (wingAreaSquareMeters >= noEffectArea)
        {
            return 0d;
        }

        var range = System.Math.Max(noEffectArea - fullEffectArea, Numeric.Epsilon);
        var blendToZero = Numeric.Clamp((wingAreaSquareMeters - fullEffectArea) / range, 0d, 1d);
        return 1d - blendToZero;
    }

    private static double ResolveMaterialWeightFactor(MaterialType materialType, double preMaterialWeightKilograms)
    {
        return materialType switch
        {
            MaterialType.Aluminum => 1d,
            MaterialType.Composite => 0.72d,
            MaterialType.WoodFabric => ResolveWoodFabricWeightFactor(preMaterialWeightKilograms),
            _ => 1d,
        };
    }

    private static double ResolveWoodFabricWeightFactor(double preMaterialWeightKilograms)
    {
        var threshold = PlaneDesignerBalance.Airframe.WoodFabricEqualWeightThresholdKilograms;
        if (preMaterialWeightKilograms <= threshold)
        {
            return 1d;
        }

        var heavyRange = System.Math.Max(
            PlaneDesignerBalance.Airframe.WoodFabricHeavyWeightReferenceKilograms - threshold,
            Numeric.Epsilon);
        var heavyBlend = Numeric.Clamp((preMaterialWeightKilograms - threshold) / heavyRange, 0d, 1d);
        return 1d + ((PlaneDesignerBalance.Airframe.WoodFabricHeavyWeightFactor - 1d) * heavyBlend);
    }

    private static double ApplySmallWoodTrussWeightModifier(double totalWeightKilograms, AirframeParameters parameters)
    {
        if (parameters.MaterialType != MaterialType.WoodFabric || parameters.StructureType != StructureType.Truss)
        {
            return totalWeightKilograms;
        }

        var threshold = PlaneDesignerBalance.Airframe.SmallWoodTrussWeightReductionThresholdKilograms;
        if (threshold <= Numeric.Epsilon
            || totalWeightKilograms >= threshold
            || totalWeightKilograms <= PlaneDesignerBalance.Airframe.WoodFabricEqualWeightThresholdKilograms)
        {
            return totalWeightKilograms;
        }

        var normalizedSmallAircraft = Numeric.Clamp((threshold - totalWeightKilograms) / threshold, 0d, 1d);
        var reductionRatio = normalizedSmallAircraft * PlaneDesignerBalance.Airframe.SmallWoodTrussMaxWeightReductionRatio;
        return totalWeightKilograms * (1d - Numeric.Clamp(reductionRatio, 0d, 0.95d));
    }
}

