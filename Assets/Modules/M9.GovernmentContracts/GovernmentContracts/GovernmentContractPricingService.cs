using System;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Contracts;

namespace WrightTycoon.Core.GovernmentContracts;

public sealed class GovernmentContractPricingService : IGovernmentContractPricingService
{
    private readonly ContractPricingCalculator _pricingCalculator = new();
    private readonly Func<double>? _reputationPriceMultiplierProvider;
    public GovernmentContractPricingService(Func<double>? reputationPriceMultiplierProvider = null)
    {
        _reputationPriceMultiplierProvider = reputationPriceMultiplierProvider;
    }

    public GovernmentContractPricingQuote CalculateQuote(GovernmentContractRequirements requirements, int quantityUnits)
    {
        if (requirements is null)
        {
            throw new ArgumentNullException(nameof(requirements));
        }

        if (quantityUnits < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(quantityUnits), "Quantity must be >= 1.");
        }

        requirements.Validate();

        if (requirements.EconomicSnapshot is { } economic)
            return new GovernmentContractPricingQuote(economic.ReferenceDirectUnitCost, economic.OfferedUnitPrice,
                RoundCurrency(economic.OfferedUnitPrice * quantityUnits), economic.OfferedUnitPrice / economic.ReferenceDirectUnitCost - 1d);

        var bridgeRequirements = new ContractRequirements
        {
            RequiredCategory = requirements.RequiredAircraftType,
            LifecycleModelVersion = requirements.LifecycleModelVersion,
            MinimumExpectedLifeHours = requirements.MinimumExpectedLifeHours,
            MaximumServiceIntervalHours = requirements.MaximumServiceIntervalHours,
            MinimumSupportMonths = requirements.MinimumSupportMonths,
            UtilizationHoursPerMonth = requirements.UtilizationHoursPerMonth,
            AverageFlightHours = requirements.AverageFlightHours,
            OperatingProfileId = requirements.OperatingProfileId,
            ProcurementPreference = requirements.ProcurementPreference,
            MaximumOperatingCostPerUnit = requirements.MaximumOperatingCostPerUnit,
            MinimumReliability = requirements.MinimumReliability,
            MinimumRangeKilometers = requirements.MinimumRangeKilometers,
            MinimumSpeed = new ContractSpeedRequirement(
                requirements.SpeedAltitudeBand.ToAltitudeMeters(),
                requirements.MinimumMaximumSpeedMetersPerSecond),
            MinimumPayloadKilograms = requirements.MinimumPayloadKilograms,
            MinimumManeuverability = requirements.MinimumManeuverability,
            MinimumCrew = requirements.MinimumCrewSize,
            MaximumRequiredRunwayLengthMeters = requirements.MaximumRequiredRunwayLengthMeters,
        };

        var estimatedMinimumUnitCost = _pricingCalculator.EstimateMinimumUnitProductionCost(bridgeRequirements);
        var appliedMarkup = (((1d + GovernmentContractBalance.PricingMargin)
            * GovernmentContractBalance.ContractPriceMarginImprovementMultiplier) - 1d)
            * GovernmentContractBalance.AdditionalMarginMultiplier;
        var unitPrice = estimatedMinimumUnitCost * (1d + appliedMarkup)
            * LifecycleCostCalculator.ReputationMultiplier(_reputationPriceMultiplierProvider);
        if (requirements.LifecycleModelVersion > 0 && requirements.ProcurementPreference == ProcurementPreference.WholeLifeCost)
        {
            var operation = LifecycleContractPolicy.ReferenceOperationQuote(requirements.RequiredAircraftType, estimatedMinimumUnitCost,
                    requirements.MinimumExpectedLifeHours, requirements.MaximumServiceIntervalHours, requirements.MinimumSupportMonths, purchasePrice: 0d);
            unitPrice = System.Math.Min(unitPrice, System.Math.Max(0d, requirements.MaximumOperatingCostPerUnit
                - operation.OperationCosts));
        }
        var totalContractValue = unitPrice * quantityUnits;

        return new GovernmentContractPricingQuote(
            RoundCurrency(estimatedMinimumUnitCost),
            RoundCurrency(unitPrice),
            RoundCurrency(totalContractValue),
            appliedMarkup);
    }

    private static double RoundCurrency(double value) =>
        System.Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
