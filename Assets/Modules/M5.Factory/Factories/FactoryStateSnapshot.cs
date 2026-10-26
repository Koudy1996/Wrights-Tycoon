using System.Collections.Generic;

namespace WrightTycoon.Core.Factories;

public sealed class FactoryStateSnapshot
{
    public int NextFactorySequence { get; set; } = 1;
    public int NextEquipmentSequence { get; set; } = 1;
    public List<FactorySiteSnapshot> Factories { get; set; } = new();
    public List<FactoryWarehouseEntrySnapshot> WarehouseEntries { get; set; } = new();
    public Dictionary<string, int> AircraftStockTargets { get; set; } = new();
    public List<FactoryEquipmentSnapshot> Equipment { get; set; } = new();
    public List<FactorySupportInventorySnapshot> SupportInventory { get; set; } = new();
    public List<FactorySupportWorkOrderSnapshot> SupportWorkOrders { get; set; } = new();
    public int ProductionBudgetMonth { get; set; } = -1;
    public int LastAircraftProductionMonth { get; set; } = -1;
    public int ProductionPriority { get; set; }
    public List<string> ChargedEquipmentIds { get; set; } = new();
    public List<FactoryMonthlyBudgetSnapshot> MonthlyBudgets { get; set; } = new();
    public bool UsesExternalEngineSupply { get; set; }
    public bool AutomaticStockReplenishment { get; set; }

    // Version 6 and older used a single factory. These members remain solely so those saves can migrate.
    public bool HasFactoryAsset { get; set; }
    public int FactoryConstructionMonthsRemaining { get; set; }
    public int FactoryOperatingMonths { get; set; }
    public List<FactoryLineSnapshot> Lines { get; set; } = new();
}

public sealed class FactorySiteSnapshot
{
    public string FactoryId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int ConstructionMonthsRemaining { get; set; }
    public int OperatingMonths { get; set; }
    public FactoryLineSnapshot? Line { get; set; }
    public int CapacitySchemaVersion { get; set; }
    public int BuildingSize { get; set; }
    public double LegacyCapacityMd { get; set; }
    public bool NeedsLegacyCapacityMigration { get; set; }
    public double BaselineAircraftManDays { get; set; }
    public int WorkforceEmployeeLimit { get; set; } = int.MaxValue;
    public int ExpansionMonthsRemaining { get; set; }
    public int PartsAllocationPercent { get; set; } = 20;
}

public sealed class FactorySupportInventorySnapshot
{
    public string DesignId { get; set; } = string.Empty;
    public int AirframeSpareParts { get; set; }
    public double? TotalCostBasis { get; set; }
    public int UnvaluedUnits { get; set; }
}

public sealed class FactorySupportWorkOrderSnapshot
{
    public string DesignId { get; set; } = string.Empty;
    public bool Guaranteed { get; set; }
    public int RemainingUnits { get; set; }
    public double CompletedManDays { get; set; }
    public double UnitManDays { get; set; }
    public double UnitMaterialCost { get; set; }
    public double UnitOverheadCost { get; set; }
    public string FactoryId { get; set; } = string.Empty;
}

public sealed class FactoryMonthlyBudgetSnapshot
{
    public string FactoryId { get; set; } = string.Empty;
    public double UsedManDays { get; set; }
    public double SupportUsedManDays { get; set; }
    public int CapacitySchemaVersion { get; set; }
    public double AvailableCapacityManDays { get; set; }
    public int PartsAllocationPercent { get; set; } = 20;
}

public sealed class FactoryLineSnapshot
{
    public string LineId { get; set; } = string.Empty;
    public string? AssignedDesignId { get; set; }
    public int MonthlyProductionTargetUnits { get; set; }
    public int TotalProductionTargetUnits { get; set; }
    public int LifetimeProducedUnits { get; set; }
    public int Employees { get; set; }
    public double CarryOverManDays { get; set; }
}

public sealed class FactoryEquipmentSnapshot
{
    public int? BuildingSize { get; set; }
    public double BaselineProductionManDays { get; set; }
    public string EquipmentId { get; set; } = string.Empty;
    public string DesignId { get; set; } = string.Empty;
    public int AutomationTier { get; set; }
    public double PurchaseCost { get; set; }
    public string? FactoryId { get; set; }
    public string? LineId { get; set; }
    public int InstallationMonthsRemaining { get; set; }
    public int LifetimeProducedUnits { get; set; }
    public double CumulativeBaselineLaborExpenses { get; set; }
    public double CumulativeActualLaborExpenses { get; set; }
    public double CumulativeActiveEquipmentExpenses { get; set; }

    // Legacy location flag. New snapshots use FactoryId and LineId.
    public bool IsInstalled { get; set; }
}

public sealed class FactoryWarehouseEntrySnapshot
{
    public string DesignId { get; set; } = string.Empty;
    public int Units { get; set; }
}
