using System;
using WrightTycoon.Core.Categories;
using WrightTycoon.Core.Math;

namespace WrightTycoon.Core.GovernmentContracts;

public sealed class GovernmentContractRequirements
{
    private WrightTycoon.Core.Contracts.ContractEconomicSnapshot? _economicSnapshot;
    private double _maximumOperatingCostPerUnit;
    public WrightTycoon.Core.Contracts.ContractEconomicSnapshot? EconomicSnapshot { get => _economicSnapshot; init => _economicSnapshot = value; }
    public int PricingModelVersion => EconomicSnapshot?.ModelVersion ?? 0;
    public GovernmentContractRequirements WithEconomics(WrightTycoon.Core.Contracts.ContractEconomicSnapshot economic)
    {
        var copy = (GovernmentContractRequirements)MemberwiseClone();
        copy._economicSnapshot = economic;
        copy._maximumOperatingCostPerUnit = economic.AllowedOperationCostPerHour * MinimumExpectedLifeHours;
        copy.Validate();
        return copy;
    }

    public WrightTycoon.Core.Contracts.ContractRequirements AsCivilRequirements() => new()
    {
        EconomicSnapshot = EconomicSnapshot,
        RequiredCategory = RequiredAircraftType, LifecycleModelVersion = LifecycleModelVersion,
        MinimumExpectedLifeHours = MinimumExpectedLifeHours, MaximumServiceIntervalHours = MaximumServiceIntervalHours,
        MinimumSupportMonths = MinimumSupportMonths, UtilizationHoursPerMonth = UtilizationHoursPerMonth,
        AverageFlightHours = AverageFlightHours, OperatingProfileId = OperatingProfileId,
        ProcurementPreference = ProcurementPreference, MaximumOperatingCostPerUnit = MaximumOperatingCostPerUnit,
        MinimumReliability = MinimumReliability, MinimumRangeKilometers = MinimumRangeKilometers,
        MinimumSpeed = new WrightTycoon.Core.Contracts.ContractSpeedRequirement(SpeedAltitudeBand.ToAltitudeMeters(), MinimumMaximumSpeedMetersPerSecond),
        MinimumPayloadKilograms = MinimumPayloadKilograms, MinimumManeuverability = MinimumManeuverability,
        MinimumCrew = MinimumCrewSize, MaximumRequiredRunwayLengthMeters = MaximumRequiredRunwayLengthMeters,
    };
    public double MaximumOperationCostPerHour => MinimumExpectedLifeHours > 0d ? MaximumOperatingCostPerUnit / MinimumExpectedLifeHours : 0d;
    public int LifecycleModelVersion { get; init; }
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
    public AircraftCategory RequiredAircraftType { get; init; } = AircraftCategory.LightSportAndRecreational;

    public GovernmentContractSpeedAltitudeBand SpeedAltitudeBand { get; init; } = GovernmentContractSpeedAltitudeBand.Medium;

    public double MinimumMaximumSpeedMetersPerSecond { get; init; }

    public double MinimumRangeKilometers { get; init; }

    public double MinimumPayloadKilograms { get; init; }

    public double MinimumReliability { get; init; }

    public double MinimumManeuverability { get; init; }

    public int MinimumCrewSize { get; init; }

    public double MaximumRequiredRunwayLengthMeters { get; init; }

    public void Validate()
    {
        if (EconomicSnapshot is { } economic)
        {
            economic.Validate();
            if (LifecycleModelVersion <= 0 || System.Math.Abs(MaximumOperationCostPerHour - economic.AllowedOperationCostPerHour) > .000001d)
                throw new InvalidOperationException("Government operating limit differs from its economic snapshot.");
        }
        if (LifecycleModelVersion > 0)
        {
            WrightTycoon.Core.Contracts.LifecycleContractPolicy.Validate(MinimumExpectedLifeHours, MaximumServiceIntervalHours,
                MinimumSupportMonths, UtilizationHoursPerMonth, AverageFlightHours, OperatingProfileId, MaximumOperatingCostPerUnit);
        }
        _ = AircraftCategoryCatalog.GetProfile(RequiredAircraftType);

        Numeric.ThrowIfNotFinite(MinimumMaximumSpeedMetersPerSecond, nameof(MinimumMaximumSpeedMetersPerSecond));
        Numeric.ThrowIfNotFinite(MinimumRangeKilometers, nameof(MinimumRangeKilometers));
        Numeric.ThrowIfNotFinite(MinimumPayloadKilograms, nameof(MinimumPayloadKilograms));
        Numeric.ThrowIfNotFinite(MinimumReliability, nameof(MinimumReliability));
        Numeric.ThrowIfNotFinite(MinimumManeuverability, nameof(MinimumManeuverability));
        Numeric.ThrowIfNotFinite(MaximumRequiredRunwayLengthMeters, nameof(MaximumRequiredRunwayLengthMeters));

        if (MinimumMaximumSpeedMetersPerSecond <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(MinimumMaximumSpeedMetersPerSecond), "Minimum speed must be > 0.");
        }

        if (MinimumRangeKilometers <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(MinimumRangeKilometers), "Minimum range must be > 0.");
        }

        if (MinimumPayloadKilograms < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(MinimumPayloadKilograms), "Minimum payload must be >= 0.");
        }

        if (MinimumReliability < 0d || MinimumReliability > 1d)
        {
            throw new ArgumentOutOfRangeException(nameof(MinimumReliability), "Minimum reliability must be within [0, 1].");
        }

        if (MinimumManeuverability < 0d || MinimumManeuverability > 100d)
        {
            throw new ArgumentOutOfRangeException(nameof(MinimumManeuverability), "Minimum maneuverability must be within [0, 100].");
        }

        if (MinimumCrewSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(MinimumCrewSize), "Minimum crew size must be >= 1.");
        }

        if (MaximumRequiredRunwayLengthMeters <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumRequiredRunwayLengthMeters), "Maximum runway length must be > 0.");
        }
    }
}
