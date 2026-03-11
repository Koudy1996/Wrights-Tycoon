using System;
using WrightTycoon.Core.Categories;
using WrightTycoon.Core.Math;

namespace WrightTycoon.Core.Contracts;

public sealed class ContractRequirements
{
    public AircraftCategory? RequiredCategory { get; init; }

    public double? MinimumReliability { get; init; }

    public double? MaximumTotalWeightKilograms { get; init; }

    public double? MinimumRangeKilometers { get; init; }

    public ContractSpeedRequirement? MinimumSpeed { get; init; }

    public double? MinimumPayloadKilograms { get; init; }

    public double? MinimumManeuverability { get; init; }

    public int? MinimumCrew { get; init; }

    public double? MaximumRequiredRunwayLengthMeters { get; init; }

    public double? MaximumFuelConsumptionKilogramsPerHour { get; init; }

    public int DefinedRequirementCount =>
        CountIfDefined(MinimumReliability.HasValue)
        + CountIfDefined(MaximumTotalWeightKilograms.HasValue)
        + CountIfDefined(MinimumRangeKilometers.HasValue)
        + CountIfDefined(MinimumSpeed.HasValue)
        + CountIfDefined(MinimumPayloadKilograms.HasValue)
        + CountIfDefined(MinimumManeuverability.HasValue)
        + CountIfDefined(MinimumCrew.HasValue)
        + CountIfDefined(MaximumRequiredRunwayLengthMeters.HasValue)
        + CountIfDefined(MaximumFuelConsumptionKilogramsPerHour.HasValue);

    public void Validate()
    {
        if (DefinedRequirementCount == 0)
        {
            throw new InvalidOperationException("Contract must define at least one required parameter.");
        }

        if (RequiredCategory.HasValue)
        {
            _ = AircraftCategoryCatalog.GetProfile(RequiredCategory.Value);
        }

        if (MinimumReliability.HasValue)
        {
            Numeric.ThrowIfNotFinite(MinimumReliability.Value, nameof(MinimumReliability));
            if (MinimumReliability.Value < 0d || MinimumReliability.Value > 1d)
            {
                throw new ArgumentOutOfRangeException(nameof(MinimumReliability), "Minimum reliability must be within [0, 1].");
            }
        }

        if (MaximumTotalWeightKilograms.HasValue)
        {
            Numeric.ThrowIfNotFinite(MaximumTotalWeightKilograms.Value, nameof(MaximumTotalWeightKilograms));
            if (MaximumTotalWeightKilograms.Value <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(MaximumTotalWeightKilograms), "Maximum total weight must be greater than zero.");
            }
        }

        if (MinimumRangeKilometers.HasValue)
        {
            Numeric.ThrowIfNotFinite(MinimumRangeKilometers.Value, nameof(MinimumRangeKilometers));
            if (MinimumRangeKilometers.Value <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(MinimumRangeKilometers), "Minimum range must be greater than zero.");
            }
        }

        if (MinimumSpeed.HasValue)
        {
            MinimumSpeed.Value.Validate();
        }

        if (MinimumPayloadKilograms.HasValue)
        {
            Numeric.ThrowIfNotFinite(MinimumPayloadKilograms.Value, nameof(MinimumPayloadKilograms));
            if (MinimumPayloadKilograms.Value < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(MinimumPayloadKilograms), "Minimum payload must be greater than or equal to zero.");
            }
        }

        if (MinimumManeuverability.HasValue)
        {
            Numeric.ThrowIfNotFinite(MinimumManeuverability.Value, nameof(MinimumManeuverability));
            if (MinimumManeuverability.Value < 0d || MinimumManeuverability.Value > 100d)
            {
                throw new ArgumentOutOfRangeException(nameof(MinimumManeuverability), "Minimum maneuverability must be within [0, 100].");
            }
        }

        if (MinimumCrew.HasValue && MinimumCrew.Value < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(MinimumCrew), "Minimum crew must be greater than or equal to 1.");
        }

        if (MaximumRequiredRunwayLengthMeters.HasValue)
        {
            Numeric.ThrowIfNotFinite(MaximumRequiredRunwayLengthMeters.Value, nameof(MaximumRequiredRunwayLengthMeters));
            if (MaximumRequiredRunwayLengthMeters.Value <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(MaximumRequiredRunwayLengthMeters), "Maximum runway length must be greater than zero.");
            }
        }

        if (MaximumFuelConsumptionKilogramsPerHour.HasValue)
        {
            Numeric.ThrowIfNotFinite(MaximumFuelConsumptionKilogramsPerHour.Value, nameof(MaximumFuelConsumptionKilogramsPerHour));
            if (MaximumFuelConsumptionKilogramsPerHour.Value <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(MaximumFuelConsumptionKilogramsPerHour), "Maximum fuel consumption must be greater than zero.");
            }
        }
    }

    private static int CountIfDefined(bool isDefined) => isDefined ? 1 : 0;
}
