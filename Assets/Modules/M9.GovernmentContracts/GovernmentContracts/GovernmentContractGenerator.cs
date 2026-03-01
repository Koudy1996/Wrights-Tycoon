using System;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Categories;
using WrightTycoon.Core.Simulation;

namespace WrightTycoon.Core.GovernmentContracts;

public sealed class GovernmentContractGenerator : IGovernmentContractGenerator
{
    private readonly IGovernmentContractRandomProvider _random;
    private readonly GovernmentContractGeneratorOptions _options;

    public GovernmentContractGenerator(
        IGovernmentContractRandomProvider random,
        GovernmentContractGeneratorOptions? options = null)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
        _options = options ?? new GovernmentContractGeneratorOptions();
        _options.Validate();
    }

    public GovernmentContract Generate(SimulationDate generatedOn, int contractSequence)
    {
        if (contractSequence < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(contractSequence), "Contract sequence must be >= 0.");
        }

        var aircraftType = NextAircraftType();
        var profile = AircraftCategoryCatalog.GetProfile(aircraftType);
        var speedBand = (GovernmentContractSpeedAltitudeBand)_random.NextIntInclusive(0, 2);

        var minimumSpeedMetersPerSecond = Round(
            SampleTopThird(
                profile.MinRequiredSpeedKilometersPerHour / 3.6d,
                profile.MaxRequiredSpeedKilometersPerHour / 3.6d),
            decimals: 1);
        var minimumRangeKilometers = Round(SampleTopThird(profile.MinRequiredRangeKilometers, profile.MaxRequiredRangeKilometers), 1);
        var minimumPayloadKilograms = Round(SampleTopThird(profile.MinRequiredPayloadKilograms, profile.MaxRequiredPayloadKilograms), 1);
        var minimumReliability = Round(SampleTopThird(_options.MinReliability, _options.MaxReliability), 3);
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

    private AircraftCategory NextAircraftType()
    {
        var index = _random.NextIntInclusive(0, _options.RequestedAircraftTypes.Count - 1);
        return _options.RequestedAircraftTypes[index];
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

