using System;
using WrightTycoon.Core.Math;

namespace WrightTycoon.Core.Engines;

public sealed class EngineDefinition
{
    public string Id { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public EngineType EngineType { get; init; } = EngineType.Piston;

    public double BaseVolumetricEfficiency { get; init; } = 0.85d;

    public double BasePowerPerLiterKilowatts { get; init; } = 35d;

    public double BaseJetThrustPerSquareMeterNewtons { get; init; } = 95000d;

    public double PropFuelConsumptionPerKilowattHour { get; init; } = 0.24d;

    public double JetFuelConsumptionPerKiloNewtonHour { get; init; } = 72d;

    public double BaseReliability { get; init; } = 0.84d;

    public double BaseWeightKilograms { get; init; } = 200d;

    public double WeightPerDisplacementLiter { get; init; } = 11d;

    public double WeightPerTurbineSquareMeter { get; init; } = 360d;

    public double BaseCost { get; init; } = 25000d;

    public double CostPerOutputUnit { get; init; } = 90d;

    public double BaseProductionManDays { get; init; } = 52d;

    public double ComplexityFactor { get; init; } = 1d;

    public int AvailableFromYear { get; init; } = 1;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id))
        {
            throw new ArgumentException("Engine definition id cannot be empty.", nameof(Id));
        }

        if (string.IsNullOrWhiteSpace(DisplayName))
        {
            throw new ArgumentException("Engine definition display name cannot be empty.", nameof(DisplayName));
        }

        Numeric.ThrowIfNegative(BaseVolumetricEfficiency, nameof(BaseVolumetricEfficiency));
        Numeric.ThrowIfNegative(BasePowerPerLiterKilowatts, nameof(BasePowerPerLiterKilowatts));
        Numeric.ThrowIfNegative(BaseJetThrustPerSquareMeterNewtons, nameof(BaseJetThrustPerSquareMeterNewtons));
        Numeric.ThrowIfNegative(PropFuelConsumptionPerKilowattHour, nameof(PropFuelConsumptionPerKilowattHour));
        Numeric.ThrowIfNegative(JetFuelConsumptionPerKiloNewtonHour, nameof(JetFuelConsumptionPerKiloNewtonHour));
        Numeric.ThrowIfNegative(BaseReliability, nameof(BaseReliability));
        Numeric.ThrowIfNegative(BaseWeightKilograms, nameof(BaseWeightKilograms));
        Numeric.ThrowIfNegative(WeightPerDisplacementLiter, nameof(WeightPerDisplacementLiter));
        Numeric.ThrowIfNegative(WeightPerTurbineSquareMeter, nameof(WeightPerTurbineSquareMeter));
        Numeric.ThrowIfNegative(BaseCost, nameof(BaseCost));
        Numeric.ThrowIfNegative(CostPerOutputUnit, nameof(CostPerOutputUnit));
        Numeric.ThrowIfNegative(BaseProductionManDays, nameof(BaseProductionManDays));
        Numeric.ThrowIfNegative(ComplexityFactor, nameof(ComplexityFactor));
        if (AvailableFromYear < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(AvailableFromYear), "Available-from year must be greater than or equal to 1.");
        }
    }
}
