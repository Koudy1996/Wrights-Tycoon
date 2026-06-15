using System;
using System.Collections.Generic;
using System.Linq;
using WrightTycoon.Core.Categories;
using WrightTycoon.Core.Math;

namespace WrightTycoon.Core.GovernmentContracts;

public readonly struct GovernmentContractAircraftMetrics
{
    public GovernmentContractAircraftMetrics(
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
        : this(
            AircraftType,
            MaximumSpeedLowAltitudeMetersPerSecond,
            MaximumSpeedMediumAltitudeMetersPerSecond,
            MaximumSpeedHighAltitudeMetersPerSecond,
            RangeKilometers,
            PayloadKilograms,
            Reliability,
            Maneuverability,
            CrewSize,
            RequiredRunwayLengthMeters,
            DevelopmentCost,
            new[] { AircraftType })
    {
    }

    public GovernmentContractAircraftMetrics(
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
        double DevelopmentCost,
        IReadOnlyList<AircraftCategory>? QualifiedAircraftTypes)
    {
        this.AircraftType = AircraftType;
        this.MaximumSpeedLowAltitudeMetersPerSecond = MaximumSpeedLowAltitudeMetersPerSecond;
        this.MaximumSpeedMediumAltitudeMetersPerSecond = MaximumSpeedMediumAltitudeMetersPerSecond;
        this.MaximumSpeedHighAltitudeMetersPerSecond = MaximumSpeedHighAltitudeMetersPerSecond;
        this.RangeKilometers = RangeKilometers;
        this.PayloadKilograms = PayloadKilograms;
        this.Reliability = Reliability;
        this.Maneuverability = Maneuverability;
        this.CrewSize = CrewSize;
        this.RequiredRunwayLengthMeters = RequiredRunwayLengthMeters;
        this.DevelopmentCost = DevelopmentCost;
        this.QualifiedAircraftTypes = QualifiedAircraftTypes is null || QualifiedAircraftTypes.Count == 0
            ? new[] { AircraftType }
            : QualifiedAircraftTypes.Distinct().ToArray();
    }

    public AircraftCategory AircraftType { get; }

    public double MaximumSpeedLowAltitudeMetersPerSecond { get; }

    public double MaximumSpeedMediumAltitudeMetersPerSecond { get; }

    public double MaximumSpeedHighAltitudeMetersPerSecond { get; }

    public double RangeKilometers { get; }

    public double PayloadKilograms { get; }

    public double Reliability { get; }

    public double Maneuverability { get; }

    public int CrewSize { get; }

    public double RequiredRunwayLengthMeters { get; }

    public double DevelopmentCost { get; }

    public IReadOnlyList<AircraftCategory> QualifiedAircraftTypes { get; }

    public bool QualifiesAs(AircraftCategory category) =>
        QualifiedAircraftTypes is not null && QualifiedAircraftTypes.Contains(category);

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

        if (QualifiedAircraftTypes is null || QualifiedAircraftTypes.Count == 0)
        {
            throw new ArgumentException("At least one qualified aircraft type must be provided.", nameof(QualifiedAircraftTypes));
        }

        for (var index = 0; index < QualifiedAircraftTypes.Count; index++)
        {
            _ = AircraftCategoryCatalog.GetProfile(QualifiedAircraftTypes[index]);
        }
    }
}
