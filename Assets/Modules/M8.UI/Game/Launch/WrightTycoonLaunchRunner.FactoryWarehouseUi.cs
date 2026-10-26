using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WrightTycoon.Game.UI;

namespace WrightTycoon.Game.Launch;

public sealed partial class WrightTycoonLaunchRunner
{
    private int _factoryWarehouseFilter;

    private void DrawFactoryWarehouseOverview(float width)
    {
        DrawModernSectionHeader("Warehouses", "Each type or engine shows its complete units and spare parts together. Versions with the same parts group share one inventory.");
        var rows = BuildFactoryWarehouseRows();
        GUILayout.BeginHorizontal();
        foreach (var category in new[] { "Aircraft", "Engines" })
        {
            BeginModernCard();
            GUILayout.Label(category, ModernStyle(WrightTycoonUiTheme.MutedLabelStyleName));
            GUILayout.Label(FormatUiInteger(rows.Where(r => r.Kind == category).Sum(r => r.Units)) + " complete units", ModernStyle(WrightTycoonUiTheme.CardHeaderStyleName));
            EndModernCard();
        }
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        _factoryWarehouseFilter = DrawPlannerTabs("warehouses", Mathf.Clamp(_factoryWarehouseFilter, 0, 2), new[] { "All", "Aircraft", "Engines" });
        GUILayout.EndHorizontal();
        var categoryFilter = new[] { "", "Aircraft", "Engines" }[_factoryWarehouseFilter];
        GUILayout.BeginHorizontal(ModernStyle(WrightTycoonUiTheme.TableHeaderStyleName));
        Cell("Type / engine", .24f); Cell("Kind", .08f); Cell("Complete units", .10f); Cell("Stock limit", .12f); Cell("Spare parts", .10f); Cell("Parts reserve", .12f); Cell("Shared parts group", .24f);
        GUILayout.EndHorizontal();
        var visible = rows.Where(r => categoryFilter.Length == 0 || r.Kind == categoryFilter).OrderBy(r => r.Kind).ThenBy(r => r.Name).ToArray();
        foreach (var row in visible)
        {
            GUILayout.BeginHorizontal(ModernStyle(WrightTycoonUiTheme.CardStyleName));
            Cell(row.Name, .24f);
            Cell(row.Kind, .08f); Cell(FormatUiInteger(row.Units), .10f);
            Cell(row.StockTarget.HasValue ? FormatUiInteger(row.StockTarget.Value) : "—", .12f); Cell(FormatUiInteger(row.Parts), .10f);
            Cell(FormatUiInteger(row.PartsReserve), .12f); Cell(row.PartsGroup, .24f);
            GUILayout.EndHorizontal();
        }
        if (visible.Length == 0) GUILayout.Label("No stock or approved products in this section yet.");
        GUILayout.Label("Production and reserve targets are configured at the factory. Compatible versions display the same shared parts quantity; do not add their rows together.", ModernStyle(WrightTycoonUiTheme.MutedLabelStyleName));
        void Cell(string text, float fraction) => GUILayout.Label(text, new GUIStyle(GUI.skin.label) { wordWrap = true }, GUILayout.Width((width - 100f) * fraction), GUILayout.MinHeight(35f));
    }

    private sealed record FactoryWarehouseRow(string Name, string Kind, int Units, int Parts, string PartsGroup, int PartsReserve, int? StockTarget = null);

    private List<FactoryWarehouseRow> BuildFactoryWarehouseRows()
    {
        var rows = new List<FactoryWarehouseRow>();
        if (_productionFactory is null) return rows;
        RefreshPartsGroups();
        var aircraftStock = _productionFactory.WarehouseUnitsByDesign;
        var support = _productionFactory.CreateSnapshot().SupportInventory;
        var representedAirframeGroups = new HashSet<string>();
        foreach (var id in aircraftStock.Keys.Union(_savedAircraftDesigns.Select(d => d.CatalogItem.DesignId)).Distinct())
        {
            var group = _airframePartsGroups.GetGroupId(id) ?? id;
            representedAirframeGroups.Add(group);
            rows.Add(new FactoryWarehouseRow(ResolveAircraftDesignName(id), "Aircraft", aircraftStock.TryGetValue(id, out var units) ? units : 0,
                _productionFactory.GetAirframeSparePartUnits(group), group, _airframeSparePartStockTargets.TryGetValue(group, out var reserve) ? reserve : 0,
                _productionFactory.GetAircraftStockTarget(id)));
        }
        foreach (var group in support.Where(s => s.AirframeSpareParts > 0).Select(s => s.DesignId).Union(_airframeSparePartStockTargets.Keys)
            .Select(g => _airframePartsGroups.GetGroupId(g) ?? g).Distinct().Where(g => !representedAirframeGroups.Contains(g)))
            rows.Add(new FactoryWarehouseRow("Unassigned aircraft group: " + group, "Aircraft", 0, _productionFactory.GetAirframeSparePartUnits(group), group,
                _airframeSparePartStockTargets.TryGetValue(group, out var reserve) ? reserve : 0));

        var engines = _engineManufacturing.CreateSnapshot();
        var catalog = BuildEngineManufacturingCatalog();
        var representedEngineGroups = new HashSet<string>();
        foreach (var id in engines.EngineStock.Keys.Union(catalog.Keys).Distinct())
        {
            var group = catalog.TryGetValue(id, out var item) ? item.PartsGroupId : _enginePartsGroups.GetGroupId(id) ?? id;
            representedEngineGroups.Add(group);
            var groupIds = catalog.Values.Where(c => c.PartsGroupId == group).Select(c => c.EngineDesignId).ToArray();
            rows.Add(new FactoryWarehouseRow(_savedEngines.FirstOrDefault(e => e.ResearchId == id)?.Name ?? id, "Engines", _engineManufacturing.GetEngineUnits(id),
                _engineManufacturing.GetPartUnits(group), group, (int)System.Math.Min(int.MaxValue, _engineManufacturing.Sites.Where(s => groupIds.Contains(s.EngineDesignId)).Sum(s => (long)s.PartReserve)),
                StockTarget: (int)System.Math.Min(int.MaxValue, _engineManufacturing.Sites.Where(s => s.EngineDesignId == id).Sum(s => (long)s.MonthlyEngineTarget))));
        }
        foreach (var group in engines.PartStock.Keys.Where(g => !representedEngineGroups.Contains(g)))
            rows.Add(new FactoryWarehouseRow("Unassigned engine group: " + group, "Engines", 0, _engineManufacturing.GetPartUnits(group), group, 0));
        return rows;
    }
}
