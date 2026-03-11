using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using WrightTycoon.Core.Aircraft;
using WrightTycoon.Core.Airframes;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Math;

namespace WrightTycoon.Core.Categories;

public sealed class CategoryComplianceEvaluator
{
    public CategoryComplianceResult Evaluate(Aircraft.Aircraft aircraft)
    {
        if (aircraft is null)
        {
            throw new ArgumentNullException(nameof(aircraft));
        }

        var profile = AircraftCategoryCatalog.GetProfile(aircraft.FamilyDefinition.Category);
        return Evaluate(aircraft, profile);
    }

    public CategoryComplianceResult Evaluate(Aircraft.Aircraft aircraft, AircraftCategoryProfile profile)
    {
        if (aircraft is null)
        {
            throw new ArgumentNullException(nameof(aircraft));
        }

        if (profile is null)
        {
            throw new ArgumentNullException(nameof(profile));
        }

        var unmet = new List<string>();

        var crew = aircraft.Airframe.Parameters.CrewCount;
        if (crew < profile.MinCrew || crew > profile.MaxCrew)
        {
            unmet.Add($"Crew {FormatUiInteger(crew)} outside [{FormatUiInteger(profile.MinCrew)}, {FormatUiInteger(profile.MaxCrew)}]");
        }

        var weight = aircraft.Performance.TotalWeight.Kilograms;
        if (weight < profile.MinMaximumWeightKilograms || weight > profile.MaxMaximumWeightKilograms)
        {
            unmet.Add($"Weight {FormatUiNumber(weight, "0.0")} kg outside [{FormatUiNumber(profile.MinMaximumWeightKilograms, "0.0")}, {FormatUiNumber(profile.MaxMaximumWeightKilograms, "0.0")}] kg");
        }

        if (aircraft.Airframe.Parameters.MaterialType == MaterialType.WoodFabric
            && weight - Numeric.Epsilon > PlaneDesignerBalance.Airframe.WoodFabricMaximumTotalWeightKilograms)
        {
            unmet.Add(
                $"Wood/Fabric limit exceeded: {FormatUiNumber(weight, "0.0")} kg > {FormatUiNumber(PlaneDesignerBalance.Airframe.WoodFabricMaximumTotalWeightKilograms, "0.0")} kg");
        }

        var speedMetersPerSecond = GetSpeedAtAltitudeMeters(aircraft.Performance, 0d);
        var speedKilometersPerHour = speedMetersPerSecond * 3.6d;
        if (speedKilometersPerHour < profile.MinRequiredSpeedKilometersPerHour
            || speedKilometersPerHour > profile.MaxRequiredSpeedKilometersPerHour)
        {
            unmet.Add(
                $"Speed {FormatUiNumber(speedKilometersPerHour, "0.0")} km/h outside [{FormatUiNumber(profile.MinRequiredSpeedKilometersPerHour, "0.0")}, {FormatUiNumber(profile.MaxRequiredSpeedKilometersPerHour, "0.0")}] km/h");
        }

        var range = aircraft.Performance.RangeKilometers;
        if (range < profile.MinRequiredRangeKilometers || range > profile.MaxRequiredRangeKilometers)
        {
            unmet.Add($"Range {FormatUiNumber(range, "0.0")} km outside [{FormatUiNumber(profile.MinRequiredRangeKilometers, "0.0")}, {FormatUiNumber(profile.MaxRequiredRangeKilometers, "0.0")}] km");
        }

        var payload = aircraft.Performance.PayloadCapacity.Kilograms;
        if (payload < profile.MinRequiredPayloadKilograms || payload > profile.MaxRequiredPayloadKilograms)
        {
            unmet.Add($"Payload {FormatUiNumber(payload, "0.0")} kg outside [{FormatUiNumber(profile.MinRequiredPayloadKilograms, "0.0")}, {FormatUiNumber(profile.MaxRequiredPayloadKilograms, "0.0")}] kg");
        }

        var maneuverability = aircraft.Airframe.Performance.Maneuverability;
        if (maneuverability < profile.MinRequiredManeuverability
            || maneuverability > profile.MaxRequiredManeuverability)
        {
            unmet.Add(
                $"Maneuverability {FormatUiNumber(maneuverability, "0.0")} outside [{FormatUiNumber(profile.MinRequiredManeuverability, "0.0")}, {FormatUiNumber(profile.MaxRequiredManeuverability, "0.0")}]");
        }

        if (profile.HasPassengerCapacityRequirement)
        {
            var passengers = aircraft.Performance.GetPassengerCapacity(PlaneDesignerBalance.Categories.PayloadKilogramsPerPassenger);
            var minimumPassengers = profile.GetMinimumPassengerCapacity();
            var maximumPassengers = profile.GetMaximumPassengerCapacity();
            if (passengers < minimumPassengers || passengers > maximumPassengers)
            {
                unmet.Add($"Passengers {FormatUiInteger(passengers)} outside [{FormatUiInteger(minimumPassengers)}, {FormatUiInteger(maximumPassengers)}]");
            }
        }

        return new CategoryComplianceResult(unmet.Count == 0, unmet);
    }


    private static readonly CultureInfo UiNumberCulture = CreateUiNumberCulture();

    private static CultureInfo CreateUiNumberCulture()
    {
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.NumberGroupSeparator = " ";
        culture.NumberFormat.NumberDecimalSeparator = ",";
        culture.NumberFormat.NumberGroupSizes = new[] { 3 };
        return culture;
    }

    private static string FormatUiNumber(double value, string format) =>
        value.ToString($"#,##{format}", UiNumberCulture);

    private static string FormatUiInteger(int value) =>
        value.ToString("#,##0", UiNumberCulture);
    private static double GetSpeedAtAltitudeMeters(AircraftPerformanceReport performance, double altitudeMeters)
    {
        var points = performance.MaxSpeedByAltitude
            .OrderBy(point => point.AltitudeMeters)
            .ToArray();

        if (points.Length == 0)
        {
            return 0d;
        }

        for (var index = 0; index < points.Length; index++)
        {
            if (Numeric.AlmostEqual(points[index].AltitudeMeters, altitudeMeters, tolerance: 1e-3))
            {
                return points[index].MaxSpeedMetersPerSecond;
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
            if (span <= Numeric.Epsilon)
            {
                return right.MaxSpeedMetersPerSecond;
            }

            var blend = (altitudeMeters - left.AltitudeMeters) / span;
            return left.MaxSpeedMetersPerSecond + ((right.MaxSpeedMetersPerSecond - left.MaxSpeedMetersPerSecond) * blend);
        }

        return points[0].MaxSpeedMetersPerSecond;
    }
}

