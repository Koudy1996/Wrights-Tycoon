using System;
using System.Collections.Generic;
using System.Globalization;
using WrightTycoon.Core.Aircraft;
using WrightTycoon.Core.Airframes;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Categories;
using WrightTycoon.Core.Engines;
using WrightTycoon.Core.Headquarters;
using WrightTycoon.Core.Production;

namespace WrightTycoon.Game.Launch;

public sealed partial class WrightTycoonLaunchRunner
{
    private static ModifierTooltipDefinition BuildEngineDesignInputTooltip(Engine engine, string metric)
    {
        var parameters = engine.Parameters;
        var lines = new List<string>();
        switch (metric)
        {
            case "turbine-diameter":
                lines.Add($"Direct design input: {FormatUiNumber(parameters.TurbineDiameterMeters * 100d, "0")} cm.");
                lines.Add("Thrust uses turbine diameter squared; a larger fan also raises frontal area, weight, parts and production work.");
                break;
            case "cylinder-count":
                lines.Add($"Direct design input: {FormatUiInteger(parameters.CylinderCount)} cylinders.");
                lines.Add($"Displacement: {FormatUiNumber(parameters.DisplacementLiters, "0.0")} L; architecture: {HumanizeUiToken(engine.Definition.EngineType.ToString())}.");
                lines.Add("More cylinders affect output, fuel burn, reliability, weight, parts and production work.");
                break;
            case "optimization-height":
                lines.Add($"Direct design input: {FormatUiNumber(parameters.OptimizationAltitudeMeters, "0")} m.");
                lines.Add($"Boost system: {HumanizeUiToken(parameters.BoostType.ToString())}.");
                lines.Add("The altitude curve compares this target with each evaluated altitude; it changes piston output and fuel burn away from sea level.");
                break;
        }

        return BuildTechnicalTooltip($"designer.engine.input.{metric}", "How This Design Input Is Used", lines);
    }

    private static ModifierTooltipDefinition BuildEngineAltitudeTooltip(
        Engine engine,
        EngineAltitudePerformance point,
        double fuelPerOutput,
        string fuelMetricLabel)
    {
        var parameters = engine.Parameters;
        var isJet = engine.Performance.OutputKind == EngineOutputKind.ThrustNewtons;
        var outputLabel = isJet ? "thrust" : "power";
        var lines = BuildEngineParameterSummary(engine);
        lines.Insert(0, $"Altitude: {FormatUiNumber(point.AltitudeMeters, "0")} m.");
        lines.Add($"Calculated {outputLabel}: {FormatUiNumber(point.Output, "0.0")} {(isJet ? "N" : "kW")}.");
        lines.Add($"Calculated fuel flow: {FormatUiNumber(point.FuelConsumptionKilogramsPerHour, "0.0")} kg/h.");
        var density = isJet ? 0.8d : PlaneDesignerBalance.Performance.FuelDensityKilogramsPerLiter;
        lines.Add(
            $"{fuelMetricLabel} = fuel kg/h / {FormatUiNumber(density, "0.00")} kg/L / {outputLabel} " +
            $"= {FormatUiNumber(fuelPerOutput, "0.0000")}.");
        lines.Add(isJet
            ? "Altitude air density scales thrust; bypass, afterburner and historical technology further shape thrust and fuel burn."
            : $"Air density, the {FormatUiNumber(parameters.OptimizationAltitudeMeters, "0")} m optimization target and boost shape the altitude result.");

        return BuildTechnicalTooltip(
            $"designer.engine.altitude.{point.AltitudeMeters.ToString("0", CultureInfo.InvariantCulture)}",
            $"Engine Performance at {FormatUiNumber(point.AltitudeMeters, "0")} m",
            lines);
    }

    private static ModifierTooltipDefinition BuildEngineReliabilityTooltip(Engine engine)
    {
        var parameters = engine.Parameters;
        var lines = BuildEngineParameterSummary(engine);
        lines.Add("Cooling and precision engineering improve reliability.");
        lines.Add("Compression, boost, high RPM and extra complexity reduce reliability; the result is clamped to the allowed reliability range.");
        lines.Add($"Final engine reliability: {FormatUiNumber(engine.Performance.Reliability, "0.000")}.");
        return BuildTechnicalTooltip("designer.engine.reliability", "Engine Reliability Influences", lines);
    }

