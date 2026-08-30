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
    public void Build_CreatesRequestedNumberOfVisiblePropellerEngines(int engineCount)
    {
        var parameters = CreateParameters();
        parameters.EngineCount = engineCount;

        _factory.Build(parameters, EngineType.Radial, 0d);

        var wingEngineCount = engineCount == 1 ? 0 : engineCount - (engineCount % 2);
        Assert.That(CountParts("RadialCowling"), Is.EqualTo(wingEngineCount));
        Assert.That(CountParts("NoseCowling"), Is.EqualTo(engineCount % 2));
        Assert.That(CountParts("PackPropeller"), Is.EqualTo(engineCount));
    }

    [TestCase(1, 0)]
    [TestCase(3, 2)]
    [TestCase(5, 4)]
    public void Build_OddPropellerEngineUsesFuselageColoredNoseCowling(
        int engineCount,
        int expectedWingEngines)
    {
        var parameters = CreateParameters();
        parameters.EngineCount = engineCount;

        _factory.Build(parameters, EngineType.Inline, 0d);

        var noseCowling = FindPart($"NoseCowling_{engineCount - 1}");
        var fuselage = FindPart("Fuselage");
        Assert.That(noseCowling, Is.Not.Null);
        Assert.That(fuselage, Is.Not.Null);
        Assert.That(noseCowling!.GetComponent<Renderer>().sharedMaterial, Is.SameAs(_materials[0]));
        var cowlingBounds = noseCowling.GetComponent<Renderer>().bounds;
        var fuselageBounds = fuselage!.GetComponent<Renderer>().bounds;
        Assert.That(cowlingBounds.Intersects(fuselageBounds), Is.True);
        Assert.That(cowlingBounds.size.x, Is.GreaterThan(cowlingBounds.size.y));
        Assert.That(CountParts("InlineCowling"), Is.EqualTo(expectedWingEngines));
    }

    [Test]
    public void Build_NosePropellerScalesWithAircraftSize()
    {
        var parameters = CreateParameters();
        parameters.EngineCount = 1;
        parameters.WingAreaSquareMeters = 12d;
        _factory.Build(parameters, EngineType.Inline, 0d);
        var smallBladeLength = FindPart("PropBlade_0_0")!.GetComponent<Renderer>().bounds.size.magnitude;

        parameters.WingAreaSquareMeters = 300d;
        _factory.Build(parameters, EngineType.Inline, 0d);
        var largeBladeLength = FindPart("PropBlade_0_0")!.GetComponent<Renderer>().bounds.size.magnitude;

        Assert.That(largeBladeLength, Is.GreaterThan(smallBladeLength * 1.25f));
    }

    [TestCase(1, 0)]
    [TestCase(3, 2)]
    [TestCase(5, 4)]
    public void Build_OddJetUsesNoseIntakeAndTailExhaust(
        int engineCount,
        int expectedWingEngines)
    {
        var parameters = CreateParameters();
        parameters.EngineCount = engineCount;

        _factory.Build(parameters, EngineType.Turbojet, 0d);

        var centerIndex = engineCount - 1;
        var fairing = FindPart($"CenterJetIntakeFairing_{centerIndex}");
        var intake = FindPart($"CenterJetIntake_{centerIndex}");
        var exhaust = FindPart($"CenterJetExhaust_{centerIndex}");
        var fuselage = FindPart("Fuselage");
        Assert.That(fairing, Is.Not.Null);
        Assert.That(intake, Is.Not.Null);
        Assert.That(exhaust, Is.Not.Null);
        Assert.That(fuselage, Is.Not.Null);
        Assert.That(fairing!.GetComponent<Renderer>().sharedMaterial, Is.SameAs(_materials[0]));
        Assert.That(fairing.GetComponent<Renderer>().bounds.Intersects(
            fuselage!.GetComponent<Renderer>().bounds), Is.True);
        Assert.That(intake!.localPosition.x, Is.GreaterThan(0f));
        Assert.That(exhaust!.localPosition.x, Is.LessThan(0f));
        Assert.That(CountParts("JetNacelle"), Is.EqualTo(expectedWingEngines));
        Assert.That(CountParts("CenterJetExhaustFairing"), Is.EqualTo(engineCount == 1 ? 1 : 0));
    }

    [Test]
    public void Build_SingleJetIntakeUsesFullTransitionWithLimitedProtrusion()
    {
        var parameters = CreateParameters();
        parameters.EngineCount = 1;
        var singleLayout = _factory.Build(parameters, EngineType.Turbojet, 0d);
        var singleLength = FindPart("CenterJetIntakeFairing_0")!
            .GetComponent<Renderer>().bounds.size.x;
        var singleIntakeX = FindPart("CenterJetIntakeLip_0")!.localPosition.x;

        parameters.EngineCount = 3;
        _factory.Build(parameters, EngineType.Turbojet, 0d);
        var multiLength = FindPart("CenterJetIntakeFairing_2")!
            .GetComponent<Renderer>().bounds.size.x;

        var protrusion = singleIntakeX - (singleLayout.FuselageLength * 0.5f);
        Assert.That(singleLength, Is.EqualTo(multiLength).Within(0.002f));
        Assert.That(protrusion, Is.GreaterThan(0f));
        Assert.That(protrusion, Is.LessThanOrEqualTo(singleLength * 0.11f));
    }

    [TestCase(EngineType.Inline, "InlineCowling_0")]
    [TestCase(EngineType.Radial, "RadialCowling_0")]
    public void Build_WingMountedPistonNacelleHasAerodynamicTail(
        EngineType engineType,
        string cowlingName)
    {
        var parameters = CreateParameters();
        parameters.EngineCount = 2;
        _factory.Build(parameters, engineType, 0d);

        var cowlingBounds = FindPart(cowlingName)!.GetComponent<Renderer>().bounds;
        var tailBounds = FindPart("PistonNacelleTail_0")!.GetComponent<Renderer>().bounds;

        Assert.That(tailBounds.max.x, Is.GreaterThanOrEqualTo(cowlingBounds.min.x));
        Assert.That(tailBounds.min.x, Is.LessThan(cowlingBounds.min.x));
    }

    [Test]
    public void Build_MultiEngineAircraftRecentersOriginalFuselageNoseWithoutChangingStructure()
    {
        var packFuselage = Resources.Load<Mesh>("WT03/Previews/AircraftModules/PackFuselage");
        Assert.That(packFuselage, Is.Not.Null);
        var parameters = CreateParameters();
        parameters.EngineCount = 2;
        _factory.Build(parameters, EngineType.Turbojet, 0d);

        var centeredMesh = FindPart("Fuselage")!.GetComponent<MeshFilter>().sharedMesh;
        Assert.That(centeredMesh, Is.Not.SameAs(packFuselage));
        Assert.That(centeredMesh.vertexCount, Is.EqualTo(packFuselage!.vertexCount));
        Assert.That(centeredMesh.triangles.Length, Is.EqualTo(packFuselage.triangles.Length));
        Assert.That(centeredMesh.bounds.size.y, Is.EqualTo(packFuselage.bounds.size.y).Within(0.001f));
        Assert.That(centeredMesh.bounds.size.z, Is.EqualTo(packFuselage.bounds.size.z).Within(0.001f));

        var frontBandStartX = Mathf.Lerp(centeredMesh.bounds.min.x, centeredMesh.bounds.max.x, 0.9f);
        var frontY = centeredMesh.vertices
            .Where(vertex => vertex.x >= frontBandStartX)
            .Average(vertex => vertex.y);
        var originalFrontBandStartX = Mathf.Lerp(packFuselage.bounds.min.x, packFuselage.bounds.max.x, 0.9f);
        var originalFrontY = packFuselage.vertices
            .Where(vertex => vertex.x >= originalFrontBandStartX)
            .Average(vertex => vertex.y);
        Assert.That(Mathf.Abs(frontY), Is.LessThan(Mathf.Abs(originalFrontY)));

        parameters.EngineCount = 1;
        _factory.Build(parameters, EngineType.Turbojet, 0d);
        Assert.That(FindPart("Fuselage")!.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(packFuselage));
    }

    [Test]
    public void Build_CargoConfigurationHasNoPassengerWindows()
    {
        var parameters = CreateParameters();

        _factory.Build(parameters, EngineType.Inline, 0d, showPassengerWindows: false);
        Assert.That(CountParts("CabinWindow"), Is.Zero);
        Assert.That(CountParts("CargoWindowBlank"), Is.EqualTo(2));

        _factory.Build(parameters, EngineType.Inline, 0d, showPassengerWindows: true);
        Assert.That(CountParts("CabinWindow"), Is.GreaterThan(0));
        Assert.That(CountParts("CargoWindowBlank"), Is.Zero);
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
        parameters.EngineCount = 1;
        _factory.Build(parameters, EngineType.Radial, 0d);

        var fuselage = FindPart("Fuselage");
        Assert.That(fuselage, Is.Not.Null);
        Assert.That(fuselage!.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(fuselageMesh));
        Assert.That(CountParts("PackPropeller"), Is.EqualTo(1));
    }

    [Test]
    public void PackFuselage_HasOutwardFacingCabinNormals()
    {
        var mesh = Resources.Load<Mesh>("WT03/Previews/AircraftModules/PackFuselage");
        Assert.That(mesh, Is.Not.Null);

        var vertices = mesh!.vertices;
        var normals = mesh.normals;
        var outwardScore = 0f;
        var evaluated = 0;
        for (var index = 0; index < vertices.Length; index++)
        {
            var radial = new Vector3(0f, vertices[index].y, vertices[index].z);
            if (radial.sqrMagnitude < 0.04f)
            {
                continue;
            }

            outwardScore += Vector3.Dot(normals[index], radial.normalized);
            evaluated++;
        }

        Assert.That(evaluated, Is.GreaterThan(0));
        Assert.That(outwardScore / evaluated, Is.GreaterThan(0.1f));
    }

    [Test]
    public void Build_CockpitCanopyIsLongAndProminent()
    {
        var parameters = CreateParameters();
        _factory.Build(parameters, EngineType.Inline, 0d);

        var fuselageBounds = FindPart("Fuselage")!.GetComponent<Renderer>().bounds;
        var canopyBounds = FindPart("CockpitCanopy")!.GetComponent<Renderer>().bounds;

        Assert.That(canopyBounds.size.x, Is.GreaterThan(fuselageBounds.size.x * 0.2f));
        Assert.That(canopyBounds.max.y, Is.GreaterThan(fuselageBounds.max.y));
    }

    [Test]
    public void Build_WingPylonBridgesNacelleAndWing()
    {
        var parameters = CreateParameters();
        parameters.EngineCount = 2;
        _factory.Build(parameters, EngineType.Radial, 0d);

        var wing = FindPart("MainWingLeft")!.GetComponent<Renderer>().bounds;
        var nacelle = FindPart("RadialCowling_0")!.GetComponent<Renderer>().bounds;
        var pylon = FindPart("EnginePylon_0")!.GetComponent<Renderer>().bounds;

        Assert.That(pylon.max.y, Is.GreaterThanOrEqualTo(wing.min.y - 0.01f));
        Assert.That(pylon.min.y, Is.LessThanOrEqualTo(nacelle.max.y + 0.01f));
    }

    [Test]
    public void Layout_HigherAspectRatioCreatesLongerNarrowerWingAtEqualArea()
    {
        var lowAspectParameters = CreateParameters();
        lowAspectParameters.WingAreaSquareMeters = 48d;
        lowAspectParameters.WingAspectRatio = 4d;
        var highAspectParameters = lowAspectParameters.Clone();
        highAspectParameters.WingAspectRatio = 16d;

        var lowAspect = AircraftPreviewLayout.From(lowAspectParameters);
        var highAspect = AircraftPreviewLayout.From(highAspectParameters);

        Assert.That(highAspect.Span, Is.EqualTo(lowAspect.Span * 2f).Within(0.001f));
        Assert.That(highAspect.WingChord, Is.LessThan(lowAspect.WingChord));
        Assert.That(highAspect.WingArea, Is.EqualTo(lowAspect.WingArea).Within(0.001f));
        Assert.That(highAspect.Span * highAspect.Span / highAspect.WingArea, Is.EqualTo(16f).Within(0.001f));
    }

    [Test]
    public void Layout_LargerWingAreaScalesBothDimensionsAtEqualAspectRatio()
    {
        var smallParameters = CreateParameters();
        smallParameters.WingAreaSquareMeters = 40d;
        smallParameters.WingAspectRatio = 8d;
        var largeParameters = smallParameters.Clone();
        largeParameters.WingAreaSquareMeters = 160d;

        var small = AircraftPreviewLayout.From(smallParameters);
        var large = AircraftPreviewLayout.From(largeParameters);

        Assert.That(large.Span, Is.EqualTo(small.Span * 2f).Within(0.001f));
        Assert.That(large.WingChord, Is.GreaterThan(small.WingChord));
        Assert.That(large.WingArea, Is.EqualTo(small.WingArea * 4f).Within(0.001f));
    }

    [Test]
    public void Build_DeltaWingUsesTriangularHalfMeshes()
    {
        var parameters = CreateParameters();
        parameters.WingShape = WingShape.Delta;
        _factory.Build(parameters, EngineType.Turbojet, 0d);

        var leftWingMesh = FindPart("MainWingLeft")!.GetComponent<MeshFilter>().sharedMesh;
        var rightWingMesh = FindPart("MainWingRight")!.GetComponent<MeshFilter>().sharedMesh;

        Assert.That(leftWingMesh.vertexCount, Is.EqualTo(6));
        Assert.That(rightWingMesh.vertexCount, Is.EqualTo(6));
    }

    [TestCase(WingShape.Straight, WingPosition.Low)]
    [TestCase(WingShape.Swept, WingPosition.High)]
    [TestCase(WingShape.Delta, WingPosition.Low)]
    [TestCase(WingShape.Straight, WingPosition.Biplane)]
    [TestCase(WingShape.Delta, WingPosition.Biplane)]
    public void Build_WingMeshesPreserveSelectedTotalArea(WingShape wingShape, WingPosition wingPosition)
    {
        var parameters = CreateParameters();
        parameters.WingShape = wingShape;
        parameters.WingPosition = wingPosition;
        parameters.WingAreaSquareMeters = 48d;
        parameters.WingAspectRatio = 8d;
        var layout = _factory.Build(parameters, EngineType.Turbojet, 0d);

        var renderedArea = CalculatePlanformArea("MainWingLeft") + CalculatePlanformArea("MainWingRight");
        if (wingPosition == WingPosition.Biplane)
        {
            renderedArea += CalculatePlanformArea("UpperWingLeft") + CalculatePlanformArea("UpperWingRight");
        }

        Assert.That(renderedArea, Is.EqualTo(layout.WingArea).Within(0.002f));
    }

    [Test]
    public void PreviewRotation_IsUnrestrictedAndCanBeReset()
    {
        using var renderer = new AircraftPreview3DRenderer();
        var defaultRotation = renderer.ModelRotation;

        renderer.RotateBy(new Vector2(480f, 500f));

        Assert.That(Quaternion.Angle(defaultRotation, renderer.ModelRotation), Is.GreaterThan(80f));
        Assert.That(renderer.ModelRotation.x * renderer.ModelRotation.x
                    + renderer.ModelRotation.y * renderer.ModelRotation.y
                    + renderer.ModelRotation.z * renderer.ModelRotation.z
                    + renderer.ModelRotation.w * renderer.ModelRotation.w,
            Is.EqualTo(1f).Within(0.001f));

        renderer.ResetView();

        Assert.That(Quaternion.Angle(defaultRotation, renderer.ModelRotation), Is.LessThan(0.001f));
    }

    [Test]
    public void PreviewRotation_MouseUpTurnsAircraftNoseUp()
    {
        using var renderer = new AircraftPreview3DRenderer();
        var defaultNoseDirection = renderer.ModelRotation * Vector3.right;

        renderer.RotateBy(new Vector2(0f, -20f));

        var rotatedNoseDirection = renderer.ModelRotation * Vector3.right;
        Assert.That(rotatedNoseDirection.y, Is.GreaterThan(defaultNoseDirection.y));
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

    private float CalculatePlanformArea(string partName)
    {
        var mesh = FindPart(partName)!.GetComponent<MeshFilter>().sharedMesh;
        var vertices = mesh.vertices;
        var polygonVertexCount = vertices.Length / 2;
        var twiceArea = 0f;
        for (var index = 0; index < polygonVertexCount; index++)
        {
            var current = vertices[index];
            var next = vertices[(index + 1) % polygonVertexCount];
            twiceArea += (current.x * next.z) - (next.x * current.z);
        }

        return Mathf.Abs(twiceArea) * 0.5f;
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
