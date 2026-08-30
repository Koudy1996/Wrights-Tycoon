using System;
using System.Collections.Generic;
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
        if (crew < profile.MinCrew)
        {
            unmet.Add($"Crew {FormatUiInteger(crew)} < {FormatUiInteger(profile.MinCrew)}");
        }

        var weight = aircraft.Performance.TotalWeight.Kilograms;
        if (weight > profile.MaxMaximumWeightKilograms)
        {
            unmet.Add($"Weight {FormatUiNumber(weight, "0.0")} kg > {FormatUiNumber(profile.MaxMaximumWeightKilograms, "0.0")} kg");
        }

        if (aircraft.Airframe.Parameters.MaterialType == MaterialType.WoodFabric
            && weight - Numeric.Epsilon > PlaneDesignerBalance.Airframe.WoodFabricMaximumTotalWeightKilograms)
        {
            unmet.Add(
                $"Wood/Fabric limit exceeded: {FormatUiNumber(weight, "0.0")} kg > {FormatUiNumber(PlaneDesignerBalance.Airframe.WoodFabricMaximumTotalWeightKilograms, "0.0")} kg");
        }

        var speedMetersPerSecond = GetMaximumSpeedMetersPerSecond(aircraft.Performance);
        var speedKilometersPerHour = speedMetersPerSecond * 3.6d;
        if (speedKilometersPerHour < profile.MinRequiredSpeedKilometersPerHour)
        {
            unmet.Add($"Speed {FormatUiNumber(speedKilometersPerHour, "0.0")} km/h < {FormatUiNumber(profile.MinRequiredSpeedKilometersPerHour, "0.0")} km/h");
        }

        var physicalRange = aircraft.Performance.RangeKilometers;
        var range = AircraftNavigationRangePolicy.GetCertifiedRangeKilometers(
            aircraft.TypeDefinition.NavigationEquipment,
            physicalRange);
        if (range < profile.MinRequiredRangeKilometers)
        {
            var rangeLabel = AircraftNavigationRangePolicy.IsRangeCapped(
                aircraft.TypeDefinition.NavigationEquipment,
                physicalRange)
                ? "Certified range"
                : "Range";
            unmet.Add($"{rangeLabel} {FormatUiNumber(range, "0.0")} km < {FormatUiNumber(profile.MinRequiredRangeKilometers, "0.0")} km");
        }

        var payload = aircraft.Performance.PayloadCapacity.Kilograms;
        if (payload < profile.MinRequiredPayloadKilograms)
        {
            unmet.Add($"Payload {FormatUiNumber(payload, "0.0")} kg < {FormatUiNumber(profile.MinRequiredPayloadKilograms, "0.0")} kg");
        }

        var maneuverability = aircraft.Airframe.Performance.Maneuverability;
        if (maneuverability < profile.MinRequiredManeuverability)
        {
            unmet.Add($"Maneuverability {FormatUiNumber(maneuverability, "0.0")} < {FormatUiNumber(profile.MinRequiredManeuverability, "0.0")}");
        }

        if (profile.HasPassengerCapacityRequirement)
        {
            var passengers = aircraft.Performance.GetPassengerCapacity(PlaneDesignerBalance.Categories.PayloadKilogramsPerPassenger);
            var minimumPassengers = profile.GetMinimumPassengerCapacity();
            if (passengers < minimumPassengers)
            {
                unmet.Add($"Passengers {FormatUiInteger(passengers)} < {FormatUiInteger(minimumPassengers)}");
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
        var policy = AircraftCategoryRequirementBalance.GetPolicy(profile.Category);

        Require(
            AircraftCategoryRequirementBalance.AllowsConstruction(profile.Category, family.ConstructionStandard),
            $"{AircraftCategoryRequirementBalance.GetConstructionDescription(profile.Category)} required",
            unmet);

        switch (policy.RoleRequirement)
        {
            case AircraftCategoryRoleRequirement.Unrestricted:
                return;

            case AircraftCategoryRoleRequirement.NoRoleModule:
                RequireNoRoleModule(type, profile.DisplayName, unmet);
                return;

            case AircraftCategoryRoleRequirement.PassengerCabin:
                RequireOnlySelectedRole(type, AircraftCategoryRoleRequirement.PassengerCabin, "passenger category", unmet);
                return;

            case AircraftCategoryRoleRequirement.CargoSpace:
                RequireOnlySelectedRole(type, AircraftCategoryRoleRequirement.CargoSpace, "cargo category", unmet);
                return;

            case AircraftCategoryRoleRequirement.SpecialEquipment:
                RequireOnlySelectedRole(type, AircraftCategoryRoleRequirement.SpecialEquipment, "special civil category", unmet);
                return;

            case AircraftCategoryRoleRequirement.CombatEquipment:
                RequireOnlySelectedRole(type, AircraftCategoryRoleRequirement.CombatEquipment, "combat category", unmet);
                Require(type.WeaponPayloadKilograms > Numeric.Epsilon, "Weapon payload required", unmet);
                RequireWeaponPayloadFitsAircraft(type, aircraft, unmet);
                if (profile.Category == AircraftCategory.Bomber
                    && type.WeaponPayloadKilograms < profile.MinRequiredPayloadKilograms)
                {
                    unmet.Add(
                        $"Weapon payload {FormatUiNumber(type.WeaponPayloadKilograms, "0.0")} kg < {FormatUiNumber(profile.MinRequiredPayloadKilograms, "0.0")} kg");
                }

                return;

            case AircraftCategoryRoleRequirement.MilitarySupport:
                Require(!type.HasPassengerCabin, "Passenger cabin conflicts with military support category", unmet);
                Require(!type.HasSpecialEquipment, "Special equipment conflicts with military support category", unmet);
                Require(type.HasCargoSpace || type.HasCombatEquipment, "Combat equipment or cargo space required", unmet);
                Require(!type.HasCargoSpace || !type.HasCombatEquipment, "Cargo space conflicts with combat equipment", unmet);
                return;

            default:
                throw new ArgumentOutOfRangeException(nameof(policy.RoleRequirement));
        }
    }

    private static void RequireNoRoleModule(
        AircraftTypeDefinition type,
        string categoryName,
        List<string> unmet)
    {
        Require(!type.HasPassengerCabin, $"Passenger cabin conflicts with {categoryName}", unmet);
        Require(!type.HasCargoSpace, $"Cargo space conflicts with {categoryName}", unmet);
        Require(!type.HasSpecialEquipment, $"Special equipment conflicts with {categoryName}", unmet);
        Require(!type.HasCombatEquipment, $"Combat equipment conflicts with {categoryName}", unmet);
        Require(type.WeaponPayloadKilograms <= Numeric.Epsilon, $"Weapon payload conflicts with {categoryName}", unmet);
    }

    private static void RequireOnlySelectedRole(
        AircraftTypeDefinition type,
        AircraftCategoryRoleRequirement selectedRole,
        string categoryName,
        List<string> unmet)
    {
        Require(type.HasPassengerCabin == (selectedRole == AircraftCategoryRoleRequirement.PassengerCabin),
            selectedRole == AircraftCategoryRoleRequirement.PassengerCabin ? "Passenger cabin required" : $"Passenger cabin conflicts with {categoryName}", unmet);
        Require(type.HasCargoSpace == (selectedRole == AircraftCategoryRoleRequirement.CargoSpace),
            selectedRole == AircraftCategoryRoleRequirement.CargoSpace ? "Cargo space required" : $"Cargo space conflicts with {categoryName}", unmet);
        Require(type.HasSpecialEquipment == (selectedRole == AircraftCategoryRoleRequirement.SpecialEquipment),
            selectedRole == AircraftCategoryRoleRequirement.SpecialEquipment ? "Special equipment required" : $"Special equipment conflicts with {categoryName}", unmet);
        Require(type.HasCombatEquipment == (selectedRole == AircraftCategoryRoleRequirement.CombatEquipment),
            selectedRole == AircraftCategoryRoleRequirement.CombatEquipment ? "Combat equipment required" : $"Combat equipment conflicts with {categoryName}", unmet);

        if (selectedRole != AircraftCategoryRoleRequirement.CombatEquipment)
        {
            Require(type.WeaponPayloadKilograms <= Numeric.Epsilon, $"Weapon payload conflicts with {categoryName}", unmet);
        }
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
    private static double GetMaximumSpeedMetersPerSecond(AircraftPerformanceReport performance)
    {
        if (performance.MaxSpeedByAltitude.Count == 0)
        {
            return 0d;
        }

        var maximumSpeed = 0d;
        for (var index = 0; index < performance.MaxSpeedByAltitude.Count; index++)
        {
            maximumSpeed = System.Math.Max(
                maximumSpeed,
                performance.MaxSpeedByAltitude[index].MaxSpeedMetersPerSecond);
        }

        return maximumSpeed;
    }
}

