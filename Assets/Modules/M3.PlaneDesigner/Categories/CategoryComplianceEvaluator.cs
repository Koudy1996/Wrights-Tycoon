using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using WrightTycoon.Core.Aircraft;
using WrightTycoon.Core.Airframes;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Math;

namespace WrightTycoon.Core.Categories;

public sealed class CategoryComplianceEvaluator
{
    public CategoryComplianceResult Evaluate(Aircraft.Aircraft aircraft)
    {
        if (aircraft is null)
        {
            throw new ArgumentNullException(nameof(aircraft));
        }

        var profile = AircraftCategoryCatalog.GetProfile(aircraft.FamilyDefinition.Category);
        return Evaluate(aircraft, profile);
    }

    public CategoryComplianceResult Evaluate(Aircraft.Aircraft aircraft, AircraftCategoryProfile profile)
    {
        if (aircraft is null)
        {
            throw new ArgumentNullException(nameof(aircraft));
        }

        if (profile is null)
        {
            throw new ArgumentNullException(nameof(profile));
        }

        var unmet = new List<string>();

        var crew = aircraft.Airframe.Parameters.CrewCount;
        if (crew < profile.MinCrew || crew > profile.MaxCrew)
        {
            unmet.Add($"Crew {FormatUiInteger(crew)} outside [{FormatUiInteger(profile.MinCrew)}, {FormatUiInteger(profile.MaxCrew)}]");
        }

        var weight = aircraft.Performance.TotalWeight.Kilograms;
        if (weight < profile.MinMaximumWeightKilograms || weight > profile.MaxMaximumWeightKilograms)
        {
            unmet.Add($"Weight {FormatUiNumber(weight, "0.0")} kg outside [{FormatUiNumber(profile.MinMaximumWeightKilograms, "0.0")}, {FormatUiNumber(profile.MaxMaximumWeightKilograms, "0.0")}] kg");
        }

        if (aircraft.Airframe.Parameters.MaterialType == MaterialType.WoodFabric
            && weight - Numeric.Epsilon > PlaneDesignerBalance.Airframe.WoodFabricMaximumTotalWeightKilograms)
        {
            unmet.Add(
                $"Wood/Fabric limit exceeded: {FormatUiNumber(weight, "0.0")} kg > {FormatUiNumber(PlaneDesignerBalance.Airframe.WoodFabricMaximumTotalWeightKilograms, "0.0")} kg");
        }

        var speedMetersPerSecond = GetSpeedAtAltitudeMeters(aircraft.Performance, 0d);
        var speedKilometersPerHour = speedMetersPerSecond * 3.6d;
        if (speedKilometersPerHour < profile.MinRequiredSpeedKilometersPerHour
            || speedKilometersPerHour > profile.MaxRequiredSpeedKilometersPerHour)
        {
            unmet.Add(
                $"Speed {FormatUiNumber(speedKilometersPerHour, "0.0")} km/h outside [{FormatUiNumber(profile.MinRequiredSpeedKilometersPerHour, "0.0")}, {FormatUiNumber(profile.MaxRequiredSpeedKilometersPerHour, "0.0")}] km/h");
        }

        var physicalRange = aircraft.Performance.RangeKilometers;
        var range = AircraftNavigationRangePolicy.GetCertifiedRangeKilometers(
            aircraft.TypeDefinition.NavigationEquipment,
            physicalRange);
        if (range < profile.MinRequiredRangeKilometers || range > profile.MaxRequiredRangeKilometers)
        {
            var rangeLabel = AircraftNavigationRangePolicy.IsRangeCapped(
                aircraft.TypeDefinition.NavigationEquipment,
                physicalRange)
                ? "Certified range"
                : "Range";
            unmet.Add($"{rangeLabel} {FormatUiNumber(range, "0.0")} km outside [{FormatUiNumber(profile.MinRequiredRangeKilometers, "0.0")}, {FormatUiNumber(profile.MaxRequiredRangeKilometers, "0.0")}] km");
        }

        var payload = aircraft.Performance.PayloadCapacity.Kilograms;
        if (payload < profile.MinRequiredPayloadKilograms || payload > profile.MaxRequiredPayloadKilograms)
        {
            unmet.Add($"Payload {FormatUiNumber(payload, "0.0")} kg outside [{FormatUiNumber(profile.MinRequiredPayloadKilograms, "0.0")}, {FormatUiNumber(profile.MaxRequiredPayloadKilograms, "0.0")}] kg");
        }

        var maneuverability = aircraft.Airframe.Performance.Maneuverability;
        if (maneuverability < profile.MinRequiredManeuverability
            || maneuverability > profile.MaxRequiredManeuverability)
        {
            unmet.Add(
                $"Maneuverability {FormatUiNumber(maneuverability, "0.0")} outside [{FormatUiNumber(profile.MinRequiredManeuverability, "0.0")}, {FormatUiNumber(profile.MaxRequiredManeuverability, "0.0")}]");
        }

        if (profile.HasPassengerCapacityRequirement)
        {
            var passengers = aircraft.Performance.GetPassengerCapacity(PlaneDesignerBalance.Categories.PayloadKilogramsPerPassenger);
            var minimumPassengers = profile.GetMinimumPassengerCapacity();
            var maximumPassengers = profile.GetMaximumPassengerCapacity();
            if (passengers < minimumPassengers || passengers > maximumPassengers)
            {
                unmet.Add($"Passengers {FormatUiInteger(passengers)} outside [{FormatUiInteger(minimumPassengers)}, {FormatUiInteger(maximumPassengers)}]");
            }
        }

        AddRoleRequirements(aircraft, profile, unmet);

        return new CategoryComplianceResult(unmet.Count == 0, unmet);
    }