    private static ModifierTooltipDefinition BuildEngineWeightTooltip(Engine engine)
    {
        var parameters = engine.Parameters;
        var lines = BuildEngineParameterSummary(engine);
        lines.Add(engine.Performance.OutputKind == EngineOutputKind.ThrustNewtons
            ? "Jet weight combines base mass, diameter-squared hardware, bypass fan/core hardware, compression, RPM, cooling and afterburner."
            : "Piston weight combines base mass, displacement, cylinders, architecture, boost, RPM, compression, output hardware and cooling.");
        lines.Add($"Precision engineering level {FormatUiInteger(parameters.PrecisionEngineeringLevel)} applies the final weight multiplier.");
        lines.Add($"Final engine weight: {FormatUiNumber(engine.Performance.Weight.Kilograms, "0.0")} kg.");
        return BuildTechnicalTooltip("designer.engine.weight", "Engine Weight Influences", lines);
    }

    private ModifierTooltipDefinition BuildEngineEngineeringTimeTooltip(Engine engine, int resultMonths)
    {
        var quote = _researchCalculator.CalculateEngineQuote(engine);
        var lines = BuildEngineParameterSummary(engine);
        lines.Add($"Calculated complexity: {FormatUiNumber(quote.ComplexityNormalized * 100d, "0.0")}%.");
        lines.Add("Complexity combines engine architecture, size, compression, installed options, RPM, efficiency optimization and precision engineering.");
        if (_engineVersionDevelopment is not null)
        {
            lines.Add($"Version development starts at {FormatUiNumber(VersionDevelopmentResearchTimeMultiplier * 100d, "0")}% of full development time, then benefits from technology progress since {_engineVersionDevelopment.SourceDevelopmentStartedYear}.");
        }

        lines.Add($"Final engineering time: {FormatUiInteger(resultMonths)} months.");
        return BuildTechnicalTooltip("designer.engine.engineering-time", "Engine Engineering Time", lines);
    }

    private static ModifierTooltipDefinition BuildEngineCompleteCostTooltip(Engine engine)
    {
        return BuildCompleteProductionCostTooltip(
            "designer.engine.complete-cost",
            "Complete Engine Production Cost",
            engine.Performance.CostBreakdown,
            engine.Performance.Cost,
            engine.Performance.ProductionManDays,
            engine.Performance.CompleteProductionCost,
            "Engine production adjustment");
    }

    private static ModifierTooltipDefinition BuildAircraftEngineOutputTooltip(Aircraft aircraft, double perEngineOutput, double totalOutput)
    {
        var count = Math.Max(aircraft.Airframe.Parameters.EngineCount, 1);
        var unit = aircraft.Engine.Performance.OutputKind == EngineOutputKind.PowerKilowatts ? "kW" : "N";
        return BuildTechnicalTooltip(
            "designer.aircraft.engine-output",
            "Installed Sea-level Engine Output",
            new[]
            {
                $"Engine: {aircraft.Engine.Definition.DisplayName}.",
                $"Per-engine output at 0 m: {FormatUiNumber(perEngineOutput, "0.0")} {unit}.",
                $"Installed engines: {FormatUiInteger(count)}.",
                $"Total output = {FormatUiNumber(perEngineOutput, "0.0")} x {FormatUiInteger(count)} = {FormatUiNumber(totalOutput, "0.0")} {unit}.",
            });
    }

    private static ModifierTooltipDefinition BuildAircraftSpeedTooltip(Aircraft aircraft)
    {
        var parameters = aircraft.Airframe.Parameters;
        var performance = aircraft.Performance;
        var category = AircraftCategoryCatalog.GetProfile(aircraft.FamilyDefinition.Category);
        var navigation = AircraftNavigationEquipmentBalance.GetEffects(aircraft.TypeDefinition.NavigationEquipment);
        var lines = new List<string>
        {
            $"Wing area: {FormatUiNumber(parameters.WingAreaSquareMeters, "0.0")} m2.",
            $"Airframe drag coefficient: {FormatUiNumber(aircraft.Airframe.Performance.DragCoefficient, "0.0000")}.",
            $"Wing: {HumanizeUiToken(parameters.WingPosition.ToString())}, {HumanizeUiToken(parameters.WingShape.ToString())}, {HumanizeUiToken(parameters.WingProfile.ToString())}.",
            $"Navigation drag change: {FormatSignedNumber(navigation.DragCoefficientDelta, "0.0000")} Cd.",
            $"Category speed change: {FormatSignedNumber(category.SpeedModifierMetersPerSecond * 3.6d, "0.0")} km/h.",
            "At each altitude, speed is derived from total engine thrust, air density, wing area and effective drag; delta/profile and jet-envelope rules are then applied.",
        };

        for (var index = 0; index < performance.MaxSpeedByAltitude.Count; index++)
        {
            var speedPoint = performance.MaxSpeedByAltitude[index];
            var thrust = aircraft.Engine.Performance.GetThrustNewtonsAtAltitude(speedPoint.AltitudeMeters)
                * Math.Max(parameters.EngineCount, 1);
            lines.Add(
                $"{FormatUiNumber(speedPoint.AltitudeMeters, "0")} m: {FormatUiNumber(thrust, "0")} N total thrust -> " +
                $"{FormatUiNumber(speedPoint.MaxSpeedMetersPerSecond * 3.6d, "0.0")} km/h.");
        }

        return BuildTechnicalTooltip("designer.aircraft.speed", "Aircraft Speed Influences", lines);
    }

