using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Factories;
using WrightTycoon.Game.UI;

namespace WrightTycoon.Game.Launch;

public sealed partial class WrightTycoonLaunchRunner
{
    private int _factoryPlannerTab;
    private int _factoryPlannerDetailTab;
    private int _factoryPlannerType;
    private int _factoryPlannerStatus;
    private FactoryPlannerSort _factoryPlannerSort;
    private bool _factoryPlannerDescending;
    private string _factoryPlannerSelection = string.Empty;
    private bool _showFactoryPlannerConstruction;
    private bool _showFactoryCapacityInspector;
    private int _factoryConstructionKind;
    private Vector2 _factoryPlannerTableScroll;
    private Vector2 _factoryPlannerDetailScroll;
    private FactoryPlannerRow[] _factoryPlannerRows = Array.Empty<FactoryPlannerRow>();
    private float _factoryPlannerRefreshAt;
    private readonly Dictionary<FactoryBuildingSize, Texture2D> _factoryBuildingTextures = new();
    private readonly Dictionary<string, AirframePartsTargetDraft> _airframePartsTargetDrafts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AircraftStockLimitDraft> _aircraftStockLimitDrafts = new(StringComparer.Ordinal);
    private readonly HashSet<string> _expandedFactoryProductionDetails = new(StringComparer.Ordinal);
    private sealed class AircraftStockLimitDraft
    {
        public int? SourceTarget;
        public string Text = "0";
    }
    private sealed class AirframePartsTargetDraft
    {
        public int SourceTarget;
        public string Text = "0";
    }

    private void ResetFactoryPlannerView()
    {
        _factoryPlannerRows = Array.Empty<FactoryPlannerRow>();
        _factoryPlannerSelection = string.Empty;
        _factoryPlannerRefreshAt = 0;
        _factoryPlannerTab = _factoryPlannerDetailTab = _factoryPlannerType = _factoryPlannerStatus = 0;
        _factoryPlannerTableScroll = _factoryPlannerDetailScroll = Vector2.zero;
        _showFactoryPlannerConstruction = false;
        _factoryWarehouseFilter = 0;
        _factoryChoiceOpen = string.Empty;
        _enginePlantDrafts.Clear();
        _airframePartsTargetDrafts.Clear();
        _aircraftStockLimitDrafts.Clear();
        _expandedFactoryProductionDetails.Clear();
    }