    private static void AddRoleRequirements(
        Aircraft.Aircraft aircraft,
        AircraftCategoryProfile profile,
        List<string> unmet)
    {
        var family = aircraft.FamilyDefinition;
        var type = aircraft.TypeDefinition;

        if (profile.Category == AircraftCategory.LightSportAndRecreational)
        {
            Require(
                family.ConstructionStandard == AircraftConstructionStandard.Ultralight,
                "Ultralight construction required",
                unmet);
            RequireNoHeavyRoleModule(type, profile, unmet);
            return;
        }

        if (profile.HasPassengerCapacityRequirement)
        {
            Require(type.HasPassengerCabin, "Passenger cabin required", unmet);
            Require(!type.HasCargoSpace, "Cargo space conflicts with passenger category", unmet);
            Require(!type.HasSpecialEquipment, "Special equipment conflicts with passenger category", unmet);
            RequireNoCombatRole(type, "passenger category", unmet);
            return;
        }

        if (IsCargoCategory(profile.Category))
        {
            Require(type.HasCargoSpace, "Cargo space required", unmet);
            Require(!type.HasPassengerCabin, "Passenger cabin conflicts with cargo category", unmet);
            Require(!type.HasSpecialEquipment, "Special equipment conflicts with cargo category", unmet);
            RequireNoCombatRole(type, "cargo category", unmet);
            return;
        }

        if (profile.Category == AircraftCategory.SpecialCivil)
        {
            Require(type.HasSpecialEquipment, "Special equipment required", unmet);
            Require(!type.HasPassengerCabin, "Passenger cabin conflicts with special civil category", unmet);
            Require(!type.HasCargoSpace, "Cargo space conflicts with special civil category", unmet);
            RequireNoCombatRole(type, "special civil category", unmet);
            return;
        }

        if (profile.Category == AircraftCategory.MilitaryTactical)
        {
            Require(family.ConstructionStandard == AircraftConstructionStandard.ArmyGrade, "Army-grade construction required", unmet);
            Require(type.HasCombatEquipment, "Combat equipment required", unmet);
            Require(type.WeaponPayloadKilograms > Numeric.Epsilon, "Weapon payload required", unmet);
            Require(!type.HasPassengerCabin, "Passenger cabin conflicts with combat category", unmet);
            RequireWeaponPayloadFitsAircraft(type, aircraft, unmet);
            return;
        }

        if (profile.Category == AircraftCategory.Bomber)
        {
            Require(family.ConstructionStandard == AircraftConstructionStandard.ArmyGrade, "Army-grade construction required", unmet);
            Require(type.HasCombatEquipment, "Combat equipment required", unmet);
            Require(!type.HasPassengerCabin, "Passenger cabin conflicts with combat category", unmet);
            RequireWeaponPayloadFitsAircraft(type, aircraft, unmet);
            if (type.WeaponPayloadKilograms < profile.MinRequiredPayloadKilograms
                || type.WeaponPayloadKilograms > profile.MaxRequiredPayloadKilograms)
            {
                unmet.Add(
                    $"Weapon payload {FormatUiNumber(type.WeaponPayloadKilograms, "0.0")} kg outside [{FormatUiNumber(profile.MinRequiredPayloadKilograms, "0.0")}, {FormatUiNumber(profile.MaxRequiredPayloadKilograms, "0.0")}] kg");
            }

            return;
        }

        if (profile.Category == AircraftCategory.MilitaryStrategicSupport)
        {
            Require(family.ConstructionStandard == AircraftConstructionStandard.ArmyGrade, "Army-grade construction required", unmet);
            Require(!type.HasPassengerCabin || !type.HasCombatEquipment, "Passenger cabin conflicts with combat equipment", unmet);
        }
    }

