using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;
using WrightTycoon.Core.Aircraft;
using WrightTycoon.Core.Airframes;
using WrightTycoon.Core.Data;
using WrightTycoon.Core.Engines;
using WrightTycoon.Core.Factories;
using WrightTycoon.Core.Physics;
using WrightTycoon.Core.Research;
using WrightTycoon.Core.Simulation;
using WrightTycoon.Game.Launch;
using AircraftModel = WrightTycoon.Core.Aircraft.Aircraft;

namespace WrightTycoon.Game.Tests;

/// <summary>Runtime wiring tests use only in-memory designs and payloads, never player save files.</summary>
public sealed class ResearchBonusRuntimeTests
{
    private sealed class Fixture : IDisposable
    {
        public const string EngineId = "research.engine";
        public const string FamilyId = "research.family";
        public const string TypeId = "research.type";
        private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly Type RunnerType = typeof(WrightTycoonLaunchRunner);
        private readonly GameObject _object;
        public readonly WrightTycoonLaunchRunner Runner;
        public readonly object SavedEngine;
        public readonly AircraftModel RawAircraft;

        public Fixture(string savedEngineId = EngineId)
        {
            _object = new GameObject("ResearchBonusRuntimeTest");
            _object.SetActive(false);
            Runner = _object.AddComponent<WrightTycoonLaunchRunner>();
            var atmosphere = new IsaAtmosphereModel();
            var aerodynamics = new AerodynamicsCalculator();
            var builder = new EngineBuilder(new EngineCalculator(), atmosphere);
            var engineDefinition = new EngineDefinition { Id = EngineId, DisplayName = "Research Engine", EngineType = EngineType.Radial };
            var airframeDefinition = new AirframeDefinition { Id = "research.airframe", DisplayName = "Research Airframe" };
            var engines = new EngineDefinitionRepository(new[] { engineDefinition });
            var airframes = new AirframeDefinitionRepository(new[] { airframeDefinition });
            var factory = new AircraftFactory(builder,
                new AirframeBuilder(new AirframeCalculator(aerodynamics, atmosphere)),
                new AircraftPerformanceCalculator(atmosphere, aerodynamics),
                new DevelopmentCostCalculator(), engines, airframes);
            var family = new AircraftFamilyDefinition
            {
                Id = FamilyId, DisplayName = "Research Family", EngineDefinitionId = EngineId,
                AirframeDefinitionId = airframeDefinition.Id, LockedEngineType = EngineType.Radial,
                LockedEngineCount = 1,
                BaseEngineParameters = new EngineParameters
                { CylinderCount = 9, DisplacementLiters = 12, CompressionRatio = 8, CoolingEfficiency = .8 },
                BaseAirframeParameters = new AirframeParameters
                { EngineCount = 1, WingAreaSquareMeters = 24, FuelAmountLiters = 200 },
            };
            var type = new AircraftTypeDefinition { Id = TypeId, FamilyId = FamilyId, DisplayName = "Research Type" };
            RawAircraft = factory.CreateAircraft(family, type);
            Set("_engineBuilder", builder);
            Set("_engineRepo", engines);
            Set("_airframeRepo", airframes);
            Set("_aircraftFactory", factory);
            Set("_currentDate", new SimulationDate(1960, 1));
            var engineType = RunnerType.GetNestedType("SavedEngineDesign", BindingFlags.NonPublic)!;
            var engineConstructor = engineType.GetConstructor(new[] { typeof(string), typeof(string), typeof(Engine),
                typeof(int), typeof(int), typeof(int?), typeof(string), typeof(int?), typeof(int?), typeof(int?) })!;
            SavedEngine = engineConstructor.Invoke(new object?[]
            { "Research Engine", savedEngineId, RawAircraft.Engine, 1946, 1946, null, "", 1946, 1946, 1 });
            Get<IList>("_savedEngines").Add(SavedEngine);
            var catalog = new FactoryAircraftTypeCatalogItem("research.catalog", "Research Aircraft", RawAircraft);
            var savedType = RunnerType.GetNestedType("SavedAircraftDesign", BindingFlags.NonPublic)!;
            var savedAircraft = savedType.GetConstructors()[0].Invoke(new object?[]
            { "Research Aircraft", catalog, savedEngineId, FamilyId, TypeId, "", 1946, 1946, null, "", 1946, 1946, 1, false });
            Get<IList>("_savedAircraftDesigns").Add(savedAircraft);
        }

