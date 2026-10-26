namespace WrightTycoon.Core.Balance;

public enum FactoryBuildingSize { Small = 0, Medium = 1, Large = 2 }

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
    public const double CapacityReductionMultiplier = .75d;
    public const double EmployeeManDaysPerMonth = 21d;
    // Input guard only; actual output is limited by the building's physical MD budget.
    public const int MaximumAircraftPerLinePerMonth = 1_000_000;
    public static double GetBuildingCapacityManDays(FactoryBuildingSize size) => size switch
    {
        FactoryBuildingSize.Small => 12_600d,
        FactoryBuildingSize.Medium => 50_400d,
        FactoryBuildingSize.Large => 201_600d,
        _ => throw new System.ArgumentOutOfRangeException(nameof(size)),
    };
    public static double GetBuildingMonthlyMaintenance(FactoryBuildingSize size) => FactoryMonthlyMaintenance * (size switch
    { FactoryBuildingSize.Small => 1d, FactoryBuildingSize.Medium => 3d, FactoryBuildingSize.Large => 9d,
        _ => throw new System.ArgumentOutOfRangeException(nameof(size)) });
    public static double GetExpansionCost(FactoryBuildingSize size) => size == FactoryBuildingSize.Small ? 160_000_000d : 480_000_000d;
    public static int GetExpansionMonths(FactoryBuildingSize size) => size == FactoryBuildingSize.Small ? 12 : 18;
    public const double FactoryConstructionCost = 80_000_000d;
    public const int FactoryConstructionMonths = 18;
    // Direct construction costs/time match the existing Small -> Medium -> Large path.
    public static double GetConstructionCost(FactoryBuildingSize size) => size switch
    {
        FactoryBuildingSize.Small => FactoryConstructionCost,
        FactoryBuildingSize.Medium => FactoryConstructionCost + GetExpansionCost(FactoryBuildingSize.Small),
        FactoryBuildingSize.Large => FactoryConstructionCost + GetExpansionCost(FactoryBuildingSize.Small) + GetExpansionCost(FactoryBuildingSize.Medium),
        _ => throw new System.ArgumentOutOfRangeException(nameof(size)),
    };
    public static int GetConstructionMonths(FactoryBuildingSize size) => size switch
    {
        FactoryBuildingSize.Small => FactoryConstructionMonths,
        FactoryBuildingSize.Medium => FactoryConstructionMonths + GetExpansionMonths(FactoryBuildingSize.Small),
        FactoryBuildingSize.Large => FactoryConstructionMonths + GetExpansionMonths(FactoryBuildingSize.Small) + GetExpansionMonths(FactoryBuildingSize.Medium),
        _ => throw new System.ArgumentOutOfRangeException(nameof(size)),
    };
    public const double FactoryMonthlyMaintenance = 600_000d;
    public const double FactoryResaleBaseShare = 0.90d;
    public const double FactoryResaleAnnualDepreciationShare = 0.01d;
    public const double FactoryResaleMinimumShare = 0.30d;
    public static double GetBuildingResaleValue(FactoryBuildingSize size, int constructionRemaining, int expansionRemaining, int operatingMonths)
    {
        var investment = GetConstructionCost(size);
        if (constructionRemaining > 0)
            return investment * FactoryResaleBaseShare * Progress(constructionRemaining, GetConstructionMonths(size));
        if (expansionRemaining > 0 && size != FactoryBuildingSize.Large)
            investment += GetExpansionCost(size) * Progress(expansionRemaining, GetExpansionMonths(size));
        var share = System.Math.Max(FactoryResaleMinimumShare, FactoryResaleBaseShare - System.Math.Max(0, operatingMonths) / 12 * FactoryResaleAnnualDepreciationShare);
        return investment * share;
    }
    private static double Progress(int remaining, int duration) => System.Math.Clamp(1d - (double)remaining / duration, 0d, 1d);
    public const double EquipmentReinstallationCostShare = 0.03d;
    public const double EquipmentMonthlyStorageCostShare = 0.0001d;
    public static double GetEquipmentPurchaseCost(FactoryAutomationTier tier, FactoryBuildingSize size)
    {
        var baseCost = tier switch
        {
            FactoryAutomationTier.HandBuilt => 1_000_000d,
            FactoryAutomationTier.JigsAndFixtures => 8_000_000d,
            FactoryAutomationTier.MechanizedCells => 25_000_000d,
            FactoryAutomationTier.AutomatedLine => 55_000_000d,
            FactoryAutomationTier.SophisticatedLine => 90_000_000d,
            _ => throw new System.ArgumentOutOfRangeException(nameof(tier)),
        };
        return baseCost * (size switch
        {
            FactoryBuildingSize.Small => 1d,
            FactoryBuildingSize.Medium => 3d,
            FactoryBuildingSize.Large => 9d,
            _ => throw new System.ArgumentOutOfRangeException(nameof(size)),
        });
    }
    public const double EquipmentScrapRecoveryShare = 0.15d;
    public const double PlaneStaffMonthlyCostPerEmployee = EmployeeManDaysPerMonth * ProductionEconomyBalance.LaborCostPerManDay;
    public const double EngineStaffMonthlyCostPerEmployee = EmployeeManDaysPerMonth * ProductionEconomyBalance.LaborCostPerManDay;

    // Legacy save migration only: new purchases use GetEquipmentPurchaseCost.
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

    public static double GetEngineProductionManDayMultiplier(FactoryAutomationTier tier) => tier switch
    {
        FactoryAutomationTier.HandBuilt => 1d,
        FactoryAutomationTier.JigsAndFixtures => 0.88d,
        FactoryAutomationTier.MechanizedCells => 0.74d,
        FactoryAutomationTier.AutomatedLine => 0.59d,
        FactoryAutomationTier.SophisticatedLine => 0.45d,
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