    private void DrawFactoryPlanner(float width, float height)
    {
        if (_productionFactory is null) { DrawError("Production system is not initialized."); return; }
        if (Event.current.type == EventType.Repaint)
        {
            _factoryPlannerControlRects.Clear();
            _factoryPlannerControlEnabled.Clear();
        }
        GUILayout.BeginVertical(GUI.skin.box, GUILayout.ExpandHeight(true));
        GUILayout.BeginHorizontal();
        var view = DrawPlannerTabs("nav", _factoryPlannerTab,
            new[] { "Aircraft Factories", "Engine Factories", "Warehouses", "Equipment Depot", "Reports" });
        if (view != _factoryPlannerTab)
        {
            _factoryPlannerTab = view;
            _showFactoryPlannerConstruction = false;
            _factoryPlannerStatus = 0;
            _factoryPlannerSelection = string.Empty;
            _factoryPlannerDetailScroll = _factoryPlannerTableScroll = Vector2.zero;
            _factoryViewTab = 0;
            _factoryChoiceOpen = string.Empty;
        }
        GUILayout.Space(12f);
        if (PlannerPrimaryButton("build.toggle", _showFactoryPlannerConstruction ? "Close Construction" : "+ Build Factory", GUILayout.Width(190f), GUILayout.Height(32f)))
        {
            if (!_showFactoryPlannerConstruction) _factoryConstructionKind = _factoryPlannerTab == 1 ? 1 : 0;
            _showFactoryPlannerConstruction = !_showFactoryPlannerConstruction;
        }
        GUILayout.EndHorizontal();

        if (_showFactoryPlannerConstruction)
        {
            _factoryPlannerDetailScroll = GUILayout.BeginScrollView(_factoryPlannerDetailScroll);
            DrawModernSectionHeader("Build a New Factory", "Choose production and building size. Each option has one construction button; production equipment is purchased separately.");
            if (PlannerButton("build.back", "← Back to Factories", GUILayout.Width(220f), GUILayout.Height(30f)))
                _showFactoryPlannerConstruction = false;
            DrawPlannerConstruction(width);
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            return;
        }

        if (_factoryPlannerTab >= 2)
        {
            _factoryPlannerDetailScroll = GUILayout.BeginScrollView(_factoryPlannerDetailScroll);
            if (_factoryPlannerTab == 2) DrawFactoryWarehouseOverview(width);
            else if (_factoryPlannerTab == 3) DrawEquipmentDepotView();
            else { DrawFactoryReport(); DrawPlannerMotorReport(); }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            return;
        }

        _factoryPlannerType = _factoryPlannerTab == 0 ? 1 : 2;
        DrawModernSectionHeader(_factoryPlannerTab == 0 ? "Aircraft Factories" : "Engine Factories",
            "1 Select a factory below   ·   2 Assign its product and targets   ·   3 Review stock in Warehouses");
        GUILayout.BeginHorizontal();
        _factoryPlannerStatus = DrawPlannerTabs("filter", _factoryPlannerStatus, new[] { "All", "Needs attention", "Building" }, 145f);
        GUILayout.FlexibleSpace();
        GUILayout.Label("Current targets / last completed work", ModernStyle(WrightTycoonUiTheme.MutedLabelStyleName), GUILayout.ExpandWidth(false));
        GUILayout.EndHorizontal();

        RefreshFactoryPlannerRows();
        var status = _factoryPlannerStatus == 1 ? FactoryPlannerStatusFilter.Attention
            : _factoryPlannerStatus == 2 ? FactoryPlannerStatusFilter.Building : FactoryPlannerStatusFilter.All;
        var rows = FactoryPlannerProjection.FilterAndSort(_factoryPlannerRows, (FactoryPlannerTypeFilter)_factoryPlannerType,
            status, _factoryPlannerSort, _factoryPlannerDescending);
        if (!rows.Any(r => PlannerKey(r) == _factoryPlannerSelection))
            _factoryPlannerSelection = rows.Count > 0 ? PlannerKey(rows[0]) : string.Empty;

        if (width >= 1150f)
        {
            GUILayout.BeginHorizontal(GUILayout.ExpandHeight(true));
            GUILayout.BeginVertical(GUILayout.Width(260f), GUILayout.ExpandHeight(true));
            var sorts = new[] { FactoryPlannerSort.Name, FactoryPlannerSort.Type, FactoryPlannerSort.Capacity, FactoryPlannerSort.Utilization };
            for (var index = 0; index < sorts.Length; index++)
            {
                if (index % 2 == 0) GUILayout.BeginHorizontal();
                var sort = sorts[index];
                if (PlannerButton("sort:" + sort, sort.ToString() + (_factoryPlannerSort == sort ? _factoryPlannerDescending ? " ▼" : " ▲" : ""), GUILayout.Height(26f)))
                {
                    _factoryPlannerDescending = _factoryPlannerSort == sort && !_factoryPlannerDescending;
                    _factoryPlannerSort = sort;
                }
                if (index % 2 == 1) GUILayout.EndHorizontal();
            }
            _factoryPlannerTableScroll = GUILayout.BeginScrollView(_factoryPlannerTableScroll, GUILayout.ExpandHeight(true));
            foreach (var row in rows)
            {
                GUILayout.BeginHorizontal(ModernStyle(PlannerKey(row) == _factoryPlannerSelection ? WrightTycoonUiTheme.SelectedCardStyleName : WrightTycoonUiTheme.CardStyleName));
                DrawFactoryBuildingIcon(row.Size, 34f);
                if (PlannerButton("plant:" + PlannerKey(row), row.Name + "\n" + row.Product + " · " + row.Status, GUILayout.Width(184f), GUILayout.Height(60f)))
                {
                    _factoryPlannerSelection = PlannerKey(row);
                    _factoryPlannerDetailTab = 0;
                    _factoryPlannerDetailScroll = Vector2.zero;
                }
                GUILayout.EndHorizontal();
            }
            if (rows.Count == 0) GUILayout.Label("No factories match this section. Use + Build Factory or change the status filter.");
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUILayout.Space(10f);
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            _factoryPlannerDetailScroll = GUILayout.BeginScrollView(_factoryPlannerDetailScroll, GUILayout.ExpandHeight(true));
            var selectedPlant = rows.FirstOrDefault(row => PlannerKey(row) == _factoryPlannerSelection);
            if (selectedPlant is not null) DrawPlannerSelectedFactory(selectedPlant, width - 306f);
            else GUILayout.Label("Select a factory to configure production and stock targets.");
            GUILayout.EndScrollView();
            RecordFactoryControl("viewport.detail");
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            return;
        }

        var tableWidth = Mathf.Max(1120f, width - 44f);
        var tableHeight = Mathf.Clamp(height * .17f, 110f, 170f);
        _factoryPlannerTableScroll = GUILayout.BeginScrollView(_factoryPlannerTableScroll,
            GUILayout.Height(tableHeight), GUILayout.ExpandWidth(true));
        GUILayout.BeginVertical(GUILayout.Width(tableWidth));
        DrawPlannerTableHeader(tableWidth);
        foreach (var row in rows) DrawPlannerTableRow(row, tableWidth);
        if (rows.Count == 0)
        {
            GUILayout.Label(_factoryPlannerStatus == 0
                ? "No factories in this section yet. Click + Build Factory above to start one."
                : "No factories match this status. Choose All to see your plants.");
        }
        GUILayout.EndVertical();
        GUILayout.EndScrollView();
        GUILayout.Label("Blue: production MD · Amber: parts/support MD · Gray: unused physical capacity. Actual work from the last processed month; not a forecast.",
            ModernStyle(WrightTycoonUiTheme.MutedLabelStyleName));

        var selected = rows.FirstOrDefault(r => PlannerKey(r) == _factoryPlannerSelection);
        _factoryPlannerDetailScroll = GUILayout.BeginScrollView(_factoryPlannerDetailScroll, GUILayout.ExpandHeight(true));
        if (selected is not null) DrawPlannerSelectedFactory(selected, width);
        GUILayout.EndScrollView();
        GUILayout.EndVertical();
    }

