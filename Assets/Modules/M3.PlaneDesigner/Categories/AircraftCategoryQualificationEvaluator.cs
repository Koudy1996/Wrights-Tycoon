using System;
using System.Collections.Generic;
using System.Linq;

namespace WrightTycoon.Core.Categories;

public sealed class AircraftCategoryQualificationEvaluator
{
    private readonly CategoryComplianceEvaluator _complianceEvaluator;

    public AircraftCategoryQualificationEvaluator()
        : this(new CategoryComplianceEvaluator())
    {
    }

    public AircraftCategoryQualificationEvaluator(CategoryComplianceEvaluator complianceEvaluator)
    {
        _complianceEvaluator = complianceEvaluator ?? throw new ArgumentNullException(nameof(complianceEvaluator));
    }

    public IReadOnlyList<AircraftCategoryQualificationResult> EvaluateAll(Aircraft.Aircraft aircraft)
    {
        return EvaluateAll(aircraft, qualificationYear: null);
    }

    public IReadOnlyList<AircraftCategoryQualificationResult> EvaluateAll(
        Aircraft.Aircraft aircraft,
        int qualificationYear,
        int playerYearOffset = 0)
    {
        return EvaluateAll(aircraft, (int?)qualificationYear, playerYearOffset);
    }

    private IReadOnlyList<AircraftCategoryQualificationResult> EvaluateAll(
        Aircraft.Aircraft aircraft,
        int? qualificationYear,
        int playerYearOffset = 0)
    {
        if (aircraft is null)
        {
            throw new ArgumentNullException(nameof(aircraft));
        }

        var profiles = AircraftCategoryCatalog.GetAllProfiles();
        var results = new List<AircraftCategoryQualificationResult>(profiles.Count);
        foreach (var baseProfile in profiles)
        {
            var profile = qualificationYear.HasValue
                ? AircraftCategoryCatalog.GetProfileForYear(
                    baseProfile.Category,
                    qualificationYear.Value,
                    playerYearOffset)
                : baseProfile;
            var compliance = _complianceEvaluator.Evaluate(aircraft, profile);
            results.Add(new AircraftCategoryQualificationResult(
                profile.Category,
                profile.DisplayName,
                compliance.IsCompliant,
                compliance.UnmetRequirements));
        }

        return results;
    }

    public IReadOnlyList<AircraftCategory> GetQualifiedCategories(Aircraft.Aircraft aircraft)
    {
        return EvaluateAll(aircraft)
            .Where(result => result.IsQualified)
            .Select(result => result.Category)
            .ToArray();
    }

    public IReadOnlyList<AircraftCategory> GetQualifiedCategories(
        Aircraft.Aircraft aircraft,
        int qualificationYear,
        int playerYearOffset = 0)
    {
        return EvaluateAll(aircraft, qualificationYear, playerYearOffset)
            .Where(result => result.IsQualified)
            .Select(result => result.Category)
            .ToArray();
    }
}

public sealed record AircraftCategoryQualificationResult(
    AircraftCategory Category,
    string DisplayName,
    bool IsQualified,
    IReadOnlyList<string> UnmetRequirements);
