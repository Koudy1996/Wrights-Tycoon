using System;
using System.Collections.Generic;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Campaign;
using WrightTycoon.Core.Categories;
using WrightTycoon.Core.Simulation;

namespace WrightTycoon.Core.Contracts;

public sealed class ContractGenerator : IContractGenerator
{
    private static readonly RequirementType[] RequirementTypesTemplate =
    {
        RequirementType.Reliability,
        RequirementType.MaximumWeight,
        RequirementType.Range,
        RequirementType.Speed,
        RequirementType.Payload,
        RequirementType.Crew,
    };

    private static readonly RequirementType[] RequirementTypesWithFuelConsumptionTemplate =
    {
        RequirementType.Reliability,
        RequirementType.MaximumWeight,
        RequirementType.Range,
        RequirementType.Speed,
        RequirementType.Payload,
        RequirementType.Crew,
        RequirementType.FuelConsumption,
    };

    private readonly Random _random;
    private readonly ContractGeneratorOptions _options;
    private readonly IHistoricalCampaignContractModifierSource? _campaignModifierSource;
    private readonly Func<SimulationDate>? _currentDateProvider;
    private readonly List<AircraftCategory> _categoryRotation = new();
    private readonly ContractPricingCalculator _pricingCalculator;
    private int _sequence;
    private int _categoryRotationIndex;
    private int _generatedOrderCount;
    private int _smallOrderCount;
    private int _lastGeneratedMonthIndex;

    private enum RequirementType
    {
        Reliability = 0,
        MaximumWeight = 1,
        Range = 2,
        Speed = 3,
        Payload = 4,
        Crew = 5,
        FuelConsumption = 6,
    }

    private enum OrderDemandBand
    {
        Low = 0,
        High = 1,
    }

    public ContractGenerator(
        int seed = 6003901,
        ContractGeneratorOptions? options = null,
        IHistoricalCampaignContractModifierSource? campaignModifierSource = null,
        Func<SimulationDate>? currentDateProvider = null)
    {
        _random = new Random(seed);
        _options = options ?? new ContractGeneratorOptions();
        _campaignModifierSource = campaignModifierSource;
        _currentDateProvider = currentDateProvider;
        _pricingCalculator = new ContractPricingCalculator(_campaignModifierSource);
        _options.Validate();
        _lastGeneratedMonthIndex = 0;
        ResetCategoryRotation(_lastGeneratedMonthIndex);
    }

    public IReadOnlyList<Contract> GenerateMonthlyContracts(int monthIndex)
    {
        if (monthIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(monthIndex), "Month index must be greater than or equal to zero.");
        }

        _lastGeneratedMonthIndex = monthIndex;
        ResetCategoryRotation(monthIndex);
        var contractCount = NextInclusive(_options.MinContractsPerMonth, _options.MaxContractsPerMonth);
        var generated = new List<Contract>(contractCount);
        for (var index = 0; index < contractCount; index++)
        {
            var demandBand = NextDemandBand();
            var category = NextCategory();
            var quantityUnits = GenerateQuantityUnits(demandBand);
            var requirements = GenerateRequirements(category, demandBand);
            var pricing = _pricingCalculator.CalculateQuote(requirements, quantityUnits);
            var contract = new Contract(
                $"ctr.{monthIndex:0000}.{_sequence:0000}",
                GenerateCompanyName(),
                quantityUnits,
                NextInclusive(_options.MinDeadlineMonths, _options.MaxDeadlineMonths),
                requirements,
                monthIndex,
                pricing.UnitPrice,
                pricing.EstimatedMinimumUnitCost,
                pricing.AppliedMarkup);
            generated.Add(contract);
            _sequence++;
        }

