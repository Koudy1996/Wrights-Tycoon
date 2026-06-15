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
        if (aircraft is null)
        {
            throw new ArgumentNullException(nameof(aircraft));
        }

        var profiles = AircraftCategoryCatalog.GetAllProfiles();
        var results = new List<AircraftCategoryQualificationResult>(profiles.Count);
        foreach (var profile in profiles)
        {
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
}

public sealed record AircraftCategoryQualificationResult(
    AircraftCategory Category,
    string DisplayName,
    bool IsQualified,
    IReadOnlyList<string> UnmetRequirements);
