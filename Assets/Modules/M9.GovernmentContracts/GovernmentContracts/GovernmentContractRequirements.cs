using System;
using WrightTycoon.Core.Categories;
using WrightTycoon.Core.Math;

namespace WrightTycoon.Core.GovernmentContracts;

public sealed class GovernmentContractRequirements
{
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
