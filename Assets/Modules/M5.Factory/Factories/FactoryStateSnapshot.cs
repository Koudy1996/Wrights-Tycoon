using System.Collections.Generic;

namespace WrightTycoon.Core.Factories;

public sealed class FactoryStateSnapshot
{
    public int NextFactorySequence { get; set; } = 1;
    public int NextEquipmentSequence { get; set; } = 1;
    public List<FactorySiteSnapshot> Factories { get; set; } = new();
    public List<FactoryWarehouseEntrySnapshot> WarehouseEntries { get; set; } = new();
    public List<FactoryEquipmentSnapshot> Equipment { get; set; } = new();

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
