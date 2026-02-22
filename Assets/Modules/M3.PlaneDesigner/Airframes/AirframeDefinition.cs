using System;
using WrightTycoon.Core.Math;

namespace WrightTycoon.Core.Airframes;

public sealed class AirframeDefinition
{
    public string Id { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public double BaseStructuralWeightKilograms { get; init; } = 900d;

    public double ReferenceWingAreaSquareMeters { get; init; } = 30d;

    public double BaseDragCoefficient { get; init; } = 0.03d;

    public double BaseLiftCoefficient { get; init; } = 1.08d;

    public double BaseReliabilityModifier { get; init; } = 1.0d;

    public double BaseCost { get; init; } = 52000d;

    public double BaseProductionManDays { get; init; } = 110d;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id))
        {
            throw new ArgumentException("Airframe definition id cannot be empty.", nameof(Id));
        }

        if (string.IsNullOrWhiteSpace(DisplayName))
        {
            throw new ArgumentException("Airframe definition display name cannot be empty.", nameof(DisplayName));
        }

        Numeric.ThrowIfNegative(BaseStructuralWeightKilograms, nameof(BaseStructuralWeightKilograms));
        Numeric.ThrowIfNegative(ReferenceWingAreaSquareMeters, nameof(ReferenceWingAreaSquareMeters));
        Numeric.ThrowIfNegative(BaseDragCoefficient, nameof(BaseDragCoefficient));
        Numeric.ThrowIfNegative(BaseLiftCoefficient, nameof(BaseLiftCoefficient));
        Numeric.ThrowIfNegative(BaseReliabilityModifier, nameof(BaseReliabilityModifier));
        Numeric.ThrowIfNegative(BaseCost, nameof(BaseCost));
        Numeric.ThrowIfNegative(BaseProductionManDays, nameof(BaseProductionManDays));
    }
}
