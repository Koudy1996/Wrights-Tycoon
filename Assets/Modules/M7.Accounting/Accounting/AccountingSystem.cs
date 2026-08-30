using System;
using System.Collections.Generic;
using WrightTycoon.Core.Math;
using WrightTycoon.Core.Simulation;

namespace WrightTycoon.Core.Accounting;

public sealed class AccountingSystem
{
    private readonly Dictionary<int, MonthlyAccumulator> _monthAccumulators = new();
    private readonly List<AccountingTransactionEntry> _transactions = new();
    private int _nextTransactionSequence = 1;

    public AccountingSystem(double startingCash)
    {
        Numeric.ThrowIfNotFinite(startingCash, nameof(startingCash));
        Cash = startingCash;
    }

    public double Cash { get; private set; }

    public IReadOnlyList<AccountingTransactionEntry> GetTransactions() => _transactions.AsReadOnly();

    public IReadOnlyList<MonthlyAccountingSnapshot> GetHistory()
    {
        var accumulators = new MonthlyAccumulator[_monthAccumulators.Count];
        var index = 0;
        foreach (var entry in _monthAccumulators.Values)
        {
            accumulators[index++] = entry;
        }

        Array.Sort(accumulators, static (left, right) => left.MonthIndex.CompareTo(right.MonthIndex));

        var snapshots = new MonthlyAccountingSnapshot[accumulators.Length];
        for (var snapshotIndex = 0; snapshotIndex < accumulators.Length; snapshotIndex++)
        {
            snapshots[snapshotIndex] = accumulators[snapshotIndex].ToSnapshot();
        }

        return Array.AsReadOnly(snapshots);
    }

    public MonthlyAccountingSnapshot? TryGetLatestSnapshot()
    {
        if (_monthAccumulators.Count == 0)
        {
            return null;
        }

        MonthlyAccumulator? latest = null;
        foreach (var accumulator in _monthAccumulators.Values)
        {
            if (latest is null || accumulator.MonthIndex > latest.MonthIndex)
            {
                latest = accumulator;
            }
        }

        return latest?.ToSnapshot();
    }

    public MonthlyAccountingSnapshot? TryGetSnapshot(int monthIndex)
    {
        if (!_monthAccumulators.TryGetValue(monthIndex, out var accumulator))
        {
            return null;
        }

        return accumulator.ToSnapshot();
    }

    public AccountingSystemSnapshot CreateSnapshot()
    {
        var snapshot = new AccountingSystemSnapshot
        {
            Cash = Cash,
        };

        foreach (var accumulator in _monthAccumulators.Values)
        {
            snapshot.Months.Add(accumulator.ToSnapshotState());
        }

        snapshot.Months.Sort(static (left, right) => left.MonthIndex.CompareTo(right.MonthIndex));

        for (var index = 0; index < _transactions.Count; index++)
        {
            var transaction = _transactions[index];
            snapshot.Transactions.Add(new AccountingTransactionSnapshot
            {
                Sequence = transaction.Sequence,
                MonthIndex = transaction.MonthIndex,
                Year = transaction.Date.Year,
                Month = transaction.Date.Month,
                Description = transaction.Description,
                Amount = transaction.Amount,
                CashAfterTransaction = transaction.CashAfterTransaction,
                Kind = (int)transaction.Kind,
                ReferenceId = transaction.ReferenceId,
            });
        }

        snapshot.Transactions.Sort(static (left, right) => left.Sequence.CompareTo(right.Sequence));
        return snapshot;
    }

