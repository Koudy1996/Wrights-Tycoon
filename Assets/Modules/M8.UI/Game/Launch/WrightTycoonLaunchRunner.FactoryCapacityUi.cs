using System;
using System.Linq;
using UnityEngine;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Factories;

namespace WrightTycoon.Game.Launch;

public sealed partial class WrightTycoonLaunchRunner
{

    private void DrawFactoryBuildingControls(FactorySiteState factory)
    {
        if (_productionFactory is null) return;
        DrawModernSectionHeader($"{factory.BuildingSize} Building", "Physical space and workforce are separate limits.");
        DrawFactoryCapacityInfo("Physical Capacity", $"{FormatUiNumber(factory.BuildingCapacityManDays, "0")} MD / month");
        DrawFactoryCapacityInfo("Property Maintenance", $"{FormatCurrency(FactoryBalance.GetBuildingMonthlyMaintenance(factory.BuildingSize))} / month");
        var standardCapacity = FactoryBalance.GetBuildingCapacityManDays(factory.BuildingSize);
        if (factory.LegacyCapacityMd > standardCapacity)
            DrawWrappedFactoryStatus($"Migrated building allowance: {FormatUiNumber(factory.LegacyCapacityMd, "0")} MD / month preserves this factory's previous production setting. Standard {factory.BuildingSize} buildings provide {FormatUiNumber(standardCapacity, "0")} MD / month.");

        var maximumEmployees = (int)System.Math.Ceiling(factory.BuildingCapacityManDays / FactoryBalance.EmployeeManDaysPerMonth);
        var employeeLimit = System.Math.Min(factory.WorkforceEmployeeLimit, maximumEmployees);
        var automatic = factory.WorkforceEmployeeLimit == int.MaxValue;
        DrawFactoryCapacityInfo("Workforce Limit", automatic
            ? $"Automatic · up to {FormatUiInteger(maximumEmployees)} employees"
            : $"{FormatUiInteger(employeeLimit)} employees · {FormatUiNumber(employeeLimit * FactoryBalance.EmployeeManDaysPerMonth, "0")} MD / month");
        GUILayout.Label(new GUIContent("Available MD = the lower of building capacity and employees × 21 days.",
            "This is a staffing ceiling, not a permanently paid roster. Actual aircraft staffing follows allocated work; support pays for MD actually worked."),
            new GUIStyle(GUI.skin.label) { wordWrap = true }, GUILayout.ExpandWidth(true));
        var previousEnabled = GUI.enabled;
        GUI.enabled = previousEnabled && factory.IsOperational && factory.ExpansionMonthsRemaining == 0;
        var changedLimit = DrawIntSlider("Maximum Employees", employeeLimit, 0, maximumEmployees, useLinearScale: true);
        if (changedLimit != employeeLimit)
        {
            _productionFactory.SetWorkforceEmployeeLimit(factory.FactoryId, changedLimit);
            _factoryForecastCacheKey = string.Empty;
        }
        if (!automatic && PlannerButton("aircraft.workforce.full", "Use Full Building Workforce", GUILayout.Height(25f)))
        {
            _productionFactory.SetWorkforceEmployeeLimit(factory.FactoryId, int.MaxValue);
            _factoryForecastCacheKey = string.Empty;
        }
        GUI.enabled = previousEnabled;
        DrawFactoryCapacityInfo("Available Work Budget", $"{FormatUiNumber(factory.AvailableManDays, "0")} MD / month");

        var equipment = _productionFactory.GetEquipmentForFactory(factory.FactoryId);
        if (equipment is not null)
        {
            var multiplier = FactoryBalance.GetProductionManDayMultiplier(equipment.AutomationTier);
            var speedBonus = (1d / multiplier - 1d) * 100d;
            DrawFactoryCapacityInfo("Equipment Automation", $"{equipment.AutomationTier} · +{FormatUiNumber(speedBonus, "0.0")}% production speed");
            DrawWrappedFactoryStatus($"Work per product: {FormatUiNumber(multiplier * 100d, "0")}% of hand-built MD. This improves output within the same building; it does not enlarge its physical capacity.");
            if (!equipment.IsReady)
                DrawWrappedFactoryStatus($"Equipment installation: {FormatUiInteger(equipment.InstallationMonthsRemaining)} months remaining. This line cannot use its budget yet.");
            else if (factory.BaselineAircraftManDays > 0d)
            {
                var adjustedMd = factory.BaselineAircraftManDays * multiplier;
                DrawFactoryCapacityInfo("Aircraft + Engines Work", $"{FormatUiNumber(adjustedMd, "0.0")} MD / aircraft");
                DrawFactoryCapacityInfo("Before Support Allocation", $"~{FormatUiNumber(factory.AvailableManDays / adjustedMd, "0.0")} aircraft / month");
                DrawWrappedFactoryStatus("Output is a work-budget estimate, not a delivery guarantee. Service priorities, production targets and existing work in progress determine completed units.");
            }
        }

        var lastLine = _lastFactoryReport?.LineReports.FirstOrDefault(line => line.FactoryId == factory.FactoryId);
        if (lastLine is not null)
        {
            var allocated = _productionFactory.GetMonthlyAllocatedManDays(factory.FactoryId);
            var supportWork = System.Math.Max(0d, allocated - lastLine.UsedManDays);
            DrawFactoryCapacityInfo("Last Processed Month", $"{FormatUiNumber(lastLine.UsedManDays, "0.0")} aircraft/engine MD + {FormatUiNumber(supportWork, "0.0")} support MD");
            if (lastLine.CarryOverManDays > 0d)
                DrawFactoryCapacityInfo("Aircraft Work in Progress", $"{FormatUiNumber(lastLine.CarryOverManDays, "0.0")} MD already worked");
        }

        DrawWrappedFactoryStatus("Aircraft and parts use the slider shares while both need production. When either target is reached, all remaining capacity transfers to the other category. Production stops at warehouse targets.");
        DrawFactoryExpansionControls(factory);
    }

