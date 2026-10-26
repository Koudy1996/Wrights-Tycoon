using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using WrightTycoon.Core.Aircraft;
using WrightTycoon.Core.Engines;
using WrightTycoon.Core.Fleet;
using WrightTycoon.Core.Simulation;
using WrightTycoon.Core.Contracts;

namespace WrightTycoon.Game.Launch;

public sealed partial class WrightTycoonLaunchRunner
{
    private string _pinnedMaintenanceSummary = string.Empty;
    private string _hoveredMaintenanceSummary = string.Empty;
    private bool _drawMaintenanceChart;
    private string _maintenanceChartCacheKey = string.Empty;
    private int[] _maintenanceGuaranteedUnits = Array.Empty<int>();
    private int[] _maintenanceActiveUnits = Array.Empty<int>();
    private readonly Dictionary<string, int> _airframeSparePartStockTargets = new(StringComparer.Ordinal);

    private IEnumerable<WrightTycoon.Core.Factories.FactorySupportRequest> BuildSparePartsProductionRequests()
    {
        RefreshPartsGroups();
        foreach (var demand in _fleet.GetServiceDemand(_currentDate))
        {
            if (demand.AirframeSpareParts > 0)
                yield return new WrightTycoon.Core.Factories.FactorySupportRequest(demand.AirframePartsGroupId, demand.AirframeSpareParts, demand.Guaranteed);
        }
        foreach (var target in _airframeSparePartStockTargets.Where(t => t.Value > 0))
            yield return new WrightTycoon.Core.Factories.FactorySupportRequest(target.Key, target.Value, false);
    }


    private void DrawEngineMaintenanceDesignControls()
    {
        DrawModernSectionHeader("6 — Maintenance & Durability", "Shorter service intervals protect engine life. Durability adds weight, material cost and production work.");
        var intervals = EngineMaintenancePolicy.ServiceIntervalsHours.ToArray();
        var selected = Array.IndexOf(intervals, _engineParameters.ServiceIntervalHours);
        GUILayout.BeginHorizontal();
        _engineParameters.ServiceIntervalHours = intervals[DrawPlannerTabs("engine.service", Mathf.Max(0, selected),
            intervals.Select(hours => FormatUiInteger(hours) + " h").ToArray())];
        GUILayout.EndHorizontal();
        _engineParameters.DurabilityLevel = DrawIntSlider("Durability", _engineParameters.DurabilityLevel, 0, 10, useLinearScale: true);
    }

    private void DrawEngineMaintenanceParameters(Engine engine, string? referenceId = null)
    {
        RefreshPartsGroups();
        var designId = string.IsNullOrWhiteSpace(referenceId) ? engine.Definition.Id : referenceId;
        var groupId = _enginePartsGroups.GetGroupId(designId) ?? designId;
        var exact = _savedEngines.FirstOrDefault(e => e.ResearchId == designId);
        var terms = EngineMaintenancePolicy.Create(engine, designId, groupId, exact is null ? null : BuildReferenceEngine(exact));
        var anchor = _savedEngines.FirstOrDefault(e => e.ResearchId == groupId);
        if (anchor is not null) terms = terms with { PartUnitPrice = FleetServiceBalance.EnginePartUnitPrice(BuildReferenceEngine(anchor)) };
        DrawParameterValueRow("Service Interval", FormatUiInteger(terms.ServiceIntervalHours) + " h");
        DrawParameterValueRow("Expected Engine Life", FormatUiInteger(terms.ReplacementLifeHours) + " h");
        DrawParameterValueRow("Durability", FormatUiInteger(engine.Parameters.DurabilityLevel) + "/10");
        DrawParameterValueRow("Parts / Service", FormatUiInteger(terms.PartsPerService));
        var cycles = System.Math.Max(0, System.Math.Ceiling((double)terms.ReplacementLifeHours / terms.ServiceIntervalHours) - 1);
        var hourly = (cycles * terms.PartsPerService * terms.PartUnitPrice + terms.ReplacementUnitPrice) / terms.ReplacementLifeHours;
        DrawParameterValueRow("Maintenance + Replacement", FormatOperationCurrency(hourly) + "/h",
            new ModifierTooltipDefinition("engine.maintenance-cost", "Maintenance + Replacement", neutralLines: new[]
            {
                "Per engine hour: routine parts and recurring whole-engine replacement; excludes fuel and the original engine purchase.",
                "Part price: " + FormatCurrency(terms.PartUnitPrice) + "; replacement: " + FormatCurrency(terms.ReplacementUnitPrice) + ".",
                "Life depends on reliability, service interval, durability and power/RPM stress. A whole-engine replacement resets its hours, not the airframe's.",
            }));
    }


