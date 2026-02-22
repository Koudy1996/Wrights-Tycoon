namespace WrightTycoon.Core.Accounting;

public sealed record AccountingCostBreakdown(
    double FixedProductionLineExpenses,
    double VariableEngineStaffExpenses,
    double VariableEngineBuildExpenses,
    double VariablePlaneStaffExpenses,
    double VariablePlaneBuildExpenses,
    double VariableResearchExpenses = 0d)
{
    public static AccountingCostBreakdown Zero { get; } = new(0d, 0d, 0d, 0d, 0d, 0d);

    public double FixedCosts => FixedProductionLineExpenses;

    public double VariableCosts =>
        VariableEngineStaffExpenses
        + VariableEngineBuildExpenses
        + VariablePlaneStaffExpenses
        + VariablePlaneBuildExpenses
        + VariableResearchExpenses;

    public double TotalCosts => FixedCosts + VariableCosts;
}
