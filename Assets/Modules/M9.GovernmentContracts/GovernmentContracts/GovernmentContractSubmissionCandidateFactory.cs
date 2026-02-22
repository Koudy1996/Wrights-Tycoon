using System;
using WrightTycoon.Core.Aircraft;
using WrightTycoon.Core.Airframes;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Factories;

namespace WrightTycoon.Core.GovernmentContracts;

public static class GovernmentContractSubmissionCandidateFactory
{
    public static GovernmentContractSubmissionCandidate FromFactoryCatalogItem(
        FactoryAircraftTypeCatalogItem catalogItem,
        AircraftLifecycleStage lifecycleStage)
    {
        if (catalogItem is null)
        {
            throw new ArgumentNullException(nameof(catalogItem));
        }

        var aircraft = catalogItem.Aircraft;
        var metrics = BuildMetrics(aircraft);
        return new GovernmentContractSubmissionCandidate(
            catalogItem.DesignId,
            catalogItem.DisplayName,
            lifecycleStage,
            metrics);
    }

    public static GovernmentContractSubmissionCandidate FromAircraft(
        string designId,
        string displayName,
        AircraftLifecycleStage lifecycleStage,
        WrightTycoon.Core.Aircraft.Aircraft aircraft)
    {
        if (aircraft is null)
        {
            throw new ArgumentNullException(nameof(aircraft));
        }

        var metrics = BuildMetrics(aircraft);
        return new GovernmentContractSubmissionCandidate(
            designId,
            displayName,
            lifecycleStage,
            metrics);
    }

    private static GovernmentContractAircraftMetrics BuildMetrics(WrightTycoon.Core.Aircraft.Aircraft aircraft)
    {
        var lowSpeed = GetSpeedAtAltitude(aircraft.Performance, GovernmentContractBalance.SpeedAltitudeLowMeters);
        var mediumSpeed = GetSpeedAtAltitude(aircraft.Performance, GovernmentContractBalance.SpeedAltitudeMediumMeters);
        var highSpeed = GetSpeedAtAltitude(aircraft.Performance, GovernmentContractBalance.SpeedAltitudeHighMeters);
        var runwayLength = ResolveRunwayLengthMeters(aircraft.Airframe.Parameters.RunwayType);

        return new GovernmentContractAircraftMetrics(
            aircraft.FamilyDefinition.Category,
            lowSpeed,
            mediumSpeed,
            highSpeed,
            aircraft.Performance.RangeKilometers,
            aircraft.Performance.PayloadCapacity.Kilograms,
            aircraft.Performance.OverallReliability,
            aircraft.Airframe.Parameters.CrewCount,
            runwayLength,
            aircraft.Development.DevelopmentCost);
    }

    private static double GetSpeedAtAltitude(
        AircraftPerformanceReport performance,
        double altitudeMeters)
    {
        if (performance.MaxSpeedByAltitude is null || performance.MaxSpeedByAltitude.Count == 0)
        {
            return 0d;
        }

        var points = new AircraftPerformancePoint[performance.MaxSpeedByAltitude.Count];
        for (var index = 0; index < points.Length; index++)
        {
            points[index] = performance.MaxSpeedByAltitude[index];
        }

        Array.Sort(points, static (left, right) => left.AltitudeMeters.CompareTo(right.AltitudeMeters));

        for (var index = 0; index < points.Length; index++)
        {
            var point = points[index];
            if (System.Math.Abs(point.AltitudeMeters - altitudeMeters) <= 1e-6)
            {
                return point.MaxSpeedMetersPerSecond;
            }
        }

        if (altitudeMeters <= points[0].AltitudeMeters)
        {
            return points[0].MaxSpeedMetersPerSecond;
        }

        if (altitudeMeters >= points[points.Length - 1].AltitudeMeters)
        {
            return points[points.Length - 1].MaxSpeedMetersPerSecond;
        }

        for (var index = 1; index < points.Length; index++)
        {
            var left = points[index - 1];
            var right = points[index];
            if (altitudeMeters < left.AltitudeMeters || altitudeMeters > right.AltitudeMeters)
            {
                continue;
            }

            var span = right.AltitudeMeters - left.AltitudeMeters;
            if (span <= 1e-9)
            {
                continue;
            }

            var blend = (altitudeMeters - left.AltitudeMeters) / span;
            return left.MaxSpeedMetersPerSecond + ((right.MaxSpeedMetersPerSecond - left.MaxSpeedMetersPerSecond) * blend);
        }

        return points[points.Length - 1].MaxSpeedMetersPerSecond;
    }

    private static double ResolveRunwayLengthMeters(RunwayType runwayType)
    {
        return runwayType switch
        {
            RunwayType.Short => PlaneDesignerBalance.Airframe.RunwayShortLengthMeters,
            RunwayType.Medium => PlaneDesignerBalance.Airframe.RunwayMediumLengthMeters,
            RunwayType.Long => PlaneDesignerBalance.Airframe.RunwayLongLengthMeters,
            _ => PlaneDesignerBalance.Airframe.RunwayMediumLengthMeters,
        };
    }
}
