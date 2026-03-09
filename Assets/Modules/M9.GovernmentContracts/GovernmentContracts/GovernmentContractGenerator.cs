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

    public GovernmentContractGenerator(
        IGovernmentContractRandomProvider random,
        GovernmentContractGeneratorOptions? options = null,
        IHistoricalCampaignContractModifierSource? campaignModifierSource = null)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
        _options = options ?? new GovernmentContractGeneratorOptions();
        _campaignModifierSource = campaignModifierSource;
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
        var minimumReliability = Round(
            TypeRequirementBalance.ScaleMinimumReliability(
                SampleTopThird(_options.MinReliability, _options.MaxReliability),
                generationYear),
            3);
        var minimumCrewSize = SampleTopThirdInt(profile.MinCrew, profile.MaxCrew);
        var maximumRunwayLengthMeters = ContractBalance.ResolveRequiredRunwayLengthMeters(aircraftType, _random.NextDouble());

        var requirements = new GovernmentContractRequirements
        {
            RequiredAircraftType = aircraftType,
            SpeedAltitudeBand = speedBand,
            MinimumMaximumSpeedMetersPerSecond = minimumSpeedMetersPerSecond,
            MinimumRangeKilometers = minimumRangeKilometers,
            MinimumPayloadKilograms = minimumPayloadKilograms,
            MinimumReliability = minimumReliability,
            MinimumCrewSize = minimumCrewSize,
            MaximumRequiredRunwayLengthMeters = maximumRunwayLengthMeters,
        };

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