    public void RestoreSnapshot(AccountingSystemSnapshot snapshot)
    {
        if (snapshot is null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        Numeric.ThrowIfNotFinite(snapshot.Cash, nameof(snapshot.Cash));

        _monthAccumulators.Clear();
        _transactions.Clear();
        _nextTransactionSequence = 1;

        if (snapshot.Months is not null)
        {
            for (var index = 0; index < snapshot.Months.Count; index++)
            {
                var month = snapshot.Months[index];
                if (month is null)
                {
                    continue;
                }

                if (month.MonthIndex < 0)
                {
                    continue;
                }

                var date = new SimulationDate(
                    month.Year <= 0 ? 1 : month.Year,
                    month.Month < 1 ? 1 : month.Month > 12 ? 12 : month.Month);

                var accumulator = new MonthlyAccumulator(month.MonthIndex, date, month.CashAfterTransactions)
                {
                    Income = month.Income,
                    Costs = month.Costs ?? AccountingCostBreakdown.Zero,
                    HasFactoryCostsBooked = month.HasFactoryCostsBooked,
                    CashAfterTransactions = month.CashAfterTransactions,
                    LoanInterestExpenses = System.Math.Max(0d, month.LoanInterestExpenses),
                    LoanFeeExpenses = System.Math.Max(0d, month.LoanFeeExpenses),
                    LoanPrincipalReceived = System.Math.Max(0d, month.LoanPrincipalReceived),
                    LoanPrincipalRepaid = System.Math.Max(0d, month.LoanPrincipalRepaid),
                };

                if (month.InventoryEntries is not null)
                {
                    var inventory = new Dictionary<string, InventoryValueEntry>(StringComparer.Ordinal);
                    for (var inventoryIndex = 0; inventoryIndex < month.InventoryEntries.Count; inventoryIndex++)
                    {
                        var entry = month.InventoryEntries[inventoryIndex];
                        if (entry is null || string.IsNullOrWhiteSpace(entry.DesignId))
                        {
                            continue;
                        }

                        inventory[entry.DesignId] = entry;
                    }

                    accumulator.SetInventory(inventory);
                }

                _monthAccumulators[accumulator.MonthIndex] = accumulator;
            }
        }

        if (snapshot.Transactions is not null)
        {
            for (var index = 0; index < snapshot.Transactions.Count; index++)
            {
                var transaction = snapshot.Transactions[index];
                if (transaction is null)
                {
                    continue;
                }

                if (transaction.Sequence <= 0 || transaction.MonthIndex < 0 || string.IsNullOrWhiteSpace(transaction.Description))
                {
                    continue;
                }

                Numeric.ThrowIfNotFinite(transaction.Amount, nameof(snapshot.Transactions));
                Numeric.ThrowIfNotFinite(transaction.CashAfterTransaction, nameof(snapshot.Transactions));

                var year = transaction.Year <= 0 ? 1 : transaction.Year;
                var month = transaction.Month < 1 ? 1 : transaction.Month > 12 ? 12 : transaction.Month;
                var date = new SimulationDate(year, month);

                _transactions.Add(new AccountingTransactionEntry(
                    transaction.Sequence,
                    transaction.MonthIndex,
                    date,
                    transaction.Description.Trim(),
                    transaction.Amount,
                    transaction.CashAfterTransaction,
                    Enum.IsDefined(typeof(AccountingTransactionKind), transaction.Kind)
                        ? (AccountingTransactionKind)transaction.Kind
                        : AccountingTransactionKind.Unknown,
                    string.IsNullOrWhiteSpace(transaction.ReferenceId) ? null : transaction.ReferenceId.Trim()));

                if (transaction.Sequence >= _nextTransactionSequence)
                {
                    _nextTransactionSequence = transaction.Sequence + 1;
                }
            }

            _transactions.Sort(static (left, right) => left.Sequence.CompareTo(right.Sequence));
        }

        Cash = snapshot.Cash;
    }

    public void RegisterMonthlyFactoryCosts(
        int monthIndex,
        SimulationDate date,
        AccountingCostBreakdown costs,
        IReadOnlyDictionary<string, InventoryValueEntry> inventoryByDesign)
    {
        if (monthIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(monthIndex), "Month index must be >= 0.");
        }

        if (costs is null)
        {
            throw new ArgumentNullException(nameof(costs));
        }

        if (inventoryByDesign is null)
        {
            throw new ArgumentNullException(nameof(inventoryByDesign));
        }

        var accumulator = GetOrCreateAccumulator(monthIndex, date);

        if (accumulator.HasFactoryCostsBooked)
        {
            AppendTransaction(
                monthIndex,
                date,
                "Factory cost reversal (rebook).",
                accumulator.Costs.TotalCosts);
        }

        accumulator.Costs = costs;
        accumulator.HasFactoryCostsBooked = true;

        ApplyFactoryCostTransactions(monthIndex, date, costs);

        accumulator.SetInventory(inventoryByDesign);
        accumulator.CashAfterTransactions = Cash;
    }

    public void RegisterContractIncome(
        int monthIndex,
        SimulationDate date,
        double income,
        IReadOnlyDictionary<string, InventoryValueEntry> inventoryByDesign,
        string description = "Contract income.")
    {
        if (monthIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(monthIndex), "Month index must be >= 0.");
        }

