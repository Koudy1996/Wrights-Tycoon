using System.Collections.Generic;

namespace WrightTycoon.Core.Factories;

public sealed class FactoryStateSnapshot
{
    public int FactoryToolLevel { get; set; }

    public int LineSequence { get; set; }

    public List<FactoryLineSnapshot> Lines { get; set; } = new();

    public List<FactoryWarehouseEntrySnapshot> WarehouseEntries { get; set; } = new();
}

public sealed class FactoryLineSnapshot
{
    public string LineId { get; set; } = string.Empty;

    public string? AssignedDesignId { get; set; }

    public int MonthlyProductionTargetUnits { get; set; }

    public int Employees { get; set; }

    public double CarryOverManDays { get; set; }

    public int LineToolLevel { get; set; }
}

public sealed class FactoryWarehouseEntrySnapshot
{
    public string DesignId { get; set; } = string.Empty;

    public int Units { get; set; }
}
