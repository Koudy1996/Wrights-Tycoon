using System;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Contracts;

namespace WrightTycoon.Core.GovernmentContracts;

public sealed class GovernmentContractPricingService : IGovernmentContractPricingService
{
    private readonly ContractPricingCalculator _pricingCalculator = new();

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

        var bridgeRequirements = new ContractRequirements
        {
            RequiredCategory = requirements.RequiredAircraftType,
            MinimumReliability = requirements.MinimumReliability,
            MinimumRangeKilometers = requirements.MinimumRangeKilometers,
            MinimumSpeed = new ContractSpeedRequirement(
                requirements.SpeedAltitudeBand.ToAltitudeMeters(),
                requirements.MinimumMaximumSpeedMetersPerSecond),
            MinimumPayloadKilograms = requirements.MinimumPayloadKilograms,
            MinimumManeuverability = requirements.MinimumManeuverability,
            MinimumCrew = requirements.MinimumCrewSize,
        };

        var estimatedMinimumUnitCost = _pricingCalculator.EstimateMinimumUnitProductionCost(bridgeRequirements);
        var unitPrice = estimatedMinimumUnitCost * (1d + GovernmentContractBalance.PricingMargin);
        var totalContractValue = unitPrice * quantityUnits;

        return new GovernmentContractPricingQuote(
            RoundCurrency(estimatedMinimumUnitCost),
            RoundCurrency(unitPrice),
            RoundCurrency(totalContractValue),
            GovernmentContractBalance.PricingMargin);
    }

    private static double RoundCurrency(double value) =>
        System.Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
