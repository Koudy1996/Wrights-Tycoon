using System;

namespace WrightTycoon.Core.Aircraft;

public sealed class AircraftTypeDefinition
{
    public string Id { get; init; } = string.Empty;

    public string FamilyId { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public EngineParameterDelta EngineDelta { get; init; } = new();

    public AirframeParameterDelta AirframeDelta { get; init; } = new();

    public AircraftNavigationEquipment NavigationEquipment { get; init; } = AircraftNavigationEquipment.None;

    public bool HasPassengerCabin { get; init; }

    public bool HasCargoSpace { get; init; }

    public double WeaponPayloadKilograms { get; init; }

    public bool HasSpecialEquipment { get; init; }

    public bool HasCombatEquipment { get; init; }

    public TypeResearchModifierPoints ResearchModifiers { get; init; } = new();

    public int RequiredLineToolLevel { get; init; } = 1;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id))
        {
            throw new ArgumentException("Type id cannot be empty.", nameof(Id));
        }

        if (string.IsNullOrWhiteSpace(FamilyId))
        {
            throw new ArgumentException("Type family id cannot be empty.", nameof(FamilyId));
        }

        if (string.IsNullOrWhiteSpace(DisplayName))
        {
            throw new ArgumentException("Type display name cannot be empty.", nameof(DisplayName));
        }

        if (RequiredLineToolLevel < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(RequiredLineToolLevel), "Required line tool level must be >= 1.");
        }

        if (!Enum.IsDefined(typeof(AircraftNavigationEquipment), NavigationEquipment))
        {
            throw new ArgumentOutOfRangeException(nameof(NavigationEquipment), "Navigation equipment must be a defined value.");
        }

        if (WeaponPayloadKilograms < 0d || double.IsNaN(WeaponPayloadKilograms) || double.IsInfinity(WeaponPayloadKilograms))
        {
            throw new ArgumentOutOfRangeException(nameof(WeaponPayloadKilograms), "Weapon payload must be a finite value >= 0.");
        }

        if (ResearchModifiers is null)
        {
            throw new ArgumentNullException(nameof(ResearchModifiers), "Research modifiers cannot be null.");
        }

        ResearchModifiers.Validate();
    }
}

public enum AircraftNavigationEquipment
{
    None = 0,
    BasicNavigationSuite = 1,
    AdvancedNavigationSuite = 2,
}

public sealed class TypeResearchModifierPoints
{
    public const int MaxPointsPerCategory = 10;

    public int ReliabilityPoints { get; set; }

    public int ProductionOptimizationPoints { get; set; }

    public int FastDevelopmentPoints { get; set; }

    public TypeResearchModifierPoints Clone() =>
        new()
        {
            ReliabilityPoints = ReliabilityPoints,
            ProductionOptimizationPoints = ProductionOptimizationPoints,
            FastDevelopmentPoints = FastDevelopmentPoints,
        };

    public void Validate()
    {
        ThrowIfOutOfRange(ReliabilityPoints, nameof(ReliabilityPoints));
        ThrowIfOutOfRange(ProductionOptimizationPoints, nameof(ProductionOptimizationPoints));
        ThrowIfOutOfRange(FastDevelopmentPoints, nameof(FastDevelopmentPoints));
    }

    public static int ClampPoints(int points)
    {
        if (points < 0)
        {
            return 0;
        }

        if (points > MaxPointsPerCategory)
        {
            return MaxPointsPerCategory;
        }

        return points;
    }

    private static void ThrowIfOutOfRange(int points, string name)
    {
        if (points < 0 || points > MaxPointsPerCategory)
        {
            throw new ArgumentOutOfRangeException(name, $"Points must be within [0, {MaxPointsPerCategory}].");
        }
    }
}