    private static ModifierTooltipDefinition BuildAircraftCertifiedRangeTooltip(Aircraft aircraft)
    {
        var performance = aircraft.Performance;
        var navigation = aircraft.TypeDefinition.NavigationEquipment;
        var fuelMass = aircraft.Airframe.Parameters.FuelAmountLiters * PlaneDesignerBalance.Performance.FuelDensityKilogramsPerLiter;
        var cruiseSpeed = performance.OptimalCruiseSpeedMetersPerSecond * 3.6d;
        var certified = AircraftNavigationRangePolicy.GetCertifiedRangeKilometers(navigation, performance.RangeKilometers);
        var cap = AircraftNavigationRangePolicy.GetMaximumCertifiedRangeKilometers(navigation);
        var capText = double.IsPositiveInfinity(cap) ? "unlimited" : $"{FormatUiNumber(cap, "0")} km";
        return BuildTechnicalTooltip(
            "designer.aircraft.certified-range",
            "Certified Range Calculation",
            new[]
            {
                $"Fuel: {FormatUiNumber(aircraft.Airframe.Parameters.FuelAmountLiters, "0")} L x {FormatUiNumber(PlaneDesignerBalance.Performance.FuelDensityKilogramsPerLiter, "0.00")} kg/L = {FormatUiNumber(fuelMass, "0.0")} kg.",
                $"Cruise burn: {FormatUiNumber(performance.AverageCruiseFuelBurnKilogramsPerHour, "0.0")} kg/h.",
                $"Optimal cruise speed: {FormatUiNumber(cruiseSpeed, "0.0")} km/h.",
                $"Physical range = fuel / burn x speed = {FormatUiNumber(performance.RangeKilometers, "0.0")} km.",
                $"Navigation: {HumanizeUiToken(navigation.ToString())}; certification limit: {capText}.",
                $"Certified range = min(physical range, limit) = {FormatUiNumber(certified, "0.0")} km.",
            });
    }

    private static ModifierTooltipDefinition BuildAircraftCruiseFuelTooltip(Aircraft aircraft)
    {
        var performance = aircraft.Performance;
        var parameters = aircraft.Airframe.Parameters;
        var category = AircraftCategoryCatalog.GetProfile(aircraft.FamilyDefinition.Category);
        var enginePoint = aircraft.Engine.Performance.GetPerformanceAtAltitude(performance.OptimalCruiseAltitudeMeters);
        var fullThrottleBurn = enginePoint.FuelConsumptionKilogramsPerHour
            * Math.Max(parameters.EngineCount, 1)
            * category.FuelConsumptionMultiplier;
        var effectiveLoad = fullThrottleBurn <= 0.0001d
            ? 0d
            : performance.AverageCruiseFuelBurnKilogramsPerHour / fullThrottleBurn;
        return BuildTechnicalTooltip(
            "designer.aircraft.cruise-fuel",
            "Cruise Fuel Burn Calculation",
            new[]
            {
                $"Cruise altitude: {FormatUiNumber(performance.OptimalCruiseAltitudeMeters, "0")} m.",
                $"Per-engine full-throttle burn there: {FormatUiNumber(enginePoint.FuelConsumptionKilogramsPerHour, "0.0")} kg/h.",
                $"Engine count: {FormatUiInteger(Math.Max(parameters.EngineCount, 1))}; category fuel multiplier: x{FormatUiNumber(category.FuelConsumptionMultiplier, "0.000")}.",
                $"Full-throttle installed burn: {FormatUiNumber(fullThrottleBurn, "0.0")} kg/h.",
                $"Required thrust depends on cruise speed, air density, wing area and effective drag; effective load after the minimum-burn rule is {FormatUiNumber(effectiveLoad * 100d, "0.0")}%. ",
                $"Final cruise burn: {FormatUiNumber(performance.AverageCruiseFuelBurnKilogramsPerHour, "0.0")} kg/h.",
            });
    }

