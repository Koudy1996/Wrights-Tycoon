using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using WrightTycoon.Core.Aircraft;
using WrightTycoon.Core.Categories;

namespace WrightTycoon.Core.Balance;

[Flags]
public enum AircraftConstructionStandardOptions
{
    None = 0,
    Ultralight = 1 << 0,
    Civil = 1 << 1,
    ArmyGrade = 1 << 2,
    Any = Ultralight | Civil | ArmyGrade,
}

public enum AircraftCategoryRoleRequirement
{
    Unrestricted = 0,
    NoRoleModule = 1,
    PassengerCabin = 2,
    CargoSpace = 3,
    SpecialEquipment = 4,
    CombatEquipment = 5,
    MilitarySupport = 6,
}

public readonly record struct AircraftCategoryRequirementPolicy(
    AircraftConstructionStandardOptions AllowedConstructionStandards,
    AircraftCategoryRoleRequirement RoleRequirement);

public static class AircraftCategoryRequirementBalance
{
    private const AircraftConstructionStandardOptions CivilOrArmy =
        AircraftConstructionStandardOptions.Civil | AircraftConstructionStandardOptions.ArmyGrade;

    private static readonly IReadOnlyDictionary<AircraftCategory, AircraftCategoryRequirementPolicy> Policies =
        new ReadOnlyDictionary<AircraftCategory, AircraftCategoryRequirementPolicy>(
            new Dictionary<AircraftCategory, AircraftCategoryRequirementPolicy>
            {
                [AircraftCategory.LightSportAndRecreational] = new(AircraftConstructionStandardOptions.Ultralight, AircraftCategoryRoleRequirement.NoRoleModule),
                [AircraftCategory.LightMultiRolePiston] = new(CivilOrArmy, AircraftCategoryRoleRequirement.Unrestricted),
                [AircraftCategory.TurbopropRegional] = new(CivilOrArmy, AircraftCategoryRoleRequirement.PassengerCabin),
                [AircraftCategory.RegionalJet] = new(CivilOrArmy, AircraftCategoryRoleRequirement.PassengerCabin),
                [AircraftCategory.MediumTransportNarrowBody] = new(CivilOrArmy, AircraftCategoryRoleRequirement.PassengerCabin),
                [AircraftCategory.HeavyTransportWideBody] = new(CivilOrArmy, AircraftCategoryRoleRequirement.PassengerCabin),
                [AircraftCategory.SpecialCivil] = new(CivilOrArmy, AircraftCategoryRoleRequirement.SpecialEquipment),
                [AircraftCategory.BusinessVipJet] = new(CivilOrArmy, AircraftCategoryRoleRequirement.PassengerCabin),
                [AircraftCategory.MilitaryTactical] = new(AircraftConstructionStandardOptions.ArmyGrade, AircraftCategoryRoleRequirement.CombatEquipment),
                [AircraftCategory.MilitaryStrategicSupport] = new(AircraftConstructionStandardOptions.ArmyGrade, AircraftCategoryRoleRequirement.MilitarySupport),
                [AircraftCategory.LightCargo] = new(CivilOrArmy, AircraftCategoryRoleRequirement.CargoSpace),
                [AircraftCategory.MediumCargo] = new(CivilOrArmy, AircraftCategoryRoleRequirement.CargoSpace),
                [AircraftCategory.HeavyCargo] = new(CivilOrArmy, AircraftCategoryRoleRequirement.CargoSpace),
                [AircraftCategory.LongRangeCargo] = new(CivilOrArmy, AircraftCategoryRoleRequirement.CargoSpace),
                [AircraftCategory.Bomber] = new(AircraftConstructionStandardOptions.ArmyGrade, AircraftCategoryRoleRequirement.CombatEquipment),
            });

    public static AircraftCategoryRequirementPolicy GetPolicy(AircraftCategory category)
    {
        if (!Policies.TryGetValue(category, out var policy))
        {
            throw new KeyNotFoundException($"Aircraft category requirement policy for '{category}' was not found.");
        }

        return policy;
    }

    public static bool AllowsConstruction(
        AircraftCategory category,
        AircraftConstructionStandard constructionStandard)
    {
        var option = constructionStandard switch
        {
            AircraftConstructionStandard.Ultralight => AircraftConstructionStandardOptions.Ultralight,
            AircraftConstructionStandard.Civil => AircraftConstructionStandardOptions.Civil,
            AircraftConstructionStandard.ArmyGrade => AircraftConstructionStandardOptions.ArmyGrade,
            _ => AircraftConstructionStandardOptions.None,
        };

        return (GetPolicy(category).AllowedConstructionStandards & option) != 0;
    }

    public static string GetConstructionDescription(AircraftCategory category) =>
        GetPolicy(category).AllowedConstructionStandards switch
        {
            AircraftConstructionStandardOptions.Ultralight => "Ultralight construction",
            AircraftConstructionStandardOptions.Civil => "Civil construction",
            AircraftConstructionStandardOptions.ArmyGrade => "Army-grade construction",
            CivilOrArmy => "Civil or army-grade construction",
            AircraftConstructionStandardOptions.Any => "Any construction",
            _ => "No construction allowed",
        };

    public static string GetRoleDescription(AircraftCategory category) =>
        GetPolicy(category).RoleRequirement switch
        {
            AircraftCategoryRoleRequirement.Unrestricted => "Any role module",
            AircraftCategoryRoleRequirement.NoRoleModule => "No role module",
            AircraftCategoryRoleRequirement.PassengerCabin => "Passenger cabin",
            AircraftCategoryRoleRequirement.CargoSpace => "Cargo space",
            AircraftCategoryRoleRequirement.SpecialEquipment => "Special equipment",
            AircraftCategoryRoleRequirement.CombatEquipment => "Combat equipment",
            AircraftCategoryRoleRequirement.MilitarySupport => "Combat equipment or cargo space",
            _ => "Unknown role requirement",
        };
}