    private void RefreshFactoryPlannerRows()
    {
        if (_factoryPlannerRows.Length > 0 && Time.realtimeSinceStartup < _factoryPlannerRefreshAt) return;
        _factoryPlannerRows = FactoryPlannerProjection.Build(_productionFactory, _engineManufacturing,
            _lastFactoryReport, _lastEngineManufacturingReport, ResolveAircraftDesignName,
            id => _savedEngines.FirstOrDefault(e => e.ResearchId == id)?.Name ?? id, _airframeSparePartStockTargets,
            id => _enginePartsGroups.GetGroupId(id) ?? id).ToArray();
        if (_separateEngineProduction)
        {
            var needs = BuildEngineManufacturingDemand().Where(d => d.ForAircraftProduction)
                .GroupBy(d => d.EngineDesignId).ToDictionary(g => g.Key, g => g.Sum(d => d.Engines));
            _factoryPlannerRows = _factoryPlannerRows.Select(row =>
            {
                if (row.IsEngine || !row.IsReady || row.Status == "Idle") return row;
                var equipment = _productionFactory?.GetEquipmentForFactory(row.Id);
                var design = _savedAircraftDesigns.FirstOrDefault(d => d.CatalogItem.DesignId == equipment?.DesignId);
                return design is not null && needs.TryGetValue(design.EngineResearchId, out var needed)
                    && needed > _engineManufacturing.GetEngineUnits(design.EngineResearchId)
                    ? row with { Status = "Engine stock low", Attention = true } : row;
            }).ToArray();
        }
        _factoryPlannerRefreshAt = Time.realtimeSinceStartup + .25f;
    }

    private static string PlannerKey(FactoryPlannerRow row) => (row.IsEngine ? "engine:" : "aircraft:") + row.Id;

