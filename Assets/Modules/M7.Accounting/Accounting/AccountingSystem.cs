using System;
using System.Collections.Generic;
using WrightTycoon.Core.Math;
using WrightTycoon.Core.Simulation;

namespace WrightTycoon.Core.Accounting;

public sealed class AccountingSystem
{
    private readonly Dictionary<int, MonthlyAccumulator> _monthAccumulators = new();

    public AccountingSystem(double startingCash)
    {
        Numeric.ThrowIfNotFinite(startingCash, nameof(startingCash));
        Cash = startingCash;
    }

    public double Cash { get; private set; }

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
            Cash += accumulator.Costs.TotalCosts;
        }

        accumulator.Costs = costs;
        accumulator.HasFactoryCostsBooked = true;

        Cash -= costs.TotalCosts;

        accumulator.SetInventory(inventoryByDesign);
        accumulator.CashAfterTransactions = Cash;
    }

    public void RegisterContractIncome(
        int monthIndex,
        SimulationDate date,
        double income,
        IReadOnlyDictionary<string, InventoryValueEntry> inventoryByDesign)
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

        var accumulator = GetOrCreateAccumulator(monthIndex, date);
        accumulator.Income += income;
        accumulator.SetInventory(inventoryByDesign);

        Cash += income;
        accumulator.CashAfterTransactions = Cash;
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
                InventoryValue);
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
            };
        }
    }
}