        public T Get<T>(string name) => (T)RunnerType.GetField(name, PrivateInstance)!.GetValue(Runner)!;
        public void Set(string name, object value) => RunnerType.GetField(name, PrivateInstance)!.SetValue(Runner, value);
        public object Call(string name, params object?[] args) => RunnerType.GetMethod(name, PrivateInstance)!.Invoke(Runner, args)!;
        public object SavedAircraft => Get<IList>("_savedAircraftDesigns")[0]!;
        public AircraftModel CatalogAircraft => ((FactoryAircraftTypeCatalogItem)SavedAircraft.GetType()
            .GetProperty("CatalogItem")!.GetValue(SavedAircraft)!).Aircraft;
        public void Points(ResearchSubjectType subject, string id, int rel, int prod, int fast = 0) =>
            Get<Dictionary<string, TypeResearchModifierPoints>>("_researchModifiersByResearchId")
                [ResearchBoard.BuildResearchId(subject, id)] = new TypeResearchModifierPoints
                { ReliabilityPoints = rel, ProductionOptimizationPoints = prod, FastDevelopmentPoints = fast };
        public void Rebuild(ResearchSubjectType subject, string id) => Call("RebuildResearchDependentCatalogs", subject, id);
        public AircraftModel Build(TypeResearchModifierPoints? family = null, TypeResearchModifierPoints? type = null) =>
            (AircraftModel)Call("BuildResearchAircraft", SavedAircraft, family, type);
        public void Dispose() => UnityEngine.Object.DestroyImmediate(_object);
    }

    [Test]
    public void MandatoryMaintenanceSellingPricesRemainFixedWhenProductionResearchImproves()
    {
        using var fixture = new Fixture();
        var before = (WrightTycoon.Core.Fleet.ServiceOfferSnapshot)fixture.Call("BuildCombinedMaintenanceOffer", fixture.SavedAircraft, null, null);
        fixture.Points(ResearchSubjectType.Engine, Fixture.EngineId, 0, 10);
        fixture.Points(ResearchSubjectType.AircraftFamily, Fixture.FamilyId, 0, 10);
        fixture.Points(ResearchSubjectType.AircraftType, Fixture.TypeId, 0, 10);
        fixture.Rebuild(ResearchSubjectType.Engine, Fixture.EngineId);
        var after = (WrightTycoon.Core.Fleet.ServiceOfferSnapshot)fixture.Call("BuildCombinedMaintenanceOffer", fixture.SavedAircraft, null, null);
        Assert.That(after.SparePartUnitPrice, Is.EqualTo(before.SparePartUnitPrice).Within(1e-8));
        Assert.That(after.EngineMaintenance!.PartUnitPrice, Is.EqualTo(before.EngineMaintenance!.PartUnitPrice).Within(1e-8));
        Assert.That(after.EngineMaintenance.ReplacementUnitPrice, Is.EqualTo(before.EngineMaintenance.ReplacementUnitPrice).Within(1e-8));
        Assert.That(after.EngineMaintenance.EngineDesignId, Is.EqualTo(before.EngineMaintenance.EngineDesignId));
    }

    [Test]
    public void DeliveryWithoutExplicitOfferUsesSavedExactEngineAndSharedAirframeIdentity()
    {
        using var fixture = new Fixture("research.exact-engine-version");
        var item = (FactoryAircraftTypeCatalogItem)fixture.SavedAircraft.GetType().GetProperty("CatalogItem")!.GetValue(fixture.SavedAircraft)!;
        fixture.Call("RegisterFleetDelivery", "fallback-delivery", item, "fallback-contract", 1, 1000d,
            new WrightTycoon.Core.Fleet.OperatingProfile("piston", 100, 2), null, null);
        var fleet = fixture.Get<WrightTycoon.Core.Fleet.FleetRegistry>("_fleet");
        Assert.That(fleet.Cohorts.Count, Is.EqualTo(1));
        var offer = fleet.Cohorts[0].Offer;
        Assert.That(offer.EngineMaintenance!.EngineDesignId, Is.EqualTo("research.exact-engine-version"));
        Assert.That(offer.EngineMaintenance.EngineDesignId, Is.Not.EqualTo(item.Aircraft.Engine.Definition.Id));
        Assert.That(offer.AirframePartsGroupId, Is.EqualTo(item.DesignId));
        offer.Validate();
    }

