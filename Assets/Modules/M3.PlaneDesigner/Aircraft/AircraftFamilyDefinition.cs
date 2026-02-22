using System;
using WrightTycoon.Core.Airframes;
using WrightTycoon.Core.Categories;
using WrightTycoon.Core.Engines;

namespace WrightTycoon.Core.Aircraft;

public sealed class AircraftFamilyDefinition
{
    public string Id { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string EngineDefinitionId { get; init; } = string.Empty;

    public string AirframeDefinitionId { get; init; } = string.Empty;

    public EngineType LockedEngineType { get; init; } = EngineType.Piston;

    public StructureType LockedStructureType { get; init; } = StructureType.Truss;

    public MaterialType LockedMaterialType { get; init; } = MaterialType.WoodFabric;

    public int LockedEngineCount { get; init; } = 1;

    public AircraftCategory Category { get; init; } = AircraftCategory.LightSportAndRecreational;

    public int RequiredFactoryToolLevel { get; init; } = 1;

    public EngineParameters BaseEngineParameters { get; init; } = new();

    public AirframeParameters BaseAirframeParameters { get; init; } = new();

    public double BaseDevelopmentCost { get; init; } = 85000d;

    public double BaseDevelopmentManDays { get; init; } = 230d;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id))
        {
            throw new ArgumentException("Family id cannot be empty.", nameof(Id));
        }

        if (string.IsNullOrWhiteSpace(DisplayName))
        {
            throw new ArgumentException("Family display name cannot be empty.", nameof(DisplayName));
        }

        if (string.IsNullOrWhiteSpace(EngineDefinitionId))
        {
            throw new ArgumentException("Family engine definition id cannot be empty.", nameof(EngineDefinitionId));
        }

        if (string.IsNullOrWhiteSpace(AirframeDefinitionId))
        {
            throw new ArgumentException("Family airframe definition id cannot be empty.", nameof(AirframeDefinitionId));
        }

        if (LockedEngineCount < 1 || LockedEngineCount > 6)
        {
            throw new ArgumentOutOfRangeException(nameof(LockedEngineCount), "Locked engine count must be within [1, 6].");
        }

        _ = AircraftCategoryCatalog.GetProfile(Category);

        if (RequiredFactoryToolLevel < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(RequiredFactoryToolLevel), "Required factory tool level must be >= 1.");
        }

        if (BaseDevelopmentCost < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(BaseDevelopmentCost), "Base development cost must be >= 0.");
        }

        if (BaseDevelopmentManDays < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(BaseDevelopmentManDays), "Base development man-days must be >= 0.");
        }

        BaseEngineParameters.Validate();
        BaseAirframeParameters.Validate();
    }
}
