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
    double InventoryValue,
    double LoanInterestExpenses = 0d,
    double LoanFeeExpenses = 0d,
    double LoanPrincipalReceived = 0d,
    double LoanPrincipalRepaid = 0d)
{
    public double OperatingProfitLoss => Income - Costs.TotalCosts;
    public double MonthlyProfitLoss => OperatingProfitLoss - LoanInterestExpenses - LoanFeeExpenses;
    public double NetFinancingCashFlow => LoanPrincipalReceived - LoanPrincipalRepaid - LoanInterestExpenses - LoanFeeExpenses;
}