        return generated;
    }

    private ContractRequirements GenerateRequirements(AircraftCategory category, OrderDemandBand demandBand)
    {
        var generationYear = ResolveGenerationYear(_lastGeneratedMonthIndex);
        var categoryProfile = AircraftCategoryCatalog.GetProfileForYear(category, generationYear);
        var includeFuelConsumptionRequirement = IsFuelConsumptionRequirementUnlocked();
        var requirementTypes = CreateRequirementTypePool(includeFuelConsumptionRequirement);

        Shuffle(requirementTypes);
        var selectedCount = NextInclusive(1, requirementTypes.Length);

        double? reliability = null;
        double? maximumWeight = null;
        double? range = null;
        ContractSpeedRequirement? speed = null;
        double? payload = null;
        int? crew = null;
        double? maximumFuelConsumption = null;
        var maximumRunwayLengthMeters = ContractBalance.ResolveRequiredRunwayLengthMeters(category, _random.NextDouble());

        for (var index = 0; index < selectedCount; index++)
        {
            switch (requirementTypes[index])
            {
                case RequirementType.Reliability:
                    var minimumReliabilityBound = TypeRequirementBalance.ScaleMinimumReliability(_options.MinReliability, generationYear);
                    var maximumReliabilityBound = TypeRequirementBalance.ScaleMaximumReliability(_options.MaxReliability, generationYear);
                    var sampledReliability = SampleBandValue(minimumReliabilityBound, maximumReliabilityBound, demandBand);
                    reliability = Round(sampledReliability, 3);
                    break;
                case RequirementType.MaximumWeight:
                    maximumWeight = Round(SampleBandValue(categoryProfile.MinMaximumWeightKilograms, categoryProfile.MaxMaximumWeightKilograms, demandBand), 1);
                    break;
                case RequirementType.Range:
                    var (minRange, maxRange) = ResolveBoundedRange(
                        categoryProfile.MinRequiredRangeKilometers,
                        categoryProfile.MaxRequiredRangeKilometers,
                        _options.MinRangeKilometers,
                        _options.MaxRangeKilometers);
                    range = Round(
                        System.Math.Max(1d, SampleBandValue(minRange, maxRange, demandBand)),
                        1);
                    break;
                case RequirementType.Speed:
                    var altitude = _options.SpeedRequirementAltitudesMeters[_random.Next(_options.SpeedRequirementAltitudesMeters.Count)];
                    var (minSpeedKmh, maxSpeedKmh) = ResolveBoundedRange(
                        categoryProfile.MinRequiredSpeedKilometersPerHour,
                        categoryProfile.MaxRequiredSpeedKilometersPerHour,
                        _options.MinSpeedMetersPerSecond * 3.6d,
                        _options.MaxSpeedMetersPerSecond * 3.6d);
                    var minimumSpeedKmh = SampleBandValue(minSpeedKmh, maxSpeedKmh, demandBand);
                    var minimumSpeed = Round(minimumSpeedKmh / 3.6d, 1);
                    speed = new ContractSpeedRequirement(altitude, minimumSpeed);
                    break;
                case RequirementType.Payload:
                    var (minPayload, maxPayload) = ResolveBoundedRange(
                        categoryProfile.MinRequiredPayloadKilograms,
                        categoryProfile.MaxRequiredPayloadKilograms,
                        _options.MinPayloadKilograms,
                        _options.MaxPayloadKilograms);
                    payload = Round(
                        System.Math.Max(0d, SampleBandValue(minPayload, maxPayload, demandBand)),
                        1);
                    break;
                case RequirementType.Crew:
                    var (minCrew, maxCrew) = ResolveBoundedRange(
                        categoryProfile.MinCrew,
                        categoryProfile.MaxCrew,
                        _options.MinCrew,
                        _options.MaxCrew);
                    crew = SampleBandInt(minCrew, maxCrew, demandBand);
                    break;
                case RequirementType.FuelConsumption:
                    var (minFuelConsumption, maxFuelConsumption) =
                        ContractBalance.ResolveFuelConsumptionRequirementRangeKilogramsPerHour(category);
                    maximumFuelConsumption = Round(
                        SampleBandValue(minFuelConsumption, maxFuelConsumption, demandBand),
                        1);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported requirement type '{requirementTypes[index]}'.");
            }
        }

        return new ContractRequirements
        {
            RequiredCategory = category,
            MinimumReliability = reliability,
            MaximumTotalWeightKilograms = maximumWeight,
            MinimumRangeKilometers = range,
            MinimumSpeed = speed,
            MinimumPayloadKilograms = payload,
            MinimumCrew = crew,
            MaximumRequiredRunwayLengthMeters = maximumRunwayLengthMeters,
            MaximumFuelConsumptionKilogramsPerHour = maximumFuelConsumption,
        };
    }

    private AircraftCategory NextCategory()
    {
        if (_categoryRotationIndex >= _categoryRotation.Count)
        {
            ResetCategoryRotation(_lastGeneratedMonthIndex);
        }

        return _categoryRotation[_categoryRotationIndex++];
    }

    private void ResetCategoryRotation(int monthIndex)
    {
        _categoryRotation.Clear();
        var generationYear = ResolveGenerationYear(monthIndex);
        var modifiers = _campaignModifierSource?.GetCurrentModifiers();

        for (var index = 0; index < _options.RequestedCategories.Count; index++)
        {
            var category = _options.RequestedCategories[index];
            if (!IsCategoryEligibleForGeneration(category, generationYear, modifiers))
            {
                continue;
            }

            var copies = 1;
            if (modifiers is not null && modifiers.HasCategoryMixAdjustment)
            {
                copies = ContractBalance.ResolveHistoricalCampaignCategoryPoolCopies(
                    modifiers.ResolveCategoryWeightMultiplier(category));
            }

            for (var copyIndex = 0; copyIndex < copies; copyIndex++)
            {
                _categoryRotation.Add(category);
            }
        }

        if (_categoryRotation.Count == 0)
        {
            var fallbackProfiles = AircraftCategoryCatalog.GetAllProfiles();
            for (var index = 0; index < fallbackProfiles.Count; index++)
            {
                var fallbackCategory = fallbackProfiles[index].Category;
                if (!IsCategoryEligibleForGeneration(fallbackCategory, generationYear, modifiers))
                {
                    continue;
                }

                _categoryRotation.Add(fallbackCategory);
            }
        }

        if (_categoryRotation.Count == 0)
        {
            throw new InvalidOperationException($"No contract categories are available for year {generationYear}.");
        }

        Shuffle(_categoryRotation);
        _categoryRotationIndex = 0;
    }

    private bool IsCategoryEligibleForGeneration(
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

    private int ResolveGenerationYear(int monthIndex)
    {
        if (_currentDateProvider is not null)
        {
            var currentDate = _currentDateProvider();
            return currentDate.Year;
        }

        var startYear = CoreBalance.HistoricalCampaignTechnology.StartYear;
        if (monthIndex <= 0)
        {
            return startYear;
        }

        return startYear + (monthIndex / 12);
    }

    private OrderDemandBand NextDemandBand()
    {
        if (_options.SmallOrderShare <= 0d)
        {
            _generatedOrderCount++;
            return OrderDemandBand.High;
        }

        if (_options.SmallOrderShare >= 1d)
        {
            _generatedOrderCount++;
            _smallOrderCount++;
            return OrderDemandBand.Low;
        }

        var expectedSmallCountAfterNext = (_generatedOrderCount + 1) * _options.SmallOrderShare;
        var selectSmall = _smallOrderCount < expectedSmallCountAfterNext;
        _generatedOrderCount++;
        if (selectSmall)
        {
            _smallOrderCount++;
            return OrderDemandBand.Low;
        }

        return OrderDemandBand.High;
    }

    private int GenerateQuantityUnits(OrderDemandBand demandBand)
    {
        var (minQuantity, maxQuantity) = demandBand == OrderDemandBand.Low
            ? (_options.SmallOrderMinQuantityUnits, _options.SmallOrderMaxQuantityUnits)
            : (_options.LargeOrderMinQuantityUnits, _options.LargeOrderMaxQuantityUnits);

        return NextInclusive(minQuantity, maxQuantity);
    }

    private int SampleBandInt(int minValue, int maxValue, OrderDemandBand demandBand)
    {
        if (minValue >= maxValue)
        {
            return minValue;
        }

        var (bandMinNormalized, bandMaxNormalized) = GetBandRange(demandBand);
        var range = maxValue - minValue;
        var bandMinValue = minValue + (range * bandMinNormalized);
        var bandMaxValue = minValue + (range * bandMaxNormalized);
        var sampleMin = (int)System.Math.Ceiling(bandMinValue);
        var sampleMax = (int)System.Math.Floor(bandMaxValue);
        if (sampleMin > sampleMax)
        {
            sampleMin = minValue;
            sampleMax = maxValue;
        }

        return NextInclusive(sampleMin, sampleMax);
    }

    private double SampleBandValue(double minValue, double maxValue, OrderDemandBand demandBand)
    {
        if (maxValue <= minValue)
        {
            return minValue;
        }

        var (bandMinNormalized, bandMaxNormalized) = GetBandRange(demandBand);
        var range = maxValue - minValue;
        var bandMinValue = minValue + (range * bandMinNormalized);
        var bandMaxValue = minValue + (range * bandMaxNormalized);
        if (bandMinValue > bandMaxValue)
        {
            (bandMinValue, bandMaxValue) = (bandMaxValue, bandMinValue);
        }

        return NextDouble(bandMinValue, bandMaxValue);
    }

    private (double MinNormalized, double MaxNormalized) GetBandRange(OrderDemandBand demandBand) => demandBand == OrderDemandBand.Low
        ? (_options.LowDemandBandMinNormalized, _options.LowDemandBandMaxNormalized)
        : (_options.HighDemandBandMinNormalized, _options.HighDemandBandMaxNormalized);

    private string GenerateCompanyName()
    {
        var prefix = _options.CompanyPrefixes[_random.Next(_options.CompanyPrefixes.Count)];
        var suffix = _options.CompanySuffixes[_random.Next(_options.CompanySuffixes.Count)];
        return $"{prefix} {suffix}";
    }

    private int NextInclusive(int minValue, int maxValue) => _random.Next(minValue, maxValue + 1);

    private double NextDouble(double minValue, double maxValue) => minValue + (_random.NextDouble() * (maxValue - minValue));

    private static double Round(double value, int decimals) => System.Math.Round(value, decimals, MidpointRounding.AwayFromZero);

    private void Shuffle<T>(IList<T> values)
    {
        for (var index = values.Count - 1; index > 0; index--)
        {
            var target = _random.Next(0, index + 1);
            (values[index], values[target]) = (values[target], values[index]);
        }
    }

    private bool IsFuelConsumptionRequirementUnlocked()
    {
        var modifiers = _campaignModifierSource?.GetCurrentModifiers();
        return modifiers is not null
            && modifiers.IsContractParameterUnlocked(ContractBalance.HistoricalCampaignContractParameterFuelConsumption);
    }

    private static RequirementType[] CreateRequirementTypePool(bool includeFuelConsumptionRequirement)
    {
        var sourceTemplate = includeFuelConsumptionRequirement
            ? RequirementTypesWithFuelConsumptionTemplate
            : RequirementTypesTemplate;
        var pool = new RequirementType[sourceTemplate.Length];
        Array.Copy(sourceTemplate, pool, sourceTemplate.Length);
        return pool;
    }

    private static (double MinValue, double MaxValue) ResolveBoundedRange(
        double profileMin,
        double profileMax,
        double optionMin,
        double optionMax)
    {
        var minValue = System.Math.Max(profileMin, optionMin);
        var maxValue = System.Math.Min(profileMax, optionMax);
        if (minValue > maxValue)
        {
            minValue = profileMin;
            maxValue = profileMax;
        }

        return (minValue, maxValue);
    }

    private static (int MinValue, int MaxValue) ResolveBoundedRange(
        int profileMin,
        int profileMax,
        int optionMin,
        int optionMax)
    {
        var minValue = System.Math.Max(profileMin, optionMin);
        var maxValue = System.Math.Min(profileMax, optionMax);
        if (minValue > maxValue)
        {
            minValue = profileMin;
            maxValue = profileMax;
        }

        return (minValue, maxValue);
    }
}


