using System;
using WrightTycoon.Core.Categories;
using WrightTycoon.Core.Math;

namespace WrightTycoon.Core.GovernmentContracts;

public readonly record struct GovernmentContractAircraftMetrics(
    AircraftCategory AircraftType,
    double MaximumSpeedLowAltitudeMetersPerSecond,
    double MaximumSpeedMediumAltitudeMetersPerSecond,
    double MaximumSpeedHighAltitudeMetersPerSecond,
    double RangeKilometers,
    double PayloadKilograms,
    double Reliability,
    double Maneuverability,
    int CrewSize,
    double RequiredRunwayLengthMeters,
    double DevelopmentCost)
{
    public double GetMaximumSpeedForBand(GovernmentContractSpeedAltitudeBand band)
    {
        return band switch
        {
            GovernmentContractSpeedAltitudeBand.Low => MaximumSpeedLowAltitudeMetersPerSecond,
            GovernmentContractSpeedAltitudeBand.Medium => MaximumSpeedMediumAltitudeMetersPerSecond,
            GovernmentContractSpeedAltitudeBand.High => MaximumSpeedHighAltitudeMetersPerSecond,
            _ => MaximumSpeedMediumAltitudeMetersPerSecond,
        };
    }

    public void Validate()
    {
        _ = AircraftCategoryCatalog.GetProfile(AircraftType);

        Numeric.ThrowIfNotFinite(MaximumSpeedLowAltitudeMetersPerSecond, nameof(MaximumSpeedLowAltitudeMetersPerSecond));
        Numeric.ThrowIfNotFinite(MaximumSpeedMediumAltitudeMetersPerSecond, nameof(MaximumSpeedMediumAltitudeMetersPerSecond));
        Numeric.ThrowIfNotFinite(MaximumSpeedHighAltitudeMetersPerSecond, nameof(MaximumSpeedHighAltitudeMetersPerSecond));
        Numeric.ThrowIfNotFinite(RangeKilometers, nameof(RangeKilometers));
        Numeric.ThrowIfNotFinite(PayloadKilograms, nameof(PayloadKilograms));
        Numeric.ThrowIfNotFinite(Reliability, nameof(Reliability));
        Numeric.ThrowIfNotFinite(Maneuverability, nameof(Maneuverability));
        Numeric.ThrowIfNotFinite(RequiredRunwayLengthMeters, nameof(RequiredRunwayLengthMeters));
        Numeric.ThrowIfNotFinite(DevelopmentCost, nameof(DevelopmentCost));

        if (MaximumSpeedLowAltitudeMetersPerSecond <= 0d
            || MaximumSpeedMediumAltitudeMetersPerSecond <= 0d
            || MaximumSpeedHighAltitudeMetersPerSecond <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumSpeedLowAltitudeMetersPerSecond), "All speed samples must be > 0.");
        }

        if (RangeKilometers <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(RangeKilometers), "Range must be > 0.");
        }

        if (PayloadKilograms < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(PayloadKilograms), "Payload must be >= 0.");
        }

        if (Reliability < 0d || Reliability > 1d)
        {
            throw new ArgumentOutOfRangeException(nameof(Reliability), "Reliability must be within [0, 1].");
        }

        if (Maneuverability < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(Maneuverability), "Maneuverability must be >= 0.");
        }

        if (CrewSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(CrewSize), "Crew size must be >= 1.");
        }

        if (RequiredRunwayLengthMeters <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(RequiredRunwayLengthMeters), "Required runway length must be > 0.");
        }

        if (DevelopmentCost < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(DevelopmentCost), "Development cost must be >= 0.");
        }
    }
}
