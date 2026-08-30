using WrightTycoon.Core.Simulation;

namespace WrightTycoon.Core.Accounting;

public enum AccountingTransactionKind
{
    Unknown = 0,
    OperatingIncome = 1,
    OperatingExpense = 2,
    CapitalExpense = 3,
    AssetSale = 4,
    LoanPrincipalReceived = 5,
    LoanPrincipalRepaid = 6,
    LoanInterestPaid = 7,
    LoanFeePaid = 8,
}

public sealed record AccountingTransactionEntry(
    int Sequence,
    int MonthIndex,
    SimulationDate Date,
    string Description,
    double Amount,
    double CashAfterTransaction,
    AccountingTransactionKind Kind = AccountingTransactionKind.Unknown,
    string? ReferenceId = null)
{
    public bool IsIncome => Amount >= 0d;
}