    private void DrawEngineFleetMaintenanceSummary(SavedEngineDesign engine)
    {
        var designIds = _savedAircraftDesigns.Where(d => string.Equals(d.EngineResearchId, engine.ResearchId, StringComparison.Ordinal))
            .Select(d => d.CatalogItem.DesignId).ToArray();
        DrawMachineFleetMaintenanceSummary("engine:" + engine.ResearchId, designIds, true, engine.ResearchId);
    }

    private void DrawMachineFleetMaintenanceSummary(string key, string[] designIds, bool engineView, string? engineResearchId = null)
    {
        var cohorts = _fleet.Cohorts.Where(c => c.ActiveCount > 0 && (engineView
            ? c.Offer.EngineMaintenance?.EngineDesignId == engineResearchId : designIds.Contains(c.DesignId))).ToArray();
        designIds = cohorts.Select(c => c.DesignId).Distinct(StringComparer.Ordinal).ToArray();
        var month = SimulationMonthId.FromDate(_currentDate).Value;
        var summary = MaintenancePresentation.Summarize(cohorts, designIds, month);
        var active = summary.ActiveAircraft;
        var grounded = summary.GroundedAircraft;
        DrawModernSectionHeader("In-service Maintenance", $"Fleet registered since {FormatSimulationDate(_fleetRecordStartDate)}; earlier sales are not reconstructed.");
        DrawModernInfoRow("Aircraft in Operation / Grounded", $"{FormatUiInteger(active - grounded)} / {FormatUiInteger(grounded)}");
        if (engineView)
        {
            DrawModernInfoRow("Installed Engines in Operation", FormatUiInteger(summary.FlyingEngines));
            DrawModernInfoRow("Engines on Grounded Aircraft", FormatUiInteger(summary.GroundedEngines));
        }
        if (active == 0)
        {
            GUILayout.Label("No surviving delivered aircraft recorded for this design. Service terms will appear after delivery.");
            return;
        }

        string Range(double[] values, string format) => values.Min() == values.Max()
            ? FormatUiNumber(values.Min(), format)
            : $"{FormatUiNumber(values.Min(), format)}–{FormatUiNumber(values.Max(), format)}";
        var intervals = summary.ServiceIntervalsHours.Select(v => (double)v).ToArray();
        DrawModernInfoRow("Agreed Main Service Interval", $"{Range(intervals, "0")} h");
        var combined = cohorts;
        if (combined.Length > 0)
        {
            if (!engineView)
            {
                DrawModernInfoRow("Airframe Parts / Cycle", Range(combined.Select(c => (double)c.Offer.SparePartsPerService).ToArray(), "0"));
                DrawModernInfoRow("Agreed Airframe Part Price", $"{Range(combined.Select(c => c.Offer.SparePartUnitPrice).ToArray(), "0")} $ / part");
                DrawModernInfoRow("Airframe Parts Cost / Cycle", $"{Range(combined.Select(c => c.Offer.ServicePricePerEvent).ToArray(), "0")} $ / aircraft");
                if (_productionFactory is not null)
                    DrawModernInfoRow("Shared Airframe Parts in Stock", FormatUiInteger(combined.Select(c => _productionFactory.ResolveAirframePartsGroup(c.Offer.AirframePartsGroupId))
                        .Distinct(StringComparer.Ordinal).Sum(_productionFactory.GetAirframeSparePartUnits)));
            }
            var engines = combined.Select(c => c.Offer.EngineMaintenance!).ToArray();
            DrawModernInfoRow("Engine Service Interval", $"{Range(engines.Select(e => (double)e.ServiceIntervalHours).ToArray(), "0")} h");
            DrawModernInfoRow("Engine Replacement Life", $"{Range(engines.Select(e => (double)e.ReplacementLifeHours).ToArray(), "0")} h");
            DrawModernInfoRow("Engine Parts / Routine Cycle", Range(engines.Select(e => (double)e.PartsPerService).ToArray(), "0"));
            DrawModernInfoRow("Agreed Engine Part Price", $"{Range(engines.Select(e => e.PartUnitPrice).ToArray(), "0")} $ / part");
            DrawModernInfoRow("Agreed Replacement Engine Price", $"{Range(engines.Select(e => e.ReplacementUnitPrice).ToArray(), "0")} $ / engine");
            DrawModernInfoRow("Shared Engine Parts in Stock", FormatUiInteger(engines.Select(e => e.PartsGroupId)
                .Distinct(StringComparer.Ordinal).Sum(_engineManufacturing.GetPartUnits)));
            DrawModernInfoRow("Exact-version Replacement Engines", FormatUiInteger(engines.Select(e => e.EngineDesignId)
                .Distinct(StringComparer.Ordinal).Sum(_engineManufacturing.GetEngineUnits)));
        }
        GUILayout.Label("Ranges cover surviving delivery batches, including grounded aircraft. Frozen engine and airframe terms remain attached to each delivery.");
        var latest = summary.LatestSupportEndMonth;
        var label = latest < 0 ? "No current warranty reputation period" : $"Warranty Reputation Period Through {FormatFleetMonth(latest)}";
        if (GUILayout.Button(new GUIContent(label, "Hover to show the combined support timeline; click to pin or close it.")))
            _pinnedMaintenanceSummary = _pinnedMaintenanceSummary == key ? string.Empty : key;
        if (Event.current.type == EventType.Layout)
            _drawMaintenanceChart = _pinnedMaintenanceSummary == key || _hoveredMaintenanceSummary == key;
        if (Event.current.type == EventType.Repaint)
            _hoveredMaintenanceSummary = GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition) ? key : string.Empty;
        if (latest >= 0)
            GUILayout.Label($"Next expiry {FormatFleetMonth(summary.NextSupportEndMonth)} • {summary.GuaranteedAircraft} aircraft • {summary.GuaranteedContracts} contracts");
        if (!_drawMaintenanceChart) return;
        var months = Math.Min(2400, Math.Max(12, latest - month + 1));
        var cacheKey = $"{key}:{month}:{months}:" + string.Join(";", cohorts.Select(c => $"{c.CohortId}:{c.ActiveCount}:{c.FlightHours}:{c.SupportEnabled}"));
        if (_maintenanceChartCacheKey != cacheKey)
        {
            _maintenanceGuaranteedUnits = new int[months];
            _maintenanceActiveUnits = new int[months];
            foreach (var id in designIds.Distinct())
            {
                var forecast = _fleet.GetSupportForecast(id, _currentDate, months);
                for (var i = 0; i < Math.Min(months, forecast.Count); i++)
                { _maintenanceGuaranteedUnits[i] += forecast[i].GuaranteedUnits; _maintenanceActiveUnits[i] += forecast[i].ActiveUnits; }
            }
            _maintenanceChartCacheKey = cacheKey;
        }
        var guaranteedUnits = _maintenanceGuaranteedUnits;
        var activeUnits = _maintenanceActiveUnits;
        var chart = GUILayoutUtility.GetRect(100f, 100f, GUILayout.ExpandWidth(true));
        DrawRect(chart, new Color(.035f, .07f, .105f));
        var maximum = Math.Max(1, Math.Max(guaranteedUnits.Max(), activeUnits.Max()));
        Vector2 Point(int i, int count) => new(chart.x + 6f + (chart.width - 12f) * i / (months - 1), chart.yMax - 8f - (chart.height - 16f) * count / maximum);
        for (var i = 1; i < months; i++)
        {
            DrawLine(Point(i - 1, guaranteedUnits[i - 1]), Point(i, guaranteedUnits[i]), new Color(.4f, .7f, 1f), 2f);
            DrawLine(Point(i - 1, activeUnits[i - 1]), Point(i, activeUnits[i]), new Color(.5f, .85f, .6f), 2f);
        }
        GUILayout.Label($"{FormatFleetMonth(month)} → {FormatFleetMonth(month + months - 1)} • Blue: guaranteed aircraft; green: forecast before accidents. Scale 0–{maximum} aircraft.");
    }
}