    private static ModifierTooltipDefinition BuildAircraftPayloadLimitTooltip(Aircraft aircraft)
    {
        var limit = aircraft.Airframe.Parameters.PayloadLimitKilograms ?? 0d;
        return BuildTechnicalTooltip(
            "designer.aircraft.payload-limit",
            "Payload Limit",
            new[]
            {
                $"Direct type input: {FormatUiNumber(limit, "0.0")} kg.",
                "The limit caps useful weight and sets final takeoff weight to aircraft weight with fuel plus the capped payload.",
            });
    }

    private static ModifierTooltipDefinition BuildAircraftUsefulWeightTooltip(Aircraft aircraft)
    {
        var performance = aircraft.Performance;
        var uncapped = Math.Max(0d, performance.MaximumTakeoffWeightKilograms - performance.AircraftWeightWithFuelKilograms);
        var lines = new List<string>
        {
            $"Maximum takeoff weight: {FormatUiNumber(performance.MaximumTakeoffWeightKilograms, "0.0")} kg.",
            $"Aircraft weight with fuel: {FormatUiNumber(performance.AircraftWeightWithFuelKilograms, "0.0")} kg.",
            $"Available useful weight = MTOW - fueled weight = {FormatUiNumber(uncapped, "0.0")} kg.",
        };
        if (aircraft.Airframe.Parameters.PayloadLimitKilograms.HasValue)
        {
            lines.Add($"Type payload limit: {FormatUiNumber(aircraft.Airframe.Parameters.PayloadLimitKilograms.Value, "0.0")} kg.");
        }

        lines.Add($"Final useful weight: {FormatUiNumber(performance.PayloadCapacity.Kilograms, "0.0")} kg.");
        return BuildTechnicalTooltip("designer.aircraft.useful-weight", "Useful Weight Calculation", lines);
    }

    private static ModifierTooltipDefinition BuildAircraftMaximumTakeoffWeightTooltip(Aircraft aircraft)
    {
        var performance = aircraft.Performance;
        var parameters = aircraft.Airframe.Parameters;
        var runwayLength = ResolveRunwayLengthMeters(parameters.RunwayType);
        var totalSeaLevelThrust = aircraft.Engine.Performance.GetThrustNewtonsAtAltitude(0d) * Math.Max(parameters.EngineCount, 1);
        return BuildTechnicalTooltip(
            "designer.aircraft.mtow",
            "Maximum Takeoff Weight Calculation",
            new[]
            {
                $"Runway: {HumanizeUiToken(parameters.RunwayType.ToString())} ({FormatUiNumber(runwayLength, "0")} m).",
                $"Wing area: {FormatUiNumber(parameters.WingAreaSquareMeters, "0.0")} m2; lift coefficient: {FormatUiNumber(aircraft.Airframe.Performance.LiftCoefficient, "0.000")}.",
                $"Sea-level engine thrust: {FormatUiNumber(totalSeaLevelThrust, "0")} N from {FormatUiInteger(Math.Max(parameters.EngineCount, 1))} engine(s).",
                "MTOW is the square root of runway length x air density x wing area x takeoff lift x total thrust / gravity.",
                $"It cannot fall below fueled aircraft weight ({FormatUiNumber(performance.AircraftWeightWithFuelKilograms, "0.0")} kg), and an enabled payload limit can lower it.",
                $"Final MTOW: {FormatUiNumber(performance.MaximumTakeoffWeightKilograms, "0.0")} kg.",
            });
    }

