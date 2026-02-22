using System.Collections.Generic;

namespace WrightTycoon.Core.Accounting;

public sealed class AccountingSystemSnapshot
{
    public double Cash { get; set; }

    public List<AccountingMonthSnapshot> Months { get; set; } = new();
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
}
