namespace WrightTycoon.Core.Balance;

public enum FactoryAutomationTier
{
    HandBuilt = 0,
    JigsAndFixtures = 1,
    MechanizedCells = 2,
    AutomatedLine = 3,
    SophisticatedLine = 4,
}

// M5 tuning surface for the paid factory and type-specific production equipment.
public static class FactoryBalance
{
    public const double EmployeeManDaysPerMonth = 21d;
    public const int MaximumAircraftPerLinePerMonth = 100;
    public const double FactoryConstructionCost = 80_000_000d;
    public const int FactoryConstructionMonths = 18;
    public const double FactoryMonthlyMaintenance = 600_000d;
    public const double FactoryResaleBaseShare = 0.55d;
    public const double FactoryResaleAnnualDepreciationShare = 0.01d;
    public const double FactoryResaleMinimumShare = 0.30d;
    public const double EquipmentReinstallationCostShare = 0.03d;
    public const double EquipmentScrapRecoveryShare = 0.15d;
    public const double PlaneStaffMonthlyCostPerEmployee = EmployeeManDaysPerMonth * ProductionEconomyBalance.LaborCostPerManDay;
    public const double EngineStaffMonthlyCostPerEmployee = EmployeeManDaysPerMonth * ProductionEconomyBalance.LaborCostPerManDay;

    public static double GetPurchaseCostMultiplier(FactoryAutomationTier tier) => tier switch
    {
        FactoryAutomationTier.HandBuilt => 5d,
        FactoryAutomationTier.JigsAndFixtures => 45d,
        FactoryAutomationTier.MechanizedCells => 140d,
        FactoryAutomationTier.AutomatedLine => 300d,
        FactoryAutomationTier.SophisticatedLine => 455d,
        _ => throw new System.ArgumentOutOfRangeException(nameof(tier)),
    };

    public static int GetInitialInstallationMonths(FactoryAutomationTier tier) => tier switch
    {
        FactoryAutomationTier.HandBuilt => 0,
        FactoryAutomationTier.JigsAndFixtures => 2,
        FactoryAutomationTier.MechanizedCells => 4,
        FactoryAutomationTier.AutomatedLine => 7,
        FactoryAutomationTier.SophisticatedLine => 10,
        _ => throw new System.ArgumentOutOfRangeException(nameof(tier)),
    };

    public static int GetReinstallationMonths(FactoryAutomationTier tier) => tier switch
    {
        FactoryAutomationTier.HandBuilt or FactoryAutomationTier.JigsAndFixtures => 1,
        FactoryAutomationTier.MechanizedCells or FactoryAutomationTier.AutomatedLine => 2,
        FactoryAutomationTier.SophisticatedLine => 3,
        _ => throw new System.ArgumentOutOfRangeException(nameof(tier)),
    };

    public static double GetProductionManDayMultiplier(FactoryAutomationTier tier) => tier switch
    {
        FactoryAutomationTier.HandBuilt => 1d,
        FactoryAutomationTier.JigsAndFixtures => 0.90d,
        FactoryAutomationTier.MechanizedCells => 0.78d,
        FactoryAutomationTier.AutomatedLine => 0.65d,
        FactoryAutomationTier.SophisticatedLine => 0.50d,
        _ => throw new System.ArgumentOutOfRangeException(nameof(tier)),
    };

    public static int GetMaximumMonthlyOutput(FactoryAutomationTier tier) => tier switch
    {
        FactoryAutomationTier.HandBuilt => 10,
        FactoryAutomationTier.JigsAndFixtures => 25,
        FactoryAutomationTier.MechanizedCells => 50,
        FactoryAutomationTier.AutomatedLine => 75,
        FactoryAutomationTier.SophisticatedLine => 100,
        _ => throw new System.ArgumentOutOfRangeException(nameof(tier)),
    };

    public static double GetMonthlyActiveEquipmentCostShare(FactoryAutomationTier tier) => tier switch
    {
        FactoryAutomationTier.HandBuilt => 0.0010d,
        FactoryAutomationTier.JigsAndFixtures => 0.0015d,
        FactoryAutomationTier.MechanizedCells => 0.0020d,
        FactoryAutomationTier.AutomatedLine => 0.0025d,
        FactoryAutomationTier.SophisticatedLine => 0.0035d,
        _ => throw new System.ArgumentOutOfRangeException(nameof(tier)),
    };
}