    private static ModifierTooltipDefinition BuildAircraftWetWeightTooltip(Aircraft aircraft)
    {
        var performance = aircraft.Performance;
        var emptyMargin = performance.EmptyAircraftWeightKilograms * PlaneDesignerBalance.Performance.WetWeightMarginRatio;
        var fuelMass = aircraft.Airframe.Parameters.FuelAmountLiters * PlaneDesignerBalance.Performance.FuelDensityKilogramsPerLiter;
        return BuildTechnicalTooltip(
            "designer.aircraft.wet-weight",
            "Aircraft Weight With Fuel",
            new[]
            {
                $"Empty aircraft: {FormatUiNumber(performance.EmptyAircraftWeightKilograms, "0.0")} kg.",
                $"Wet-weight margin ({FormatUiNumber(PlaneDesignerBalance.Performance.WetWeightMarginRatio * 100d, "0.0")}%): {FormatUiNumber(emptyMargin, "0.0")} kg.",
                $"Fuel: {FormatUiNumber(aircraft.Airframe.Parameters.FuelAmountLiters, "0")} L x {FormatUiNumber(PlaneDesignerBalance.Performance.FuelDensityKilogramsPerLiter, "0.00")} kg/L = {FormatUiNumber(fuelMass, "0.0")} kg.",
                $"Total: {FormatUiNumber(performance.AircraftWeightWithFuelKilograms, "0.0")} kg.",
            });
    }

    private static ModifierTooltipDefinition BuildAircraftEmptyWeightTooltip(Aircraft aircraft)
    {
        var performance = aircraft.Performance;
        var parameters = aircraft.Airframe.Parameters;
        var fuelMass = parameters.FuelAmountLiters * PlaneDesignerBalance.Performance.FuelDensityKilogramsPerLiter;
        var structureWithoutFuel = Math.Max(
            PlaneDesignerBalance.Performance.MinimumEmptyWeightKilograms,
            aircraft.Airframe.Performance.TotalWeight.Kilograms - fuelMass);
        var engines = aircraft.Engine.Performance.Weight.Kilograms * Math.Max(parameters.EngineCount, 1);
        var nominalSystems = PlaneDesignerBalance.Performance.EmptyWeightBaseSystemsKilograms
            + (Math.Max(parameters.EngineCount, 1) * PlaneDesignerBalance.Performance.EmptyWeightPerEngineSystemsKilograms)
            + (parameters.CrewCount * PlaneDesignerBalance.Performance.EmptyWeightPerCrewSystemsKilograms);
        var category = AircraftCategoryCatalog.GetProfile(aircraft.FamilyDefinition.Category);
        var navigation = AircraftNavigationEquipmentBalance.GetEffects(aircraft.TypeDefinition.NavigationEquipment);
        var knownSubtotal = structureWithoutFuel + engines + nominalSystems + category.WeightModifierKilograms + navigation.ExtraWeightKilograms;
        var scalingResidual = performance.EmptyAircraftWeightKilograms - knownSubtotal;
        return BuildTechnicalTooltip(
            "designer.aircraft.empty-weight",
            "Empty Aircraft Weight Breakdown",
            new[]
            {
                $"Airframe structure without carried fuel: {FormatUiNumber(structureWithoutFuel, "0.0")} kg.",
                $"Engines: {FormatUiInteger(Math.Max(parameters.EngineCount, 1))} x {FormatUiNumber(aircraft.Engine.Performance.Weight.Kilograms, "0.0")} = {FormatUiNumber(engines, "0.0")} kg.",
                $"Nominal systems (base + engines + {FormatUiInteger(parameters.CrewCount)} crew): {FormatUiNumber(nominalSystems, "0.0")} kg.",
                $"Category structure: {FormatSignedNumber(category.WeightModifierKilograms, "0.0")} kg; navigation: {FormatSignedNumber(navigation.ExtraWeightKilograms, "0.0")} kg.",
                $"Aircraft-size and high-speed structural scaling: {FormatSignedNumber(scalingResidual, "0.0")} kg.",
                $"Final empty weight: {FormatUiNumber(performance.EmptyAircraftWeightKilograms, "0.0")} kg.",
            });
    }

    private static ModifierTooltipDefinition BuildAircraftPassengerTooltip(Aircraft aircraft)
    {
        var payloadPerPassenger = PlaneDesignerBalance.Categories.PayloadKilogramsPerPassenger;
        return BuildTechnicalTooltip(
            "designer.aircraft.passengers",
            "Passenger Capacity Calculation",
            new[]
            {
                $"Useful weight: {FormatUiNumber(aircraft.Performance.PayloadCapacity.Kilograms, "0.0")} kg.",
                $"Planning allowance: {FormatUiNumber(payloadPerPassenger, "0")} kg per passenger.",
                $"Passengers = floor(useful weight / allowance) = {FormatUiInteger(aircraft.Performance.PassengerCapacity)}.",
            });
    }

