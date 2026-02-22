using System;

namespace WrightTycoon.Core.Aircraft;

public sealed class AircraftTypeDefinition
{
    public string Id { get; init; } = string.Empty;

    public string FamilyId { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public EngineParameterDelta EngineDelta { get; init; } = new();

    public AirframeParameterDelta AirframeDelta { get; init; } = new();

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
    }
}