    [Test]
    public void SavedEngineResearch_IsIdenticalInStandaloneAndInstalledFrozenReports()
    {
        using var fixture = new Fixture();
        var baseline = (Engine)fixture.Call("BuildSavedEngineAtReferenceDate", fixture.SavedEngine);
        fixture.Points(ResearchSubjectType.Engine, Fixture.EngineId, 10, 10);
        var final = (Engine)fixture.Call("BuildSavedEngineAtReferenceDate", fixture.SavedEngine);
        var installed = fixture.Build();
        Assert.That(final.Performance.Reliability, Is.EqualTo(baseline.Performance.Reliability + .2).Within(1e-9));
        Assert.That(final.Performance.Cost, Is.EqualTo(baseline.Performance.Cost * .9).Within(1e-8));
        Assert.That(final.Performance.ProductionManDays, Is.EqualTo(baseline.Performance.ProductionManDays * .8).Within(1e-8));
        Assert.That(installed.Engine.Performance.Reliability, Is.EqualTo(final.Performance.Reliability));
        Assert.That(installed.Engine.Performance.Cost, Is.EqualTo(final.Performance.Cost));
        Assert.That(installed.Engine.Performance.ProductionManDays, Is.EqualTo(final.Performance.ProductionManDays));
        fixture.Set("_currentDate", new SimulationDate(1980, 1));
        var future = (Engine)fixture.Call("BuildSavedEngineAtReferenceDate", fixture.SavedEngine);
        Assert.That(future.Performance.Cost, Is.EqualTo(final.Performance.Cost));
        Assert.That(future.Performance.Reliability, Is.EqualTo(final.Performance.Reliability));
    }

    [Test]
    public void CatalogRebuild_ResolvesThreeSourcesAndDoesNotAmplifyRepeatedEffects()
    {
        using var fixture = new Fixture();
        fixture.Points(ResearchSubjectType.Engine, Fixture.EngineId, 5, 5);
        var engineOnly = fixture.Build();
        fixture.Points(ResearchSubjectType.AircraftFamily, Fixture.FamilyId, 10, 10);
        fixture.Points(ResearchSubjectType.AircraftType, Fixture.TypeId, 10, 10);
        fixture.Rebuild(ResearchSubjectType.Engine, Fixture.EngineId);
        var result = fixture.CatalogAircraft;
        Assert.That(result.Performance.OverallReliability,
            Is.EqualTo(engineOnly.Performance.OverallReliability + .4).Within(1e-9));
        Assert.That(result.Performance.AirframeProductionCost,
            Is.EqualTo(engineOnly.Performance.AirframeProductionCost * .8).Within(1e-8));
        Assert.That(result.Performance.AirframeProductionManDays,
            Is.EqualTo(engineOnly.Performance.AirframeProductionManDays * .6).Within(1e-8));
        Assert.That(result.Performance.EnginesProductionCost, Is.EqualTo(engineOnly.Performance.EnginesProductionCost));
        for (var index = 0; index < 3; index++)
        {
            fixture.Rebuild(ResearchSubjectType.AircraftFamily, Fixture.FamilyId);
            fixture.Rebuild(ResearchSubjectType.AircraftType, Fixture.TypeId);
            Assert.That(fixture.CatalogAircraft.Performance.TotalProductionCost, Is.EqualTo(result.Performance.TotalProductionCost));
            Assert.That(fixture.CatalogAircraft.Performance.OverallReliability, Is.EqualTo(result.Performance.OverallReliability));
        }
        Assert.That(result.Performance.CostBreakdown!.Total, Is.EqualTo(result.Performance.TotalProductionCost).Within(1e-7));
        Assert.That(result.Performance.ProductionManDaysBreakdown!.Total,
            Is.EqualTo(result.Performance.TotalProductionManDays).Within(1e-7));
    }

