namespace WrightTycoon.Core.Contracts;

public readonly record struct ContractPricingQuote(
    double EstimatedMinimumUnitCost,
    double UnitPrice,
    double TotalPrice,
    double AppliedMarkup);