    private void DrawPlannerConstruction(float width)
    {
        GUILayout.BeginHorizontal();
        _factoryConstructionKind = DrawPlannerTabs("build.type", _factoryConstructionKind, new[] { "Aircraft Factory", "Engine Factory" });
        GUILayout.EndHorizontal();
        var engine = _factoryConstructionKind == 1;
        var horizontal = width >= 1060f;
        if (horizontal) GUILayout.BeginHorizontal();
        foreach (var size in new[] { FactoryBuildingSize.Small, FactoryBuildingSize.Medium, FactoryBuildingSize.Large })
        {
            BeginModernCard();
            DrawFactoryBuildingIcon(size, 110f);
            GUILayout.Label(size + (engine ? " Engine Factory" : " Aircraft Factory"), ModernStyle(WrightTycoonUiTheme.CardHeaderStyleName));
            var cost = FactoryBalance.GetConstructionCost(size) * (engine ? .5d : 1d);
            var months = FactoryBalance.GetConstructionMonths(size);
            DrawWrappedFactoryStatus("Construction: " + FormatCurrency(cost));
            DrawWrappedFactoryStatus("Ready in: " + months + " months");
            DrawWrappedFactoryStatus("Capacity: " + FormatUiNumber(FactoryBalance.GetBuildingCapacityManDays(size), "0") + " MD/month");
            DrawWrappedFactoryStatus("Upkeep: " + FormatCurrency(engine ? EngineManufacturingNetwork.Maintenance(size) : FactoryBalance.GetBuildingMonthlyMaintenance(size)) + "/month");
            var enabled = GUI.enabled;
            GUI.enabled = enabled && _accountingSystem is not null && _accountingSystem.Cash >= cost;
            var key = "build." + (engine ? "engine" : "aircraft") + (size == FactoryBuildingSize.Small ? "" : "." + size);
            if (PlannerPrimaryButton(key, "Build " + size + " — " + FormatCurrency(cost), GUILayout.Height(36f)) && _accountingSystem is not null)
            {
                double paid;
                string message;
                var started = engine
                    ? _engineManufacturing.TryConstruct(size, _accountingSystem.Cash, out paid, out message)
                    : _productionFactory!.TryStartFactoryConstruction(size, _accountingSystem.Cash, out paid, out message);
                if (started)
                {
                    _accountingSystem.RegisterCapitalExpense(_elapsedMonths, _currentDate, paid, size + (engine ? " engine" : " aircraft") + " factory construction.");
                    _factoryPlannerTab = engine ? 1 : 0;
                    _factoryPlannerSelection = engine ? "engine:" + _engineManufacturing.Sites.Last().SiteId : "aircraft:" + _productionFactory!.Factories.Last().FactoryId;
                    _factoryPlannerStatus = 0;
                    _factoryPlannerRefreshAt = 0;
                    _showFactoryPlannerConstruction = false;
                }
                _factoryActionMessage = message;
            }
            GUI.enabled = enabled;
            DrawFactoryFinancing(engine, size, key);
            if (_accountingSystem is not null && _accountingSystem.Cash < cost)
                GUILayout.Label("Additional cash needed: " + FormatCurrency(cost - _accountingSystem.Cash), ModernStyle(WrightTycoonUiTheme.MutedLabelStyleName));
            EndModernCard();
        }
        if (horizontal) GUILayout.EndHorizontal();
        GUILayout.Label("Medium and Large use the same total price and construction time as building Small and completing the corresponding expansions. Automation is configured after construction.", ModernStyle(WrightTycoonUiTheme.MutedLabelStyleName));
        if (!string.IsNullOrWhiteSpace(_factoryActionMessage)) DrawWrappedFactoryStatus(_factoryActionMessage);
    }

    private float[] PlannerColumns(float width) => new[] { width * .18f, width * .14f, width * .14f,
        width * .10f, width * .18f, width * .16f, width * .10f };

    private void DrawPlannerTableHeader(float width)
    {
        var columns = PlannerColumns(width);
        GUILayout.BeginHorizontal(ModernStyle(WrightTycoonUiTheme.TableHeaderStyleName));
        var labels = new[] { "Plant", "Product", "Size / Automation", "MD / month", "Last-month allocation", "Target / Actual or Stock", "Status" };
        for (var i = 0; i < labels.Length; i++)
        {
            var sort = i == 0 ? FactoryPlannerSort.Name : i == 2 ? FactoryPlannerSort.Type : i == 3 ? FactoryPlannerSort.Capacity : FactoryPlannerSort.Utilization;
            if (i == 0 || i == 2 || i == 3 || i == 4)
            {
                var arrow = _factoryPlannerSort == sort ? _factoryPlannerDescending ? " ▼" : " ▲" : string.Empty;
                if (PlannerButton("sort:" + sort, labels[i] + arrow, GUILayout.Width(columns[i] - 5f), GUILayout.Height(30f)))
                {
                    _factoryPlannerDescending = _factoryPlannerSort == sort && !_factoryPlannerDescending;
                    _factoryPlannerSort = sort;
                }
            }
            else GUILayout.Label(labels[i], GUILayout.Width(columns[i] - 5f), GUILayout.Height(30f));
        }
        GUILayout.EndHorizontal();
    }

