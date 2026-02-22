using System.Collections.Generic;
using WrightTycoon.Core.Simulation;

namespace WrightTycoon.Core.Accounting;

public sealed record MonthlyAccountingSnapshot(
    int MonthIndex,
    SimulationDate Date,
    double CashAfterTransactions,
    double Income,
    AccountingCostBreakdown Costs,
    IReadOnlyDictionary<string, InventoryValueEntry> InventoryByDesign,
    double InventoryValue)
{
    public double MonthlyProfitLoss => Income - Costs.TotalCosts;
}
