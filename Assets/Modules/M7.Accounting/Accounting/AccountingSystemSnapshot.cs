using System.Collections.Generic;

namespace WrightTycoon.Core.Accounting;

public sealed class AccountingSystemSnapshot
{
    public double Cash { get; set; }

    public List<AccountingMonthSnapshot> Months { get; set; } = new();

    public List<AccountingTransactionSnapshot> Transactions { get; set; } = new();
}

public sealed class AccountingMonthSnapshot
{
    public int MonthIndex { get; set; }

    public int Year { get; set; }

    public int Month { get; set; }

    public double CashAfterTransactions { get; set; }

    public double Income { get; set; }

    public AccountingCostBreakdown Costs { get; set; } = AccountingCostBreakdown.Zero;

    public bool HasFactoryCostsBooked { get; set; }

    public List<InventoryValueEntry> InventoryEntries { get; set; } = new();

    public double InventoryValue { get; set; }

    public double LoanInterestExpenses { get; set; }

    public double LoanFeeExpenses { get; set; }

    public double LoanPrincipalReceived { get; set; }

    public double LoanPrincipalRepaid { get; set; }
}

public sealed class AccountingTransactionSnapshot
{
    public int Sequence { get; set; }

    public int MonthIndex { get; set; }

    public int Year { get; set; }

    public int Month { get; set; }

    public string Description { get; set; } = string.Empty;

    public double Amount { get; set; }

    public double CashAfterTransaction { get; set; }

    public int Kind { get; set; }

    public string? ReferenceId { get; set; }
}