        Numeric.ThrowIfNotFinite(income, nameof(income));
        if (income < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(income), "Income must be >= 0.");
        }

        if (inventoryByDesign is null)
        {
            throw new ArgumentNullException(nameof(inventoryByDesign));
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Income description cannot be empty.", nameof(description));
        }

        var accumulator = GetOrCreateAccumulator(monthIndex, date);
        accumulator.Income += income;
        accumulator.SetInventory(inventoryByDesign);

        AppendTransaction(monthIndex, date, description.Trim(), income, AccountingTransactionKind.OperatingIncome);
        accumulator.CashAfterTransactions = Cash;
    }

    public void RegisterCapitalExpense(
        int monthIndex,
        SimulationDate date,
        double expense,
        string description)
    {
        if (monthIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(monthIndex), "Month index must be >= 0.");
        }

        Numeric.ThrowIfNotFinite(expense, nameof(expense));
        if (expense < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(expense), "Expense must be >= 0.");
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Description cannot be empty.", nameof(description));
        }

        var accumulator = GetOrCreateAccumulator(monthIndex, date);
        AppendTransaction(monthIndex, date, description.Trim(), -expense, AccountingTransactionKind.CapitalExpense);
        accumulator.CashAfterTransactions = Cash;
    }

    public void RegisterLoanDisbursement(
        int monthIndex,
        SimulationDate date,
        double principal,
        string loanId,
        string description = "Loan proceeds.")
    {
        ValidateLoanTransaction(monthIndex, principal, loanId, description, nameof(principal));
        var accumulator = GetOrCreateAccumulator(monthIndex, date);
        accumulator.LoanPrincipalReceived += principal;
        AppendTransaction(monthIndex, date, description.Trim(), principal, AccountingTransactionKind.LoanPrincipalReceived, loanId.Trim());
        accumulator.CashAfterTransactions = Cash;
    }

    public void RegisterLoanDebtService(
        int monthIndex,
        SimulationDate date,
        double interest,
        double principal,
        string loanId)
    {
        ValidateLoanTransaction(monthIndex, interest, loanId, "Loan interest payment.", nameof(interest));
        Numeric.ThrowIfNotFinite(principal, nameof(principal));
        if (principal < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(principal), "Principal must be >= 0.");
        }

        var accumulator = GetOrCreateAccumulator(monthIndex, date);
        if (interest > 0d)
        {
            accumulator.LoanInterestExpenses += interest;
            AppendTransaction(monthIndex, date, "Loan interest payment.", -interest, AccountingTransactionKind.LoanInterestPaid, loanId.Trim());
        }

        if (principal > 0d)
        {
            accumulator.LoanPrincipalRepaid += principal;
            AppendTransaction(monthIndex, date, "Loan principal repayment.", -principal, AccountingTransactionKind.LoanPrincipalRepaid, loanId.Trim());
        }

        accumulator.CashAfterTransactions = Cash;
    }

    public void RegisterLoanFee(
        int monthIndex,
        SimulationDate date,
        double fee,
        string loanId,
        string description = "Loan fee.")
    {
        ValidateLoanTransaction(monthIndex, fee, loanId, description, nameof(fee));
        var accumulator = GetOrCreateAccumulator(monthIndex, date);
        accumulator.LoanFeeExpenses += fee;
        AppendTransaction(monthIndex, date, description.Trim(), -fee, AccountingTransactionKind.LoanFeePaid, loanId.Trim());
        accumulator.CashAfterTransactions = Cash;
    }

    private static void ValidateLoanTransaction(int monthIndex, double amount, string loanId, string description, string parameterName)
    {
        if (monthIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(monthIndex), "Month index must be >= 0.");
        }

        Numeric.ThrowIfNotFinite(amount, parameterName);
        if (amount < 0d)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Amount must be >= 0.");
        }

        if (string.IsNullOrWhiteSpace(loanId))
        {
            throw new ArgumentException("Loan id cannot be empty.", nameof(loanId));
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Description cannot be empty.", nameof(description));
        }
    }

    private void ApplyFactoryCostTransactions(int monthIndex, SimulationDate date, AccountingCostBreakdown costs)
    {
        ApplyCostComponentTransaction(monthIndex, date, "Factory property maintenance.", costs.FactoryMaintenanceExpenses);
        ApplyCostComponentTransaction(monthIndex, date, "Active production equipment maintenance.", costs.ActiveEquipmentExpenses);
        ApplyCostComponentTransaction(monthIndex, date, "Factory variable costs - engine staff.", costs.VariableEngineStaffExpenses);
        ApplyCostComponentTransaction(monthIndex, date, "Factory variable costs - engine build.", costs.VariableEngineBuildExpenses);
        ApplyCostComponentTransaction(monthIndex, date, "Factory variable costs - aircraft staff.", costs.VariablePlaneStaffExpenses);
        ApplyCostComponentTransaction(monthIndex, date, "Factory variable costs - aircraft build.", costs.VariablePlaneBuildExpenses);
        ApplyCostComponentTransaction(monthIndex, date, "Factory variable costs - R&D.", costs.VariableResearchExpenses);
        ApplyCostComponentTransaction(monthIndex, date, "Headquarters operating costs.", costs.HeadquartersExpenses);
    }

    private void ApplyCostComponentTransaction(int monthIndex, SimulationDate date, string description, double amount)
    {
        Numeric.ThrowIfNotFinite(amount, nameof(amount));
        if (amount <= 0d)
        {
            return;
        }

        AppendTransaction(monthIndex, date, description, -amount, AccountingTransactionKind.OperatingExpense);
    }

    private void AppendTransaction(
        int monthIndex,
        SimulationDate date,
        string description,
        double amount,
        AccountingTransactionKind kind = AccountingTransactionKind.Unknown,
        string? referenceId = null)
    {
        Numeric.ThrowIfNotFinite(amount, nameof(amount));
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Transaction description cannot be empty.", nameof(description));
        }

        Cash += amount;
        _transactions.Add(new AccountingTransactionEntry(
            _nextTransactionSequence++,
            monthIndex,
            date,
            description.Trim(),
            amount,
            Cash,
            kind,
            referenceId));
    }

    private MonthlyAccumulator GetOrCreateAccumulator(int monthIndex, SimulationDate date)
    {
        if (_monthAccumulators.TryGetValue(monthIndex, out var existing))
        {
            existing.Date = date;
            return existing;
        }

        var created = new MonthlyAccumulator(monthIndex, date, Cash);
        _monthAccumulators.Add(monthIndex, created);
        return created;
    }

    private sealed class MonthlyAccumulator
    {
        public MonthlyAccumulator(int monthIndex, SimulationDate date, double cashAtCreation)
        {
            MonthIndex = monthIndex;
            Date = date;
            CashAfterTransactions = cashAtCreation;
        }

        public int MonthIndex { get; }

        public SimulationDate Date { get; set; }

        public double Income { get; set; }

        public AccountingCostBreakdown Costs { get; set; } = AccountingCostBreakdown.Zero;

        public bool HasFactoryCostsBooked { get; set; }

        public double InventoryValue { get; private set; }

        public double LoanInterestExpenses { get; set; }

        public double LoanFeeExpenses { get; set; }

        public double LoanPrincipalReceived { get; set; }

        public double LoanPrincipalRepaid { get; set; }

        public Dictionary<string, InventoryValueEntry> InventoryByDesign { get; } = new(StringComparer.Ordinal);

        public double CashAfterTransactions { get; set; }

        public void SetInventory(IReadOnlyDictionary<string, InventoryValueEntry> entries)
        {
            InventoryByDesign.Clear();

            var inventoryValue = 0d;
            foreach (var entry in entries)
            {
                InventoryByDesign[entry.Key] = entry.Value;
                inventoryValue += entry.Value.TotalValue;
            }

            InventoryValue = inventoryValue;
        }

        public MonthlyAccountingSnapshot ToSnapshot()
        {
            return new MonthlyAccountingSnapshot(
                MonthIndex,
                Date,
                CashAfterTransactions,
                Income,
                Costs,
                new Dictionary<string, InventoryValueEntry>(InventoryByDesign, StringComparer.Ordinal),
                InventoryValue,
                LoanInterestExpenses,
                LoanFeeExpenses,
                LoanPrincipalReceived,
                LoanPrincipalRepaid);
        }

        public AccountingMonthSnapshot ToSnapshotState()
        {
            var inventoryEntries = new List<InventoryValueEntry>(InventoryByDesign.Count);
            foreach (var entry in InventoryByDesign.Values)
            {
                inventoryEntries.Add(entry);
            }

            return new AccountingMonthSnapshot
            {
                MonthIndex = MonthIndex,
                Year = Date.Year,
                Month = Date.Month,
                CashAfterTransactions = CashAfterTransactions,
                Income = Income,
                Costs = Costs,
                HasFactoryCostsBooked = HasFactoryCostsBooked,
                InventoryEntries = inventoryEntries,
                InventoryValue = InventoryValue,
                LoanInterestExpenses = LoanInterestExpenses,
                LoanFeeExpenses = LoanFeeExpenses,
                LoanPrincipalReceived = LoanPrincipalReceived,
                LoanPrincipalRepaid = LoanPrincipalRepaid,
            };
        }
    }
}
