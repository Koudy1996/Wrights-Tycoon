namespace WrightTycoon.Core.Accounting;

public sealed record InventoryValueEntry(
    string DesignId,
    int Units,
    double UnitCost,
    double TotalValue);
