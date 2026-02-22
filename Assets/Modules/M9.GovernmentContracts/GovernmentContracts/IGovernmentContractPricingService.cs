namespace WrightTycoon.Core.GovernmentContracts;

public interface IGovernmentContractPricingService
{
    GovernmentContractPricingQuote CalculateQuote(GovernmentContractRequirements requirements, int quantityUnits);
}
