namespace WrightTycoon.Core.Accounting;

public sealed record AccountingCostBreakdown(
    double FactoryMaintenanceExpenses,
    double ActiveEquipmentExpenses,
    double VariableEngineStaffExpenses,
    double VariableEngineBuildExpenses,
    double VariablePlaneStaffExpenses,
    double VariablePlaneBuildExpenses,
    double VariableResearchExpenses = 0d,
    double HeadquartersExpenses = 0d,
    double FleetServiceExpenses = 0d)
{
    public static AccountingCostBreakdown Zero { get; } = new(0d, 0d, 0d, 0d, 0d, 0d, 0d, 0d);

    public double FixedCosts => FactoryMaintenanceExpenses + ActiveEquipmentExpenses;

    public double VariableCosts =>
        VariableEngineStaffExpenses
        + VariableEngineBuildExpenses
        + VariablePlaneStaffExpenses
        + VariablePlaneBuildExpenses
        + VariableResearchExpenses
        + HeadquartersExpenses
        + FleetServiceExpenses;

    public double TotalCosts => FixedCosts + VariableCosts;
}