    private void DrawPlannerTableRow(FactoryPlannerRow row, float width)
    {
        var columns = PlannerColumns(width);
        var selected = PlannerKey(row) == _factoryPlannerSelection;
        GUILayout.BeginHorizontal(ModernStyle(selected ? WrightTycoonUiTheme.SelectedCardStyleName : WrightTycoonUiTheme.CardStyleName));
        GUILayout.BeginHorizontal(GUILayout.Width(columns[0] - 5f), GUILayout.Height(62f));
        DrawFactoryBuildingIcon(row.Size, 58f);
        if (PlannerButton("plant:" + PlannerKey(row), row.Name + "\n" + (row.IsEngine ? "Engine plant" : "Aircraft plant"), GUILayout.Height(58f), GUILayout.ExpandWidth(true)))
        {
            _factoryPlannerSelection = PlannerKey(row);
            _factoryPlannerDetailTab = 0;
            _factoryPlannerDetailScroll = Vector2.zero;
        }
        GUILayout.EndHorizontal();
        PlannerCell(row.Product, columns[1]);
        PlannerCell(row.Size + "\n" + (row.Automation.HasValue ? PlannerAutomationName(row.Automation.Value) : "No equipment"), columns[2]);
        PlannerCell(FormatUiNumber(row.PhysicalMD, "0") + " physical\n" + FormatUiNumber(row.AvailableMD, "0") + " staffed", columns[3]);
        GUILayout.BeginVertical(GUILayout.Width(columns[4] - 5f));
        var bar = GUILayoutUtility.GetRect(columns[4] - 20f, 15f, GUILayout.ExpandWidth(true));
        DrawPlannerAllocationBar(bar, row);
        GUILayout.Label(row.HasMeasuredAllocation
            ? $"{FormatUiNumber(row.ProductionMD!.Value, "0")} + {FormatUiNumber(row.SupportMD!.Value, "0")} MD\n{(row.MeasuredMonth.HasValue ? "Month #" + row.MeasuredMonth : "Last report")}"
            : "— no measured work", ModernStyle(WrightTycoonUiTheme.MutedLabelStyleName));
        GUILayout.EndVertical();
        PlannerCell(row.Target + "\n" + (row.IsEngine ? row.Actual : "Last: " + row.Actual), columns[5]);
        GUILayout.BeginVertical(GUILayout.Width(columns[6] - 5f));
        GUILayout.Label(row.Status, ModernStyle(row.Attention || row.IsExpanding || row.IsInstalling
            ? WrightTycoonUiTheme.WarningBadgeStyleName : row.IsReady ? WrightTycoonUiTheme.SuccessBadgeStyleName : WrightTycoonUiTheme.NeutralBadgeStyleName));
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
        var rowRect = GUILayoutUtility.GetLastRect();
        if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
        {
            _factoryPlannerSelection = PlannerKey(row);
            _factoryPlannerDetailTab = 0;
            _factoryPlannerDetailScroll = Vector2.zero;
            Event.current.Use();
        }
    }

    private static void PlannerCell(string text, float width) => GUILayout.Label(text, GUILayout.Width(width - 5f), GUILayout.MinHeight(58f));

    private static string PlannerAutomationName(FactoryAutomationTier tier) => tier switch
    {
        FactoryAutomationTier.HandBuilt => "Hand-built",
        FactoryAutomationTier.JigsAndFixtures => "Jigs",
        FactoryAutomationTier.MechanizedCells => "Mechanized",
        FactoryAutomationTier.AutomatedLine => "Automated",
        _ => "Sophisticated",
    };

    private static void DrawPlannerAllocationBar(Rect rect, FactoryPlannerRow row)
    {
        DrawRect(rect, WrightTycoonUiTheme.BorderStrong);
        if (!row.HasMeasuredAllocation) return;
        var allocation = row.Allocation;
        var productionWidth = rect.width * (float)allocation.NormalizedProductionFraction;
        var supportWidth = rect.width * (float)allocation.SupportFraction;
        DrawRect(new Rect(rect.x, rect.y, productionWidth, rect.height), WrightTycoonUiTheme.Accent);
        DrawRect(new Rect(rect.x + productionWidth, rect.y, supportWidth, rect.height), WrightTycoonUiTheme.Warning);
    }

