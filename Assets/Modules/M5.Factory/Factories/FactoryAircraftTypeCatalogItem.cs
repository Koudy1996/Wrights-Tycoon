using System;
using System.Collections.Generic;
using System.Linq;
using WrightTycoon.Core.Categories;
using AircraftModel = WrightTycoon.Core.Aircraft.Aircraft;

namespace WrightTycoon.Core.Factories;

public sealed class FactoryAircraftTypeCatalogItem
{
    public FactoryAircraftTypeCatalogItem(
        string designId,
        string displayName,
        AircraftModel aircraft)
    {
        DesignId = designId ?? throw new ArgumentNullException(nameof(designId));
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        Aircraft = aircraft ?? throw new ArgumentNullException(nameof(aircraft));
        QualifiedCategories = new AircraftCategoryQualificationEvaluator().GetQualifiedCategories(Aircraft);
        Validate();
    }

    public string DesignId { get; }

    public string DisplayName { get; }

    public AircraftModel Aircraft { get; }

    public IReadOnlyList<AircraftCategory> QualifiedCategories { get; }

    public bool QualifiesForCategory(AircraftCategory category) => QualifiedCategories.Contains(category);

    public IReadOnlyList<AircraftCategory> GetQualifiedCategoriesForYear(int qualificationYear) =>
        new AircraftCategoryQualificationEvaluator().GetQualifiedCategories(Aircraft, qualificationYear);

    public bool QualifiesForCategory(AircraftCategory category, int qualificationYear) =>
        GetQualifiedCategoriesForYear(qualificationYear).Contains(category);

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(DesignId))
        {
            throw new ArgumentException("Design id cannot be empty.", nameof(DesignId));
        }

        if (string.IsNullOrWhiteSpace(DisplayName))
        {
            throw new ArgumentException("Display name cannot be empty.", nameof(DisplayName));
        }

    }
}