    private ModifierTooltipDefinition BuildAircraftReliabilityTooltip(Aircraft aircraft)
    {
        var parameters = aircraft.Airframe.Parameters;
        var category = AircraftCategoryCatalog.GetProfile(aircraft.FamilyDefinition.Category);
        var navigation = AircraftNavigationEquipmentBalance.GetEffects(aircraft.TypeDefinition.NavigationEquipment);
        var companyBonus = HeadquartersBalance.GetCompanyReliabilityBonus(_headquarters.CompanyQualityLevel);
        var effective = ResolveEffectiveAircraftReliability(aircraft.Performance.OverallReliability);
        return BuildTechnicalTooltip(
            "designer.aircraft.reliability",
            "Overall Aircraft Reliability",
            new[]
            {
                $"Engine reliability: {FormatUiNumber(aircraft.Engine.Performance.Reliability, "0.000")} across {FormatUiInteger(Math.Max(parameters.EngineCount, 1))} engine(s); multi-engine complexity applies a penalty.",
                $"Airframe reliability multiplier: x{FormatUiNumber(aircraft.Airframe.Performance.ReliabilityModifier, "0.000")}.",
                $"Category change: {FormatSignedNumber(category.ReliabilityModifierDelta, "0.000")}; navigation change: {FormatSignedNumber(navigation.ReliabilityDelta, "0.000")}.",
                $"After type research modifiers: {FormatUiNumber(aircraft.Performance.OverallReliability, "0.000")}.",
                $"Company quality level {FormatUiInteger(_headquarters.CompanyQualityLevel)} adds up to {FormatSignedNumber(companyBonus, "0.000")}.",
                $"Final displayed reliability (after cap): {FormatUiNumber(effective, "0.000")}.",
            });
    }

    private static ModifierTooltipDefinition BuildAircraftManeuverabilityTooltip(Aircraft aircraft)
    {
        var parameters = aircraft.Airframe.Parameters;
        var wingLoading = aircraft.Airframe.Performance.TotalWeight.Kilograms
            / Math.Max(parameters.WingAreaSquareMeters, 0.001d);
        return BuildTechnicalTooltip(
            "designer.aircraft.maneuverability",
            "Maneuverability Influences",
            new[]
            {
                $"Wing loading: {FormatUiNumber(wingLoading, "0.0")} kg/m2 from {FormatUiNumber(aircraft.Airframe.Performance.TotalWeight.Kilograms, "0.0")} kg / {FormatUiNumber(parameters.WingAreaSquareMeters, "0.0")} m2.",
                $"Wing: {HumanizeUiToken(parameters.WingPosition.ToString())}, {HumanizeUiToken(parameters.WingShape.ToString())}, {HumanizeUiToken(parameters.WingProfile.ToString())}, aspect ratio {FormatUiNumber(parameters.WingAspectRatio, "0.0")}.",
                $"High-lift and tail controls: {HumanizeUiToken(parameters.FlapType.ToString())}, leading edge flaps {(parameters.HasLeadingEdgeFlaps ? "enabled" : "disabled")}, {HumanizeUiToken(parameters.TailType.ToString())} tail.",
                $"Construction standard: {HumanizeUiToken(parameters.ConstructionStandard.ToString())}.",
                $"Final airframe maneuverability: {FormatUiNumber(aircraft.Airframe.Performance.Maneuverability, "0.0")}.",
            });
    }

    private static ModifierTooltipDefinition BuildAircraftProductionPartsTooltip(Aircraft aircraft)
    {
        var performance = aircraft.Performance;
        var breakdown = performance.CostBreakdown ?? ProductionBreakdown.FromTotal("Aircraft", performance.TotalProductionCost);
        var tooltip = BuildProductionBreakdownTooltip(
            "designer.aircraft.production-parts",
            "Aircraft Production Parts Breakdown",
            breakdown,
            performance.TotalProductionCost,
            "0",
            "Type production optimization");
        return AppendTooltipNotes(
            tooltip,
            "Fuel capacity is included in 'Physical size (wing, payload and fuel capacity)'.",
            "Crew count affects aircraft weight; the current economy model has no separate crew-seat parts charge.");
    }

