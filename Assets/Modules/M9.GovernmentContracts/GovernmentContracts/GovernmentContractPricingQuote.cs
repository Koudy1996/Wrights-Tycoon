namespace WrightTycoon.Core.GovernmentContracts;

public readonly record struct GovernmentContractPricingQuote(
    double EstimatedMinimumUnitCost,
    double UnitPrice,
    double TotalContractValue,
    double AppliedMargin);
