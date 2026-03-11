using WrightTycoon.Core.Simulation;

namespace WrightTycoon.Core.Accounting;

public sealed record AccountingTransactionEntry(
    int Sequence,
    int MonthIndex,
    SimulationDate Date,
    string Description,
    double Amount,
    double CashAfterTransaction)
{
    public bool IsIncome => Amount >= 0d;
}
