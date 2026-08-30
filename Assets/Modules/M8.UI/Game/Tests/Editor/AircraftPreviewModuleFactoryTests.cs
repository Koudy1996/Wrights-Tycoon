using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using WrightTycoon.Core.Airframes;
using WrightTycoon.Core.Engines;
using WrightTycoon.Game.UI;

namespace WrightTycoon.Game.Tests;

[TestFixture]
public sealed class AircraftPreviewModuleFactoryTests
{
    private GameObject _root = null!;
    private Material[] _materials = null!;
    private AircraftPreviewModuleFactory _factory = null!;

    [SetUp]
    public void SetUp()
    {
        _root = new GameObject("AircraftPreviewTestRoot");
        var shader = Shader.Find("Standard") ?? Shader.Find("Legacy Shaders/Diffuse");
        Assert.That(shader, Is.Not.Null);
        _materials = Enumerable.Range(0, 6).Select(_ => new Material(shader!)).ToArray();
        _factory = new AircraftPreviewModuleFactory(
            _root.transform,
            31,
            _materials[0],
            _materials[1],
            _materials[2],
            _materials[3],
            _materials[4],
            _materials[5]);
    }

    [TearDown]
    public void TearDown()
    {
        _factory.Dispose();
        foreach (var material in _materials)
        {
            UnityEngine.Object.DestroyImmediate(material);
        }

        UnityEngine.Object.DestroyImmediate(_root);
    }

    [TestCase(EngineType.Inline, "InlineCowling")]
    [TestCase(EngineType.V, "VBankLeft")]
    [TestCase(EngineType.Radial, "RadialCowling")]
    [TestCase(EngineType.Turbojet, "JetNacelle")]
    [TestCase(EngineType.Turbofan, "FanBlade")]
    public void Build_CreatesDistinctEngineModules(EngineType engineType, string expectedPartPrefix)
    {
        var parameters = CreateParameters();
        parameters.EngineCount = 2;

        _factory.Build(parameters, engineType, engineType == EngineType.Turbofan ? 12d : 0d);

        Assert.That(CountParts(expectedPartPrefix), Is.GreaterThanOrEqualTo(2));
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    [TestCase(5)]
    [TestCase(6)]
    public void Build_CreatesRequestedNumberOfVisibleEngines(int engineCount)
    {
        var parameters = CreateParameters();
        parameters.EngineCount = engineCount;

        _factory.Build(parameters, EngineType.Radial, 0d);

        Assert.That(CountParts("RadialCowling"), Is.EqualTo(engineCount));
    }

    [TestCase(TailType.Classic, "ClassicFin")]
    [TestCase(TailType.TTail, "TFin")]
    [TestCase(TailType.VTail, "VTailLeft")]
    [TestCase(TailType.Twin, "TwinFinLeft")]
    public void Build_CreatesEverySupportedTailLayout(TailType tailType, string expectedPart)
    {
        var parameters = CreateParameters();
        parameters.TailType = tailType;

        _factory.Build(parameters, EngineType.Inline, 0d);

        Assert.That(FindPart(expectedPart), Is.Not.Null);
    }

    [Test]
    public void Build_BiplaneCreatesIndependentUpperWingAndStruts()
    {
        var parameters = CreateParameters();
        parameters.WingPosition = WingPosition.Biplane;

        _factory.Build(parameters, EngineType.Inline, 0d);

        Assert.That(FindPart("UpperWingLeft"), Is.Not.Null);
        Assert.That(FindPart("UpperWingRight"), Is.Not.Null);
        Assert.That(FindPart("BiplaneStrutLeft"), Is.Not.Null);
        Assert.That(FindPart("BiplaneStrutRight"), Is.Not.Null);
    }

    [Test]
    public void Build_UsesExtractedSimpleAircraftPackModules()
    {
        var fuselageMesh = Resources.Load<Mesh>("WT03/Previews/AircraftModules/PackFuselage");
        var propellerMesh = Resources.Load<Mesh>("WT03/Previews/AircraftModules/PackPropeller");
        Assert.That(fuselageMesh, Is.Not.Null);
        Assert.That(propellerMesh, Is.Not.Null);

        var parameters = CreateParameters();
        parameters.EngineCount = 3;
        _factory.Build(parameters, EngineType.Radial, 0d);

        var fuselage = FindPart("Fuselage");
        Assert.That(fuselage, Is.Not.Null);
        Assert.That(fuselage!.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(fuselageMesh));
        Assert.That(CountParts("PackPropeller"), Is.EqualTo(3));
    }

    private int CountParts(string prefix)
    {
        return _root.GetComponentsInChildren<Transform>(true)
            .Count(transform => transform.name.StartsWith(prefix, StringComparison.Ordinal));
    }

    private Transform? FindPart(string name)
    {
        return _root.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(transform => string.Equals(transform.name, name, StringComparison.Ordinal));
    }

    private static AirframeParameters CreateParameters()
    {
        return new AirframeParameters
        {
            EngineCount = 1,
            CrewCount = 3,
            WingAreaSquareMeters = 42d,
            FuelAmountLiters = 1_000d,
            WingPosition = WingPosition.Low,
            WingShape = WingShape.Straight,
            TailType = TailType.Classic,
            LandingGearType = LandingGearType.Fixed,
            MaterialType = MaterialType.Aluminum,
        };
    }
}