    private static ModifierTooltipDefinition BuildAircraftProductionManDaysTooltip(Aircraft aircraft)
    {
        var performance = aircraft.Performance;
        var breakdown = performance.ProductionManDaysBreakdown
            ?? ProductionBreakdown.FromTotal("Aircraft", performance.TotalProductionManDays);
        return BuildProductionBreakdownTooltip(
            "designer.aircraft.production-md",
            "Aircraft Production MD Breakdown",
            breakdown,
            performance.TotalProductionManDays,
            "0.0",
            "Type production optimization");
    }

    private static ModifierTooltipDefinition BuildAircraftCompleteCostTooltip(Aircraft aircraft)
    {
        var performance = aircraft.Performance;
        var breakdown = performance.CostBreakdown ?? ProductionBreakdown.FromTotal("Aircraft", performance.TotalProductionCost);
        var tooltip = BuildCompleteProductionCostTooltip(
            "designer.aircraft.complete-cost",
            "Complete Aircraft Production Cost",
            breakdown,
            performance.TotalProductionCost,
            performance.TotalProductionManDays,
            performance.CompleteProductionCost,
            "Type production optimization");
        return AppendTooltipNotes(
            tooltip,
            "Fuel capacity is included in the shared physical-size item; crew count has no separate seat price in the current economy model.");
    }

    private ModifierTooltipDefinition BuildAircraftEngineeringTimeTooltip(Aircraft aircraft, int resultMonths)
    {
        var quote = _researchCalculator.CalculateTypeQuote(aircraft);
        var lines = new List<string>
        {
            $"Change magnitude: {FormatUiNumber(aircraft.Development.ChangeMagnitudeScore, "0.000")} (50% of complexity).",
            $"Changed parameters: {FormatUiInteger(aircraft.Development.ChangedParameterCount)} (25% of complexity).",
            $"Aircraft weight: {FormatUiNumber(aircraft.Performance.TotalWeight.Kilograms, "0.0")} kg (15% of complexity).",
            $"Sea-level speed: {FormatUiNumber(aircraft.Performance.MaxSpeedByAltitude.Count > 0 ? aircraft.Performance.MaxSpeedByAltitude[0].MaxSpeedMetersPerSecond * 3.6d : 0d, "0.0")} km/h (10% of complexity).",
            $"Calculated complexity: {FormatUiNumber(quote.ComplexityNormalized * 100d, "0.0")}%; fast-development research points: {FormatUiInteger(aircraft.TypeDefinition.ResearchModifiers.FastDevelopmentPoints)}.",
        };
        var baselineYear = ResolveActiveTypeVersionDiscountBaselineYear();
        if (baselineYear.HasValue)
        {
            lines.Add($"Version development starts at {FormatUiNumber(VersionDevelopmentResearchTimeMultiplier * 100d, "0")}% of full development time, then benefits from technology progress since {baselineYear.Value}.");
        }

        lines.Add($"Final engineering time: {FormatUiInteger(resultMonths)} months.");
        return BuildTechnicalTooltip("designer.aircraft.engineering-time", "Aircraft Engineering Time", lines);
    }

    private static ModifierTooltipDefinition BuildCompleteProductionCostTooltip(
        string id,
        string title,
        ProductionBreakdown partsBreakdown,
        double partsTotal,
        double productionManDays,
        double completeTotal,
        string adjustmentLabel)
    {
        var lines = new List<string>(partsBreakdown.Items.Count + 7)
        {
            "Production parts:",
        };
        for (var index = 0; index < partsBreakdown.Items.Count; index++)
        {
            var item = partsBreakdown.Items[index];
            lines.Add($"  {HumanizeProductionBreakdownLabel(item.Label)}: {FormatUiNumber(item.Amount, "0")}");
        }

        var partsAdjustment = partsTotal - partsBreakdown.Total;
        if (Math.Abs(partsAdjustment) > 0.0001d)
        {
            lines.Add($"  {adjustmentLabel}: {FormatUiNumber(partsAdjustment, "0")}");
        }

        var laborCost = ProductionEconomyBalance.CalculateLaborCost(productionManDays);
        lines.Add("------------------------------");
        lines.Add($"Parts subtotal: {FormatUiNumber(partsTotal, "0")}");
        lines.Add(
            $"Labor: {FormatUiNumber(productionManDays, "0.0")} MD x {FormatUiNumber(ProductionEconomyBalance.LaborCostPerManDay, "0")} " +
            $"= {FormatUiNumber(laborCost, "0")}");
        lines.Add($"Complete cost: {FormatUiNumber(completeTotal, "0")}");
        return new ModifierTooltipDefinition(id, title, neutralLines: lines);
    }

