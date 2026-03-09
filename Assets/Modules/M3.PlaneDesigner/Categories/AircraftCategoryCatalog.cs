using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using WrightTycoon.Core.Balance;

namespace WrightTycoon.Core.Categories;

public static class AircraftCategoryCatalog
{
    private static readonly IReadOnlyList<AircraftCategoryProfile> Profiles = BuildProfiles();
    private static readonly IReadOnlyDictionary<AircraftCategory, AircraftCategoryProfile> ProfilesByCategory =
        new ReadOnlyDictionary<AircraftCategory, AircraftCategoryProfile>(
            Profiles.ToDictionary(profile => profile.Category, profile => profile));

    public static IReadOnlyList<AircraftCategoryProfile> GetAllProfiles() => Profiles;

    public static AircraftCategoryProfile GetProfile(AircraftCategory category)
    {
        if (!ProfilesByCategory.TryGetValue(category, out var profile))
        {
            throw new KeyNotFoundException($"Aircraft category profile for '{category}' was not found.");
        }

        return profile;
    }

    public static AircraftCategoryProfile GetProfileForYear(
        AircraftCategory category,
        int year,
        int playerYearOffset = 0)
    {
        var baseProfile = GetProfile(category);
        return TypeRequirementBalance.CreateYearAdjustedProfile(baseProfile, year, playerYearOffset);
    }

    private static IReadOnlyList<AircraftCategoryProfile> BuildProfiles()
    {
        var profiles = PlaneDesignerBalance.CategoryEntries
            .Select(entry => entry.ToProfile())
            .ToArray();
        return Array.AsReadOnly(profiles);
    }
}

