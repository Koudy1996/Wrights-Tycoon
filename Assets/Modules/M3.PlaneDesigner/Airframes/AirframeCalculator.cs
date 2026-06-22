using System;
using System.Collections.Generic;
using System.Linq;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Campaign;
using WrightTycoon.Core.Math;
using WrightTycoon.Core.Physics;
using WrightTycoon.Core.Production;
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
        var constructionStandardEffects = AircraftConstructionStandardBalance.GetEffects(parameters.ConstructionStandard);

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
        var aspectRatioEffects = ResolveAspectRatioEffects(parameters.WingAspectRatio);
        var precisionEffects = PrecisionEngineeringBalance.GetEffects(parameters.PrecisionEngineeringLevel);

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
            * PlaneDesignerBalance.Airframe.WingMassCoefficient
            * aspectRatioEffects.WingMassMultiplier;
        // Larger planforms need disproportionately more spar/rib reinforcement.
        var wingReinforcementMass = definition.BaseStructuralWeightKilograms
            * PlaneDesignerBalance.Airframe.WingReinforcementMassBaseShare
            * System.Math.Pow(
                System.Math.Max(referenceAreaRatio, PlaneDesignerBalance.Airframe.StructuralAreaScaleMinRatio),
                PlaneDesignerBalance.Airframe.WingReinforcementMassExponent)
            * aspectRatioEffects.WingMassMultiplier;
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

        var precisionEligibleMass = structuralMass
            + wingMass
            + wingReinforcementMass
            + wingSystemsMass
            + fuelTankAndSystemMass
            + fuelLoadSupportMass
            + installationMass
            + systemsMass;
        var preMaterialWeightKg = ((precisionEligibleMass * precisionEffects.WeightMultiplier)
                                   + fuelMass
                                   + crewMass)
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
        totalWeightKg *= constructionStandardEffects.WeightMultiplier;
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
        dragCoefficient *= aspectRatioEffects.DragMultiplier;
        dragCoefficient *= constructionStandardEffects.DragMultiplier;
        dragCoefficient *= aerodynamicCleanlinessDragFactor;
        dragCoefficient *= precisionEffects.DragMultiplier;
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

        maneuverability *= aspectRatioEffects.ManeuverabilityMultiplier;
        maneuverability *= constructionStandardEffects.ManeuverabilityMultiplier;
        maneuverability = Numeric.Clamp(maneuverability, 1d, 100d);

        var pricingPayloadKilograms = parameters.PayloadLimitKilograms
            ?? System.Math.Max(
                ProductionEconomyBalance.Airframe.DefaultPayloadKilograms,
                definition.BaseStructuralWeightKilograms * ProductionEconomyBalance.Airframe.BaseWeightPayloadFactor);
        var structuralIndex = ProductionEconomyBalance.CalculateStructuralIndex(
            parameters.WingAreaSquareMeters,
            pricingPayloadKilograms,
            parameters.FuelAmountLiters);
        var baseProductionCost = ProductionEconomyBalance.CalculateAirframeBaseParts(structuralIndex);
        var productionCostItems = new List<ProductionBreakdownItem>
        {
            new("Physical size (wing, payload and fuel capacity)", baseProductionCost),
        };
        var productionCost = baseProductionCost;
        AddMultiplierAdjustment(
            productionCostItems,
            $"Material ({parameters.MaterialType})",
            ref productionCost,
            GetMaterialProductionMultiplier(parameters.MaterialType, parts: true));
        AddMultiplierAdjustment(
            productionCostItems,
            $"Structure ({parameters.StructureType})",
            ref productionCost,
            GetStructureProductionMultiplier(parameters.StructureType, parts: true));
        AddMultiplierAdjustment(
            productionCostItems,
            $"Construction standard ({parameters.ConstructionStandard})",
            ref productionCost,
            constructionStandardEffects.ProductionCostMultiplier);
        var productionCostAddonBase = productionCost;
        AddAirframeProductionAddons(
            productionCostItems,
            ref productionCost,
            productionCostAddonBase,
            parameters,
            aerodynamicCleanlinessRatio,
            parts: true);
        AddMultiplierAdjustment(
            productionCostItems,
            "Airframe technology",
            ref productionCost,
            1d / effectiveTechnologyModifiers.AirframeEfficiencyMultiplier);
        AddMinimumAdjustment(productionCostItems, "Minimum airframe cost", ref productionCost, 0d);
        var productionCostBreakdown = new ProductionBreakdown(productionCostItems);

        var baseProductionManDays = ProductionEconomyBalance.CalculateAirframeBaseManDays(structuralIndex);
        var productionManDaysItems = new List<ProductionBreakdownItem>
        {
            new("Physical size (wing, payload and fuel capacity)", baseProductionManDays),
        };
        var productionManDays = baseProductionManDays;
        AddMultiplierAdjustment(
            productionManDaysItems,
            $"Material ({parameters.MaterialType})",
            ref productionManDays,
            GetMaterialProductionMultiplier(parameters.MaterialType, parts: false));
        AddMultiplierAdjustment(
            productionManDaysItems,
            $"Structure ({parameters.StructureType})",
            ref productionManDays,
            GetStructureProductionMultiplier(parameters.StructureType, parts: false));
        AddMultiplierAdjustment(
            productionManDaysItems,
            $"Construction standard ({parameters.ConstructionStandard})",
            ref productionManDays,
            constructionStandardEffects.ProductionManDaysMultiplier);
        var productionManDaysAddonBase = productionManDays;
        AddAirframeProductionAddons(
            productionManDaysItems,
            ref productionManDays,
            productionManDaysAddonBase,
            parameters,
            aerodynamicCleanlinessRatio,
            parts: false);
        AddMultiplierAdjustment(
            productionManDaysItems,
            "Airframe technology",
            ref productionManDays,
            1d / effectiveTechnologyModifiers.AirframeEfficiencyMultiplier);
        AddMinimumAdjustment(productionManDaysItems, "Minimum airframe production time", ref productionManDays, 0d);
        var productionManDaysBreakdown = new ProductionBreakdown(productionManDaysItems);

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

        reliabilityModifier += constructionStandardEffects.ReliabilityDelta;
        reliabilityModifier *= precisionEffects.ReliabilityMultiplier;
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
            aerodynamicCleanlinessPercent,
            productionCostBreakdown,
            productionManDaysBreakdown);
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

    private static void AddAirframeProductionAddons(
        List<ProductionBreakdownItem> items,
        ref double total,
        double addonBase,
        AirframeParameters parameters,
        double cleanlinessRatio,
        bool parts)
    {
        AddRateAdjustment(items, "Wing shape", ref total, addonBase, parameters.WingShape switch
        {
            WingShape.Swept => parts ? ProductionEconomyBalance.Airframe.SweptWingPartsRate : ProductionEconomyBalance.Airframe.SweptWingManDaysRate,
            WingShape.Delta => parts ? ProductionEconomyBalance.Airframe.DeltaWingPartsRate : ProductionEconomyBalance.Airframe.DeltaWingManDaysRate,
            _ => 0d,
        });
        AddRateAdjustment(items, "Wing position", ref total, addonBase, parameters.WingPosition switch
        {
            WingPosition.High => parts ? ProductionEconomyBalance.Airframe.HighWingPartsRate : ProductionEconomyBalance.Airframe.HighWingManDaysRate,
            WingPosition.Biplane => parts ? ProductionEconomyBalance.Airframe.BiplanePartsRate : ProductionEconomyBalance.Airframe.BiplaneManDaysRate,
            _ => 0d,
        });
        AddRateAdjustment(items, "Wing profile", ref total, addonBase, parameters.WingProfile switch
        {
            WingProfileType.Naca4DigitLight => parts ? ProductionEconomyBalance.Airframe.LightProfilePartsRate : ProductionEconomyBalance.Airframe.LightProfileManDaysRate,
            WingProfileType.Laminar6SeriesAerodynamic => parts ? ProductionEconomyBalance.Airframe.LaminarProfilePartsRate : ProductionEconomyBalance.Airframe.LaminarProfileManDaysRate,
            WingProfileType.SymmetricalAcrobatic => parts ? ProductionEconomyBalance.Airframe.AcrobaticProfilePartsRate : ProductionEconomyBalance.Airframe.AcrobaticProfileManDaysRate,
            _ => 0d,
        });
        AddRateAdjustment(items, "Wing aspect ratio", ref total, addonBase, GetAspectRatioProductionRate(parameters.WingAspectRatio, parts));
        AddRateAdjustment(items, "Tail", ref total, addonBase, parameters.TailType switch
        {
            TailType.TTail => parts ? ProductionEconomyBalance.Airframe.TTailPartsRate : ProductionEconomyBalance.Airframe.TTailManDaysRate,
            TailType.VTail => parts ? ProductionEconomyBalance.Airframe.VTailPartsRate : ProductionEconomyBalance.Airframe.VTailManDaysRate,
            TailType.Twin => parts ? ProductionEconomyBalance.Airframe.TwinTailPartsRate : ProductionEconomyBalance.Airframe.TwinTailManDaysRate,
            _ => 0d,
        });
        AddRateAdjustment(items, "Flaps", ref total, addonBase, parameters.FlapType switch
        {
            FlapType.None => parts ? ProductionEconomyBalance.Airframe.NoFlapsPartsRate : ProductionEconomyBalance.Airframe.NoFlapsManDaysRate,
            FlapType.Fowler => parts ? ProductionEconomyBalance.Airframe.FowlerFlapsPartsRate : ProductionEconomyBalance.Airframe.FowlerFlapsManDaysRate,
            FlapType.LeadingEdge => parts ? ProductionEconomyBalance.Airframe.LeadingEdgeFlapsPartsRate : ProductionEconomyBalance.Airframe.LeadingEdgeFlapsManDaysRate,
            _ => 0d,
        });
        if (parameters.HasLeadingEdgeFlaps && parameters.FlapType != FlapType.LeadingEdge)
        {
            AddRateAdjustment(
                items,
                "Leading-edge flaps",
                ref total,
                addonBase,
                parts
                    ? ProductionEconomyBalance.Airframe.LeadingEdgeFlapsPartsRate
                    : ProductionEconomyBalance.Airframe.LeadingEdgeFlapsManDaysRate);
        }
        AddRateAdjustment(items, "Landing gear", ref total, addonBase, parameters.LandingGearType switch
        {
            LandingGearType.SemiRetractable => parts ? ProductionEconomyBalance.Airframe.SemiRetractableGearPartsRate : ProductionEconomyBalance.Airframe.SemiRetractableGearManDaysRate,
            LandingGearType.Retractable => parts ? ProductionEconomyBalance.Airframe.RetractableGearPartsRate : ProductionEconomyBalance.Airframe.RetractableGearManDaysRate,
            _ => 0d,
        });
        if (parameters.HasBackupSystems)
        {
            AddRateAdjustment(
                items,
                "Backup systems",
                ref total,
                addonBase,
                parts ? ProductionEconomyBalance.Airframe.BackupSystemsPartsRate : ProductionEconomyBalance.Airframe.BackupSystemsManDaysRate);
        }

        AddRateAdjustment(
            items,
            "Aerodynamic cleanliness",
            ref total,
            addonBase,
            cleanlinessRatio * (parts
                ? ProductionEconomyBalance.Airframe.MaximumCleanlinessPartsRate
                : ProductionEconomyBalance.Airframe.MaximumCleanlinessManDaysRate));
        var additionalEngines = System.Math.Max(0, parameters.EngineCount - 1);
        AddRateAdjustment(
            items,
            "Additional engine installations",
            ref total,
            addonBase,
            additionalEngines * (parts
                ? ProductionEconomyBalance.Airframe.AdditionalEngineInstallationPartsRate
                : ProductionEconomyBalance.Airframe.AdditionalEngineInstallationManDaysRate));
    }

    private static void AddRateAdjustment(
        List<ProductionBreakdownItem> items,
        string label,
        ref double total,
        double addonBase,
        double rate)
    {
        if (System.Math.Abs(rate) <= Numeric.Epsilon)
        {
            return;
        }

        var adjustment = addonBase * rate;
        items.Add(new ProductionBreakdownItem(label, adjustment));
        total += adjustment;
    }

    private static double GetAspectRatioProductionRate(double aspectRatio, bool parts)
    {
        if (aspectRatio <= 10d)
        {
            return 0d;
        }

        if (aspectRatio <= 15d)
        {
            return parts ? 0.06d : 0.08d;
        }

        if (aspectRatio <= 25d)
        {
            return parts ? 0.14d : 0.18d;
        }

        if (aspectRatio <= 35d)
        {
            return parts ? 0.24d : 0.30d;
        }

        return parts ? 0.40d : 0.50d;
    }

    private static double GetMaterialProductionMultiplier(MaterialType material, bool parts) => material switch
    {
        MaterialType.WoodFabric => parts ? ProductionEconomyBalance.Airframe.WoodPartsMultiplier : ProductionEconomyBalance.Airframe.WoodManDaysMultiplier,
        MaterialType.Composite => parts ? ProductionEconomyBalance.Airframe.CompositePartsMultiplier : ProductionEconomyBalance.Airframe.CompositeManDaysMultiplier,
        _ => 1d,
    };

    private static double GetStructureProductionMultiplier(StructureType structure, bool parts) => structure switch
    {
        StructureType.Truss => parts ? ProductionEconomyBalance.Airframe.TrussPartsMultiplier : ProductionEconomyBalance.Airframe.TrussManDaysMultiplier,
        _ => 1d,
    };

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

    private static AspectRatioEffectPoint ResolveAspectRatioEffects(double aspectRatio)
    {
        var curve = PlaneDesignerBalance.Airframe.WingAspectRatioEffectCurve;
        if (curve.Count == 0)
        {
            throw new InvalidOperationException("Wing aspect ratio effect curve is empty.");
        }

        var clampedAspectRatio = Numeric.Clamp(
            aspectRatio,
            PlaneDesignerBalance.Airframe.WingAspectRatioMin,
            PlaneDesignerBalance.Airframe.WingAspectRatioMax);
        if (clampedAspectRatio <= curve[0].AspectRatio)
        {
            return curve[0];
        }

        var lastIndex = curve.Count - 1;
        if (clampedAspectRatio >= curve[lastIndex].AspectRatio)
        {
            return curve[lastIndex];
        }

        for (var index = 1; index < curve.Count; index++)
        {
            var upper = curve[index];
            if (clampedAspectRatio > upper.AspectRatio)
            {
                continue;
            }

            var lower = curve[index - 1];
            var aspectRatioRange = upper.AspectRatio - lower.AspectRatio;
            if (aspectRatioRange <= Numeric.Epsilon)
            {
                throw new InvalidOperationException($"Wing aspect ratio effect curve has invalid range at index {index}.");
            }

            var blend = (clampedAspectRatio - lower.AspectRatio) / aspectRatioRange;
            return new AspectRatioEffectPoint(
                clampedAspectRatio,
                lower.DragMultiplier + ((upper.DragMultiplier - lower.DragMultiplier) * blend),
                lower.WingMassMultiplier + ((upper.WingMassMultiplier - lower.WingMassMultiplier) * blend),
                lower.ManeuverabilityMultiplier + ((upper.ManeuverabilityMultiplier - lower.ManeuverabilityMultiplier) * blend));
        }

        return curve[lastIndex];
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

