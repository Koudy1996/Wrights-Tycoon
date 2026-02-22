namespace WrightTycoon.Core.Balance;

// M5 tuning surface for production throughput/tooling defaults.
public static class FactoryBalance
{
    public const double EmployeeManDaysPerMonth = 21d;
    public const int MinimumToolLevel = 1;
    public const int InitialFactoryToolLevel = 1;

    public const double FixedMonthlyCostPerProductionLine = 12000d;
    public const double PlaneStaffMonthlyCostPerEmployee = 4500d;
    public const double EngineStaffMonthlyCostPerEmployee = 5200d;
}
