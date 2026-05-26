using System;
using System.Collections.Generic;
using System.Linq;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Math;
using WrightTycoon.Core.Simulation;

namespace WrightTycoon.Core.Factories;

public sealed class AircraftProductionFactory
{
    public const double EmployeeManDaysPerMonth = FactoryBalance.EmployeeManDaysPerMonth;

    private readonly Dictionary<string, AircraftProductionLineState> _lines = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _warehouseUnitsByDesign = new(StringComparer.Ordinal);
    private int _lineSequence;

    public int FactoryToolLevel { get; private set; } = FactoryBalance.InitialFactoryToolLevel;

    public IReadOnlyList<AircraftProductionLineState> Lines => _lines.Values.OrderBy(line => line.LineId, StringComparer.Ordinal).ToArray();

    public IReadOnlyDictionary<string, int> WarehouseUnitsByDesign => new Dictionary<string, int>(_warehouseUnitsByDesign, StringComparer.Ordinal);

    public string AddLine(string? requestedId = null)
    {
        var id = string.IsNullOrWhiteSpace(requestedId)
            ? $"line.{_lineSequence + 1:000}"
            : requestedId.Trim();

        if (_lines.ContainsKey(id))
        {
            throw new InvalidOperationException($"Line '{id}' already exists.");
        }

        _lines.Add(id, new AircraftProductionLineState(id));
        _lineSequence++;
        return id;
    }

    public bool RemoveLine(string lineId)
    {
        if (string.IsNullOrWhiteSpace(lineId))
        {
            throw new ArgumentException("Line id cannot be empty.", nameof(lineId));
        }

        return _lines.Remove(lineId.Trim());
    }

    public void AssignDesign(string lineId, string? designId) => GetLine(lineId).AssignDesign(designId);

    public void SetMonthlyProductionTargetUnits(string lineId, int targetUnits) => GetLine(lineId).SetMonthlyProductionTargetUnits(targetUnits);

    public void SetTotalProductionTargetUnits(string lineId, int targetUnits) => GetLine(lineId).SetTotalProductionTargetUnits(targetUnits);

    public void UpgradeFactoryToolLevel(int targetLevel)
    {
        if (targetLevel < FactoryToolLevel)
        {
            throw new InvalidOperationException("Factory tooling cannot be downgraded.");
        }

        if (targetLevel < FactoryBalance.MinimumToolLevel)
        {
            throw new ArgumentOutOfRangeException(nameof(targetLevel), $"Factory tool level must be >= {FactoryBalance.MinimumToolLevel}.");
        }

        FactoryToolLevel = targetLevel;
    }

    public void UpgradeLineToolLevel(string lineId, int targetLevel) => GetLine(lineId).UpgradeLineToolLevel(targetLevel);

    public int GetWarehouseUnits(string designId)
    {
        if (string.IsNullOrWhiteSpace(designId))
        {
            return 0;
        }

        return _warehouseUnitsByDesign.TryGetValue(designId.Trim(), out var units)
            ? units
            : 0;
    }

    public void AddWarehouseUnits(string designId, int units)
    {
        if (string.IsNullOrWhiteSpace(designId))
        {
            throw new ArgumentException("Design id cannot be empty.", nameof(designId));
        }

        if (units < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(units), "Units must be >= 0.");
        }

        if (units == 0)
        {
            return;
        }

