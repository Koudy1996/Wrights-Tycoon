using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WrightTycoon.Core.Aircraft;
using WrightTycoon.Core.Airframes;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Math;

namespace WrightTycoon.Core.Contracts;

public sealed class ContractEvaluator : IContractEvaluator
{
    public ContractEvaluationReport Evaluate(Contract contract, WrightTycoon.Core.Aircraft.Aircraft aircraft)
    {
        if (contract is null)
        {
            throw new ArgumentNullException(nameof(contract));
        }

        if (aircraft is null)
        {
            throw new ArgumentNullException(nameof(aircraft));
        }

        contract.Validate();
        var requirements = contract.Requirements;

        var unmetRequirements = new List<string>();
        if (requirements.RequiredCategory.HasValue)
        {
            var requiredCategory = requirements.RequiredCategory.Value;
            var actualCategory = aircraft.FamilyDefinition.Category;
            if (actualCategory != requiredCategory)
            {
                unmetRequirements.Add($"Category {actualCategory} != {requiredCategory}");
            }
        }

        if (requirements.MinimumReliability.HasValue)
        {
            var minimumReliability = requirements.MinimumReliability.Value;
            var actualReliability = aircraft.Performance.OverallReliability;
            if (actualReliability + Numeric.Epsilon < minimumReliability)
            {
                unmetRequirements.Add($"Reliability {FormatUiNumber(actualReliability, "0.000")} < {FormatUiNumber(minimumReliability, "0.000")}");
            }
        }

        if (requirements.MaximumTotalWeightKilograms.HasValue)
        {
            var maximumWeight = requirements.MaximumTotalWeightKilograms.Value;
            var actualWeight = aircraft.Performance.TotalWeight.Kilograms;
            if (actualWeight - Numeric.Epsilon > maximumWeight)
            {
                unmetRequirements.Add($"Weight {FormatUiNumber(actualWeight, "0.0")} kg > {FormatUiNumber(maximumWeight, "0.0")} kg");
            }
        }

        if (requirements.MinimumRangeKilometers.HasValue)
        {
            var minimumRange = requirements.MinimumRangeKilometers.Value;
            var actualRange = aircraft.Performance.RangeKilometers;
            if (actualRange + Numeric.Epsilon < minimumRange)
            {
                unmetRequirements.Add($"Range {FormatUiNumber(actualRange, "0.0")} km < {FormatUiNumber(minimumRange, "0.0")} km");
            }
        }

        if (requirements.MinimumSpeed.HasValue)
        {
            var speedRequirement = requirements.MinimumSpeed.Value;
            if (!TryGetSpeedAtAltitude(aircraft.Performance, speedRequirement.AltitudeMeters, out var actualSpeed))
            {
                unmetRequirements.Add($"No speed sample available at {FormatUiNumber(speedRequirement.AltitudeMeters, "0")} m.");
            }
            else if (actualSpeed + Numeric.Epsilon < speedRequirement.MinimumSpeedMetersPerSecond)
            {
                unmetRequirements.Add(
                    $"Speed @{FormatUiNumber(speedRequirement.AltitudeMeters, "0")}m {FormatUiNumber(actualSpeed * 3.6d, "0.0")} km/h < {FormatUiNumber(speedRequirement.MinimumSpeedMetersPerSecond * 3.6d, "0.0")} km/h");
            }
        }

        if (requirements.MinimumPayloadKilograms.HasValue)
        {
            var minimumPayload = requirements.MinimumPayloadKilograms.Value;
            var actualPayload = aircraft.Performance.PayloadCapacity.Kilograms;
            if (actualPayload + Numeric.Epsilon < minimumPayload)
            {
                unmetRequirements.Add($"Payload {FormatUiNumber(actualPayload, "0.0")} kg < {FormatUiNumber(minimumPayload, "0.0")} kg");
            }
        }

        if (requirements.MinimumCrew.HasValue)
        {
            var minimumCrew = requirements.MinimumCrew.Value;
            var actualCrew = aircraft.Airframe.Parameters.CrewCount;
            if (actualCrew < minimumCrew)
            {
                unmetRequirements.Add($"Crew {FormatUiInteger(actualCrew)} < {FormatUiInteger(minimumCrew)}");
            }
        }

        if (requirements.MaximumRequiredRunwayLengthMeters.HasValue)
        {
            var maximumRunwayLength = requirements.MaximumRequiredRunwayLengthMeters.Value;
            var actualRunwayLength = ResolveRunwayLengthMeters(aircraft.Airframe.Parameters.RunwayType);
            if (actualRunwayLength - Numeric.Epsilon > maximumRunwayLength)
            {
                unmetRequirements.Add($"Runway {FormatUiNumber(actualRunwayLength, "0.0")} m > {FormatUiNumber(maximumRunwayLength, "0.0")} m");
            }
        }

        return new ContractEvaluationReport(unmetRequirements);
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

    private static bool TryGetSpeedAtAltitude(
        AircraftPerformanceReport performance,
        double altitudeMeters,
        out double speedMetersPerSecond)
    {
        var points = performance.MaxSpeedByAltitude
            .OrderBy(point => point.AltitudeMeters)
            .ToArray();

        if (points.Length == 0)
        {
            speedMetersPerSecond = 0d;
            return false;
        }

        for (var index = 0; index < points.Length; index++)
        {
            var point = points[index];
            if (Numeric.AlmostEqual(point.AltitudeMeters, altitudeMeters, tolerance: 1e-3))
            {
                speedMetersPerSecond = point.MaxSpeedMetersPerSecond;
                return true;
            }
        }

        if (altitudeMeters < points[0].AltitudeMeters || altitudeMeters > points[points.Length - 1].AltitudeMeters)
        {
            speedMetersPerSecond = 0d;
            return false;
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
                continue;
            }

            var blend = (altitudeMeters - left.AltitudeMeters) / span;
            speedMetersPerSecond = left.MaxSpeedMetersPerSecond + ((right.MaxSpeedMetersPerSecond - left.MaxSpeedMetersPerSecond) * blend);
            return true;
        }

        speedMetersPerSecond = 0d;
        return false;
    }

    private static double ResolveRunwayLengthMeters(RunwayType runwayType) =>
        runwayType switch
        {
            RunwayType.Short => PlaneDesignerBalance.Airframe.RunwayShortLengthMeters,
            RunwayType.Medium => PlaneDesignerBalance.Airframe.RunwayMediumLengthMeters,
            RunwayType.Long => PlaneDesignerBalance.Airframe.RunwayLongLengthMeters,
            _ => PlaneDesignerBalance.Airframe.RunwayMediumLengthMeters,
        };
}
