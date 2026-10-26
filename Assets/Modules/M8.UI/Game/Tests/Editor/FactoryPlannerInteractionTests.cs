using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WrightTycoon.Core.Accounting;
using WrightTycoon.Core.Factories;
using WrightTycoon.Core.Aircraft;
using WrightTycoon.Core.Airframes;
using WrightTycoon.Core.Engines;
using WrightTycoon.Core.Physics;
using WrightTycoon.Core.Research;
using WrightTycoon.Core.Simulation;
using WrightTycoon.Game.Launch;

namespace WrightTycoon.Game.Tests;

/// <summary>Exercises actual IMGUI buttons through their recorded screen rectangles, never by invoking action handlers.</summary>
public sealed class FactoryPlannerInteractionTests
{
    private sealed class View : IDisposable
    {
        public readonly GameObject Object;
        public readonly WrightTycoonLaunchRunner Runner;
        public readonly FactoryPlannerGuiTestWindow Window;
        public readonly AircraftProductionFactory Aircraft = new();
        public readonly EngineManufacturingNetwork Engines = new();
        public readonly AccountingSystem Accounting = new(10_000_000_000d);
        public FactoryAircraftTypeCatalogItem? FixtureAircraft;
        public Engine? FixtureEngine;
        private static readonly Type RunnerType = typeof(WrightTycoonLaunchRunner);

        public View(int width, bool sites = false, bool approvedDesigns = false)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("Mouse interaction tests require a graphics-enabled Unity EditMode run.");
            Object = new GameObject("FactoryPlannerInteractionTest");
            Object.SetActive(false);
            Runner = Object.AddComponent<WrightTycoonLaunchRunner>();
            if (sites)
            {
                Aircraft.TryStartFactoryConstruction(double.MaxValue, out _, out _);
                Engines.TryConstruct(double.MaxValue, out _, out _);
                for (var month = 0; month < 18; month++) { Aircraft.AdvanceMonth(); Engines.AdvanceMonth(); }
                Aircraft.TryStartFactoryConstruction(double.MaxValue, out _, out _);
            }
            Set("_productionFactory", Aircraft); Set("_engineManufacturing", Engines); Set("_accountingSystem", Accounting);
            if (approvedDesigns) AddApprovedLocalDesigns();
            var draw = RunnerType.GetMethod("DrawFactoryPlanner", BindingFlags.Instance | BindingFlags.NonPublic)!;
            Window = ScriptableObject.CreateInstance<FactoryPlannerGuiTestWindow>();
            Window.position = new Rect(20, 20, width, 900);
            Window.Draw = () => draw.Invoke(Runner, new object[] { (float)width, 900f });
            try { Window.ShowUtility(); Window.Focus(); Render(); }
            catch { Window.Draw = null; Window.Close(); UnityEngine.Object.DestroyImmediate(Object); throw; }
        }