    private void DrawFactoryExpansionControls(FactorySiteState factory)
    {
        if (_productionFactory is null) return;
        DrawModernSectionHeader("Building Expansion");
        if (factory.ExpansionMonthsRemaining > 0)
        {
            var next = (FactoryBuildingSize)System.Math.Min((int)FactoryBuildingSize.Large, (int)factory.BuildingSize + 1);
            DrawWrappedFactoryStatus($"Expanding to {next} · {FormatUiInteger(factory.ExpansionMonthsRemaining)} months remaining. Production and support work are paused here until completion; property maintenance continues.");
            return;
        }
        if (factory.BuildingSize == FactoryBuildingSize.Large)
        {
            DrawWrappedFactoryStatus("Maximum building size reached. Build another factory for additional physical capacity.");
            return;
        }
        var nextSize = (FactoryBuildingSize)((int)factory.BuildingSize + 1);
        var nextCapacity = System.Math.Max(factory.LegacyCapacityMd, FactoryBalance.GetBuildingCapacityManDays(nextSize));
        var cost = FactoryBalance.GetExpansionCost(factory.BuildingSize);
        var months = FactoryBalance.GetExpansionMonths(factory.BuildingSize);
        DrawFactoryCapacityInfo($"Expand to {nextSize}", $"{FormatUiNumber(nextCapacity, "0")} MD / month · {FormatCurrency(cost)}");
        DrawFactoryCapacityInfo("After Expansion Upkeep", $"{FormatCurrency(FactoryBalance.GetBuildingMonthlyMaintenance(nextSize))} / month");
        DrawWrappedFactoryStatus($"Expansion takes {FormatUiInteger(months)} months and pauses all production and support work at this factory. Property maintenance continues during construction.");
        if (nextCapacity <= factory.BuildingCapacityManDays)
            DrawWrappedFactoryStatus("This migrated factory already has at least the next size's capacity allowance. Expansion changes its standard building size and upkeep without increasing its current effective MD limit.");
        var previousEnabled = GUI.enabled;
        var hasEquipment = _productionFactory.GetEquipmentForFactory(factory.FactoryId) is not null;
        if (hasEquipment) GUILayout.Label("Move equipment to the depot before expansion. Equipment only fits its original building size.");
        GUI.enabled = previousEnabled && factory.IsOperational && !hasEquipment && _accountingSystem is not null && _accountingSystem.Cash >= cost;
        if (PlannerPrimaryButton("aircraft.expand", $"Expand to {nextSize} — {FormatCurrency(cost)}", GUILayout.Height(30f)) && _accountingSystem is not null)
        {
            if (_productionFactory.TryStartFactoryExpansion(factory.FactoryId, _accountingSystem.Cash, out var paidCost, out var message))
            {
                _accountingSystem.RegisterCapitalExpense(_elapsedMonths, _currentDate, paidCost, $"Factory building expansion — {factory.DisplayName} to {nextSize}.");
                _factoryForecastCacheKey = string.Empty;
            }
            _factoryActionMessage = message;
        }
        GUI.enabled = previousEnabled;
        if (_accountingSystem is not null && _accountingSystem.Cash < cost)
            GUILayout.Label($"Additional cash needed: {FormatCurrency(cost - _accountingSystem.Cash)}");
    }

    // Factory controls also live in the narrow left pane. Avoid the general info row's fixed 210px label column.
    private static void DrawFactoryCapacityInfo(string label, string value) => DrawWrappedFactoryStatus($"{label}: {value}");
}