    private void DrawFactoryBuildingIcon(FactoryBuildingSize size, float pixels)
    {
        if (!_factoryBuildingTextures.TryGetValue(size, out var texture))
        {
            texture = Resources.Load<Texture2D>(FactoryBuildingIcons.ResourceKey(size));
            _factoryBuildingTextures[size] = texture;
        }
        var rect = GUILayoutUtility.GetRect(pixels, pixels, GUILayout.Width(pixels), GUILayout.Height(pixels));
        if (texture != null) GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, true);
        else GUI.Label(rect, size.ToString());
    }

    private void DrawPlannerSelectedFactory(FactoryPlannerRow row, float width)
    {
        BeginModernCard(selected: true);
        GUILayout.BeginHorizontal();
        DrawFactoryBuildingIcon(row.Size, 52f);
        GUILayout.BeginVertical();
        GUILayout.Label(row.Name + " — " + row.Product, ModernStyle(WrightTycoonUiTheme.CardHeaderStyleName));
        GUILayout.Label($"{row.Size} · {row.Status} · {FormatUiNumber(row.PhysicalMD, "0")} physical MD/month", ModernStyle(WrightTycoonUiTheme.MutedLabelStyleName));
        GUILayout.BeginHorizontal();
        _factoryPlannerDetailTab = DrawPlannerTabs("detail", _factoryPlannerDetailTab, new[] { "Production & Equipment", "Building & Workforce" });
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
        EndModernCard();

        var horizontal = width >= 1100f;
        if (horizontal) GUILayout.BeginHorizontal();
        GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        if (row.IsEngine)
        {
            var plant = _engineManufacturing.Sites.FirstOrDefault(s => s.SiteId == row.Id);
            if (plant is not null)
            {
                _enginePlantSelection = _engineManufacturing.Sites.ToList().FindIndex(s => s.SiteId == row.Id);
                BeginModernCard();
                DrawEnginePlantProductionSummary(plant, row);
                if (_factoryPlannerDetailTab == 1 || !plant.IsOperational) DrawEnginePlantBuilding(plant);
                else DrawEnginePlantConfiguration(plant);
                DrawEngineSupplyModeSwitch();
                EndModernCard();
            }
        }
        else DrawPlannerAircraftDetails(row);
        GUILayout.EndVertical();
        if (horizontal)
        {
            GUILayout.Space(10f);
            GUILayout.BeginVertical(GUILayout.Width(width * .28f));
            DrawPlannerCapacityDetails(row);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }
        else
        {
            if (PlannerButton("inspector.toggle", _showFactoryCapacityInspector ? "Hide Capacity & Supply" : "Show Capacity & Supply", GUILayout.Height(28f)))
                _showFactoryCapacityInspector = !_showFactoryCapacityInspector;
            if (_showFactoryCapacityInspector) DrawPlannerCapacityDetails(row);
        }
    }

    private void DrawPlannerAircraftDetails(FactoryPlannerRow row)
    {
        var factory = _productionFactory?.GetFactory(row.Id);
        if (factory is null || _productionFactory is null) return;
        _selectedFactoryIndex = _productionFactory.Factories.ToList().FindIndex(f => f.FactoryId == row.Id);
        var equipment = _productionFactory.GetEquipmentForFactory(row.Id);
        BeginModernCard();
        if (!factory.IsUnderConstruction && _factoryPlannerDetailTab == 0)
        {
            var percent = DrawFactoryPartsAllocation("aircraft.parts-share", factory.PartsAllocationPercent, factory.AvailableManDays);
            if (percent != factory.PartsAllocationPercent)
            {
                _productionFactory.SetPartsAllocationPercent(factory.FactoryId, percent);
                _factoryPlannerRefreshAt = 0;
            }
        }
        if (factory.IsUnderConstruction)
            DrawWrappedFactoryStatus($"Under construction · {factory.ConstructionMonthsRemaining} months remaining. Production starts after building completion and equipment installation.");
        else if (_factoryPlannerDetailTab == 1)
        {
            DrawFactoryBuildingControls(factory);
            if (equipment is null && PlannerDangerButton("aircraft.sell", "Sell Empty Factory", GUILayout.Height(28f))
                && _productionFactory.TrySellFactory(factory.FactoryId, out var income, out var message))
            {
                _accountingSystem?.RegisterContractIncome(_elapsedMonths, _currentDate, income,
                    CalculateInventoryWithSupport(_productionFactory.WarehouseUnitsByDesign, BuildProductionCatalog(includeOnlyResearched: false)), "Factory asset sale.");
                _factoryActionMessage = message;
                _factoryPlannerRefreshAt = 0;
            }
        }
        else if (equipment is null) DrawFactoryEquipmentPurchase(factory);
        else
        {
            DrawModernInfoRow("Aircraft Type", ResolveAircraftDesignName(equipment.DesignId));
            DrawFactoryAutomationChoice("aircraft.automation", (int)equipment.AutomationTier, confirmed: true);
            DrawAircraftStockLimit(equipment.DesignId);
            var group = _airframePartsGroups.GetGroupId(equipment.DesignId) ?? equipment.DesignId;
            DrawAirframePartsTarget(group);
            GUILayout.Label("Reserve is shared by compatible types and all plants using this parts group, not a monthly output target.", ModernStyle(WrightTycoonUiTheme.MutedLabelStyleName));
            DrawInstalledFactoryEquipment(factory, equipment, compact: true);
            if (_factoryViewTab == 1) { _factoryPlannerTab = 3; _factoryViewTab = 0; }
        }
        if (!string.IsNullOrWhiteSpace(_factoryActionMessage)) DrawWrappedFactoryStatus(_factoryActionMessage);
        EndModernCard();
    }

    private void DrawAircraftStockLimit(string designId)
    {
        if (_productionFactory is null) return;
        var target = _productionFactory.GetAircraftStockTarget(designId);
        if (!_aircraftStockLimitDrafts.TryGetValue(designId, out var draft) || draft.SourceTarget != target)
        {
            draft = new AircraftStockLimitDraft { SourceTarget = target,
                Text = (target ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture) };
            _aircraftStockLimitDrafts[designId] = draft;
        }
        draft.Text = DrawEngineStockTargetInput("aircraft.stock-target", "Aircraft Stock Limit", designId, draft.Text);
        var valid = TryParseEngineStockTarget(draft.Text, out var next);
        if (!valid) DrawError("Stock limits must be whole numbers from 0 to 2 147 483 647.");
        var enabled = GUI.enabled;
        GUILayout.BeginHorizontal();
        GUI.enabled = enabled && valid;
        if (PlannerPrimaryButton("aircraft.stock.apply", "Apply Aircraft Stock Limit"))
        {
            _productionFactory.SetAircraftStockTarget(designId, next);
            draft.SourceTarget = target = next;
            _factoryPlannerRefreshAt = 0;
        }
        GUI.enabled = enabled && target.HasValue;
        if (PlannerButton("aircraft.stock.clear", "Remove Limit"))
        {
            _productionFactory.ClearAircraftStockTarget(designId);
            draft.SourceTarget = target = null;
            _factoryPlannerRefreshAt = 0;
        }
        GUI.enabled = enabled;
        GUILayout.EndHorizontal();
        DrawWrappedFactoryStatus($"Completed aircraft in stock: {FormatUiInteger(_productionFactory.GetWarehouseUnits(designId))} · limit: {(target.HasValue ? FormatUiInteger(target.Value) : "unlimited")}. Shared by all plants producing this exact type. Zero pauses new aircraft work; withdrawals automatically restart replenishment.");
    }

    private void DrawAirframePartsTarget(string group)
    {
        var targets = _airframeSparePartStockTargets;
        var key = "airframe:" + group;
        var id = "aircraft.parts";
        var target = targets.TryGetValue(group, out var quantity) ? quantity : 0;
        if (!_airframePartsTargetDrafts.TryGetValue(key, out var draft) || draft.SourceTarget != target)
        {
            draft = new AirframePartsTargetDraft { SourceTarget = target, Text = target.ToString(System.Globalization.CultureInfo.InvariantCulture) };
            _airframePartsTargetDrafts[key] = draft;
        }
        draft.Text = DrawEngineStockTargetInput(id + "-target", "Shared Airframe Parts Target", group, draft.Text);
        var valid = TryParseEngineStockTarget(draft.Text, out var next);
        if (!valid) DrawError("Stock targets must be whole numbers from 0 to 2 147 483 647.");
        var enabled = GUI.enabled;
        GUI.enabled = enabled && valid;
        if (PlannerPrimaryButton(id + ".apply", "Apply Parts Stock Target"))
        {
            targets[group] = next;
            draft.SourceTarget = next;
            _factoryPlannerRefreshAt = 0;
        }
        GUI.enabled = enabled;
    }

    private void DrawPlannerCapacityDetails(FactoryPlannerRow row)
    {
        BeginModernCard();
        DrawModernSectionHeader("Capacity Allocation", "Actual last-month work; current settings are not a production guarantee.");
        DrawModernInfoRow("Physical building", FormatUiNumber(row.PhysicalMD, "0") + " MD/month");
        DrawModernInfoRow("Current staffed capacity", FormatUiNumber(row.AvailableMD, "0") + " MD/month");
        if (row.HasMeasuredAllocation)
        {
            DrawModernInfoRow("New production", FormatUiNumber(row.ProductionMD!.Value, "0.0") + " MD");
            DrawModernInfoRow("Parts / support", FormatUiNumber(row.SupportMD!.Value, "0.0") + " MD");
            DrawModernInfoRow("Unused physical capacity", FormatUiNumber(row.PhysicalMD * row.FreeFraction, "0.0") + " MD");
            var rect = GUILayoutUtility.GetRect(100f, 18f, GUILayout.ExpandWidth(true));
            DrawPlannerAllocationBar(rect, row);
        }
        else GUILayout.Label("No per-plant measured work yet. Allocation appears when this factory reports work; missing data is not treated as zero production.");
        GUILayout.Label("Automation reduces work per product; it does not increase the building's physical MD capacity.", ModernStyle(WrightTycoonUiTheme.MutedLabelStyleName));
        EndModernCard();
        if (row.IsEngine)
        {
            var plant = _engineManufacturing.Sites.FirstOrDefault(p => p.SiteId == row.Id);
            var catalog = BuildEngineManufacturingCatalog();
            if (plant is not null && catalog.TryGetValue(plant.EngineDesignId, out var item))
            {
                BeginModernCard();
                DrawModernSectionHeader("Engine Supply", "Exact engines and shared parts are separate stocks.");
                DrawModernInfoRow("Whole engines in stock", FormatUiInteger(_engineManufacturing.GetEngineUnits(item.EngineDesignId)));
                DrawModernInfoRow("Shared engine parts", FormatUiInteger(_engineManufacturing.GetPartUnits(item.PartsGroupId)));
                DrawModernInfoRow("Required replacements", FormatUiInteger(_fleet.GetEngineServiceDemand(_currentDate).Where(d => d.EngineDesignId == item.EngineDesignId).Sum(d => d.ReplacementEngines)));
                EndModernCard();
            }
        }
        else
        {
            var equipment = _productionFactory?.GetEquipmentForFactory(row.Id);
            if (equipment is null) return;
            var design = _savedAircraftDesigns.FirstOrDefault(d => d.CatalogItem.DesignId == equipment.DesignId);
            if (design is null) return;
            BeginModernCard();
            DrawModernSectionHeader("Supply & Support", "Selected-type forecast; inventories are shared. Not demand allocated to this plant.");
            DrawModernInfoRow("Engine stock", FormatUiInteger(_engineManufacturing.GetEngineUnits(design.EngineResearchId)));
            var group = _airframePartsGroups.GetGroupId(equipment.DesignId) ?? equipment.DesignId;
            DrawModernInfoRow("Shared airframe parts", FormatUiInteger(_productionFactory!.GetAirframeSparePartUnits(group)));
            foreach (var point in _fleet.GetSupportForecast(equipment.DesignId, _currentDate, 3))
                DrawModernInfoRow(FormatFleetMonth(point.MonthIndex), point.AirframeSpareParts + " airframe parts due");
            EndModernCard();
        }
    }


    private void DrawPlannerMotorReport()
    {
        if (_lastEngineManufacturingReport is not { } report) return;
        DrawModernSectionHeader("Motor Network — Last Month", "Aggregate accounting, not the selected plant's profit.");
        DrawModernInfoRow("Completed motors / parts", $"{report.CompletedEngines} / {report.CompletedParts}");
        DrawModernInfoRow("Worked", FormatUiNumber(report.UsedManDays, "0.0") + " MD");
        DrawModernInfoRow("Total manufacturing expenses", FormatCurrency(report.TotalExpenses));
    }
}
