using System;
using WrightTycoon.Core.Balance;
using System.Collections.Generic;
using WrightTycoon.Core.Units;

namespace WrightTycoon.Core.Aircraft;

public sealed record AircraftPerformanceReport(
    IReadOnlyList<AircraftPerformancePoint> MaxSpeedByAltitude,
    double RangeKilometers,
    Mass PayloadCapacity,
    double TotalProductionCost,
    double TotalProductionManDays,
    double OverallReliability,
    Mass TotalWeight,
    double MaximumTakeoffWeightKilograms = 0d,
    double AircraftWeightWithFuelKilograms = 0d,
    double EmptyAircraftWeightKilograms = 0d,
    double AircraftLengthMeters = 0d,
    double AircraftFrontalAreaSquareMeters = 0d,
    double TotalDragAreaSquareMeters = 0d,
    double OptimalCruiseSpeedMetersPerSecond = 0d,
    double OptimalCruiseAltitudeMeters = 0d,
    double AverageCruiseFuelBurnKilogramsPerHour = 0d,
    double FullFuelCruiseBurnKilogramsPerHour = 0d,
    double EmptyFuelCruiseBurnKilogramsPerHour = 0d)
{
    public int PassengerCapacity => GetPassengerCapacity(PlaneDesignerBalance.Categories.PayloadKilogramsPerPassenger);

    public int GetPassengerCapacity(double kilogramsPerPassenger)
    {
        if (double.IsNaN(kilogramsPerPassenger) || double.IsInfinity(kilogramsPerPassenger) || kilogramsPerPassenger <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(kilogramsPerPassenger), "Kilograms per passenger must be finite and greater than zero.");
        }

        var payload = System.Math.Max(0d, PayloadCapacity.Kilograms);
        return (int)System.Math.Floor(payload / kilogramsPerPassenger);
    }
}