    [Test]
    public void NewEngineTemplateDoesNotInheritSourceResearchWhileEditingKeepsOwnPoints()
    {
        using var fixture = new Fixture();
        fixture.Set("_engineDefinitions", new List<EngineDefinition> { fixture.RawAircraft.Engine.Definition });
        fixture.Set("_engineParameters", fixture.RawAircraft.Engine.Parameters.Clone());
        fixture.Set("_selectedEngineDefinitionIndex", 0);
        fixture.Set("_selectedSavedEngineIndex", 0);
        fixture.Set("_engineDesignName", "Research Engine");
        fixture.Set("_editingEngineResearchId", string.Empty);
        fixture.Call("RebuildEnginePreview");
        var baseline = fixture.Get<Engine>("_enginePreview");
        Assert.That(baseline, Is.Not.Null, fixture.Get<string>("_enginePreviewError"));
        fixture.Points(ResearchSubjectType.Engine, Fixture.EngineId, 10, 10);
        fixture.Call("RebuildEnginePreview");
        var template = fixture.Get<Engine>("_enginePreview");
        Assert.That(template.Performance.Reliability, Is.EqualTo(baseline.Performance.Reliability));
        Assert.That(template.Performance.CompleteProductionCost, Is.EqualTo(baseline.Performance.CompleteProductionCost));
        fixture.Set("_editingEngineResearchId", Fixture.EngineId);
        fixture.Call("RebuildEnginePreview");
        var edited = fixture.Get<Engine>("_enginePreview");
        Assert.That(edited.Performance.Reliability, Is.EqualTo(baseline.Performance.Reliability + .2).Within(1e-8));
        Assert.That(edited.Performance.Cost, Is.EqualTo(baseline.Performance.Cost * .9).Within(1e-8));
    }

    [Test]
    public void ApprovalOverridePreview_MatchesFinalDictionaryAndCatalogBuild()
    {
        using var fixture = new Fixture();
        fixture.Points(ResearchSubjectType.Engine, Fixture.EngineId, 7, 6);
        fixture.Points(ResearchSubjectType.AircraftFamily, Fixture.FamilyId, 5, 4);
        var points = new TypeResearchModifierPoints { ReliabilityPoints = 8, ProductionOptimizationPoints = 9, FastDevelopmentPoints = 3 };
        var preview = fixture.Build(type: points);
        fixture.Points(ResearchSubjectType.AircraftType, Fixture.TypeId, 8, 9, 3);
        fixture.Rebuild(ResearchSubjectType.AircraftType, Fixture.TypeId);
        var approved = fixture.CatalogAircraft;
        Assert.That(approved.Performance.OverallReliability, Is.EqualTo(preview.Performance.OverallReliability));
        Assert.That(approved.Performance.ExpectedLifeHours, Is.EqualTo(preview.Performance.ExpectedLifeHours));
        Assert.That(approved.Performance.TotalProductionCost, Is.EqualTo(preview.Performance.TotalProductionCost));
        Assert.That(approved.Performance.TotalProductionManDays, Is.EqualTo(preview.Performance.TotalProductionManDays));
        Assert.That(approved.Performance.CompleteProductionCost, Is.EqualTo(preview.Performance.CompleteProductionCost));
    }

