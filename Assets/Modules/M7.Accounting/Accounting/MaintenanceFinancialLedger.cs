using System;
using System.Collections.Generic;
using System.Linq;
using WrightTycoon.Core.Fleet;

namespace WrightTycoon.Core.Accounting;

public sealed class MaintenanceFinancialSnapshot
{
    public int FirstTrackedMonth { get; set; } = -1;
    public int LastProcessedMonth { get; set; } = -1;
    public Dictionary<string, FleetMaintenanceResult> TotalsByDesign { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, FleetMaintenanceResult> LastMonthByDesign { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>Realized maintenance sales and the manufacturing cost of the stock actually consumed.
/// Production cash spending remains in factory accounting; this ledger never books it a second time.</summary>
public sealed class MaintenanceFinancialLedger
{
    private readonly Dictionary<string, FleetMaintenanceResult> totals = new(StringComparer.Ordinal);
    private readonly Dictionary<string, FleetMaintenanceResult> lastMonth = new(StringComparer.Ordinal);
    public int FirstTrackedMonth { get; private set; } = -1;
    public int LastProcessedMonth { get; private set; } = -1;
    public double Revenue => totals.Values.Sum(value => value.Revenue);
    public double ProductionCost => totals.Values.Sum(value => value.ProductionCost);
    public double Profit => Revenue - ProductionCost;
    public FleetMaintenanceResult GetTotals(string designId) => totals.TryGetValue(designId, out var value) ? value : new(0d, 0d);
    public FleetMaintenanceResult GetLastMonth(string designId) => lastMonth.TryGetValue(designId, out var value) ? value : new(0d, 0d);

    public void RegisterMonth(int monthIndex, FleetMonthReport report)
    {
        if (monthIndex < 0) throw new ArgumentOutOfRangeException(nameof(monthIndex));
        if (report is null) throw new ArgumentNullException(nameof(report));
        if (monthIndex <= LastProcessedMonth) return;
        foreach (var pair in report.MaintenanceByDesign) Validate(pair.Key, pair.Value);
        lastMonth.Clear();
        foreach (var pair in report.MaintenanceByDesign)
        {
            var previous = GetTotals(pair.Key);
            totals[pair.Key] = new(previous.Revenue + pair.Value.Revenue, previous.ProductionCost + pair.Value.ProductionCost);
            lastMonth[pair.Key] = pair.Value;
        }
        if (FirstTrackedMonth < 0) FirstTrackedMonth = monthIndex;
        LastProcessedMonth = monthIndex;
    }

    public MaintenanceFinancialSnapshot CreateSnapshot() => new()
    {
        FirstTrackedMonth = FirstTrackedMonth, LastProcessedMonth = LastProcessedMonth,
        TotalsByDesign = new(totals, StringComparer.Ordinal), LastMonthByDesign = new(lastMonth, StringComparer.Ordinal),
    };

    public void RestoreSnapshot(MaintenanceFinancialSnapshot snapshot)
    {
        if (snapshot is null) throw new ArgumentNullException(nameof(snapshot));
        if (snapshot.FirstTrackedMonth < -1 || snapshot.LastProcessedMonth < -1
            || snapshot.FirstTrackedMonth > snapshot.LastProcessedMonth) throw new ArgumentException("Invalid maintenance accounting period.");
        foreach (var pair in snapshot.TotalsByDesign) Validate(pair.Key, pair.Value);
        foreach (var pair in snapshot.LastMonthByDesign) Validate(pair.Key, pair.Value);
        totals.Clear(); lastMonth.Clear();
        foreach (var pair in snapshot.TotalsByDesign) totals.Add(pair.Key, pair.Value);
        foreach (var pair in snapshot.LastMonthByDesign) lastMonth.Add(pair.Key, pair.Value);
        FirstTrackedMonth = snapshot.FirstTrackedMonth; LastProcessedMonth = snapshot.LastProcessedMonth;
    }

    private static void Validate(string id, FleetMaintenanceResult value)
    {
        if (string.IsNullOrWhiteSpace(id) || value is null) throw new ArgumentException("Invalid maintenance sales entry.");
        LifecycleCalculator.RequireFiniteNonNegative(value.Revenue, nameof(value.Revenue));
        LifecycleCalculator.RequireFiniteNonNegative(value.ProductionCost, nameof(value.ProductionCost));
    }
}
