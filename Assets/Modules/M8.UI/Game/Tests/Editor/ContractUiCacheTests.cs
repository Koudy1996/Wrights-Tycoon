using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WrightTycoon.Core.Aircraft;
using WrightTycoon.Core.Airframes;
using WrightTycoon.Core.Contracts;
using WrightTycoon.Core.Engines;
using WrightTycoon.Core.Factories;
using WrightTycoon.Core.Physics;
using WrightTycoon.Core.Simulation;
using WrightTycoon.Game.Launch;

namespace WrightTycoon.Game.Tests;

public sealed class ContractUiCacheTests
{
    private sealed class Evaluator : IContractEvaluator, IContractGenerator
    {
        public int Calls;
        public ContractEvaluationReport Evaluate(Contract contract, WrightTycoon.Core.Aircraft.Aircraft aircraft)
        {
            Calls++;
            return new ContractEvaluationReport(Array.Empty<string>());
        }
        public IReadOnlyList<Contract> GenerateMonthlyContracts(int monthIndex) => Array.Empty<Contract>();
    }

    private sealed class Fixture : IDisposable
    {
        private readonly GameObject _object = new("ContractUiCacheTest");
        public readonly WrightTycoonLaunchRunner Runner;
        public readonly Evaluator Evaluator = new();
        public readonly ContractBoard Board;
        public readonly AircraftProductionFactory Factory = new();
        public readonly Contract Contract = new("cache.contract", "Customer", 5, 12,
            new ContractRequirements { MinimumPayloadKilograms = 1 }, 0, 100000);
        public readonly FactoryAircraftTypeCatalogItem Item;
        private const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;
        public Fixture()
        {
            _object.SetActive(false);
            Runner = _object.AddComponent<WrightTycoonLaunchRunner>();
            Board = new ContractBoard(Evaluator, Evaluator);
            Board.InjectGeneratedContractsForCurrentMonth(new[] { Contract });
            Set("_contractBoard", Board);
            Set("_productionFactory", Factory);
            Set("_currentDate", new SimulationDate(1949, 1));
            var atmosphere = new IsaAtmosphereModel();
            var aero = new AerodynamicsCalculator();
            var engine = new EngineBuilder(new EngineCalculator(), atmosphere).Build(
                new EngineDefinition { Id = "cache.engine", DisplayName = "Engine", EngineType = EngineType.Radial },
                new EngineParameters { DisplacementLiters = 12, CylinderCount = 9, CompressionRatio = 8, CoolingEfficiency = .8 });
            var airframe = new AirframeBuilder(new AirframeCalculator(aero, atmosphere)).Build(
                new AirframeDefinition { Id = "cache.airframe", DisplayName = "Airframe" },
                new AirframeParameters { EngineCount = 1, WingAreaSquareMeters = 24, FuelAmountLiters = 200 });
            var family = new AircraftFamilyDefinition { Id = "cache.family", DisplayName = "Family",
                EngineDefinitionId = engine.Definition.Id, AirframeDefinitionId = airframe.Definition.Id,
                BaseEngineParameters = engine.Parameters.Clone(), BaseAirframeParameters = airframe.Parameters.Clone() };
            var type = new AircraftTypeDefinition { Id = "cache.type", DisplayName = "Type", FamilyId = family.Id };
            var aircraft = new WrightTycoon.Core.Aircraft.Aircraft(family, type, engine, airframe,
                new AircraftPerformanceCalculator(atmosphere, aero).Calculate(engine, airframe), new DevelopmentCostReport(0, 0, 85000, 230));
            Item = new FactoryAircraftTypeCatalogItem("cache.aircraft", "Aircraft", aircraft);
        }
        public void Set(string field, object value) => typeof(WrightTycoonLaunchRunner).GetField(field, Flags)!.SetValue(Runner, value);
        public object Call(string method, params object[] args) => typeof(WrightTycoonLaunchRunner).GetMethod(method, Flags)!.Invoke(Runner, args)!;
        public IDisposable Begin() => (IDisposable)Call("BeginContractUiCache");
        public void Evaluate() => Call("EvaluateContractForUi", Contract, Board, Item.Aircraft);
        public void Dispose() => UnityEngine.Object.DestroyImmediate(_object);
    }

    [Test]
    public void RepeatedUiPassesReuseEvaluationButSimulationAndDeliveryValidationStayLive()
    {
        using var f = new Fixture();
        for (var pass = 0; pass < 100; pass++)
        {
            using var scope = f.Begin();
            f.Evaluate(); f.Evaluate();
        }
        Assert.That(f.Evaluator.Calls, Is.EqualTo(1));
        f.Evaluate();
        Assert.That(f.Evaluator.Calls, Is.EqualTo(2), "Outside drawing, evaluation must not be cached.");
        using (f.Begin())
        {
            f.Call("ClearContractUiCache");
            f.Evaluate();
            Assert.That(f.Evaluator.Calls, Is.EqualTo(3));
        }
    }

    [Test]
    public void StockMonthAndBoardReplacementInvalidateAndCandidatesAreReusedWithoutStaleStock()
    {
        using var f = new Fixture();
        var catalog = new Dictionary<string, FactoryAircraftTypeCatalogItem> { [f.Item.DesignId] = f.Item };
        object Candidates() => f.Call("BuildContractDeliveryCandidates", f.Contract, f.Board, catalog, f.Factory.WarehouseUnitsByDesign);
        object before;
        using (f.Begin()) { before = Candidates(); Assert.That(Candidates(), Is.SameAs(before)); f.Evaluate(); }
        f.Factory.AddWarehouseUnits(f.Item.DesignId, 5);
        using (f.Begin())
        {
            var added = Candidates();
            Assert.That(added, Is.Not.SameAs(before));
            Assert.That(((System.Collections.ICollection)added).Count, Is.EqualTo(1));
            f.Evaluate();
        }
        Assert.That(f.Evaluator.Calls, Is.EqualTo(2), "Each stock state evaluates the aircraft only once.");
        f.Factory.TryWithdrawFromWarehouse(f.Item.DesignId, 5, out _);
        using (f.Begin()) { Assert.That(((System.Collections.ICollection)Candidates()).Count, Is.Zero); f.Evaluate(); }
        Assert.That(f.Evaluator.Calls, Is.EqualTo(3));
        f.Set("_currentDate", new SimulationDate(1949, 2));
        using (f.Begin()) f.Evaluate();
        Assert.That(f.Evaluator.Calls, Is.EqualTo(4));
        f.Set("_contractBoard", new ContractBoard(f.Evaluator, f.Evaluator));
        using (f.Begin()) f.Evaluate();
        Assert.That(f.Evaluator.Calls, Is.EqualTo(5));
    }
}
