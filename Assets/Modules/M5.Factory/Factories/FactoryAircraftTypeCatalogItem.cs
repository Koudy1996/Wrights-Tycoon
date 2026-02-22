using System;
using WrightTycoon.Core.Categories;
using AircraftModel = WrightTycoon.Core.Aircraft.Aircraft;

namespace WrightTycoon.Core.Factories;

public sealed class FactoryAircraftTypeCatalogItem
{
    public FactoryAircraftTypeCatalogItem(
        string designId,
        string displayName,
        AircraftModel aircraft,
        int requiredFactoryToolLevel,
        int requiredLineToolLevel)
    {
        DesignId = designId ?? throw new ArgumentNullException(nameof(designId));
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        Aircraft = aircraft ?? throw new ArgumentNullException(nameof(aircraft));
        RequiredFactoryToolLevel = requiredFactoryToolLevel;
        RequiredLineToolLevel = requiredLineToolLevel;

        Validate();
    }

    public string DesignId { get; }

    public string DisplayName { get; }

    public AircraftModel Aircraft { get; }

    public AircraftCategory Category => Aircraft.FamilyDefinition.Category;

    public int RequiredFactoryToolLevel { get; }

    public int RequiredLineToolLevel { get; }

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

        if (RequiredFactoryToolLevel < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(RequiredFactoryToolLevel), "Required factory tool level must be >= 1.");
        }

        if (RequiredLineToolLevel < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(RequiredLineToolLevel), "Required line tool level must be >= 1.");
        }
    }
}
