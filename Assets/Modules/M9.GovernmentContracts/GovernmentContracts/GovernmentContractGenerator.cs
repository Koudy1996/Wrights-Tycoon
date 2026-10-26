using System;
using System.Collections.Generic;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Campaign;
using WrightTycoon.Core.Categories;
using WrightTycoon.Core.Simulation;

namespace WrightTycoon.Core.GovernmentContracts;

public sealed class GovernmentContractGenerator : IGovernmentContractGenerator
{
    private readonly IGovernmentContractRandomProvider _random;
    private readonly GovernmentContractGeneratorOptions _options;
    private readonly IHistoricalCampaignContractModifierSource? _campaignModifierSource;
    private readonly WrightTycoon.Core.Contracts.ContractPricingCalculator _economicPricing;

    public GovernmentContractGenerator(
        IGovernmentContractRandomProvider random,
        GovernmentContractGeneratorOptions? options = null,
        IHistoricalCampaignContractModifierSource? campaignModifierSource = null,
        Func<double>? reputationPriceMultiplierProvider = null)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
        _options = options ?? new GovernmentContractGeneratorOptions();
        _campaignModifierSource = campaignModifierSource;
        _economicPricing = new WrightTycoon.Core.Contracts.ContractPricingCalculator(campaignModifierSource, reputationPriceMultiplierProvider);
        _options.Validate();
    }

    public GovernmentContract Generate(SimulationDate generatedOn, int contractSequence)
    {
        if (contractSequence < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(contractSequence), "Contract sequence must be >= 0.");
        }

        var generationYear = generatedOn.Year;
        var aircraftType = NextAircraftType(generatedOn);
        var profile = AircraftCategoryCatalog.GetProfileForYear(aircraftType, generationYear);
        var speedBand = (GovernmentContractSpeedAltitudeBand)_random.NextIntInclusive(0, 2);

        var minimumSpeedMetersPerSecond = Round(
            SampleTopThird(
                profile.MinRequiredSpeedKilometersPerHour / 3.6d,
                profile.MaxRequiredSpeedKilometersPerHour / 3.6d),
            decimals: 1);
        var minimumRangeKilometers = Round(SampleTopThird(profile.MinRequiredRangeKilometers, profile.MaxRequiredRangeKilometers), 1);
        var minimumPayloadKilograms = Round(SampleTopThird(profile.MinRequiredPayloadKilograms, profile.MaxRequiredPayloadKilograms), 1);
        var minimumManeuverability = Round(SampleTopThird(profile.MinRequiredManeuverability, profile.MaxRequiredManeuverability), 1);
        var minimumReliabilityBound = TypeRequirementBalance.ScaleMinimumReliability(_options.MinReliability, generationYear);
        var maximumReliabilityBound = TypeRequirementBalance.ScaleMaximumReliability(_options.MaxReliability, generationYear);
        var minimumReliability = Round(
            SampleTopThird(minimumReliabilityBound, maximumReliabilityBound),
            3);
        var minimumCrewSize = SampleTopThirdInt(profile.MinCrew, profile.MaxCrew);
        var maximumRunwayLengthMeters = ContractBalance.ResolveRequiredRunwayLengthMeters(aircraftType, _random.NextDouble());

        var requirements = new GovernmentContractRequirements
        {
            RequiredAircraftType = aircraftType,
            LifecycleModelVersion = WrightTycoon.Core.Contracts.LifecycleContractPolicy.CurrentLifecycleModelVersion,
            MinimumExpectedLifeHours = WrightTycoon.Core.Contracts.LifecycleContractPolicy.RequiredLife(aircraftType),
            MaximumServiceIntervalHours = WrightTycoon.Core.Fleet.LifecycleBalance.ServiceIntervalsHours[_random.NextIntInclusive(0, WrightTycoon.Core.Fleet.LifecycleBalance.ServiceIntervalsHours.Count - 1)],
            MinimumSupportMonths = WrightTycoon.Core.Contracts.LifecycleContractPolicy.SupportOptionsMonths[_random.NextIntInclusive(0, WrightTycoon.Core.Contracts.LifecycleContractPolicy.SupportOptionsMonths.Count - 1)],
            OperatingProfileId = WrightTycoon.Core.Contracts.LifecycleContractPolicy.Profile(aircraftType).Id,
            UtilizationHoursPerMonth = WrightTycoon.Core.Contracts.LifecycleContractPolicy.Profile(aircraftType).UtilizationHoursPerMonth,
            AverageFlightHours = WrightTycoon.Core.Contracts.LifecycleContractPolicy.Profile(aircraftType).AverageFlightHours,
            ProcurementPreference = _random.NextDouble() < WrightTycoon.Core.Contracts.LifecycleContractPolicy.PurchasePriceShare
                ? WrightTycoon.Core.Contracts.ProcurementPreference.PurchasePrice : WrightTycoon.Core.Contracts.ProcurementPreference.WholeLifeCost,
            MaximumOperatingCostPerUnit = 1d, // Draft; the snapshot supplies the final operating limit.
            SpeedAltitudeBand = speedBand,
            MinimumMaximumSpeedMetersPerSecond = minimumSpeedMetersPerSecond,
            MinimumRangeKilometers = minimumRangeKilometers,
            MinimumPayloadKilograms = minimumPayloadKilograms,
            MinimumReliability = minimumReliability,
            MinimumManeuverability = minimumManeuverability,
            MinimumCrewSize = minimumCrewSize,
            MaximumRequiredRunwayLengthMeters = maximumRunwayLengthMeters,
        };

        var priority = _options.BalancedPriorityShare > 0d && _random.NextDouble() < _options.BalancedPriorityShare
            ? WrightTycoon.Core.Contracts.OperationalCostPriority.Balanced
            : WrightTycoon.Core.Contracts.ContractPricingBalance.Priority(requirements.ProcurementPreference);
        requirements = requirements.WithEconomics(_economicPricing.CreateEconomicSnapshot(requirements.AsCivilRequirements(), generatedOn, 1, priority));
        var contractId = $"{_options.ContractIdPrefix}.{generatedOn.Year:0000}{generatedOn.Month:00}.{contractSequence:0000}";
        return new GovernmentContract(contractId, generatedOn, requirements);
    }

    private AircraftCategory NextAircraftType(SimulationDate generatedOn)
    {
        var modifiers = _campaignModifierSource?.GetCurrentModifiers();
        var generationYear = generatedOn.Year;
        var eligibleCategories = new List<AircraftCategory>(_options.RequestedAircraftTypes.Count);

        for (var index = 0; index < _options.RequestedAircraftTypes.Count; index++)
        {
            var category = _options.RequestedAircraftTypes[index];
            if (!IsCategoryEligibleForGeneration(category, generationYear, modifiers))
            {
                continue;
            }

            eligibleCategories.Add(category);
        }

        if (eligibleCategories.Count == 0)
        {
            var fallbackProfiles = AircraftCategoryCatalog.GetAllProfiles();
            for (var index = 0; index < fallbackProfiles.Count; index++)
            {
                var category = fallbackProfiles[index].Category;
                if (!IsCategoryEligibleForGeneration(category, generationYear, modifiers))
                {
                    continue;
                }

                eligibleCategories.Add(category);
            }
        }

        if (eligibleCategories.Count == 0)
        {
            throw new InvalidOperationException($"No government contract categories are available for year {generationYear}.");
        }

        var selectedIndex = _random.NextIntInclusive(0, eligibleCategories.Count - 1);
        return eligibleCategories[selectedIndex];
    }

    private static bool IsCategoryEligibleForGeneration(
        AircraftCategory category,
        int generationYear,
        HistoricalCampaignContractModifiers? modifiers)
    {
        if (!ContractBalance.IsCategoryAvailableForYear(category, generationYear))
        {
            return false;
        }

        return modifiers is null || modifiers.IsCategoryUnlocked(category);
    }

    private double SampleTopThird(double minValue, double maxValue)
    {
        if (maxValue <= minValue)
        {
            return minValue;
        }

        var lowerBound = minValue + ((maxValue - minValue) * GovernmentContractBalance.TopThirdLowerBoundFactor);
        return _random.NextDouble(lowerBound, maxValue);
    }

    private int SampleTopThirdInt(int minValue, int maxValue)
    {
        if (maxValue <= minValue)
        {
            return minValue;
        }

        var lowerBound = (int)System.Math.Ceiling(minValue + ((maxValue - minValue) * GovernmentContractBalance.TopThirdLowerBoundFactor));
        lowerBound = System.Math.Max(lowerBound, minValue);
        return _random.NextIntInclusive(lowerBound, maxValue);
    }

    private static double Round(double value, int decimals) =>
        System.Math.Round(value, decimals, MidpointRounding.AwayFromZero);
}

