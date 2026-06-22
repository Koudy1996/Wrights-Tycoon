namespace WrightTycoon.Core.Balance;

// M5 tuning surface for production throughput/tooling defaults.
public static class FactoryBalance
{
    public const double EmployeeManDaysPerMonth = 21d;
    public const int MaximumAircraftPerLinePerMonth = 100;
    public const int MinimumToolLevel = 1;
    public const int InitialFactoryToolLevel = 1;

    public const double AssignedLineMonthlyAircraftCostShare = 0.5d;
    public const double UnassignedLineMonthlyStandbyCost = 5000d;
    public const double PlaneStaffMonthlyCostPerEmployee = EmployeeManDaysPerMonth * ProductionEconomyBalance.LaborCostPerManDay;
    public const double EngineStaffMonthlyCostPerEmployee = EmployeeManDaysPerMonth * ProductionEconomyBalance.LaborCostPerManDay;
}