        var key = designId.Trim();
        var current = GetWarehouseUnits(key);
        _warehouseUnitsByDesign[key] = checked(current + units);
    }

    public bool TryWithdrawFromWarehouse(string designId, int units, out string message)
    {
        if (string.IsNullOrWhiteSpace(designId))
        {
            throw new ArgumentException("Design id cannot be empty.", nameof(designId));
        }

        if (units <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(units), "Units must be > 0.");
        }

        var key = designId.Trim();
        var available = GetWarehouseUnits(key);
        if (available < units)
        {
            message = $"Warehouse has {available} units, but {units} are required.";
            return false;
        }

        var remaining = available - units;
        if (remaining == 0)
        {
            _warehouseUnitsByDesign.Remove(key);
        }
        else
        {
            _warehouseUnitsByDesign[key] = remaining;
        }

        message = $"Withdrew {units} units. Remaining stock: {remaining}.";
        return true;
    }

    public FactoryMonthlyProductionReport SimulateMonth(
        IReadOnlyDictionary<string, FactoryAircraftTypeCatalogItem> catalog,
        int monthIndex,
        SimulationDate date)
    {
        if (catalog is null)
        {
            throw new ArgumentNullException(nameof(catalog));
        }

        if (monthIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(monthIndex), "Month index must be >= 0.");
        }

        foreach (var item in catalog.Values)
        {
            item.Validate();
        }

        var lineReports = new List<LineProductionReport>(_lines.Count);
        var engineReports = new List<EngineProductionReport>(_lines.Count);
        var linesToAutoRemove = new List<string>();

        var totalAvailableManDays = 0d;
        var totalUsedManDays = 0d;
        var totalProducedAircraft = 0;

        foreach (var line in _lines.Values.OrderBy(entry => entry.LineId, StringComparer.Ordinal))
        {
            var carryIn = line.CarryOverManDays;
            var availableManDays = carryIn;
            var targetUnits = line.MonthlyProductionTargetUnits;
            var totalTargetUnits = line.TotalProductionTargetUnits;
            line.SetAutoAssignedEmployees(0);

            var producedUnits = 0;
            var usedManDays = 0d;
            var requiredFactoryToolLevel = 1;
            var requiredLineToolLevel = 1;
            var requiredEngines = 0;
            var engineFactoryEmployees = 0;
            var engineDiscarded = 0;
            var designName = "Unassigned";
            var blocked = false;
            var status = "Idle";
            var capacityUnits = 0;
            var remainingTotalTargetUnits = totalTargetUnits > 0
                ? System.Math.Max(totalTargetUnits - line.LifetimeProducedUnits, 0)
                : int.MaxValue;

            if (totalTargetUnits > 0 && remainingTotalTargetUnits <= 0)
            {
                blocked = true;
                status = $"Total target reached ({line.LifetimeProducedUnits}/{totalTargetUnits}). Line removed.";
                availableManDays = 0d;
                line.SetCarryOverWithoutProduction(0d);
                linesToAutoRemove.Add(line.LineId);
            }
            else if (string.IsNullOrWhiteSpace(line.AssignedDesignId))
            {
                blocked = true;
                status = "No aircraft type assigned.";
                line.SetCarryOverWithoutProduction(availableManDays);
            }
            else if (!catalog.TryGetValue(line.AssignedDesignId, out var catalogItem))
            {
                blocked = true;
                status = $"Unknown aircraft type '{line.AssignedDesignId}'.";
                line.SetCarryOverWithoutProduction(availableManDays);
            }
            else
            {
                designName = catalogItem.DisplayName;
                requiredFactoryToolLevel = catalogItem.RequiredFactoryToolLevel;
                var unitManDays = catalogItem.Aircraft.Performance.TotalProductionManDays;
                if (unitManDays <= Numeric.Epsilon)
                {
                    blocked = true;
                    status = "Aircraft production MD is invalid.";
                    line.SetCarryOverWithoutProduction(availableManDays);
                }
                else if (FactoryToolLevel < requiredFactoryToolLevel)
                {
                    blocked = true;
                    status = $"Blocked by factory tool level ({FactoryToolLevel}/{requiredFactoryToolLevel}).";
                    line.SetCarryOverWithoutProduction(availableManDays);
                }
                else
                {
                    var monthTargetUnits = targetUnits;
                    if (totalTargetUnits > 0)
                    {
                        monthTargetUnits = System.Math.Min(monthTargetUnits, remainingTotalTargetUnits);
                    }

                    if (monthTargetUnits <= 0)
                    {
                        status = "No production target set.";
                        line.SetCarryOverWithoutProduction(availableManDays);
                    }
                    else
                    {
                        var requiredManDays = monthTargetUnits * unitManDays;
                        var requiredAdditionalManDays = System.Math.Max(0d, requiredManDays - carryIn);
                        var autoAssignedEmployees = (int)System.Math.Ceiling(requiredAdditionalManDays / EmployeeManDaysPerMonth);
                        line.SetAutoAssignedEmployees(autoAssignedEmployees);
                        availableManDays = carryIn + (autoAssignedEmployees * EmployeeManDaysPerMonth);

                        capacityUnits = System.Math.Max(0, (int)System.Math.Floor(availableManDays / unitManDays));
                        producedUnits = System.Math.Min(monthTargetUnits, capacityUnits);
                        usedManDays = producedUnits * unitManDays;
                        var carryOut = System.Math.Max(0d, availableManDays - usedManDays);
                        line.ApplyMonthResult(producedUnits, carryOut);
                        status = producedUnits >= monthTargetUnits
                            ? $"Produced {producedUnits} aircraft."
                            : $"Produced {producedUnits}/{monthTargetUnits} aircraft.";

                        if (totalTargetUnits > 0 && line.LifetimeProducedUnits >= totalTargetUnits)
                        {
                            status += $" Total target reached ({line.LifetimeProducedUnits}/{totalTargetUnits}). Line removed.";
                            linesToAutoRemove.Add(line.LineId);
                        }

                        if (producedUnits > 0 && !string.IsNullOrWhiteSpace(line.AssignedDesignId))
                        {
                            AddWarehouseUnits(line.AssignedDesignId, producedUnits);
                        }

                        requiredEngines = producedUnits * catalogItem.Aircraft.Airframe.Parameters.EngineCount;
                        if (requiredEngines > 0)
                        {
                            var engineManDaysPerUnit = System.Math.Max(catalogItem.Aircraft.Engine.Performance.ProductionManDays, Numeric.Epsilon);
                            var requiredEngineManDays = requiredEngines * engineManDaysPerUnit;
                            engineFactoryEmployees = (int)System.Math.Ceiling(requiredEngineManDays / EmployeeManDaysPerMonth);
                            var engineCapacity = (int)System.Math.Floor((engineFactoryEmployees * EmployeeManDaysPerMonth) / engineManDaysPerUnit);
                            engineDiscarded = System.Math.Max(0, engineCapacity - requiredEngines);

                            engineReports.Add(new EngineProductionReport(
                                line.LineId,
                                catalogItem.Aircraft.Engine.Definition.DisplayName,
                                requiredEngines,
                                requiredEngines,
                                engineDiscarded,
                                engineFactoryEmployees,
                                requiredEngineManDays,
                                engineFactoryEmployees * EmployeeManDaysPerMonth));
                        }
                    }
                }
            }

            totalAvailableManDays += availableManDays;
            totalUsedManDays += usedManDays;
            totalProducedAircraft += producedUnits;

            lineReports.Add(new LineProductionReport(
                line.LineId,
                line.AssignedDesignId,
                designName,
                line.Employees,
                targetUnits,
                totalTargetUnits,
                line.LifetimeProducedUnits,
                line.LineToolLevel,
                producedUnits,
                availableManDays,
                usedManDays,
                line.CarryOverManDays,
                capacityUnits,
                blocked,
                status,
                requiredFactoryToolLevel,
                requiredLineToolLevel,
                requiredEngines,
                engineFactoryEmployees,
                engineDiscarded,
                string.IsNullOrWhiteSpace(line.AssignedDesignId) ? 0 : GetWarehouseUnits(line.AssignedDesignId)));
        }

        for (var removeIndex = 0; removeIndex < linesToAutoRemove.Count; removeIndex++)
        {
            _lines.Remove(linesToAutoRemove[removeIndex]);
        }

        var totalEngineRequired = 0;
        var totalEngineProduced = 0;
        var totalEngineDiscarded = 0;
        var totalEngineEmployees = 0;
        var totalEngineRequiredManDays = 0d;
        var totalEngineCapacityManDays = 0d;
        for (var engineIndex = 0; engineIndex < engineReports.Count; engineIndex++)
        {
            var report = engineReports[engineIndex];
            totalEngineRequired += report.RequiredEngines;
            totalEngineProduced += report.ProducedEngines;
            totalEngineDiscarded += report.DiscardedEngines;
            totalEngineEmployees += report.AutoAssignedEmployees;
            totalEngineRequiredManDays += report.RequiredManDays;
            totalEngineCapacityManDays += report.CapacityManDays;
        }

        var engineFactoryReport = new EngineFactoryProductionReport(
            engineReports,
            totalEngineRequired,
            totalEngineProduced,
            totalEngineDiscarded,
            totalEngineEmployees,
            totalEngineRequiredManDays,
            totalEngineCapacityManDays);

        var utilization = totalAvailableManDays <= Numeric.Epsilon
            ? 0d
            : Numeric.Clamp(totalUsedManDays / totalAvailableManDays, 0d, 1d);

        var warehouseSnapshot = new Dictionary<string, int>(_warehouseUnitsByDesign, StringComparer.Ordinal);
        var totalWarehouseUnits = 0;
        foreach (var units in warehouseSnapshot.Values)
        {
            totalWarehouseUnits += units;
        }

        return new FactoryMonthlyProductionReport(
            monthIndex,
            date,
            lineReports,
            engineFactoryReport,
            FactoryToolLevel,
            totalAvailableManDays,
            totalUsedManDays,
            utilization,
            totalProducedAircraft,
            warehouseSnapshot,
            totalWarehouseUnits);
    }

    public FactoryStateSnapshot CreateSnapshot()
    {
        var snapshot = new FactoryStateSnapshot
        {
            FactoryToolLevel = FactoryToolLevel,
            LineSequence = _lineSequence,
        };

        foreach (var line in _lines.Values.OrderBy(entry => entry.LineId, StringComparer.Ordinal))
        {
            snapshot.Lines.Add(new FactoryLineSnapshot
            {
                LineId = line.LineId,
                AssignedDesignId = line.AssignedDesignId,
                MonthlyProductionTargetUnits = line.MonthlyProductionTargetUnits,
                TotalProductionTargetUnits = line.TotalProductionTargetUnits,
                LifetimeProducedUnits = line.LifetimeProducedUnits,
                Employees = line.Employees,
                CarryOverManDays = line.CarryOverManDays,
                LineToolLevel = line.LineToolLevel,
            });
        }

        foreach (var entry in _warehouseUnitsByDesign.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            snapshot.WarehouseEntries.Add(new FactoryWarehouseEntrySnapshot
            {
                DesignId = entry.Key,
                Units = entry.Value,
            });
        }

        return snapshot;
    }

    public void RestoreSnapshot(FactoryStateSnapshot snapshot)
    {
        if (snapshot is null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        _lines.Clear();
        _warehouseUnitsByDesign.Clear();

        FactoryToolLevel = System.Math.Max(snapshot.FactoryToolLevel, FactoryBalance.MinimumToolLevel);
        _lineSequence = System.Math.Max(snapshot.LineSequence, 0);

        if (snapshot.Lines is not null)
        {
            for (var index = 0; index < snapshot.Lines.Count; index++)
            {
                var lineSnapshot = snapshot.Lines[index];
                if (lineSnapshot is null || string.IsNullOrWhiteSpace(lineSnapshot.LineId))
                {
                    continue;
                }

                if (_lines.ContainsKey(lineSnapshot.LineId))
                {
                    continue;
                }

                var state = new AircraftProductionLineState(lineSnapshot.LineId);
                state.AssignDesign(lineSnapshot.AssignedDesignId);
                var restoredTargetUnits = lineSnapshot.MonthlyProductionTargetUnits > 0
                    ? lineSnapshot.MonthlyProductionTargetUnits
                    : lineSnapshot.Employees;
                restoredTargetUnits = System.Math.Clamp(restoredTargetUnits, 0, FactoryBalance.MaximumAircraftPerLinePerMonth);
                state.SetMonthlyProductionTargetUnits(restoredTargetUnits);
                state.SetTotalProductionTargetUnits(System.Math.Max(lineSnapshot.TotalProductionTargetUnits, 0));
                state.SetLifetimeProducedUnits(System.Math.Max(lineSnapshot.LifetimeProducedUnits, 0));
                state.SetAutoAssignedEmployees(System.Math.Max(lineSnapshot.Employees, 0));
                state.SetCarryOverWithoutProduction(System.Math.Max(lineSnapshot.CarryOverManDays, 0d));

                _lines.Add(state.LineId, state);
            }
        }

        if (snapshot.WarehouseEntries is not null)
        {
            for (var index = 0; index < snapshot.WarehouseEntries.Count; index++)
            {
                var entry = snapshot.WarehouseEntries[index];
                if (entry is null || string.IsNullOrWhiteSpace(entry.DesignId) || entry.Units <= 0)
                {
                    continue;
                }

                _warehouseUnitsByDesign[entry.DesignId] = entry.Units;
            }
        }

        if (_lineSequence < _lines.Count)
        {
            _lineSequence = _lines.Count;
        }
    }

    private AircraftProductionLineState GetLine(string lineId)
    {
        if (string.IsNullOrWhiteSpace(lineId))
        {
            throw new ArgumentException("Line id cannot be empty.", nameof(lineId));
        }

        if (!_lines.TryGetValue(lineId.Trim(), out var line))
        {
            throw new KeyNotFoundException($"Line '{lineId}' was not found.");
        }

        return line;
    }
}
