using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using WrightTycoon.Core.Aircraft;
using WrightTycoon.Core.Airframes;
using WrightTycoon.Core.Campaign;
using WrightTycoon.Core.Data;
using WrightTycoon.Core.Engines;
using WrightTycoon.Core.Factories;
using WrightTycoon.Core.Physics;
using WrightTycoon.Core.Research;
using WrightTycoon.Core.Simulation;
using WrightTycoon.Game.Launch;
using AircraftModel = WrightTycoon.Core.Aircraft.Aircraft;

namespace WrightTycoon.Game.Tests;

/// <summary>Independent end-to-end checks. No real save files, scene or player data are touched.</summary>
public sealed class TechnologyFreezeRuntimeTests
{
    private sealed class Fixture : IDisposable
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly GameObject owner;
        public readonly WrightTycoonLaunchRunner Runner;
        public readonly AircraftModel Initial;
        public Fixture() : this(350d, EngineType.Radial) { }

        public Fixture(double designSpeed, EngineType architecture)
        {
            owner = new GameObject("TechnologyFreezeRuntimeTests");
            owner.SetActive(false);
            Runner = owner.AddComponent<WrightTycoonLaunchRunner>();
            var atmosphere = new IsaAtmosphereModel();
            var aerodynamics = new AerodynamicsCalculator();
            var engineDefinition = new EngineDefinition { Id = "freeze.engine", DisplayName = "Freeze engine", EngineType = architecture };
            var airframeDefinition = new AirframeDefinition { Id = "freeze.airframe", DisplayName = "Freeze airframe" };
            var engineRepository = new EngineDefinitionRepository(new[] { engineDefinition });
            var airframeRepository = new AirframeDefinitionRepository(new[] { airframeDefinition });
            var engineBuilder = new EngineBuilder(new EngineCalculator(), atmosphere);
            var factory = new AircraftFactory(engineBuilder,
                new AirframeBuilder(new AirframeCalculator(aerodynamics, atmosphere)),
                new AircraftPerformanceCalculator(atmosphere, aerodynamics),
                new DevelopmentCostCalculator(), engineRepository, airframeRepository);
            var family = new AircraftFamilyDefinition
            {
                Id = "freeze.family", DisplayName = "Freeze family", EngineDefinitionId = engineDefinition.Id,
                AirframeDefinitionId = airframeDefinition.Id, LockedEngineType = architecture,
                TechnologySnapshot = new AirframeTechnologySnapshot(1946, 2, 1.10, 1.10),
                InstalledEngineTechnologyModifiers = HistoricalCampaignTechnologyModifiers.Neutral,
                BaseEngineParameters = new EngineParameters
                { CylinderCount = 9, DisplacementLiters = 12, CompressionRatio = 8, CoolingEfficiency = .8,
                    TurbineDiameterMeters = 1.4, MaximumSpeed = architecture is EngineType.Turbojet or EngineType.Turbofan ? 45000 : 2500 },
                BaseAirframeParameters = new AirframeParameters
                { UseModularWingProfile = true, WingDesignSpeedKph = designSpeed, WingAreaSquareMeters = 24, FuelAmountLiters = 200 },
            };
            var type = new AircraftTypeDefinition
            {
                Id = "freeze.type", FamilyId = family.Id, DisplayName = "Freeze type", TechnologyModelVersion = 2,
                TechnologySnapshot = new AirframeTechnologySnapshot(1950, 3, 1.30, 1.30),
            };
            Initial = factory.CreateAircraft(family, type);
            Set("_engineBuilder", engineBuilder);
            Set("_engineRepo", engineRepository);
            Set("_airframeRepo", airframeRepository);
            Set("_aircraftFactory", factory);
            Set("_engineDefinitions", new List<EngineDefinition> { engineDefinition });
            Set("_airframeDefinitions", new List<AirframeDefinition> { airframeDefinition });
            Set("_productionFactory", new AircraftProductionFactory());
            Set("_currentDate", new SimulationDate(1960, 1));
            var engineType = typeof(WrightTycoonLaunchRunner).GetNestedType("SavedEngineDesign", BindingFlags.NonPublic)!;
            var engineConstructor = engineType.GetConstructor(new[] { typeof(string), typeof(string), typeof(Engine),
                typeof(int), typeof(int), typeof(int?), typeof(string), typeof(int?), typeof(int?), typeof(int?) })!;
            var engine = engineConstructor.Invoke(new object?[]
            { "Freeze engine", "freeze.engine", Initial.Engine, 1946, 1946, null, "", 1946, 1946, 1 });
            Get<IList>("_savedEngines").Add(engine);
            var savedType = typeof(WrightTycoonLaunchRunner).GetNestedType("SavedAircraftDesign", BindingFlags.NonPublic)!;
            var saved = savedType.GetConstructors()[0].Invoke(new object?[]
            { "Freeze type", new FactoryAircraftTypeCatalogItem("freeze.catalog", "Freeze type", Initial),
                "freeze.engine", "freeze.family", "freeze.type", "", 1946, 1950, null, "", 1950, 1950, 3, false });
            Get<IList>("_savedAircraftDesigns").Add(saved);
        }
        public T Get<T>(string field) => (T)typeof(WrightTycoonLaunchRunner).GetField(field, Flags)!.GetValue(Runner)!;
        public void Set(string field, object value) => typeof(WrightTycoonLaunchRunner).GetField(field, Flags)!.SetValue(Runner, value);
        public object Call(string method, params object?[] args) => typeof(WrightTycoonLaunchRunner).GetMethod(method, Flags)!.Invoke(Runner, args)!;
        public object Saved => Get<IList>("_savedAircraftDesigns")[0]!;
        public AircraftModel Build() => (AircraftModel)Call("BuildResearchAircraft", Saved, null, null);
        public void Dispose() => UnityEngine.Object.DestroyImmediate(owner);
    }

    [Test]
    [Category("Extended")]
    public void NewFamilyImprovesSameTypeWithoutChangingEngineAcrossProfileAndEngineGrid(
        [Values(100d, 750d, 2000d)] double speed,
        [Values(EngineType.Radial, EngineType.Turbojet, EngineType.Turbofan)] EngineType architecture)
    {
        using var f = new Fixture(speed, architecture);
        var factory = f.Get<AircraftFactory>("_aircraftFactory");
        var old = factory.CreateAircraft(f.Initial.FamilyDefinition.WithTechnologySnapshot(
            new AirframeTechnologySnapshot(1946, 2, .8, .8)), f.Initial.TypeDefinition);
        var modern = factory.CreateAircraft(f.Initial.FamilyDefinition.WithTechnologySnapshot(
            new AirframeTechnologySnapshot(1950, 2, 1.2, 1.2)), f.Initial.TypeDefinition);
        Assert.That(modern.Airframe.Performance.TotalWeight.Kilograms, Is.LessThan(old.Airframe.Performance.TotalWeight.Kilograms));
        Assert.That(modern.Airframe.Performance.LiftCoefficient, Is.GreaterThan(old.Airframe.Performance.LiftCoefficient));
        Assert.That(modern.Airframe.Performance.DragCoefficient, Is.LessThanOrEqualTo(old.Airframe.Performance.DragCoefficient));
        Assert.That(modern.Airframe.Performance.WingProfileDragCoefficient, Is.LessThan(old.Airframe.Performance.WingProfileDragCoefficient));
        Assert.That(modern.Performance.AirframeProductionCost, Is.LessThan(old.Performance.AirframeProductionCost));
        Assert.That(modern.Performance.AirframeProductionManDays, Is.LessThan(old.Performance.AirframeProductionManDays));
        Assert.That(modern.Engine.Performance.Cost, Is.EqualTo(old.Engine.Performance.Cost));
        Assert.That(modern.Engine.Performance.Weight, Is.EqualTo(old.Engine.Performance.Weight));
        Assert.That(modern.Performance.EnginesProductionCost, Is.EqualTo(old.Performance.EnginesProductionCost));
        Assert.That(modern.Performance.CostBreakdown!.Total, Is.EqualTo(modern.Performance.TotalProductionCost).Within(1e-7));
        Assert.That(modern.Performance.ProductionManDaysBreakdown!.Total, Is.EqualTo(modern.Performance.TotalProductionManDays).Within(1e-7));
    }

    [Test]
    public void DatedBuildersCannotOverwriteStoredFamilyOrTypeTechnology()
    {
        using var f = new Fixture();
        var first = (AircraftModel)f.Call("BuildAircraftForDate", f.Initial.FamilyDefinition,
            f.Initial.TypeDefinition, new SimulationDate(1946, 1));
        var future = (AircraftModel)f.Call("BuildAircraftForDate", f.Initial.FamilyDefinition,
            f.Initial.TypeDefinition, new SimulationDate(2000, 1));
        Assert.That(future.Airframe.Performance.TotalWeight, Is.EqualTo(first.Airframe.Performance.TotalWeight));
        Assert.That(future.Airframe.Performance.DragCoefficient, Is.EqualTo(first.Airframe.Performance.DragCoefficient));
        Assert.That(future.Airframe.Performance.WingProfileDragCoefficient, Is.EqualTo(first.Airframe.Performance.WingProfileDragCoefficient));
        Assert.That(future.Airframe.Performance.LiftCoefficient, Is.EqualTo(first.Airframe.Performance.LiftCoefficient));
        Assert.That(future.Performance.EmptyAircraftWeightKilograms, Is.EqualTo(first.Performance.EmptyAircraftWeightKilograms));
        Assert.That(future.Performance.TotalProductionCost, Is.EqualTo(first.Performance.TotalProductionCost));
        Assert.That(future.Engine.Performance.Cost, Is.EqualTo(first.Engine.Performance.Cost));
    }

    [Test]
    public void RuntimeResearchRebuildPreservesSnapshotAndFullAdditiveResearchBonuses()
    {
        using var f = new Fixture();
        var baseline = f.Build();
        var points = f.Get<Dictionary<string, TypeResearchModifierPoints>>("_researchModifiersByResearchId");
        points[ResearchBoard.BuildResearchId(ResearchSubjectType.AircraftFamily, "freeze.family")] =
            new TypeResearchModifierPoints { ReliabilityPoints = 10, ProductionOptimizationPoints = 10 };
        points[ResearchBoard.BuildResearchId(ResearchSubjectType.AircraftType, "freeze.type")] =
            new TypeResearchModifierPoints { ReliabilityPoints = 10, ProductionOptimizationPoints = 10 };
        for (var iteration = 0; iteration < 3; iteration++)
        {
            f.Call("RebuildResearchDependentCatalogs", ResearchSubjectType.AircraftType, "freeze.type");
            var saved = f.Saved;
            var aircraft = ((FactoryAircraftTypeCatalogItem)saved.GetType().GetProperty("CatalogItem")!.GetValue(saved)!).Aircraft;
            Assert.That(aircraft.FamilyDefinition.TechnologySnapshot, Is.EqualTo(f.Initial.FamilyDefinition.TechnologySnapshot));
            Assert.That(aircraft.TypeDefinition.TechnologySnapshot, Is.EqualTo(f.Initial.TypeDefinition.TechnologySnapshot));
            Assert.That(aircraft.TypeDefinition.TechnologyModelVersion, Is.EqualTo(2));
            Assert.That(aircraft.Performance.OverallReliability, Is.EqualTo(baseline.Performance.OverallReliability + .4).Within(1e-9));
            Assert.That(aircraft.Performance.AirframeProductionCost, Is.EqualTo(baseline.Performance.AirframeProductionCost * .8).Within(1e-7));
            Assert.That(aircraft.Performance.AirframeProductionManDays, Is.EqualTo(baseline.Performance.AirframeProductionManDays * .6).Within(1e-7));
            Assert.That(aircraft.Performance.EnginesProductionCost, Is.EqualTo(baseline.Performance.EnginesProductionCost));
        }
    }

    [Test]
    [Category("Extended")]
    public void ActualDefinitionJsonRetainsTechnologyValuesAndLegacyDefaultsAreNotUpgraded()
    {
        using var f = new Fixture();
        var family = JsonConvert.DeserializeObject<AircraftFamilyDefinition>(JsonConvert.SerializeObject(f.Initial.FamilyDefinition))!;
        var type = JsonConvert.DeserializeObject<AircraftTypeDefinition>(JsonConvert.SerializeObject(f.Initial.TypeDefinition))!;
        Assert.That(family.TechnologySnapshot, Is.EqualTo(f.Initial.FamilyDefinition.TechnologySnapshot));
        Assert.That(type.TechnologySnapshot, Is.EqualTo(f.Initial.TypeDefinition.TechnologySnapshot));
        Assert.That(type.TechnologyModelVersion, Is.EqualTo(2));
        var legacy = JsonConvert.DeserializeObject<AircraftTypeDefinition>("{}")!;
        Assert.That(legacy.TechnologyModelVersion, Is.EqualTo(1));
        Assert.That(legacy.TechnologySnapshot, Is.Null);
    }

    [TestCase(14)]
    [TestCase(15)]
    public void WholeSaveLoadPreservesTechnologyModeAndPaidWorkWithoutTouchingDisk(int version)
    {
        using var f = new Fixture();
        var production = f.Get<AircraftProductionFactory>("_productionFactory");
        var factoryState = production.CreateSnapshot();
        factoryState.SupportWorkOrders.Add(new FactorySupportWorkOrderSnapshot
        {
            DesignId = "unrelated.frozen.work", RemainingUnits = 5, CompletedManDays = 2,
            UnitManDays = 10, UnitMaterialCost = 123.45, UnitOverheadCost = 7.25,
            Guaranteed = true, FactoryId = "frozen.factory",
        });
        production.RestoreSnapshot(factoryState);
        var payload = f.Call("serializeGameState");
        var json = JObject.Parse(JsonConvert.SerializeObject(payload));
        json["Version"] = version;
        if (version == 14)
        {
            foreach (var design in (JArray)json["SavedAircraftDesigns"]!)
            {
                ((JObject)design["FamilyDefinition"]!).Remove("TechnologySnapshot");
                ((JObject)design["TypeDefinition"]!).Remove("TechnologySnapshot");
                ((JObject)design["TypeDefinition"]!).Remove("TechnologyModelVersion");
            }
        }
        var restored = JsonConvert.DeserializeObject(json.ToString(), payload.GetType())!;
        f.Call("loadGameState", restored);
        Assert.That(f.Get<IList>("_savedAircraftDesigns").Count, Is.EqualTo(1));
        var saved = f.Saved;
        var aircraft = ((FactoryAircraftTypeCatalogItem)saved.GetType().GetProperty("CatalogItem")!.GetValue(saved)!).Aircraft;
        Assert.That(aircraft.TypeDefinition.TechnologyModelVersion, Is.EqualTo(version == 14 ? 1 : 2));
        if (version == 15)
        {
            Assert.That(aircraft.FamilyDefinition.TechnologySnapshot, Is.EqualTo(f.Initial.FamilyDefinition.TechnologySnapshot));
            Assert.That(aircraft.TypeDefinition.TechnologySnapshot, Is.EqualTo(f.Initial.TypeDefinition.TechnologySnapshot));
        }
        else Assert.That(aircraft.TypeDefinition.TechnologySnapshot, Is.Null);
        var paid = f.Get<AircraftProductionFactory>("_productionFactory").CreateSnapshot().SupportWorkOrders;
        Assert.That(paid.Count, Is.EqualTo(1));
        Assert.That(paid[0].CompletedManDays, Is.EqualTo(2));
        Assert.That(paid[0].RemainingUnits, Is.EqualTo(5));
        Assert.That(paid[0].UnitManDays, Is.EqualTo(10));
        Assert.That(paid[0].UnitMaterialCost, Is.EqualTo(123.45));
        Assert.That(paid[0].UnitOverheadCost, Is.EqualTo(7.25));
        Assert.That(paid[0].FactoryId, Is.EqualTo("frozen.factory"));
        Assert.That(paid[0].Guaranteed, Is.True);
        Assert.That((int)f.Call("serializeGameState").GetType().GetProperty("Version")!.GetValue(f.Call("serializeGameState"))!, Is.EqualTo(16));
    }

    [Test]
    public void LoadingPendingTypeRestoresIncomingResearchBoardBeforeResolvingFamilyTechnology()
    {
        using var f = new Fixture();
        f.Call("EnsureFamilySaved", "freeze.family", f.Initial.FamilyDefinition, "Freeze family");
        var board = new ResearchBoard();
        board.UpsertProposal(ResearchSubjectType.AircraftFamily, "freeze.family", "Freeze family", new ResearchQuote(1000, 5, .5));
        board.UpsertProposal(ResearchSubjectType.AircraftType, "freeze.type", "Freeze type", new ResearchQuote(1000, 5, .5));
        f.Set("_researchBoard", board);
        var payload = f.Call("serializeGameState");
        var json = JObject.Parse(JsonConvert.SerializeObject(payload));
        foreach (var family in (JArray)json["SavedFamilies"]!) ((JObject)family).Remove("TechnologySnapshot");
        foreach (var design in (JArray)json["SavedAircraftDesigns"]!)
        {
            design["FrozenYear"] = null;
            design["FrozenMonth"] = null;
            design["ApprovedYear"] = null;
            design["FamilyDefinition"]!["TechnologySnapshot"]!["Origin"] = "preview";
            design["TypeDefinition"]!["TechnologySnapshot"]!["Origin"] = "preview";
        }
        // Model a different running game's empty research board at the load boundary.
        f.Set("_researchBoard", new ResearchBoard());
        f.Call("loadGameState", JsonConvert.DeserializeObject(json.ToString(), payload.GetType())!);
        Assert.That(f.Get<IList>("_savedAircraftDesigns").Count, Is.EqualTo(1),
            "A pending weighted type must not be silently dropped when its incoming board has not yet been restored.");
        Assert.That(f.Get<ResearchBoard>("_researchBoard").GetAllProjects().Count, Is.EqualTo(2));
    }
}
