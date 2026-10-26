using System;
using WrightTycoon.Core.Categories;
using WrightTycoon.Core.Math;

namespace WrightTycoon.Core.Contracts;

public sealed class ContractRequirements
{
    private ContractEconomicSnapshot? _economicSnapshot;
    private double _maximumOperatingCostPerUnit;
    public ContractEconomicSnapshot? EconomicSnapshot { get => _economicSnapshot; init => _economicSnapshot = value; }
    public int PricingModelVersion => EconomicSnapshot?.ModelVersion ?? 0;
    public ContractRequirements WithEconomics(ContractEconomicSnapshot economic)
    {
        var copy = (ContractRequirements)MemberwiseClone();
        copy._economicSnapshot = economic;
        copy._maximumOperatingCostPerUnit = economic.AllowedOperationCostPerHour * MinimumExpectedLifeHours;
        copy.Validate();
        return copy;
    }
    public double MaximumOperationCostPerHour => MinimumExpectedLifeHours > 0d ? MaximumOperatingCostPerUnit / MinimumExpectedLifeHours : 0d;
    public int LifecycleModelVersion { get; init; }
    public int MaintenanceModelVersion { get; init; } = 2;
    public double MinimumExpectedLifeHours { get; init; }
    // Persisted legacy name; this is the minimum acceptable time BETWEEN services.
    public int MaximumServiceIntervalHours { get; init; } = 1000;
    public int MinimumServiceIntervalHours => MaximumServiceIntervalHours;
    public int MinimumSupportMonths { get; init; } = 120;
    public double UtilizationHoursPerMonth { get; init; } = 100d;
    public double AverageFlightHours { get; init; } = 2d;
    public string OperatingProfileId { get; init; } = "civil";
    public WrightTycoon.Core.Contracts.ProcurementPreference ProcurementPreference { get; init; }
    public double MaximumOperatingCostPerUnit { get => _maximumOperatingCostPerUnit; init => _maximumOperatingCostPerUnit = value; }
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
        + (LifecycleModelVersion > 0 ? 3 + (PricingModelVersion > 0 || ProcurementPreference == ProcurementPreference.WholeLifeCost ? 1 : 0) : 0)
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
        if (EconomicSnapshot is { } economic)
        {
            economic.Validate();
            if (LifecycleModelVersion <= 0 || System.Math.Abs(MaximumOperationCostPerHour - economic.AllowedOperationCostPerHour) > .000001d)
                throw new InvalidOperationException("Contract operating limit differs from its economic snapshot.");
        }
        if (MaintenanceModelVersion != 2)
            throw new ArgumentOutOfRangeException(nameof(MaintenanceModelVersion));
        if (LifecycleModelVersion > 0)
        {
            WrightTycoon.Core.Contracts.LifecycleContractPolicy.Validate(MinimumExpectedLifeHours, MaximumServiceIntervalHours,
                MinimumSupportMonths, UtilizationHoursPerMonth, AverageFlightHours, OperatingProfileId, MaximumOperatingCostPerUnit);
        }
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