    private static List<string> BuildEngineParameterSummary(Engine engine)
    {
        var parameters = engine.Parameters;
        var lines = new List<string>
        {
            $"Engine type: {HumanizeUiToken(engine.Definition.EngineType.ToString())}.",
        };
        if (engine.Performance.OutputKind == EngineOutputKind.ThrustNewtons)
        {
            lines.Add(
                $"Diameter {FormatUiNumber(parameters.TurbineDiameterMeters * 100d, "0")} cm; bypass {FormatUiNumber(parameters.BypassRatio, "0.00")}; " +
                $"compression {FormatUiNumber(parameters.CompressionRatio, "0.00")}.");
            lines.Add($"Afterburner {(parameters.HasAfterburner ? "enabled" : "disabled")}; boost {HumanizeUiToken(parameters.BoostType.ToString())}.");
        }
        else
        {
            lines.Add(
                $"{FormatUiInteger(parameters.CylinderCount)} cylinders; {FormatUiNumber(parameters.DisplacementLiters, "0.0")} L; " +
                $"compression {FormatUiNumber(parameters.CompressionRatio, "0.00")}.");
            lines.Add(
                $"Boost {HumanizeUiToken(parameters.BoostType.ToString())}; injection {HumanizeUiToken(parameters.PistonInjectionType.ToString())}; " +
                $"optimization height {FormatUiNumber(parameters.OptimizationAltitudeMeters, "0")} m.");
        }

        lines.Add(
            $"RPM {FormatUiNumber(parameters.MaximumSpeed, "0")}; cooling {FormatUiNumber(parameters.CoolingEfficiency * 100d, "0.0")}% ; " +
            $"efficiency optimization {FormatUiNumber(parameters.EfficiencyOptimizationsPercent, "0.0")}% ; precision level {FormatUiInteger(parameters.PrecisionEngineeringLevel)}.");
        return lines;
    }

    private static ModifierTooltipDefinition BuildTechnicalTooltip(string id, string title, IEnumerable<string> lines) =>
        new(id, title, neutralLines: new List<string>(lines));

    private static ModifierTooltipDefinition AppendTooltipNotes(ModifierTooltipDefinition tooltip, params string[] notes)
    {
        var lines = new List<string>(tooltip.NeutralLines.Count + notes.Length);
        lines.AddRange(tooltip.NeutralLines);
        for (var index = 0; index < notes.Length; index++)
        {
            if (!string.IsNullOrWhiteSpace(notes[index]))
            {
                lines.Add(notes[index]);
            }
        }

        return new ModifierTooltipDefinition(
            tooltip.Id,
            tooltip.Title,
            tooltip.PositiveLines,
            tooltip.NegativeLines,
            lines);
    }

    private static string HumanizeProductionBreakdownLabel(string label)
    {
        if (label.Contains("Role module (PassengerCabin)", StringComparison.Ordinal))
        {
            return label.Replace("Role module (PassengerCabin)", "Passenger cabin and seats", StringComparison.Ordinal);
        }

        if (label.Contains("Role module (CargoSpace)", StringComparison.Ordinal))
        {
            return label.Replace("Role module (CargoSpace)", "Cargo space", StringComparison.Ordinal);
        }

        if (label.Contains("Role module (SpecialEquipment)", StringComparison.Ordinal))
        {
            return label.Replace("Role module (SpecialEquipment)", "Special equipment", StringComparison.Ordinal);
        }

        if (label.Contains("Role module (CombatEquipment)", StringComparison.Ordinal))
        {
            return label.Replace("Role module (CombatEquipment)", "Combat equipment", StringComparison.Ordinal);
        }

        return label;
    }

    private static string FormatSignedNumber(double value, string format) =>
        $"{(value >= 0d ? "+" : string.Empty)}{FormatUiNumber(value, format)}";

    private static double ResolveRunwayLengthMeters(RunwayType runwayType) => runwayType switch
    {
        RunwayType.Short => PlaneDesignerBalance.Airframe.RunwayShortLengthMeters,
        RunwayType.Medium => PlaneDesignerBalance.Airframe.RunwayMediumLengthMeters,
        RunwayType.Long => PlaneDesignerBalance.Airframe.RunwayLongLengthMeters,
        _ => PlaneDesignerBalance.Airframe.RunwayMediumLengthMeters,
    };
}
