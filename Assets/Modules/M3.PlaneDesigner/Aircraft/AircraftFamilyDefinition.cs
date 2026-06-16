using System;
using WrightTycoon.Core.Airframes;
using WrightTycoon.Core.Campaign;
using WrightTycoon.Core.Categories;
using WrightTycoon.Core.Engines;
using WrightTycoon.Core.Math;

namespace WrightTycoon.Core.Aircraft;

public sealed class AircraftFamilyDefinition
{
    public string Id { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string EngineDefinitionId { get; init; } = string.Empty;

    public string AirframeDefinitionId { get; init; } = string.Empty;

    public EngineType LockedEngineType { get; init; } = EngineType.Piston;

    public AircraftAllowedEngineFamily AllowedEngineFamily { get; init; } = AircraftAllowedEngineFamily.Any;

    public AircraftConstructionStandard ConstructionStandard { get; init; } = AircraftConstructionStandard.Civil;

    public AircraftEngineClassification EngineClassification { get; init; } = AircraftEngineClassification.Any;

    public StructureType LockedStructureType { get; init; } = StructureType.Truss;

    public MaterialType LockedMaterialType { get; init; } = MaterialType.WoodFabric;

    public int LockedEngineCount { get; init; } = 1;

    public double? ReferenceWingAreaSquareMeters { get; init; }

    public AircraftWingAspectRatio WingAspectRatio { get; init; } = AircraftWingAspectRatio.HighAspectRatio;

    public WingShape? LockedWingShape { get; init; }

    public WingPosition? LockedWingPosition { get; init; }

    public AircraftCategory Category { get; init; } = AircraftCategory.LightSportAndRecreational;

    public int RequiredFactoryToolLevel { get; init; } = 1;

    public HistoricalCampaignTechnologyModifiers? InstalledEngineTechnologyModifiers { get; init; }

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

        if (!Enum.IsDefined(typeof(AircraftAllowedEngineFamily), AllowedEngineFamily))
        {
            throw new ArgumentOutOfRangeException(nameof(AllowedEngineFamily), "Allowed engine family must be a defined value.");
        }

        if (!Enum.IsDefined(typeof(AircraftConstructionStandard), ConstructionStandard))
        {
            throw new ArgumentOutOfRangeException(nameof(ConstructionStandard), "Construction standard must be a defined value.");
        }

        if (!Enum.IsDefined(typeof(AircraftEngineClassification), EngineClassification))
        {
            throw new ArgumentOutOfRangeException(nameof(EngineClassification), "Engine classification must be a defined value.");
        }

        if (!Enum.IsDefined(typeof(AircraftWingAspectRatio), WingAspectRatio))
        {
            throw new ArgumentOutOfRangeException(nameof(WingAspectRatio), "Wing aspect ratio must be a defined value.");
        }

        if (ReferenceWingAreaSquareMeters.HasValue)
        {
            Numeric.ThrowIfNotFinite(ReferenceWingAreaSquareMeters.Value, nameof(ReferenceWingAreaSquareMeters));
            if (ReferenceWingAreaSquareMeters.Value < 1d || ReferenceWingAreaSquareMeters.Value > 1000d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(ReferenceWingAreaSquareMeters),
                    "Reference wing area must be within [1, 1000] m2.");
            }
        }

        if (LockedWingShape.HasValue && !Enum.IsDefined(typeof(WingShape), LockedWingShape.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(LockedWingShape), "Locked wing shape must be a defined value.");
        }

        if (LockedWingPosition.HasValue && !Enum.IsDefined(typeof(WingPosition), LockedWingPosition.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(LockedWingPosition), "Locked wing position must be a defined value.");
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

    public AircraftFamilyDefinition WithInstalledEngineTechnologyModifiers(
        HistoricalCampaignTechnologyModifiers? modifiers) =>
        new()
        {
            Id = Id,
            DisplayName = DisplayName,
            EngineDefinitionId = EngineDefinitionId,
            AirframeDefinitionId = AirframeDefinitionId,
            LockedEngineType = LockedEngineType,
            AllowedEngineFamily = AllowedEngineFamily,
            ConstructionStandard = ConstructionStandard,
            EngineClassification = EngineClassification,
            LockedStructureType = LockedStructureType,
            LockedMaterialType = LockedMaterialType,
            LockedEngineCount = LockedEngineCount,
            ReferenceWingAreaSquareMeters = ReferenceWingAreaSquareMeters,
            WingAspectRatio = WingAspectRatio,
            LockedWingShape = LockedWingShape,
            LockedWingPosition = LockedWingPosition,
            Category = Category,
            RequiredFactoryToolLevel = RequiredFactoryToolLevel,
            InstalledEngineTechnologyModifiers = modifiers.HasValue
                ? HistoricalCampaignTechnologyModifiers.Sanitize(modifiers.Value)
                : null,
            BaseEngineParameters = BaseEngineParameters.Clone(),
            BaseAirframeParameters = BaseAirframeParameters.Clone(),
            BaseDevelopmentCost = BaseDevelopmentCost,
            BaseDevelopmentManDays = BaseDevelopmentManDays,
        };
}

public enum AircraftConstructionStandard
{
    Ultralight = 0,
    Civil = 1,
    ArmyGrade = 2,
}

public enum AircraftAllowedEngineFamily
{
    Any = 0,
    Propeller = 1,
    Jet = 2,
}

public enum AircraftEngineClassification
{
    Any = 0,
    Piston = 1,
    Jet = 2,
}

public enum AircraftWingAspectRatio
{
    HighAspectRatio = 0,
    LowAspectRatio = 1,
}

public static class AircraftEngineFamilyCompatibility
{
    public static bool IsEngineTypeAllowed(AircraftAllowedEngineFamily allowedFamily, EngineType engineType)
    {
        return allowedFamily switch
        {
            AircraftAllowedEngineFamily.Any => true,
            AircraftAllowedEngineFamily.Propeller => IsPropellerEngine(engineType),
            AircraftAllowedEngineFamily.Jet => IsJetEngine(engineType),
            _ => false,
        };
    }

    public static AircraftAllowedEngineFamily GetFamilyForEngineType(EngineType engineType) =>
        IsJetEngine(engineType) ? AircraftAllowedEngineFamily.Jet : AircraftAllowedEngineFamily.Propeller;

    private static bool IsPropellerEngine(EngineType engineType)
    {
        return engineType == EngineType.Piston
            || engineType == EngineType.Inline
            || engineType == EngineType.V
            || engineType == EngineType.Radial;
    }

    private static bool IsJetEngine(EngineType engineType)
    {
        return engineType == EngineType.Jet
            || engineType == EngineType.Turbojet
            || engineType == EngineType.Turbofan;
    }
}

public static class AircraftEngineClassificationCompatibility
{
    public static bool IsEngineTypeAllowed(AircraftEngineClassification classification, EngineType engineType)
    {
        return classification switch
        {
            AircraftEngineClassification.Any => true,
            AircraftEngineClassification.Piston => IsPistonEngine(engineType),
            AircraftEngineClassification.Jet => IsJetEngine(engineType),
            _ => false,
        };
    }

    private static bool IsPistonEngine(EngineType engineType)
    {
        return engineType == EngineType.Piston
            || engineType == EngineType.Inline
            || engineType == EngineType.V
            || engineType == EngineType.Radial;
    }

    private static bool IsJetEngine(EngineType engineType)
    {
        return engineType == EngineType.Jet
            || engineType == EngineType.Turbojet
            || engineType == EngineType.Turbofan;
    }
}
