using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WrightTycoon.Core.Aircraft;
using WrightTycoon.Core.Airframes;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Categories;
using WrightTycoon.Core.Math;
using WrightTycoon.Core.Headquarters;

namespace WrightTycoon.Core.Contracts;

public sealed class ContractEvaluator : IContractEvaluator
{
    private readonly Func<int>? _qualificationYearProvider;
    private readonly Func<int>? _companyQualityLevelProvider;
    private readonly Func<WrightTycoon.Core.Aircraft.Aircraft, double>? _fuelPriceProvider;
    private readonly Func<WrightTycoon.Core.Aircraft.Aircraft, ContractRequirements, double, WrightTycoon.Core.Fleet.ServiceOfferSnapshot>? _maintenanceOfferProvider;

    public ContractEvaluator(
        Func<int>? qualificationYearProvider = null,
        Func<int>? companyQualityLevelProvider = null,
        Func<WrightTycoon.Core.Aircraft.Aircraft, ContractRequirements, double, WrightTycoon.Core.Fleet.ServiceOfferSnapshot>? maintenanceOfferProvider = null,
        Func<WrightTycoon.Core.Aircraft.Aircraft, double>? fuelPriceProvider = null)
    {
        _qualificationYearProvider = qualificationYearProvider;
        _companyQualityLevelProvider = companyQualityLevelProvider;
        _maintenanceOfferProvider = maintenanceOfferProvider;
        _fuelPriceProvider = fuelPriceProvider;
    }

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
            var category = requirements.RequiredCategory.Value;
            var profile = _qualificationYearProvider is null
                ? AircraftCategoryCatalog.GetProfile(category)
                : AircraftCategoryCatalog.GetProfileForYear(category, _qualificationYearProvider());
            var categoryCompliance = new CategoryComplianceEvaluator().Evaluate(aircraft, profile);
            if (!categoryCompliance.IsCompliant)
            {
                unmetRequirements.Add($"Aircraft does not qualify for requested category '{profile.DisplayName}'.");
                for (var index = 0; index < categoryCompliance.UnmetRequirements.Count; index++)
                {
                    unmetRequirements.Add($"Category: {categoryCompliance.UnmetRequirements[index]}");
                }
            }
        }

        if (requirements.MinimumReliability.HasValue)
        {
            var minimumReliability = requirements.MinimumReliability.Value;
            var actualReliability = HeadquartersBalance.ApplyCompanyReliabilityBonus(
                aircraft.Performance.OverallReliability,
                _companyQualityLevelProvider?.Invoke() ?? 0);
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
            var actualRange = AircraftNavigationRangePolicy.GetCertifiedRangeKilometers(
                aircraft.TypeDefinition.NavigationEquipment,
                aircraft.Performance.RangeKilometers);
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

        if (requirements.MinimumManeuverability.HasValue)
        {
            var minimumManeuverability = requirements.MinimumManeuverability.Value;
            var actualManeuverability = aircraft.Airframe.Performance.Maneuverability;
            if (actualManeuverability + Numeric.Epsilon < minimumManeuverability)
            {
                unmetRequirements.Add(
                    $"Maneuverability {FormatUiNumber(actualManeuverability, "0.0")} < {FormatUiNumber(minimumManeuverability, "0.0")}");
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

        if (requirements.MaximumFuelConsumptionKilogramsPerHour.HasValue)
        {
            var maximumFuelConsumption = requirements.MaximumFuelConsumptionKilogramsPerHour.Value;
            var actualFuelConsumption = aircraft.Performance.AverageCruiseFuelBurnKilogramsPerHour;
            if (actualFuelConsumption - Numeric.Epsilon > maximumFuelConsumption)
            {
                unmetRequirements.Add(
                    $"Fuel burn {FormatUiNumber(actualFuelConsumption, "0.0")} kg/h > {FormatUiNumber(maximumFuelConsumption, "0.0")} kg/h");
            }
        }

        if (requirements.LifecycleModelVersion > 0)
        {
            var reliability = HeadquartersBalance.ApplyCompanyReliabilityBonus(aircraft.Performance.OverallReliability, _companyQualityLevelProvider?.Invoke() ?? 0);
            var life = WrightTycoon.Core.Fleet.LifecycleCalculator.CalculateExpectedLifeHours(aircraft.FamilyDefinition.Category,
                reliability, aircraft.TypeDefinition.ServiceIntervalHours, aircraft.TypeDefinition.ConstructionDurabilityLevel);
            if (life + Numeric.Epsilon < requirements.MinimumExpectedLifeHours) unmetRequirements.Add("Expected life is below contractual minimum.");
            if (aircraft.TypeDefinition.ServiceIntervalHours < requirements.MinimumServiceIntervalHours) unmetRequirements.Add("Service interval is below contractual minimum.");
            var offer = _maintenanceOfferProvider?.Invoke(aircraft, requirements, reliability)
                ?? LifecycleCostCalculator.CreateContractServiceOffer(aircraft, requirements, reliability);
            var quote = LifecycleCostCalculator.Calculate(LifecycleCostCalculator.ExpectedOperatingLifeHours(requirements.MinimumExpectedLifeHours, life), contract.UnitPrice,
                aircraft.Performance.AverageCruiseFuelBurnKilogramsPerHour, _fuelPriceProvider?.Invoke(aircraft) ?? LifecycleCostCalculator.FuelPricePerKilogram(aircraft),
                offer, requirements.UtilizationHoursPerMonth);
            if ((requirements.PricingModelVersion > 0 || requirements.ProcurementPreference == ProcurementPreference.WholeLifeCost)
                && quote.OperationCostsPerHour > requirements.MaximumOperationCostPerHour + Numeric.Epsilon)
                unmetRequirements.Add("Operational costs exceed customer budget.");
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