    [Test]
    public void ActualSavePayload_JsonRoundTripPreservesEveryResearchSourceAndEmbeddedContext()
    {
        using var fixture = new Fixture();
        fixture.Points(ResearchSubjectType.Engine, Fixture.EngineId, 7, 6, 5);
        fixture.Points(ResearchSubjectType.AircraftFamily, Fixture.FamilyId, 10, 9, 8);
        fixture.Points(ResearchSubjectType.AircraftType, Fixture.TypeId, 4, 3, 2);
        fixture.Rebuild(ResearchSubjectType.AircraftType, Fixture.TypeId);
        var payload = fixture.Call("serializeGameState");
        var json = JsonConvert.SerializeObject(payload);
        var restored = JsonConvert.DeserializeObject(json, payload.GetType())!;
        var entries = (IList)restored.GetType().GetProperty("ResearchModifiersByResearchId")!.GetValue(restored)!;
        Assert.That(entries.Count, Is.EqualTo(3));
        foreach (var entry in entries)
        {
            var type = entry!.GetType();
            var key = (string)type.GetProperty("ResearchId")!.GetValue(entry)!;
            var original = fixture.Get<Dictionary<string, TypeResearchModifierPoints>>("_researchModifiersByResearchId")[key];
            Assert.That(type.GetProperty("ReliabilityPoints")!.GetValue(entry), Is.EqualTo(original.ReliabilityPoints));
            Assert.That(type.GetProperty("ProductionOptimizationPoints")!.GetValue(entry), Is.EqualTo(original.ProductionOptimizationPoints));
            Assert.That(type.GetProperty("FastDevelopmentPoints")!.GetValue(entry), Is.EqualTo(original.FastDevelopmentPoints));
        }
        var designs = (IList)restored.GetType().GetProperty("SavedAircraftDesigns")!.GetValue(restored)!;
        var family = (AircraftFamilyDefinition)designs[0]!.GetType().GetProperty("FamilyDefinition")!.GetValue(designs[0])!;
        var aircraftType = (AircraftTypeDefinition)designs[0]!.GetType().GetProperty("TypeDefinition")!.GetValue(designs[0])!;
        Assert.That(family.ResearchModifiers.ReliabilityPoints, Is.EqualTo(10));
        Assert.That(family.InstalledEngineResearchModifiers.ReliabilityPoints, Is.EqualTo(7));
        Assert.That(aircraftType.ResearchModifiers.ReliabilityPoints, Is.EqualTo(4));
    }