        public T Get<T>(string field) => (T)RunnerType.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Runner)!;
        public void Set(string field, object value) => RunnerType.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Runner, value);
        public Dictionary<string, Rect> Rects => Get<Dictionary<string, Rect>>("_factoryPlannerControlRects");
        public Dictionary<string, bool> Enabled => Get<Dictionary<string, bool>>("_factoryPlannerControlEnabled");

        private void AddApprovedLocalDesigns()
        {
            // Real runtime builders and locally defined designs; no player save, files, or action handlers are used.
            var atmosphere = new IsaAtmosphereModel();
            var aerodynamics = new AerodynamicsCalculator();
            var engineBuilder = new EngineBuilder(new EngineCalculator(), atmosphere);
            var engine = engineBuilder.Build(new EngineDefinition { Id = "interaction.engine", DisplayName = "Interaction Engine", EngineType = EngineType.Radial },
                new EngineParameters { DisplacementLiters = 12, CompressionRatio = 8, CylinderCount = 9, CoolingEfficiency = .8 });
            FixtureEngine = engine;
            Set("_engineBuilder", engineBuilder);
            Set("_currentDate", new SimulationDate(1960, 1));
            var airframe = new AirframeBuilder(new AirframeCalculator(aerodynamics, atmosphere)).Build(
                new AirframeDefinition { Id = "interaction.airframe", DisplayName = "Interaction Airframe" },
                new AirframeParameters { EngineCount = 1, WingAreaSquareMeters = 24, FuelAmountLiters = 200 });
            var family = new AircraftFamilyDefinition { Id = "interaction.family", DisplayName = "Interaction Family",
                EngineDefinitionId = engine.Definition.Id, AirframeDefinitionId = airframe.Definition.Id, LockedEngineType = EngineType.Radial,
                LockedEngineCount = 1, LockedStructureType = airframe.Parameters.StructureType, LockedMaterialType = airframe.Parameters.MaterialType,
                BaseEngineParameters = engine.Parameters.Clone(), BaseAirframeParameters = airframe.Parameters.Clone() };
            var type = new AircraftTypeDefinition { Id = "interaction.type", DisplayName = "Interaction Type", FamilyId = family.Id };
            var performance = new AircraftPerformanceCalculator(atmosphere, aerodynamics).Calculate(engine, airframe);
            var model = new WrightTycoon.Core.Aircraft.Aircraft(family, type, engine, airframe, performance, new DevelopmentCostReport(0, 0, 85_000, 230));
            var item = new FactoryAircraftTypeCatalogItem("interaction.aircraft", "Interaction Aircraft", model);
            FixtureAircraft = item;
            const string engineResearch = "interaction-engine";
            const string familyResearch = "interaction-family";
            const string typeResearch = "interaction-type";
            var engineSavedType = RunnerType.GetNestedType("SavedEngineDesign", BindingFlags.NonPublic)!;
            var engineConstructor = engineSavedType.GetConstructor(new[] { typeof(string), typeof(string), typeof(Engine), typeof(int), typeof(int),
                typeof(int?), typeof(string), typeof(int?), typeof(int?), typeof(int?) })!;
            var savedEngine = engineConstructor.Invoke(new object?[] { "Interaction Engine", engineResearch, engine, 1950, 1950, null, "", 1950, null, null });
            ((IList)RunnerType.GetField("_savedEngines", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Runner)!).Add(savedEngine);
            var savedAircraftType = RunnerType.GetNestedType("SavedAircraftDesign", BindingFlags.NonPublic)!;
            var aircraftConstructor = savedAircraftType.GetConstructors()[0];
            var savedAircraft = aircraftConstructor.Invoke(new object?[] { "Interaction Aircraft", item, engineResearch, familyResearch, typeResearch,
                "", 1950, 1950, null, "", 1950, null, null, false });
            ((IList)RunnerType.GetField("_savedAircraftDesigns", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Runner)!).Add(savedAircraft);
            var board = new ResearchBoard();
            board.UpsertProposal(ResearchSubjectType.Engine, engineResearch, "Interaction Engine", new ResearchQuote(0, 1, 0));
            board.UpsertProposal(ResearchSubjectType.AircraftFamily, familyResearch, "Interaction Family", new ResearchQuote(0, 1, 0));
            board.UpsertProposal(ResearchSubjectType.AircraftType, typeResearch, "Interaction Type", new ResearchQuote(0, 1, 0));
            var snapshot = board.CreateSnapshot();
            foreach (var project in snapshot.Projects)
            { project.Status = ResearchStatus.Completed; project.ProgressMonths = 1; project.StartedMonthIndex = 0; project.CompletedMonthIndex = 1; }
            board.RestoreSnapshot(snapshot); Set("_researchBoard", board);
        }
        public void Render()
        {
            Window.SendEvent(new Event { type = EventType.Layout });
            Window.SendEvent(new Event { type = EventType.Repaint });
            Assert.That(Window.Error, Is.Null, Window.Error?.ToString());
        }
        public void Click(string key)
        {
            Render();
            Assert.That(Rects.ContainsKey(key), Is.True, $"Missing visible control '{key}'. Visible: {string.Join(", ", Rects.Keys)}");
            // Detail panels are scrollable. Reach their real controls using wheel input, not by mutating scroll state.
            for (var attempt = 0; attempt < 12; attempt++)
            {
                var point = Rects[key].center - Window.GuiOrigin;
                if (point.y >= 20f && point.y <= Window.position.height - 25f) break;
                Window.SendEvent(new Event { type = EventType.ScrollWheel,
                    mousePosition = new Vector2(300f, Window.position.height - 90f),
                    delta = new Vector2(0f, point.y > Window.position.height - 25f ? 5f : -5f) });
                Render();
                Assert.That(Rects.ContainsKey(key), Is.True, $"Control '{key}' disappeared while scrolling to it.");
            }
            Assert.That(Enabled.TryGetValue(key, out var enabled) && enabled, Is.True, $"Control '{key}' is disabled.");
            var rect = Rects[key];
            Assert.That(rect.width, Is.GreaterThan(0)); Assert.That(rect.height, Is.GreaterThan(0));
            var local = rect.center - Window.GuiOrigin;
            Assert.That(local.x, Is.InRange(0f, Window.position.width));
            Assert.That(local.y, Is.InRange(0f, Window.position.height));
            Window.SendEvent(new Event { type = EventType.MouseDown, mousePosition = local, button = 0, clickCount = 1 });
            Window.SendEvent(new Event { type = EventType.MouseUp, mousePosition = local, button = 0, clickCount = 1 });
            Render();
        }
        public void TypeInto(string key, string value)
        {
            Click(key);
            Window.SendEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.A, modifiers = EventModifiers.Control });
            Window.SendEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.Backspace });
            foreach (var character in value)
                Window.SendEvent(new Event { type = EventType.KeyDown, character = character });
            Render();
        }

        public void SlideTo(string key, bool maximum)
        {
            Click(key);
            var rect = Rects[key];
            var start = rect.center - Window.GuiOrigin;
            var end = new Vector2(maximum ? rect.xMax - 1f : rect.xMin + 1f, rect.center.y) - Window.GuiOrigin;
            Window.SendEvent(new Event { type = EventType.MouseDown, mousePosition = start, button = 0 });
            Window.SendEvent(new Event { type = EventType.MouseDrag, mousePosition = end, button = 0 });
            Window.SendEvent(new Event { type = EventType.MouseUp, mousePosition = end, button = 0 });
            Render();
        }

        public void Dispose()
        {
            Window.Draw = null; Window.Close(); UnityEngine.Object.DestroyImmediate(Object);
        }
    }

    [TestCase(1280)]
    [TestCase(1920)]
    public void NavigationAndConstructionAreRealClickableControlsWithoutAutomaticEmptyStateExpansion(int width)
    {
        using var view = new View(width);
        Assert.That(view.Get<bool>("_showFactoryPlannerConstruction"), Is.False);
        Assert.That(view.Rects.ContainsKey("build.aircraft"), Is.False, "Empty factories should show the table/empty state, not expand construction automatically.");
        for (var tab = 0; tab < 5; tab++)
        {
            view.Click("nav:" + tab);
            Assert.That(view.Get<int>("_factoryPlannerTab"), Is.EqualTo(tab));
        }
        view.Click("build.toggle");
        Assert.That(view.Get<bool>("_showFactoryPlannerConstruction"), Is.True);
        Assert.That(view.Rects.ContainsKey("build.aircraft"), Is.True);
        Assert.That(view.Rects.ContainsKey("build.engine"), Is.False, "Only the chosen factory kind has construction buttons.");
        Assert.That(view.Rects.ContainsKey("build.aircraft.Medium"), Is.True);
        Assert.That(view.Rects.ContainsKey("build.aircraft.Large"), Is.True);
        view.Click("build.back");
        Assert.That(view.Get<bool>("_showFactoryPlannerConstruction"), Is.False);
        Assert.That(view.Aircraft.Factories, Is.Empty); Assert.That(view.Engines.Sites, Is.Empty);
        Assert.That(view.Accounting.Cash, Is.EqualTo(10_000_000_000d));
    }

    [TestCase(false, "build.aircraft", 80_000_000d, 0, "aircraft:factory.001")]
    [TestCase(true, "build.engine", 40_000_000d, 1, "engine:engine-plant.001")]
    public void BuildButtonsCreateOnePlantAndBookOneCapitalPayment(bool motor, string key, double cost, int selectedTab, string selectedPlant)
    {
        using var view = new View(1280);
        view.Click("build.toggle");
        if (motor) view.Click("build.type:1");
        view.Click(key);
        Assert.That(view.Aircraft.Factories.Count, Is.EqualTo(motor ? 0 : 1));
        Assert.That(view.Engines.Sites.Count, Is.EqualTo(motor ? 1 : 0));
        Assert.That(view.Accounting.Cash, Is.EqualTo(10_000_000_000d - cost));
        Assert.That(view.Accounting.GetTransactions().Count, Is.EqualTo(1));
        Assert.That(view.Get<int>("_factoryPlannerTab"), Is.EqualTo(selectedTab));
        Assert.That(view.Get<string>("_factoryPlannerSelection"), Is.EqualTo(selectedPlant));
        view.Render(); view.Render();
        Assert.That(view.Accounting.Cash, Is.EqualTo(10_000_000_000d - cost), "Repaint must not replay the construction action.");
    }

    [TestCase(1280)]
    [TestCase(1920)]
    public void FiltersSortingRowsAndDetailTabsRespondToMouseInput(int width)
    {
        using var view = new View(width, sites: true);
        view.Click("nav:0");
        view.Click("filter:1"); Assert.That(view.Get<int>("_factoryPlannerStatus"), Is.EqualTo((int)FactoryPlannerStatusFilter.Attention));
        view.Click("filter:2"); Assert.That(view.Get<int>("_factoryPlannerStatus"), Is.EqualTo(2));
        view.Click("filter:0"); Assert.That(view.Get<int>("_factoryPlannerStatus"), Is.EqualTo((int)FactoryPlannerStatusFilter.All));
        foreach (var sort in new[] { FactoryPlannerSort.Name, FactoryPlannerSort.Type, FactoryPlannerSort.Capacity, FactoryPlannerSort.Utilization })
        {
            view.Click("sort:" + sort);
            Assert.That(view.Get<FactoryPlannerSort>("_factoryPlannerSort"), Is.EqualTo(sort));
        }
        view.Click("plant:aircraft:factory.002");
        Assert.That(view.Get<string>("_factoryPlannerSelection"), Is.EqualTo("aircraft:factory.002"));
        view.Click("plant:aircraft:factory.001");
        view.Click("detail:1"); Assert.That(view.Get<int>("_factoryPlannerDetailTab"), Is.EqualTo(1));
        view.Click("detail:0"); Assert.That(view.Get<int>("_factoryPlannerDetailTab"), Is.Zero);
        view.Click("nav:1"); view.Click("plant:engine:engine-plant.001");
        Assert.That(view.Get<string>("_factoryPlannerSelection"), Is.EqualTo("engine:engine-plant.001"));
    }

    [TestCase(1280)]
    [TestCase(1920)]
    public void NavigationBuildAndFiltersStayAboveTheFirstFactoryRowAndWithinWindow(int width)
    {
        using var view = new View(width, sites: true);
        view.Click("nav:0");
        var row = view.Rects["plant:aircraft:factory.001"];
        foreach (var key in new[] { "nav:0", "nav:1", "nav:2", "nav:3", "nav:4", "build.toggle", "filter:0", "filter:1", "filter:2" })
        {
            Assert.That(view.Rects.ContainsKey(key), Is.True, key);
            var rect = view.Rects[key];
            Assert.That(rect.yMax, Is.LessThanOrEqualTo(row.yMin + 1f), $"'{key}' must not overlap the table rows.");
            Assert.That(rect.xMin, Is.GreaterThanOrEqualTo(view.Window.GuiOrigin.x - 1f), key);
            Assert.That(rect.xMax, Is.LessThanOrEqualTo(view.Window.GuiOrigin.x + view.Window.position.width + 1f), key);
        }
    }

    [Test]
    public void AircraftBuildingExpansionButtonBooksOnlyOneUpgradeAndPausesThatFactory()
    {
        using var view = new View(1280, sites: true);
        view.Click("nav:0"); view.Click("plant:aircraft:factory.001"); view.Click("detail:1");
        view.Click("aircraft.expand");
        Assert.That(view.Aircraft.Factories[0].ExpansionMonthsRemaining, Is.EqualTo(12));
        Assert.That(view.Aircraft.Factories[0].AvailableManDays, Is.Zero);
        Assert.That(view.Accounting.Cash, Is.EqualTo(10_000_000_000d - 160_000_000d));
        view.Render(); Assert.That(view.Accounting.GetTransactions().Count, Is.EqualTo(1));
    }

    [TestCase(false, "Medium", 240_000_000d, 30)]
    [TestCase(false, "Large", 720_000_000d, 48)]
    [TestCase(true, "Medium", 120_000_000d, 30)]
    [TestCase(true, "Large", 360_000_000d, 48)]
    [Category("Extended")]
    public void LargerConstructionButtonsBuildSelectedSizeOnce(bool engine, string size, double cost, int months)
    {
        using var view = new View(1280);
        view.Click("build.toggle");
        view.Click(engine ? "build.type:1" : "build.type:0");
        Assert.That(view.Rects.ContainsKey(engine ? "build.aircraft" : "build.engine"), Is.False);
        view.Click("build." + (engine ? "engine" : "aircraft") + "." + size);
        Assert.That(view.Accounting.Cash, Is.EqualTo(10_000_000_000d - cost));
        Assert.That(view.Accounting.GetTransactions().Count, Is.EqualTo(1));
        Assert.That(engine ? view.Engines.Sites[0].BuildingSize.ToString() : view.Aircraft.Factories[0].BuildingSize.ToString(), Is.EqualTo(size));
        Assert.That(engine ? view.Engines.Sites[0].ConstructionMonthsRemaining : view.Aircraft.Factories[0].ConstructionMonthsRemaining, Is.EqualTo(months));
    }

    [TestCase(false, "", 80_000_000d)]
    [TestCase(false, ".Medium", 240_000_000d)]
    [TestCase(false, ".Large", 720_000_000d)]
    [TestCase(true, "", 40_000_000d)]
    [TestCase(true, ".Medium", 120_000_000d)]
    [TestCase(true, ".Large", 360_000_000d)]
    [Category("Extended")]
    public void FinanceButtonBuildsOnceWithExactlyNinetyPercentStandardLoan(bool engine, string suffix, double cost)
    {
        using var view = new View(1280);
        view.Click("build.toggle"); view.Click(engine ? "build.type:1" : "build.type:0");
        var key = (engine ? "engine" : "aircraft") + suffix;
        Assert.That(view.Rects["finance." + key].yMin, Is.GreaterThanOrEqualTo(view.Rects["build." + key].yMax));
        view.Click("finance." + key);
        var loans = view.Get<LoanPortfolio>("_loanPortfolio");
        Assert.That(loans.ActiveLoans.Count, Is.EqualTo(1));
        Assert.That(loans.OutstandingPrincipal, Is.EqualTo(cost * .9));
        Assert.That(loans.ActiveLoans[0].OriginalTermMonths, Is.EqualTo(120));
        var constructionSize = suffix == ".Medium" ? WrightTycoon.Core.Balance.FactoryBuildingSize.Medium
            : suffix == ".Large" ? WrightTycoon.Core.Balance.FactoryBuildingSize.Large : WrightTycoon.Core.Balance.FactoryBuildingSize.Small;
        Assert.That(loans.ActiveLoans[0].NextDueMonthIndex, Is.EqualTo(WrightTycoon.Core.Balance.FactoryBalance.GetConstructionMonths(constructionSize)));
        Assert.That(loans.ActiveLoans[0].RepaymentMode, Is.EqualTo(LoanRepaymentMode.Amortized));
        Assert.That(view.Accounting.Cash, Is.EqualTo(10_000_000_000d - cost * .1045d).Within(.01));
        Assert.That(view.Accounting.GetTransactions().Count, Is.EqualTo(3));
        Assert.That(engine ? view.Engines.Sites.Count : view.Aircraft.Factories.Count, Is.EqualTo(1));
        view.Render(); view.Render();
        Assert.That(loans.ActiveLoans.Count, Is.EqualTo(1));
    }

    [Test]
    public void FinancingWorksWithDepositAndFeeButWithoutCashForOutrightConstruction()
    {
        using var view = new View(1280);
        var cash = new AccountingSystem(8_360_000d); view.Set("_accountingSystem", cash);
        view.Click("build.toggle");
        Assert.That(view.Enabled["build.aircraft"], Is.False);
        Assert.That(view.Enabled["finance.aircraft"], Is.True);
        view.Click("finance.aircraft");
        Assert.That(cash.Cash, Is.EqualTo(0).Within(.01));
        Assert.That(view.Aircraft.Factories.Count, Is.EqualTo(1));
        Assert.That(view.Get<LoanPortfolio>("_loanPortfolio").OutstandingPrincipal, Is.EqualTo(72_000_000d));
    }

    [Test]
    public void WarehouseFilterButtonsAreVisibleAndClickable()
    {
        using var view = new View(1280, sites: true);
        view.Click("nav:2");
        for (var filter = 0; filter < 3; filter++) view.Click("warehouses:" + filter);
        Assert.That(view.Rects.ContainsKey("warehouses:3"), Is.False);
        Assert.That(view.Get<int>("_factoryPlannerTab"), Is.EqualTo(2));
        Assert.That(view.Accounting.GetTransactions(), Is.Empty);
    }

    [Test]
    public void EngineDesignerServiceIntervalChoicesRespondToRealMouseClicks()
    {
        using var view = new View(1280);
        var method = typeof(WrightTycoonLaunchRunner).GetMethod("DrawEngineMaintenanceDesignControls", BindingFlags.Instance | BindingFlags.NonPublic)!;
        view.Window.Draw = () => method.Invoke(view.Runner, null);
        foreach (var entry in new[] { (index: 0, hours: 200), (index: 1, hours: 500), (index: 2, hours: 1000), (index: 3, hours: 2000) })
        {
            view.Click("engine.service:" + entry.index);
            Assert.That(view.Get<EngineParameters>("_engineParameters").ServiceIntervalHours, Is.EqualTo(entry.hours));
        }
    }

    [TestCase(1280)]
    [TestCase(1920)]
    public void HorizontalPlannerGivesDetailMajorityWidthAndMoreThanHalfWindowHeight(int width)
    {
        using var view = new View(width, sites: true);
        var detail = view.Rects["viewport.detail"];
        var plant = view.Rects["plant:aircraft:factory.001"];
        Assert.That(detail.xMin, Is.GreaterThan(plant.xMax));
        Assert.That(detail.height, Is.GreaterThan(view.Window.position.height * .55f));
        Assert.That(detail.width, Is.GreaterThan(view.Window.position.width * .65f));
        Assert.That(view.Rects["detail:0"].yMin, Is.LessThan(plant.yMax + 90f));
    }

    [Test]
    public void EngineExpansionButtonStartsOneUpgradeAndBooksItsRealCapitalCost()
    {
        using var view = new View(1280, sites: true);
        view.Click("nav:1"); view.Click("plant:engine:engine-plant.001"); view.Click("detail:1");
        view.Click("engine.expand");
        Assert.That(view.Engines.Sites[0].ExpansionMonthsRemaining, Is.EqualTo(12));
        Assert.That(view.Engines.Sites[0].AvailableManDays, Is.Zero);
        Assert.That(view.Accounting.Cash, Is.EqualTo(10_000_000_000d - 80_000_000d));
        view.Render(); Assert.That(view.Accounting.GetTransactions().Count, Is.EqualTo(1));
    }

    [Test]
    public void LowCashBuildButtonsAreDisabledAndMouseInputCannotCreatePlants()
    {
        using var view = new View(1280);
        var lowCash = new AccountingSystem(1d); view.Set("_accountingSystem", lowCash);
        view.Click("build.toggle");
        foreach (var key in new[] { "build.aircraft", "build.engine" })
        {
            view.Click(key == "build.engine" ? "build.type:1" : "build.type:0");
            view.Render();
            Assert.That(view.Rects.ContainsKey(key), Is.True);
            Assert.That(view.Enabled[key], Is.False);
            var point = view.Rects[key].center - view.Window.GuiOrigin;
            view.Window.SendEvent(new Event { type = EventType.MouseDown, button = 0, clickCount = 1, mousePosition = point });
            view.Window.SendEvent(new Event { type = EventType.MouseUp, button = 0, clickCount = 1, mousePosition = point });
            view.Render();
        }
        Assert.That(view.Aircraft.Factories, Is.Empty); Assert.That(view.Engines.Sites, Is.Empty);
        Assert.That(lowCash.Cash, Is.EqualTo(1d)); Assert.That(lowCash.GetTransactions(), Is.Empty);
    }

    [Test]
    [Category("Extended")]
    public void AirframePartsTargetIsTypedWithoutSliderCapAndInvalidTextCannotApply()
    {
        using var view = new View(1280, sites: true, approvedDesigns: true);
        view.Click("nav:0"); view.Click("plant:aircraft:factory.001"); view.Click("detail:0");
        view.Click("aircraft.product.open"); view.Click("aircraft.product.option:0"); view.Click("aircraft.purchase");
        view.TypeInto("aircraft.parts-target", "-1");
        Assert.That(view.Enabled["aircraft.parts.apply"], Is.False);
        var targets = view.Get<Dictionary<string, int>>("_airframeSparePartStockTargets");
        Assert.That(targets, Is.Empty);
        view.TypeInto("aircraft.parts-target", "150000"); view.Click("aircraft.parts.apply");
        Assert.That(targets["interaction.aircraft"], Is.EqualTo(150000));
        view.TypeInto("aircraft.parts-target", "2147483648");
        Assert.That(view.Enabled["aircraft.parts.apply"], Is.False);
        Assert.That(targets["interaction.aircraft"], Is.EqualTo(150000));
    }

    [Test]
    public void AircraftStockLimitAlsoCapsAssemblyEngineDemandAcrossTwoPlants()
    {
        using var view = new View(1280, sites: true, approvedDesigns: true);
        for (var month = 0; month < 18; month++) view.Aircraft.AdvanceMonth();
        var item = view.FixtureAircraft!;
        foreach (var site in view.Aircraft.Factories)
        {
            Assert.That(view.Aircraft.TryPurchaseAndInstallEquipment(site.FactoryId, item.DesignId,
                WrightTycoon.Core.Balance.FactoryAutomationTier.HandBuilt, item.Aircraft.Performance.TotalProductionManDays,
                double.MaxValue, out _, out _), Is.True);
            Assert.That(view.Aircraft.TrySetMonthlyProductionTargetUnits(site.FactoryId, 3, out _, out _), Is.True);
            view.Aircraft.SetPartsAllocationPercent(site.FactoryId, 100);
        }
        var method = typeof(WrightTycoonLaunchRunner).GetMethod("BuildEngineManufacturingDemand", BindingFlags.Instance | BindingFlags.NonPublic)!;
        int RequestedEngines()
        {
            var total = 0;
            foreach (var request in (IEnumerable<EngineManufacturingDemand>)method.Invoke(view.Runner, null)!) total += request.Engines;
            return total;
        }
        view.Aircraft.SetAircraftStockTarget(item.DesignId, 2);
        Assert.That(RequestedEngines(), Is.EqualTo(2));
        view.Aircraft.AddWarehouseUnits(item.DesignId, 2);
        Assert.That(RequestedEngines(), Is.Zero);
        view.Aircraft.SetAircraftStockTarget(item.DesignId, 0);
        Assert.That(RequestedEngines(), Is.Zero);
        view.Aircraft.ClearAircraftStockTarget(item.DesignId);
        Assert.That(RequestedEngines(), Is.EqualTo(6));
    }

    [Test]
    public void AircraftStockLimitUsesTypedInputAppliesZeroAndCanReturnToUnlimited()
    {
        using var view = new View(1280, sites: true, approvedDesigns: true);
        view.Click("nav:0"); view.Click("plant:aircraft:factory.001"); view.Click("detail:0");
        view.Click("aircraft.product.open"); view.Click("aircraft.product.option:0"); view.Click("aircraft.purchase");
        const string design = "interaction.aircraft";
        Assert.That(view.Aircraft.GetAircraftStockTarget(design), Is.Null);
        Assert.That(view.Enabled["aircraft.stock.clear"], Is.False);
        view.TypeInto("aircraft.stock-target", "-1");
        Assert.That(view.Enabled["aircraft.stock.apply"], Is.False);
        Assert.That(view.Aircraft.GetAircraftStockTarget(design), Is.Null);
        view.TypeInto("aircraft.stock-target", "25000"); view.Click("aircraft.stock.apply");
        Assert.That(view.Aircraft.GetAircraftStockTarget(design), Is.EqualTo(25000));
        view.TypeInto("aircraft.stock-target", "2147483648");
        Assert.That(view.Enabled["aircraft.stock.apply"], Is.False);
        Assert.That(view.Aircraft.GetAircraftStockTarget(design), Is.EqualTo(25000));
        view.TypeInto("aircraft.stock-target", "0"); view.Click("aircraft.stock.apply");
        Assert.That(view.Aircraft.GetAircraftStockTarget(design), Is.Zero);
        view.Click("aircraft.stock.clear");
        Assert.That(view.Aircraft.GetAircraftStockTarget(design), Is.Null);
        Assert.That(view.Aircraft.Lines[0].CarryOverManDays, Is.Zero);
    }

    [Test]
    public void BothFactoryPartsShareSlidersUseRealMouseEventsAndPersistEndpoints()
    {
        using var view = new View(1280, sites: true, approvedDesigns: true);
        view.Click("nav:0"); view.Click("plant:aircraft:factory.001"); view.Click("detail:0");
        view.Click("aircraft.product.open"); view.Click("aircraft.product.option:0"); view.Click("aircraft.purchase");
        Assert.That(view.Aircraft.TrySetMonthlyProductionTargetUnits("factory.001", 1, out _, out _), Is.True);
        view.SlideTo("aircraft.parts-share", true);
        Assert.That(view.Aircraft.Factories[0].PartsAllocationPercent, Is.EqualTo(100));
        Assert.That(view.Aircraft.Factories[0].Line!.MonthlyProductionTargetUnits, Is.EqualTo(1),
            "Changing the parts share must not erase the player's aircraft target.");
        view.SlideTo("aircraft.parts-share", false);
        Assert.That(view.Aircraft.Factories[0].PartsAllocationPercent, Is.EqualTo(1));
        view.Click("nav:1"); view.Click("plant:engine:engine-plant.001"); view.Click("detail:0");
        view.SlideTo("engine.parts-share", true);
        Assert.That(view.Engines.Sites[0].PartsAllocationPercent, Is.EqualTo(100));
        view.SlideTo("engine.parts-share", false);
        Assert.That(view.Engines.Sites[0].PartsAllocationPercent, Is.EqualTo(1));
        view.Click("nav:0"); view.Click("plant:aircraft:factory.001"); view.Click("detail:0");
        Assert.That(view.Aircraft.Factories[0].PartsAllocationPercent, Is.EqualTo(1));
    }

    [Test]
    [Category("Extended")]
    public void EngineStockTargetsAreTypedWithoutOldSliderCapsAndInvalidInputCannotApply()
    {
        using var view = new View(1280, sites: true, approvedDesigns: true);
        view.Click("nav:1"); view.Click("plant:engine:engine-plant.001"); view.Click("detail:0");
        view.TypeInto("engine.stock-target", "-1");
        Assert.That(view.Enabled["engine.apply"], Is.False);
        Assert.That(view.Engines.Sites[0].MonthlyEngineTarget, Is.Zero);
        view.TypeInto("engine.stock-target", "25000");
        view.TypeInto("engine.parts-target", "150000");
        Assert.That(view.Enabled["engine.apply"], Is.True);
        view.Click("engine.apply");
        Assert.That(view.Engines.Sites[0].MonthlyEngineTarget, Is.EqualTo(25000));
        Assert.That(view.Engines.Sites[0].PartReserve, Is.EqualTo(150000));
        view.Render();
        Assert.That(view.Engines.Sites[0].MonthlyEngineTarget, Is.EqualTo(25000));
    }

    [TestCase(false, 1280)]
    [TestCase(true, 1280)]
    [TestCase(false, 1920)]
    [TestCase(true, 1920)]
    [Category("Extended")]
    public void AutomationChoicesDisappearAfterApprovalAndStayHiddenWhenTargetsChange(bool engine, int width)
    {
        using var view = new View(width, sites: true, approvedDesigns: true);
        var prefix = engine ? "engine" : "aircraft";
        var factoryKey = engine ? "plant:engine:engine-plant.001" : "plant:aircraft:factory.001";
        view.Click(engine ? "nav:1" : "nav:0"); view.Click(factoryKey); view.Click("detail:0");
        for (var tier = 0; tier < 5; tier++) Assert.That(view.Rects.ContainsKey(prefix + ".automation:" + tier), Is.True);
        view.Click(prefix + ".automation:2");
        view.Click(engine ? "engine.apply" : "aircraft.purchase");
        var paidCash = view.Accounting.Cash;
        Assert.That(view.Rects.ContainsKey(prefix + ".automation.current"), Is.True);
        for (var tier = 0; tier < 5; tier++) Assert.That(view.Rects.ContainsKey(prefix + ".automation:" + tier), Is.False);
        if (engine)
        {
            Assert.That(view.Engines.Sites[0].AutomationTier, Is.EqualTo(WrightTycoon.Core.Balance.FactoryAutomationTier.MechanizedCells));
            for (var month = 0; month < 4; month++) view.Engines.AdvanceMonth();
            view.TypeInto("engine.stock-target", "25"); view.TypeInto("engine.parts-target", "100"); view.Click("engine.apply");
            Assert.That(view.Engines.Sites[0].MonthlyEngineTarget, Is.EqualTo(25));
            Assert.That(view.Engines.Sites[0].PartReserve, Is.EqualTo(100));
            view.Engines.RestoreSnapshot(view.Engines.CreateSnapshot());
        }
        else
        {
            Assert.That(view.Aircraft.GetEquipmentForFactory("factory.001")!.AutomationTier, Is.EqualTo(WrightTycoon.Core.Balance.FactoryAutomationTier.MechanizedCells));
            view.TypeInto("aircraft.stock-target", "25"); view.Click("aircraft.stock.apply");
            view.TypeInto("aircraft.parts-target", "100"); view.Click("aircraft.parts.apply");
            Assert.That(view.Aircraft.GetAircraftStockTarget("interaction.aircraft"), Is.EqualTo(25));
            Assert.That(view.Get<Dictionary<string, int>>("_airframeSparePartStockTargets")["interaction.aircraft"], Is.EqualTo(100));
            view.Aircraft.RestoreSnapshot(view.Aircraft.CreateSnapshot());
        }
        Assert.That(view.Accounting.Cash, Is.EqualTo(paidCash), "Changing goals does not buy equipment again.");
        view.Click("nav:2"); view.Click(engine ? "nav:1" : "nav:0"); view.Click(factoryKey); view.Click("detail:0");
        Assert.That(view.Rects.ContainsKey(prefix + ".automation.current"), Is.True);
        for (var tier = 0; tier < 5; tier++) Assert.That(view.Rects.ContainsKey(prefix + ".automation:" + tier), Is.False);
    }

    [Test]
    public void ApprovedEngine_ApplyEquipmentButtonInstallsAndBillsOnlyChangedEquipment()
    {
        using var view = new View(1280, sites: true, approvedDesigns: true);
        view.Click("nav:1"); view.Click("plant:engine:engine-plant.001"); view.Click("detail:0");
        Assert.That(view.Rects.ContainsKey("engine.product.open"), Is.True);
        Assert.That(view.Rects.ContainsKey("engine.output-preview"), Is.True, "Output must be visible before buying equipment.");
        Assert.That(view.Engines.Sites[0].EquipmentCost, Is.Zero);
        view.Click("engine.product.open"); view.Click("engine.product.option:0");
        view.Click("engine.apply");
        var plant = view.Engines.Sites[0];
        Assert.That(plant.EngineDesignId, Is.EqualTo("interaction-engine"));
        Assert.That(plant.IsReady, Is.True);
        Assert.That(plant.EquipmentCost, Is.GreaterThan(0));
        var paid = plant.EquipmentCost;
        Assert.That(view.Accounting.Cash, Is.EqualTo(10_000_000_000d - paid));
        view.Click("engine.apply");
        Assert.That(view.Accounting.Cash, Is.EqualTo(10_000_000_000d - paid));
        Assert.That(view.Accounting.GetTransactions().Count, Is.EqualTo(1));
    }

    [Test]
    public void AircraftReplenishmentUsesOnlyTypedStockGoalAndRequestsEnginesDespiteOldZeroMonthlyTarget()
    {
        using var view = new View(1280, sites: true, approvedDesigns: true);
        view.Click("nav:0"); view.Click("plant:aircraft:factory.001"); view.Click("detail:0");
        view.Click("aircraft.product.open"); view.Click("aircraft.product.option:0");
        view.Click("aircraft.purchase");
        view.Aircraft.AutomaticStockReplenishment = true;
        view.TypeInto("aircraft.stock-target", "4"); view.Click("aircraft.stock.apply");
        Assert.That(view.Aircraft.GetAircraftStockTarget(view.FixtureAircraft!.DesignId), Is.EqualTo(4));
        Assert.That(view.Aircraft.Factories[0].Line!.MonthlyProductionTargetUnits, Is.Zero);
        view.Set("_separateEngineProduction", true);
        var configure = typeof(WrightTycoonLaunchRunner).GetMethod("ConfigureAircraftEngineSupply", BindingFlags.NonPublic | BindingFlags.Instance)!;
        configure.Invoke(view.Runner, null);
        var demand = (IEnumerable<EngineManufacturingDemand>)typeof(WrightTycoonLaunchRunner)
            .GetMethod("BuildEngineManufacturingDemand", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(view.Runner, null)!;
        Assert.That(demand.Where(item => item.ForAircraftProduction).Sum(item => item.Engines),
            Is.EqualTo(4 * view.FixtureAircraft.Aircraft.Airframe.Parameters.EngineCount));
        view.TypeInto("aircraft.stock-target", "0"); view.Click("aircraft.stock.apply");
        Assert.That(view.Aircraft.GetRequestedAircraftUnits("factory.001"), Is.Zero);
        Assert.That(view.Enabled["aircraft.store"], Is.True);
    }

    [Test]
    public void MotorEquipmentCanBeStoredReinstalledAndSoldThroughDepot()
    {
        using var view = new View(1280, sites: true, approvedDesigns: true);
        view.Click("nav:1"); view.Click("plant:engine:engine-plant.001"); view.Click("detail:0");
        view.Click("engine.apply");
        Assert.That(view.Engines.Sites[0].EquipmentCost, Is.EqualTo(1_000_000d));
        view.Click("engine.store");
        Assert.That(view.Engines.StoredEquipment.Count, Is.EqualTo(1));
        Assert.That(view.Engines.Sites[0].EngineDesignId, Is.Empty);
        view.Click("nav:3");
        var equipment = view.Engines.StoredEquipment[0];
        view.Click("depot.motor.install:" + equipment.EquipmentId + ":engine-plant.001");
        Assert.That(view.Engines.StoredEquipment.Count, Is.Zero);
        Assert.That(view.Engines.Sites[0].EngineDesignId, Is.EqualTo("interaction-engine"));
        view.Engines.AdvanceMonth();
        view.Click("nav:1"); view.Click("plant:engine:engine-plant.001"); view.Click("detail:0");
        view.Click("engine.store"); view.Click("nav:3");
        view.Click("depot.motor.sell:" + view.Engines.StoredEquipment[0].EquipmentId);
        Assert.That(view.Engines.StoredEquipment.Count, Is.Zero);
        Assert.That(view.Accounting.Cash, Is.EqualTo(10_000_000_000d - 1_000_000d - 30_000d + 150_000d));
    }

    [Test]
    public void ApprovedAircraft_PurchaseStoreReinstallAndSellDepotEquipmentUseRealMouseActions()
    {
        using var view = new View(1280, sites: true, approvedDesigns: true);
        view.Click("nav:0"); view.Click("plant:aircraft:factory.001"); view.Click("detail:0");
        view.Click("aircraft.product.open"); view.Click("aircraft.product.option:0");
        view.Click("aircraft.purchase");
        var equipment = view.Aircraft.GetEquipmentForFactory("factory.001");
        Assert.That(equipment, Is.Not.Null); Assert.That(equipment!.IsReady, Is.True);
        var purchase = equipment.PurchaseCost;
        Assert.That(view.Accounting.Cash, Is.EqualTo(10_000_000_000d - purchase));
        view.Click("aircraft.details");
        Assert.That(view.Get<HashSet<string>>("_expandedFactoryProductionDetails").Contains("factory.001"), Is.True);
        view.Click("aircraft.details");
        Assert.That(view.Get<HashSet<string>>("_expandedFactoryProductionDetails").Contains("factory.001"), Is.False);
        view.Click("aircraft.store");
        Assert.That(equipment.IsInstalled, Is.False);
        Assert.That(view.Get<int>("_factoryPlannerTab"), Is.EqualTo(3));
        view.Click("depot.target:" + equipment.EquipmentId + ".open");
        view.Click("depot.target:" + equipment.EquipmentId + ".option:0");
        view.Click("depot.install:" + equipment.EquipmentId);
        Assert.That(equipment.IsInstalled, Is.True);
        Assert.That(equipment.InstallationMonthsRemaining, Is.EqualTo(1));
        view.Aircraft.AdvanceMonth();
        view.Click("nav:0"); view.Click("plant:aircraft:factory.001"); view.Click("detail:0");
        view.Click("aircraft.store"); view.Click("depot.sell:" + equipment.EquipmentId);
        Assert.That(view.Aircraft.Equipment.Count, Is.Zero);
        Assert.That(view.Accounting.Cash, Is.EqualTo(10_000_000_000d - purchase - purchase * .03d + purchase * .15d).Within(.00001));
        Assert.That(view.Accounting.GetTransactions().Count, Is.EqualTo(3));
    }
}
