using System;
using System.Collections.Generic;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Factories;

namespace WrightTycoon.Core.Accounting;

public sealed class FactoryAccountingCalculator
{
    public AccountingCostBreakdown CalculateMonthlyCosts(
        FactoryMonthlyProductionReport report,
        IReadOnlyDictionary<string, FactoryAircraftTypeCatalogItem> catalog)
    {
        if (report is null)
        {
            throw new ArgumentNullException(nameof(report));
        }

        if (catalog is null)
        {
            throw new ArgumentNullException(nameof(catalog));
        }

        var fixedProductionLineExpenses = report.LineReports.Count * FactoryBalance.FixedMonthlyCostPerProductionLine;

        var variablePlaneStaffExpenses = 0d;
        var variablePlaneBuildExpenses = 0d;
        var variableEngineBuildExpenses = 0d;

        foreach (var line in report.LineReports)
        {
            variablePlaneStaffExpenses += line.Employees * FactoryBalance.PlaneStaffMonthlyCostPerEmployee;

            if (line.ProducedUnits <= 0 || string.IsNullOrWhiteSpace(line.AssignedDesignId))
            {
                continue;
            }

            if (!catalog.TryGetValue(line.AssignedDesignId, out var design))
            {
                continue;
            }

            var producedUnits = line.ProducedUnits;
            var aircraft = design.Aircraft;
            variablePlaneBuildExpenses += producedUnits * aircraft.Airframe.Performance.ProductionCost;
            variableEngineBuildExpenses += producedUnits * aircraft.Airframe.Parameters.EngineCount * aircraft.Engine.Performance.Cost;
        }

        var variableEngineStaffExpenses = report.EngineFactoryReport.TotalAutoAssignedEmployees
            * FactoryBalance.EngineStaffMonthlyCostPerEmployee;

        return new AccountingCostBreakdown(
            fixedProductionLineExpenses,
            variableEngineStaffExpenses,
            variableEngineBuildExpenses,
            variablePlaneStaffExpenses,
            variablePlaneBuildExpenses,
            0d);
    }

    public IReadOnlyDictionary<string, InventoryValueEntry> CalculateInventoryValueByDesign(
        IReadOnlyDictionary<string, int> warehouseUnitsByDesign,
        IReadOnlyDictionary<string, FactoryAircraftTypeCatalogItem> catalog)
    {
        if (warehouseUnitsByDesign is null)
        {
            throw new ArgumentNullException(nameof(warehouseUnitsByDesign));
        }

        if (catalog is null)
        {
            throw new ArgumentNullException(nameof(catalog));
        }

        var valuation = new Dictionary<string, InventoryValueEntry>(StringComparer.Ordinal);
        foreach (var warehouseEntry in warehouseUnitsByDesign)
        {
            if (warehouseEntry.Value <= 0)
            {
                continue;
            }

            if (!catalog.TryGetValue(warehouseEntry.Key, out var design))
            {
                continue;
            }

            var unitCost = design.Aircraft.Performance.TotalProductionCost;
            var totalValue = unitCost * warehouseEntry.Value;
            valuation[warehouseEntry.Key] = new InventoryValueEntry(
                warehouseEntry.Key,
                warehouseEntry.Value,
                unitCost,
                totalValue);
        }

        return valuation;
    }
}