    [TestCase(ResearchSubjectType.Engine)]
    [TestCase(ResearchSubjectType.AircraftFamily)]
    [TestCase(ResearchSubjectType.AircraftType)]
    public void ApprovalEffects_RenderRealPreviewAndMatchHighPointFinalReport(ResearchSubjectType subject)
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            Assert.Ignore("Approval IMGUI previews require a graphics-enabled Unity EditMode run.");
        using var fixture = new Fixture();
        fixture.Points(ResearchSubjectType.Engine, Fixture.EngineId, 5, 5);
        fixture.Points(ResearchSubjectType.AircraftFamily, Fixture.FamilyId, 5, 5);
        fixture.Points(ResearchSubjectType.AircraftType, Fixture.TypeId, 5, 5);
        fixture.Rebuild(ResearchSubjectType.AircraftType, Fixture.TypeId);
        var points = new TypeResearchModifierPoints { ReliabilityPoints = 10, ProductionOptimizationPoints = 10 };
        var effects = TypeResearchModifierCalculator.Calculate(points);
        var id = subject == ResearchSubjectType.Engine ? Fixture.EngineId
            : subject == ResearchSubjectType.AircraftFamily ? Fixture.FamilyId : Fixture.TypeId;
        var draw = subject == ResearchSubjectType.Engine ? "DrawEngineResearchModifierEffects"
            : subject == ResearchSubjectType.AircraftFamily ? "DrawFamilyResearchModifierEffects" : "DrawTypeResearchModifierEffects";
        var expectedEngine = subject == ResearchSubjectType.Engine
            ? (Engine)fixture.Call("BuildSavedEngineWithResearch", fixture.SavedEngine, points) : null;
        var expectedAircraft = subject == ResearchSubjectType.AircraftFamily ? fixture.Build(family: points)
            : subject == ResearchSubjectType.AircraftType ? fixture.Build(type: points) : null;
        var window = ScriptableObject.CreateInstance<FactoryPlannerGuiTestWindow>();
        window.position = new Rect(10, 10, 1100, 900);
        window.Draw = () => fixture.Call(draw, id, points, effects);
        try
        {
            window.ShowUtility();
            window.SendEvent(new Event { type = EventType.Layout });
            window.SendEvent(new Event { type = EventType.Repaint });
            Assert.That(window.Error, Is.Null, window.Error?.ToString());
            Assert.That(window.DrawCount, Is.GreaterThan(0));
            fixture.Points(subject, id, 10, 10);
            fixture.Rebuild(subject, id);
            if (expectedEngine is not null)
            {
                var final = (Engine)fixture.Call("BuildSavedEngineAtReferenceDate", fixture.SavedEngine);
                Assert.That(final.Performance.Reliability, Is.EqualTo(expectedEngine.Performance.Reliability));
                Assert.That(final.Performance.CompleteProductionCost, Is.EqualTo(expectedEngine.Performance.CompleteProductionCost));
                Assert.That(final.Performance.ProductionManDays, Is.EqualTo(expectedEngine.Performance.ProductionManDays));
            }
            else
            {
                var final = fixture.CatalogAircraft;
                Assert.That(final.Performance.OverallReliability, Is.EqualTo(expectedAircraft!.Performance.OverallReliability));
                Assert.That(final.Performance.ExpectedLifeHours, Is.EqualTo(expectedAircraft.Performance.ExpectedLifeHours));
                Assert.That(final.Performance.CompleteProductionCost, Is.EqualTo(expectedAircraft.Performance.CompleteProductionCost));
                Assert.That(final.Performance.TotalProductionManDays, Is.EqualTo(expectedAircraft.Performance.TotalProductionManDays));
            }
        }
        finally
        {
            window.Draw = null;
            window.Close();
        }
    }

    [TestCase(ResearchSubjectType.Engine)]
    [TestCase(ResearchSubjectType.AircraftFamily)]
    [TestCase(ResearchSubjectType.AircraftType)]
    public void RegisteringOptimizedFinalDesign_UsesSameOwnNeutralFrozenQuoteAsApproval(ResearchSubjectType subject)
    {
        using var fixture = new Fixture();
        var board = new ResearchBoard();
        fixture.Set("_researchBoard", board);
        fixture.Points(ResearchSubjectType.Engine, Fixture.EngineId, 7, 6, 5);
        fixture.Points(ResearchSubjectType.AircraftFamily, Fixture.FamilyId, 7, 6, 5);
        fixture.Points(ResearchSubjectType.AircraftType, Fixture.TypeId, 7, 6, 5);
        fixture.Rebuild(ResearchSubjectType.AircraftType, Fixture.TypeId);
        // The supplied reports already contain all technical effects. Registration
        // must remove only its own production optimization before quoting, and
        // must use the saved science date rather than the current campaign year.
        switch (subject)
        {
            case ResearchSubjectType.Engine:
                var finalEngine = (Engine)fixture.Call("BuildSavedEngineAtReferenceDate", fixture.SavedEngine);
                fixture.Call("RegisterEngineResearchProposal", fixture.SavedEngine, finalEngine);
                break;
            case ResearchSubjectType.AircraftFamily:
                fixture.Call("RegisterFamilyResearchProposal", Fixture.FamilyId, "Research Family", fixture.CatalogAircraft);
                break;
            case ResearchSubjectType.AircraftType:
                fixture.Call("RegisterTypeResearchProposal", Fixture.TypeId, "Research Type", fixture.CatalogAircraft);
                break;
            default:
                Assert.Fail("Unexpected research subject.");
                return;
        }
        var project = board.GetAllProjects()[0];
        var points = new TypeResearchModifierPoints
        { ReliabilityPoints = 7, ProductionOptimizationPoints = 6, FastDevelopmentPoints = 5 };
        object?[] arguments = { project, points, null, null, null };
        Assert.That(fixture.Call("TryBuildResearchQuote", arguments), Is.True);
        var canonical = (ResearchQuote)arguments[4]!;
        Assert.That(project.ResearchCost, Is.EqualTo(canonical.ResearchCost).Within(.001d),
            "Initial registration must not discount its own research budget or change frozen science date.");
        Assert.That(project.ResearchTimeMonths, Is.EqualTo(canonical.ResearchTimeMonths));
        Assert.That(project.ComplexityNormalized, Is.EqualTo(canonical.ComplexityNormalized).Within(1e-9));
    }
}