    private static bool IsCargoCategory(AircraftCategory category) =>
        category is AircraftCategory.LightCargo
            or AircraftCategory.MediumCargo
            or AircraftCategory.HeavyCargo
            or AircraftCategory.LongRangeCargo;

    private static void RequireNoHeavyRoleModule(
        AircraftTypeDefinition type,
        AircraftCategoryProfile profile,
        List<string> unmet)
    {
        Require(!type.HasPassengerCabin, "Passenger cabin conflicts with light sport category", unmet);
        Require(!type.HasCargoSpace, "Cargo space conflicts with light sport category", unmet);
        Require(!type.HasSpecialEquipment, "Special equipment conflicts with light sport category", unmet);
        RequireNoCombatRole(type, profile.DisplayName, unmet);
    }

    private static void RequireNoCombatRole(
        AircraftTypeDefinition type,
        string categoryName,
        List<string> unmet)
    {
        Require(!type.HasCombatEquipment, $"Combat equipment conflicts with {categoryName}", unmet);
        Require(type.WeaponPayloadKilograms <= Numeric.Epsilon, $"Weapon payload conflicts with {categoryName}", unmet);
    }

    private static void RequireWeaponPayloadFitsAircraft(
        AircraftTypeDefinition type,
        Aircraft.Aircraft aircraft,
        List<string> unmet)
    {
        if (type.WeaponPayloadKilograms > aircraft.Performance.PayloadCapacity.Kilograms + Numeric.Epsilon)
        {
            unmet.Add(
                $"Weapon payload {FormatUiNumber(type.WeaponPayloadKilograms, "0.0")} kg exceeds aircraft payload capacity {FormatUiNumber(aircraft.Performance.PayloadCapacity.Kilograms, "0.0")} kg");
        }
    }

    private static void Require(bool condition, string unmetRequirement, List<string> unmet)
    {
        if (!condition)
        {
            unmet.Add(unmetRequirement);
        }
    }


    private static readonly CultureInfo UiNumberCulture = CreateUiNumberCulture();

    private static CultureInfo CreateUiNumberCulture()
    {
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.NumberGroupSeparator = " ";
        culture.NumberFormat.NumberDecimalSeparator = ",";
        culture.NumberFormat.NumberGroupSizes = new[] { 3 };
        return culture;
    }

    private static string FormatUiNumber(double value, string format) =>
        value.ToString($"#,##{format}", UiNumberCulture);

    private static string FormatUiInteger(int value) =>
        value.ToString("#,##0", UiNumberCulture);
    private static double GetSpeedAtAltitudeMeters(AircraftPerformanceReport performance, double altitudeMeters)
    {
        var points = performance.MaxSpeedByAltitude
            .OrderBy(point => point.AltitudeMeters)
            .ToArray();

        if (points.Length == 0)
        {
            return 0d;
        }

        for (var index = 0; index < points.Length; index++)
        {
            if (Numeric.AlmostEqual(points[index].AltitudeMeters, altitudeMeters, tolerance: 1e-3))
            {
                return points[index].MaxSpeedMetersPerSecond;
            }
        }

        if (altitudeMeters <= points[0].AltitudeMeters)
        {
            return points[0].MaxSpeedMetersPerSecond;
        }

        if (altitudeMeters >= points[points.Length - 1].AltitudeMeters)
        {
            return points[points.Length - 1].MaxSpeedMetersPerSecond;
        }

        for (var index = 1; index < points.Length; index++)
        {
            var left = points[index - 1];
            var right = points[index];
            if (altitudeMeters < left.AltitudeMeters || altitudeMeters > right.AltitudeMeters)
            {
                continue;
            }

            var span = right.AltitudeMeters - left.AltitudeMeters;
            if (span <= Numeric.Epsilon)
            {
                return right.MaxSpeedMetersPerSecond;
            }

            var blend = (altitudeMeters - left.AltitudeMeters) / span;
            return left.MaxSpeedMetersPerSecond + ((right.MaxSpeedMetersPerSecond - left.MaxSpeedMetersPerSecond) * blend);
        }

        return points[0].MaxSpeedMetersPerSecond;
    }
}

