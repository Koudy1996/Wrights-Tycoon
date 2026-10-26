using System;
using System.Linq;
using UnityEngine;
using WrightTycoon.Core.Fleet;
using WrightTycoon.Core.Simulation;
using WrightTycoon.Game.UI;

namespace WrightTycoon.Game.Launch;

public sealed partial class WrightTycoonLaunchRunner
{
    private string _selectedFleetDesignId = string.Empty;
    private string _fleetActionMessage = string.Empty;
    private bool _pinSupportChart;
    private int _forecastHorizonIndex = 1;
    private SupportForecastPoint[] _supportForecast = Array.Empty<SupportForecastPoint>();
    private string _forecastCacheKey = string.Empty;

    private void DrawOperatingFleet()
    {
        DrawModernSectionHeader("Operating Fleet", $"Customer fleet recorded since {FormatSimulationDate(_fleetRecordStartDate)}. Earlier sales are not reconstructed.");
        GUILayout.Label($"Active: {FormatUiInteger(_fleet.ActiveUnits)} | Reputation: {FormatUiNumber(_reputation.Score, "0.0")}/100");
        if (_lastFleetReport is not null)
        {
            GUILayout.Label($"Last month: {_lastFleetReport.CompletedServices} completed maintenance cycles; {_lastFleetReport.GroundedUnits} grounded; {_lastFleetReport.Retirements} retired.");
            GUILayout.Label($"Accident losses: {_lastFleetReport.AccidentLosses} actual / {FormatUiNumber(_lastFleetReport.ExpectedAccidentLosses, "0.00")} period baseline expected.");
            GUILayout.Label($"Warranty maintenance: {_lastFleetReport.WarrantyServicedAircraft} serviced / {_lastFleetReport.WarrantyMissedAircraft} missed / {_lastFleetReport.WarrantyAircraft} aircraft in reputation window.");

        }
        DrawFleetInfoRow("Tržby z údržby (celkem)", FormatCurrency(_maintenanceFinance.Revenue));
        DrawFleetInfoRow("Zisk z údržby (celkem)", FormatCurrency(_maintenanceFinance.Profit));
        if (_lastFleetReport is not null)
        {
            DrawFleetInfoRow("Tržby z údržby (poslední měsíc)", FormatCurrency(_lastFleetReport.Revenue));
            DrawFleetInfoRow("Zisk z údržby (poslední měsíc)", FormatCurrency(_lastFleetReport.Revenue - _lastFleetReport.ProductionCost));
        }
        GUILayout.Label("Profit deducts the manufacturing cost of consumed parts and motors, including allocated factory overhead. Unsold reserves and idle-capacity overhead remain in factory accounting.");
        if (_maintenanceFinance.FirstTrackedMonth >= 0)
            GUILayout.Label($"Maintenance financial history recorded since {FormatSimulationDate(_currentDate.AddMonths(_maintenanceFinance.FirstTrackedMonth - _elapsedMonths))}.");
        var groups = _fleet.Cohorts.GroupBy(c => c.DesignId).OrderBy(g => g.First().DesignName).ToArray();
        if (groups.Length == 0) { GUILayout.Label("Delivered aircraft will appear here after your first delivery."); return; }
        var month = SimulationMonthId.FromDate(_currentDate).Value;
        foreach (var group in groups)
        {
            var active = group.Sum(c => c.ActiveCount);
            var live = group.Where(c => c.ActiveCount > 0).ToArray();
            var guarantees = live.Where(c => c.SupportEndMonth >= month).ToArray();
            var lastEnd = guarantees.Length == 0 ? -1 : guarantees.Max(c => c.SupportEndMonth);
            BeginModernCard(_selectedFleetDesignId == group.Key);
            if (GUILayout.Button($"{group.First().DesignName}  •  {active} active / {group.Sum(c => c.OriginalCount)} delivered"))
                _selectedFleetDesignId = group.Key;
            var hours = active == 0 ? 0d : live.Sum(c => c.FlightHours * c.ActiveCount) / active;
            GUILayout.Label($"Average {FormatUiNumber(hours, "0")} h | {live.Where(c => c.Grounded).Sum(c => c.ActiveCount)} grounded | {group.Sum(c => c.AccidentLosses)} losses | {group.Sum(c => c.RetiredCount)} retired");
            if (active > 0)
            {
                var annualRisk = live.Sum(c => c.ActiveCount * FleetAccidentCatalog.ReferenceLossProbability(
                    _currentDate, c.Specification.Category, c.Profile.Id, c.Profile.UtilizationHoursPerMonth * 12d,
                    c.Profile.AverageFlightHours, c.Specification.Reliability, c.Wear, c.FlightHours / ((c.ContractLifeHours + c.Specification.ExpectedLifeHours) * .5d))) / active;
                GUILayout.Label(new GUIContent($"Reference accident loss risk: {FormatUiNumber(annualRisk * 100d, "0.00")}% / year at planned hours",
                    "Game estimate at today's rates and wear. Excludes grounding, retirement and future changes in rates/wear; not a survival forecast or historical fact."));
            }
            var endLabel = lastEnd < 0 ? "No current warranty reputation period" : $"Warranty reputation period through {FormatFleetMonth(lastEnd)}";
            if (GUILayout.Button(new GUIContent(endLabel, "Show guaranteed fleet over time; click to pin the support chart.")))
            { _selectedFleetDesignId = group.Key; _pinSupportChart = !_pinSupportChart; }
            if (!_pinSupportChart && Event.current.type == EventType.Repaint && GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
                _selectedFleetDesignId = group.Key;
            if (guarantees.Length > 0)
                GUILayout.Label($"Next expiry {FormatFleetMonth(guarantees.Min(c => c.SupportEndMonth))} | {guarantees.Sum(c => c.ActiveCount)} aircraft in warranty");
            EndModernCard();
        }
        if (string.IsNullOrEmpty(_selectedFleetDesignId)) _selectedFleetDesignId = groups[0].Key;
        if (!string.IsNullOrWhiteSpace(_fleetActionMessage)) GUILayout.Label(_fleetActionMessage);
    }

    private static string FormatFleetMonth(int month) => FormatSimulationDate(new SimulationDate((month / 12) + 1, (month % 12) + 1));

    private void DrawFleetSupportReport()
    {
        var selected = _fleet.Cohorts.Where(c => c.DesignId == _selectedFleetDesignId).ToArray();
        if (selected.Length == 0) { GUILayout.Label("Select an operating type to review support obligations."); return; }
        DrawModernSectionHeader(selected[0].DesignName, "Guaranteed support and forecast operating fleet");
        _forecastHorizonIndex = GUILayout.Toolbar(_forecastHorizonIndex, new[] { "12 months", "60 months", "Full guarantee" });
        var month = SimulationMonthId.FromDate(_currentDate).Value;
        var months = _forecastHorizonIndex == 0 ? 12 : _forecastHorizonIndex == 1 ? 60
            : System.Math.Min(2400, System.Math.Max(2, selected.Max(c => c.SupportEndMonth) - month + 1));
        var key = $"{_selectedFleetDesignId}:{month}:{months}";
        if (_forecastCacheKey != key)
        {
            _supportForecast = _fleet.GetSupportForecast(_selectedFleetDesignId, _currentDate, months).ToArray();
            _forecastCacheKey = key;
        }
        var rect = GUILayoutUtility.GetRect(100f, 180f, GUILayout.ExpandWidth(true));
        DrawRect(rect, new Color(0.035f, 0.07f, 0.105f, 1f));
        var maximum = System.Math.Max(1, _supportForecast.Select(p => System.Math.Max(p.GuaranteedUnits, p.ActiveUnits)).DefaultIfEmpty(1).Max());
        for (var i = 1; i < _supportForecast.Length; i++)
        {
            Vector2 Point(int index, int units) => new(rect.x + 10f + (rect.width - 20f) * index / System.Math.Max(1, _supportForecast.Length - 1),
                rect.yMax - 15f - (rect.height - 30f) * units / maximum);
            DrawLine(Point(i - 1, _supportForecast[i - 1].GuaranteedUnits), Point(i, _supportForecast[i].GuaranteedUnits), new Color(.4f, .7f, 1f), 2f);
            DrawLine(Point(i - 1, _supportForecast[i - 1].ActiveUnits), Point(i, _supportForecast[i].ActiveUnits), new Color(.5f, .85f, .6f), 2f);
        }
        GUILayout.Label($"Blue: guaranteed current aircraft. Green: forecast active before accidents. Scale 0–{maximum} aircraft.");
        GUILayout.Label($"{FormatFleetMonth(month)} → {FormatFleetMonth(month + months - 1)} | chart {(_pinSupportChart ? "pinned" : "follows selection / date hover")}");
        var live = selected.Where(c => c.ActiveCount > 0).ToArray();
        if (live.Length > 0)
            DrawFleetInfoRow("Operating Life (contract/design mean)", $"{FormatUiNumber(live.Sum(c => (c.ContractLifeHours + c.Specification.ExpectedLifeHours) * .5d * c.ActiveCount) / live.Sum(c => c.ActiveCount), "0")} h average");
        var obligations = live.Where(c => c.SupportEndMonth >= month).Sum(c => c.ActiveCount);
        GUILayout.Label($"Warranty reputation windows: {live.Where(c => c.SupportEndMonth >= month).Select(c => c.ContractId).Distinct().Count()} contracts / {obligations} aircraft");
        var upcomingAirframe = _fleet.GetServiceDemand(_currentDate).Where(d => d.DesignId == _selectedFleetDesignId).Sum(d => d.AirframeSpareParts);
        var upcomingEngines = _fleet.GetEngineServiceDemand(_currentDate, _selectedFleetDesignId);
        GUILayout.Label($"Upcoming month: {upcomingAirframe} airframe parts; {upcomingEngines.Sum(d => d.EngineParts)} engine parts; {upcomingEngines.Sum(d => d.ReplacementEngines)} replacement engines");
        foreach (var terms in live
            .GroupBy(c => c.Offer.EngineMaintenance!.EngineDesignId))
        {
            var engine = terms.First().Offer.EngineMaintenance!;
            var engines = terms.Sum(c => c.ActiveCount * c.EngineCount);
            var averageAge = engines == 0 ? 0d : terms.Sum(c => c.EngineHoursSinceReplacement * c.ActiveCount * c.EngineCount) / engines;
            var name = _savedEngines.FirstOrDefault(e => e.ResearchId == engine.EngineDesignId)?.Name ?? engine.EngineDesignId;
            GUILayout.Label($"{name}: {engines} installed · average {FormatUiNumber(averageAge, "0")} h since replacement · engine life {engine.ReplacementLifeHours} h");
            GUILayout.Label($"Stock: {_engineManufacturing.GetEngineUnits(engine.EngineDesignId)} exact-version engines; {_engineManufacturing.GetPartUnits(engine.PartsGroupId)} shared engine parts. Routine service every {engine.ServiceIntervalHours} h.");
        }
        foreach (var group in live
            .Select(c => _airframePartsGroups.GetGroupId(c.Offer.AirframePartsGroupId) ?? c.Offer.AirframePartsGroupId).Distinct())
            GUILayout.Label($"Shared airframe parts ({ResolveAircraftDesignName(group)}): {_productionFactory?.GetAirframeSparePartUnits(group) ?? 0}");
        var remainingWarranty = _fleet.GetGuaranteeMaintenanceDemand(_selectedFleetDesignId, _currentDate);
        DrawFleetInfoRow("Předpokládaná potřeba dílů během zbývající garance", $"{FormatUiNumber(remainingWarranty.AirframeParts + remainingWarranty.EngineParts, "0")} dílů");
        GUILayout.Label($"Drak: {remainingWarranty.AirframeParts}; motory: {remainingWarranty.EngineParts}; celé náhradní motory: {remainingWarranty.ReplacementEngines}.");
        GUILayout.Label("Estimate covers surviving aircraft and their remaining warranty, assumes full planned flight hours and available maintenance, and excludes future accidents and deliveries.");
        var financial = _maintenanceFinance.GetTotals(_selectedFleetDesignId);
        var financialMonth = _maintenanceFinance.GetLastMonth(_selectedFleetDesignId);
        DrawFleetInfoRow("Tržby z údržby (typ, celkem / měsíc)", $"{FormatCurrency(financial.Revenue)} / {FormatCurrency(financialMonth.Revenue)}");
        DrawFleetInfoRow("Zisk z údržby (typ, celkem / měsíc)", $"{FormatCurrency(financial.Profit)} / {FormatCurrency(financialMonth.Profit)}");
        GUILayout.Label("Warranty dates affect reputation only. They do not change production priority, prices or support availability.");
        if (_fleet.IsSupported(_selectedFleetDesignId))
        {
            if (GUILayout.Button("Stop Maintenance Supply")) EndFleetSupport();
        }
        else if (live.Length > 0 && GUILayout.Button("Resume Maintenance Supply"))
        { _fleet.ResumeSupport(_selectedFleetDesignId); _forecastCacheKey = string.Empty; }

    }

    private static void DrawFleetInfoRow(string label, string value)
    {
        GUILayout.BeginVertical(ModernStyle(WrightTycoonUiTheme.CardStyleName));
        GUILayout.Label(label, new GUIStyle(ModernStyle(WrightTycoonUiTheme.MutedLabelStyleName)) { wordWrap = true });
        GUILayout.Label(value, new GUIStyle(GUI.skin.label) { wordWrap = true });
        GUILayout.EndVertical();
    }

    private void EndFleetSupport()
    {
        if (_fleet.TryEndSupport(_selectedFleetDesignId, _currentDate, true, out var affected))
        {
            _fleetActionMessage = $"Maintenance supply stopped. {affected} aircraft remain in their warranty reputation period; missed maintenance will affect reputation.";
            _forecastCacheKey = string.Empty;
        }
    }
}
