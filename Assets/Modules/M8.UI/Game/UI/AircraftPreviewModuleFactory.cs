using System;
using System.Collections.Generic;
using UnityEngine;
using WrightTycoon.Core.Airframes;
using WrightTycoon.Core.Engines;

namespace WrightTycoon.Game.UI;

internal readonly struct AircraftPreviewLayout
{
    private const float PreviewMetersScale = 0.28f;

    public AircraftPreviewLayout(
        float span,
        float fuselageLength,
        float fuselageRadius,
        float wingChord,
        float wingArea)
    {
        Span = span;
        FuselageLength = fuselageLength;
        FuselageRadius = fuselageRadius;
        WingChord = wingChord;
        WingArea = wingArea;
    }

    public float Span { get; }

    public float FuselageLength { get; }

    public float FuselageRadius { get; }

    public float WingChord { get; }

    public float WingArea { get; }

    public static AircraftPreviewLayout From(AirframeParameters parameters)
    {
        var physicalWingArea = Mathf.Clamp((float)parameters.WingAreaSquareMeters, 1f, 1_000f);
        var aspectRatio = Mathf.Clamp((float)parameters.WingAspectRatio, 1f, 50f);
        var wingArea = physicalWingArea * PreviewMetersScale * PreviewMetersScale;
        var span = Mathf.Sqrt(physicalWingArea * aspectRatio) * PreviewMetersScale;
        var fuselageLength = Mathf.Clamp(
            2.9f + (float)(parameters.FuelAmountLiters * 0.0011d) + (parameters.CrewCount * 0.2f),
            3.2f,
            8.6f);
        var fuselageRadius = Mathf.Clamp(0.3f + (parameters.CrewCount * 0.035f), 0.32f, 0.78f);
        var tipChordRatio = ResolveTipChordRatio(parameters.WingShape);
        var rootOffset = Mathf.Min(fuselageRadius * 0.45f, span * 0.21f);
        var exposedSpan = Mathf.Max(0.05f, span - (rootOffset * 2f));
        var wingChord = (wingArea * 2f) / (exposedSpan * (1f + tipChordRatio));
        return new AircraftPreviewLayout(span, fuselageLength, fuselageRadius, wingChord, wingArea);
    }

    internal static float ResolveTipChordRatio(WingShape wingShape) =>
        wingShape switch
        {
            WingShape.Straight => 0.72f,
            WingShape.Swept => 0.48f,
            WingShape.Delta => 0f,
            _ => 0.72f,
        };
}

/// <summary>
/// Builds the aircraft preview from independent low-poly modules. Each module deliberately owns
/// its mesh so imported pack parts can replace it later without changing the layout or GUI code.
/// The aircraft points along +X, spans along Z and uses +Y as up.
/// </summary>
internal sealed class AircraftPreviewModuleFactory : IDisposable
{
    private const int RadialSegments = 10;

    private readonly Transform _root;
    private readonly int _layer;
    private readonly Material _fuselageMaterial;
    private readonly Material _wingMaterial;
    private readonly Material _engineMaterial;
    private readonly Material _accentMaterial;
    private readonly Material _darkMaterial;
    private readonly Material _glassMaterial;
    private readonly Mesh _packFuselageMesh;
    private readonly Mesh _packPropellerMesh;
    private readonly List<GameObject> _parts = new();
    private readonly List<Mesh> _meshes = new();

    public AircraftPreviewModuleFactory(
        Transform root,
        int layer,
        Material fuselageMaterial,
        Material wingMaterial,
        Material engineMaterial,
        Material accentMaterial,
        Material darkMaterial,
        Material glassMaterial)
    {
        _root = root;
        _layer = layer;
        _fuselageMaterial = fuselageMaterial;
        _wingMaterial = wingMaterial;
        _engineMaterial = engineMaterial;
        _accentMaterial = accentMaterial;
        _darkMaterial = darkMaterial;
        _glassMaterial = glassMaterial;
        _packFuselageMesh = Resources.Load<Mesh>("WT03/Previews/AircraftModules/PackFuselage");
        _packPropellerMesh = Resources.Load<Mesh>("WT03/Previews/AircraftModules/PackPropeller");
    }

    public AircraftPreviewLayout Build(AirframeParameters parameters, EngineType engineType, double bypassRatio)
    {
        Clear();
        var layout = AircraftPreviewLayout.From(parameters);

        BuildFuselage(parameters, layout);
        BuildWings(parameters, layout);
        BuildTail(parameters.TailType, layout);
        BuildEngines(parameters, engineType, bypassRatio, layout);
        BuildLandingGear(parameters.LandingGearType, layout);

        return layout;
    }

    public void Dispose() => Clear();

    public void Clear()
    {
        for (var index = 0; index < _parts.Count; index++)
        {
            if (_parts[index] != null)
            {
                UnityEngine.Object.DestroyImmediate(_parts[index]);
            }
        }

        _parts.Clear();

        for (var index = 0; index < _meshes.Count; index++)
        {
            if (_meshes[index] != null)
            {
                UnityEngine.Object.DestroyImmediate(_meshes[index]);
            }
        }

        _meshes.Clear();
    }

    internal static float[] CalculateWingEngineStations(int engineCount)
    {
        var normalizedCount = Mathf.Clamp(engineCount, 1, 6);
        var wingEngineCount = normalizedCount == 1 ? 0 : normalizedCount - (normalizedCount % 2);
        if (wingEngineCount == 0)
        {
            return Array.Empty<float>();
        }

        var stationsPerWing = wingEngineCount / 2;
        var stations = new float[wingEngineCount];
        for (var sideIndex = 0; sideIndex < 2; sideIndex++)
        {
            var side = sideIndex == 0 ? -1f : 1f;
            for (var stationIndex = 0; stationIndex < stationsPerWing; stationIndex++)
            {
                var normalizedStation = stationsPerWing switch
                {
                    1 => 0.54f,
                    2 => 0.34f + (stationIndex * 0.36f),
                    _ => 0.24f + (stationIndex * 0.26f),
                };
                stations[(sideIndex * stationsPerWing) + stationIndex] = side * normalizedStation;
            }
        }

        return stations;
    }

    internal static string ResolveEngineModuleName(EngineType engineType, double bypassRatio)
    {
        return engineType switch
        {
            EngineType.Radial => "Radial",
            EngineType.V => "V",
            EngineType.Piston or EngineType.Inline => "Inline",
            EngineType.Turbojet => "Turbojet",
            EngineType.Turbofan when bypassRatio >= 10d => "HighBypassTurbofan",
            EngineType.Turbofan => "LowBypassTurbofan",
            _ => "Inline",
        };
    }

    private void BuildFuselage(AirframeParameters parameters, AircraftPreviewLayout layout)
    {
        var length = layout.FuselageLength;
        var radius = layout.FuselageRadius;
        if (_packFuselageMesh != null)
        {
            CreateSharedMeshPart(
                "Fuselage",
                _packFuselageMesh,
                Vector3.zero,
                new Vector3(length, radius, radius),
                Quaternion.identity,
                _fuselageMaterial);
        }
        else
        {
            var x = new[]
            {
                -length * 0.52f,
                -length * 0.47f,
                -length * 0.34f,
                -length * 0.12f,
                length * 0.18f,
                length * 0.36f,
                length * 0.47f,
                length * 0.53f,
            };
            var radiusScale = new[] { 0.10f, 0.52f, 0.88f, 1f, 1f, 0.94f, 0.58f, 0.06f };
            CreateLoftPart("Fuselage", x, radiusScale, radiusScale, Vector3.zero, _fuselageMaterial, radius);
        }

        var canopyLength = Mathf.Clamp(length * 0.26f, 0.92f, 1.9f);
        var canopyX = length * 0.27f;
        CreateLoftPart(
            "CockpitCanopy",
            new[]
            {
                -canopyLength * 0.5f,
                -canopyLength * 0.34f,
                -canopyLength * 0.02f,
                canopyLength * 0.31f,
                canopyLength * 0.5f,
            },
            new[] { 0.04f, 0.43f, 0.62f, 0.5f, 0.04f },
            new[] { 0.04f, 0.68f, 0.78f, 0.62f, 0.04f },
            new Vector3(canopyX, radius * 0.67f, 0f),
            _glassMaterial,
            radius);

        var windowRows = Mathf.Clamp(parameters.CrewCount + 1, 2, 7);
        var windowSpacing = Mathf.Min(0.42f, (length * 0.38f) / windowRows);
        for (var index = 0; index < windowRows; index++)
        {
            var windowX = (length * 0.13f) - (index * windowSpacing);
            CreateCabinWindow(windowX, radius, -1f);
            CreateCabinWindow(windowX, radius, 1f);
        }

        var stripeLength = length * 0.58f;
        CreateBoxPart(
            "FuselageStripeLeft",
            new Vector3(0f, -radius * 0.13f, -radius * 0.97f),
            new Vector3(stripeLength, 0.055f, 0.025f),
            Quaternion.identity,
            _accentMaterial);
        CreateBoxPart(
            "FuselageStripeRight",
            new Vector3(0f, -radius * 0.13f, radius * 0.97f),
            new Vector3(stripeLength, 0.055f, 0.025f),
            Quaternion.identity,
            _accentMaterial);
    }

    private void CreateCabinWindow(float x, float radius, float side)
    {
        CreateBoxPart(
            "CabinWindow",
            new Vector3(x, radius * 0.22f, side * radius * 0.985f),
            new Vector3(0.16f, 0.13f, 0.022f),
            Quaternion.identity,
            _glassMaterial);
    }

    private void BuildWings(AirframeParameters parameters, AircraftPreviewLayout layout)
    {
        var wingY = parameters.WingPosition switch
        {
            WingPosition.High => layout.FuselageRadius * 0.62f,
            WingPosition.Low => -layout.FuselageRadius * 0.58f,
            WingPosition.Biplane => -layout.FuselageRadius * 0.18f,
            _ => 0f,
        };

        var tipChordRatio = AircraftPreviewLayout.ResolveTipChordRatio(parameters.WingShape);
        var sweepRatio = parameters.WingShape switch
        {
            WingShape.Straight => 0.04f,
            WingShape.Swept => 0.25f,
            WingShape.Delta => 0.42f,
            _ => 0.04f,
        };
        var mainWingChord = ResolveMainWingChord(parameters, layout);
        var rootX = -mainWingChord * 0.05f;
        var thickness = ResolveWingThickness(mainWingChord);
        var dihedral = parameters.WingPosition == WingPosition.High ? -0.07f : 0.12f;
        var rootOffset = ResolveWingRootOffset(layout);

        BuildWingPair(
            "MainWing",
            rootX,
            wingY,
            rootOffset,
            layout.Span * 0.5f,
            mainWingChord,
            tipChordRatio,
            sweepRatio,
            thickness,
            dihedral,
            _wingMaterial);

        if (parameters.WingPosition == WingPosition.Biplane)
        {
            BuildWingPair(
                "UpperWing",
                rootX - (mainWingChord * 0.04f),
                wingY + (layout.FuselageRadius * 1.55f),
                ResolveUpperWingRootOffset(layout),
                layout.Span * 0.47f,
                mainWingChord * 0.9f,
                ResolveUpperWingTipChordRatio(parameters.WingShape),
                sweepRatio * 0.65f,
                thickness * 0.88f,
                0.06f,
                _wingMaterial);

            BuildBiplaneStruts(layout, wingY);
        }

        if (parameters.HasLeadingEdgeFlaps || parameters.FlapType == FlapType.LeadingEdge)
        {
            var flapSpan = layout.Span * 0.68f;
            CreateBoxPart(
                "LeadingEdgeAccent",
                new Vector3(rootX + (mainWingChord * 0.43f), wingY + (thickness * 0.55f), 0f),
                new Vector3(0.07f, 0.035f, flapSpan),
                Quaternion.identity,
                _accentMaterial);
        }
    }

    private static float ResolveMainWingChord(AirframeParameters parameters, AircraftPreviewLayout layout)
    {
        if (parameters.WingPosition != WingPosition.Biplane)
        {
            return layout.WingChord;
        }

        var lowerTipRatio = AircraftPreviewLayout.ResolveTipChordRatio(parameters.WingShape);
        var upperTipRatio = ResolveUpperWingTipChordRatio(parameters.WingShape);
        var lowerSemiSpan = Mathf.Max(0.025f, (layout.Span * 0.5f) - ResolveWingRootOffset(layout));
        var upperSemiSpan = Mathf.Max(0.025f, (layout.Span * 0.47f) - ResolveUpperWingRootOffset(layout));
        var upperToLowerAreaFactor =
            (0.9f * (1f + upperTipRatio) * upperSemiSpan)
            / ((1f + lowerTipRatio) * lowerSemiSpan);
        return layout.WingChord / (1f + upperToLowerAreaFactor);
    }

    private static float ResolveUpperWingTipChordRatio(WingShape wingShape) =>
        wingShape == WingShape.Delta
            ? 0f
            : Mathf.Max(0.46f, AircraftPreviewLayout.ResolveTipChordRatio(wingShape));

    private static float ResolveWingThickness(float rootChord) =>
        Mathf.Clamp(rootChord * 0.085f, 0.025f, 0.18f);

    private static float ResolveWingRootOffset(AircraftPreviewLayout layout) =>
        Mathf.Min(layout.FuselageRadius * 0.45f, layout.Span * 0.21f);

    private static float ResolveUpperWingRootOffset(AircraftPreviewLayout layout) =>
        Mathf.Min(layout.FuselageRadius * 0.38f, layout.Span * 0.19f);

    private void BuildBiplaneStruts(AircraftPreviewLayout layout, float lowerWingY)
    {
        var height = layout.FuselageRadius * 1.55f;
        var z = layout.Span * 0.27f;
        CreateCylinderPart("BiplaneStrutLeft", new Vector3(0f, lowerWingY + (height * 0.5f), -z), 0.025f, height, Quaternion.identity, _darkMaterial, 8);
        CreateCylinderPart("BiplaneStrutRight", new Vector3(0f, lowerWingY + (height * 0.5f), z), 0.025f, height, Quaternion.identity, _darkMaterial, 8);
    }

    private void BuildWingPair(
        string name,
        float rootX,
        float rootY,
        float rootOffset,
        float halfSpan,
        float rootChord,
        float tipChordRatio,
        float sweepRatio,
        float thickness,
        float dihedralRatio,
        Material material)
    {
        BuildWingHalf(name + "Left", -1f, rootX, rootY, rootOffset, halfSpan, rootChord, tipChordRatio, sweepRatio, thickness, dihedralRatio, material);
        BuildWingHalf(name + "Right", 1f, rootX, rootY, rootOffset, halfSpan, rootChord, tipChordRatio, sweepRatio, thickness, dihedralRatio, material);
    }

    private void BuildWingHalf(
        string name,
        float side,
        float rootX,
        float rootY,
        float rootOffset,
        float halfSpan,
        float rootChord,
        float tipChordRatio,
        float sweepRatio,
        float thickness,
        float dihedralRatio,
        Material material)
    {
        var tipChord = rootChord * tipChordRatio;
        var tipX = rootX - (halfSpan * sweepRatio);
        var rootZ = side * rootOffset;
        var tipZ = side * halfSpan;
        var tipY = rootY + (halfSpan * dihedralRatio * 0.12f);
        IReadOnlyList<Vector3> polygon;
        if (tipChordRatio <= 0.001f)
        {
            polygon = new[]
            {
                new Vector3(rootX + (rootChord * 0.5f), rootY, rootZ),
                new Vector3(tipX, tipY, tipZ),
                new Vector3(rootX - (rootChord * 0.5f), rootY, rootZ),
            };
        }
        else
        {
            polygon = new[]
            {
                new Vector3(rootX + (rootChord * 0.5f), rootY, rootZ),
                new Vector3(tipX + (tipChord * 0.5f), tipY, tipZ),
                new Vector3(tipX - (tipChord * 0.5f), tipY, tipZ),
                new Vector3(rootX - (rootChord * 0.5f), rootY, rootZ),
            };
        }

        CreateExtrudedPolygonPart(name, polygon, Vector3.up * thickness, material);
    }

    private void BuildTail(TailType tailType, AircraftPreviewLayout layout)
    {
        var tailX = -layout.FuselageLength * 0.40f;
        var tailSpan = Mathf.Clamp(layout.Span * 0.28f, 1.35f, 2.6f);
        var tailChord = Mathf.Clamp(layout.WingChord * 0.58f, 0.5f, 1.15f);
        var finHeight = Mathf.Clamp(layout.FuselageRadius * 2.25f, 0.9f, 1.75f);

        switch (tailType)
        {
            case TailType.Classic:
                BuildHorizontalTail(tailX, layout.FuselageRadius * 0.20f, tailSpan, tailChord);
                BuildVerticalFin("ClassicFin", tailX, layout.FuselageRadius * 0.12f, 0f, finHeight, tailChord * 0.9f);
                break;

            case TailType.TTail:
                BuildVerticalFin("TFin", tailX, layout.FuselageRadius * 0.05f, 0f, finHeight * 1.18f, tailChord * 0.92f);
                BuildHorizontalTail(tailX - (tailChord * 0.08f), layout.FuselageRadius * 0.08f + (finHeight * 1.08f), tailSpan, tailChord);
                break;

            case TailType.VTail:
                BuildVTail(tailX, layout.FuselageRadius * 0.18f, tailSpan * 0.62f, tailChord);
                break;

            case TailType.Twin:
                BuildHorizontalTail(tailX, layout.FuselageRadius * 0.18f, tailSpan, tailChord);
                BuildVerticalFin("TwinFinLeft", tailX, layout.FuselageRadius * 0.18f, -tailSpan * 0.38f, finHeight * 0.82f, tailChord * 0.72f);
                BuildVerticalFin("TwinFinRight", tailX, layout.FuselageRadius * 0.18f, tailSpan * 0.38f, finHeight * 0.82f, tailChord * 0.72f);
                break;
        }
    }

    private void BuildHorizontalTail(float rootX, float y, float span, float chord)
    {
        BuildWingPair(
            "HorizontalTail",
            rootX,
            y,
            0.04f,
            span * 0.5f,
            chord,
            0.48f,
            0.18f,
            0.055f,
            0.05f,
            _accentMaterial);
    }

    private void BuildVerticalFin(string name, float rootX, float rootY, float z, float height, float chord)
    {
        var polygon = new[]
        {
            new Vector3(rootX + (chord * 0.46f), rootY, z),
            new Vector3(rootX + (chord * 0.05f), rootY + height, z),
            new Vector3(rootX - (chord * 0.31f), rootY + (height * 0.88f), z),
            new Vector3(rootX - (chord * 0.5f), rootY, z),
        };
        CreateExtrudedPolygonPart(name, polygon, Vector3.forward * 0.075f, _accentMaterial);
    }

    private void BuildVTail(float rootX, float rootY, float halfSpan, float chord)
    {
        for (var sideIndex = 0; sideIndex < 2; sideIndex++)
        {
            var side = sideIndex == 0 ? -1f : 1f;
            var root = new Vector3(rootX, rootY, side * 0.04f);
            var tip = new Vector3(rootX - (chord * 0.22f), rootY + (halfSpan * 0.72f), side * halfSpan);
            var forward = Vector3.right * (chord * 0.5f);
            var polygon = new[]
            {
                root + forward,
                tip + (forward * 0.42f),
                tip - (forward * 0.42f),
                root - forward,
            };
            CreateExtrudedPolygonPart(side < 0f ? "VTailLeft" : "VTailRight", polygon, Vector3.up * 0.055f, _accentMaterial);
        }
    }

    private void BuildEngines(AirframeParameters parameters, EngineType engineType, double bypassRatio, AircraftPreviewLayout layout)
    {
        var engineCount = Mathf.Clamp(parameters.EngineCount, 1, 6);
        var moduleName = ResolveEngineModuleName(engineType, bypassRatio);
        var isJet = engineType is EngineType.Turbojet or EngineType.Turbofan;

        if (engineCount == 1)
        {
            if (isJet)
            {
                BuildCenterlineJet(bypassRatio, layout, 0);
            }
            else
            {
                BuildNosePropellerEngine(moduleName, layout, 0);
            }

            return;
        }

        var stations = CalculateWingEngineStations(engineCount);
        var mainWingChord = ResolveMainWingChord(parameters, layout);
        for (var index = 0; index < stations.Length; index++)
        {
            var normalizedStation = stations[index];
            var z = normalizedStation * layout.Span * 0.5f;
            var x = (mainWingChord * 0.38f) - (Mathf.Abs(normalizedStation) * layout.Span * 0.035f);
            var scale = isJet ? 0.9f : 0.98f;
            var engineRadius = ResolveEngineRadius(moduleName, bypassRatio) * scale;
            var wingBottomY = CalculateWingBottomY(parameters, layout, normalizedStation);
            var y = wingBottomY - engineRadius + 0.025f;
            var position = new Vector3(x, y, z);
            BuildEngineModule(moduleName, engineType, bypassRatio, position, scale, index);
            BuildEnginePylon(index, position, engineRadius, wingBottomY);
        }

        if ((engineCount % 2) != 0)
        {
            if (isJet)
            {
                BuildCenterlineJet(bypassRatio, layout, engineCount - 1);
            }
            else
            {
                BuildNosePropellerEngine(moduleName, layout, engineCount - 1);
            }
        }
    }

    private void BuildNosePropellerEngine(
        string moduleName,
        AircraftPreviewLayout layout,
        int index)
    {
        var isRadial = moduleName == "Radial";
        var cowlingLength = isRadial
            ? Mathf.Clamp(layout.FuselageRadius * 1.7f, 0.58f, 1.18f)
            : Mathf.Clamp(layout.FuselageRadius * 2.7f, 0.86f, 1.75f);
        var cowlingRadiusY = layout.FuselageRadius * (isRadial ? 0.9f : 0.66f);
        var cowlingRadiusZ = layout.FuselageRadius * (isRadial ? 0.9f : 0.82f);
        var noseX = layout.FuselageLength * 0.5f;
        var cowlingCenter = new Vector3(noseX - (cowlingLength * 0.28f), 0f, 0f);
        CreateLoftPart(
            $"NoseCowling_{index}",
            new[]
            {
                -cowlingLength * 0.55f,
                -cowlingLength * 0.32f,
                0f,
                cowlingLength * 0.34f,
                cowlingLength * 0.55f,
            },
            new[] { 0.5f, 0.92f, 1f, 0.9f, 0.58f },
            new[] { 0.5f, 0.94f, 1f, 0.88f, 0.55f },
            cowlingCenter,
            _fuselageMaterial,
            cowlingRadiusY,
            cowlingRadiusZ,
            12);

        var aircraftSize = Mathf.Sqrt(layout.WingArea);
        var propellerRadius = Mathf.Clamp(
            0.24f + (aircraftSize * 0.19f),
            layout.FuselageRadius * 0.92f,
            1.28f);
        var bladeCount = moduleName is "Radial" or "V" ? 3 : 2;
        BuildPropeller(
            cowlingCenter.x + (cowlingLength * 0.58f),
            0f,
            0f,
            propellerRadius,
            bladeCount,
            index);
    }

    private void BuildCenterlineJet(double bypassRatio, AircraftPreviewLayout layout, int index)
    {
        var bypassScale = Mathf.InverseLerp(0f, 20f, (float)bypassRatio);
        var intakeRadius = Mathf.Clamp(
            layout.FuselageRadius * Mathf.Lerp(0.7f, 0.82f, bypassScale),
            0.22f,
            0.58f);
        var exhaustRadius = intakeRadius * 0.68f;
        var intakeFairingLength = Mathf.Clamp(layout.FuselageRadius * 1.55f, 0.58f, 1.24f);
        var noseX = layout.FuselageLength * 0.5f;
        var fairingCenterX = noseX - (intakeFairingLength * 0.24f);
        var intakeX = fairingCenterX + (intakeFairingLength * 0.5f);
        var tailX = -layout.FuselageLength * 0.505f;
        var alongX = Quaternion.Euler(0f, 0f, 90f);

        CreateLoftPart(
            $"CenterJetIntakeFairing_{index}",
            new[]
            {
                -intakeFairingLength * 0.52f,
                -intakeFairingLength * 0.28f,
                intakeFairingLength * 0.08f,
                intakeFairingLength * 0.5f,
            },
            new[] { 0.42f, 0.88f, 1f, 1f },
            new[] { 0.42f, 0.88f, 1f, 1f },
            new Vector3(fairingCenterX, 0f, 0f),
            _fuselageMaterial,
            intakeRadius,
            intakeRadius,
            12);
        CreateCylinderPart($"CenterJetIntakeLip_{index}", new Vector3(intakeX, 0f, 0f), intakeRadius, 0.055f, alongX, _engineMaterial, 12);
        CreateCylinderPart($"CenterJetIntake_{index}", new Vector3(intakeX + 0.032f, 0f, 0f), intakeRadius * 0.82f, 0.06f, alongX, _darkMaterial, 12);
        CreateCylinderPart($"CenterJetExhaustLip_{index}", new Vector3(tailX, 0f, 0f), exhaustRadius, 0.045f, alongX, _engineMaterial, 12);
        CreateCylinderPart($"CenterJetExhaust_{index}", new Vector3(tailX - 0.028f, 0f, 0f), exhaustRadius * 0.72f, 0.05f, alongX, _darkMaterial, 12);
    }

    private static float ResolveEngineRadius(string moduleName, double bypassRatio)
    {
        return moduleName switch
        {
            "Radial" => 0.29f,
            "V" => 0.20f,
            "Turbojet" => 0.18f,
            "HighBypassTurbofan" => Mathf.Lerp(0.29f, 0.38f, Mathf.InverseLerp(10f, 20f, (float)bypassRatio)),
            "LowBypassTurbofan" => 0.24f,
            _ => 0.17f,
        };
    }

    private static float CalculateWingBottomY(
        AirframeParameters parameters,
        AircraftPreviewLayout layout,
        float normalizedStation)
    {
        var wingY = parameters.WingPosition switch
        {
            WingPosition.High => layout.FuselageRadius * 0.62f,
            WingPosition.Low => -layout.FuselageRadius * 0.58f,
            WingPosition.Biplane => -layout.FuselageRadius * 0.18f,
            _ => 0f,
        };
        var rootOffset = ResolveWingRootOffset(layout);
        var halfSpan = layout.Span * 0.5f;
        var station = Mathf.Abs(normalizedStation) * halfSpan;
        var dihedral = parameters.WingPosition == WingPosition.High ? -0.07f : 0.12f;
        var stationProgress = Mathf.InverseLerp(rootOffset, halfSpan, station);
        var centerY = wingY + (halfSpan * dihedral * 0.12f * stationProgress);
        var thickness = ResolveWingThickness(ResolveMainWingChord(parameters, layout));
        return centerY - (thickness * 0.5f);
    }

    private void BuildEnginePylon(int index, Vector3 enginePosition, float engineRadius, float wingBottomY)
    {
        var nacelleTopY = enginePosition.y + (engineRadius * 0.72f);
        var attachmentY = wingBottomY + 0.025f;
        var polygon = new[]
        {
            new Vector3(enginePosition.x + 0.22f, nacelleTopY, enginePosition.z - 0.035f),
            new Vector3(enginePosition.x + 0.06f, attachmentY, enginePosition.z - 0.035f),
            new Vector3(enginePosition.x - 0.18f, nacelleTopY, enginePosition.z - 0.035f),
        };
        CreateExtrudedPolygonPart($"EnginePylon_{index}", polygon, Vector3.forward * 0.07f, _engineMaterial);
    }

    private void BuildEngineModule(
        string moduleName,
        EngineType engineType,
        double bypassRatio,
        Vector3 position,
        float scale,
        int index)
    {
        switch (moduleName)
        {
            case "Radial":
                BuildRadialEngine(position, scale, index);
                break;
            case "V":
                BuildVEngine(position, scale, index);
                break;
            case "Turbojet":
                BuildJetEngine(position, scale, index, 0.18f, 0.92f, false);
                break;
            case "HighBypassTurbofan":
                BuildJetEngine(position, scale, index, Mathf.Lerp(0.29f, 0.38f, Mathf.InverseLerp(10f, 20f, (float)bypassRatio)), 0.86f, true);
                break;
            case "LowBypassTurbofan":
                BuildJetEngine(position, scale, index, 0.24f, 0.9f, true);
                break;
            default:
                BuildInlineEngine(position, scale, index, engineType == EngineType.V);
                break;
        }
    }

    private void BuildInlineEngine(Vector3 position, float scale, int index, bool wide)
    {
        var length = (wide ? 0.76f : 0.68f) * scale;
        var radiusY = (wide ? 0.20f : 0.17f) * scale;
        var radiusZ = (wide ? 0.24f : 0.15f) * scale;
        CreateLoftPart(
            $"InlineCowling_{index}",
            new[] { -length * 0.5f, -length * 0.38f, length * 0.34f, length * 0.5f },
            new[] { 0.72f, 1f, 0.96f, 0.62f },
            new[] { 0.72f, 1f, 0.96f, 0.62f },
            position,
            _engineMaterial,
            radiusY,
            radiusZ);

        if (wide)
        {
            CreateLoftPart($"VBankLeft_{index}", new[] { -0.25f, 0.25f }, new[] { 1f, 0.78f }, new[] { 1f, 0.78f }, position + new Vector3(0f, radiusY * 0.68f, -radiusZ * 0.56f), _darkMaterial, radiusY * 0.42f, radiusZ * 0.34f, 6);
            CreateLoftPart($"VBankRight_{index}", new[] { -0.25f, 0.25f }, new[] { 1f, 0.78f }, new[] { 1f, 0.78f }, position + new Vector3(0f, radiusY * 0.68f, radiusZ * 0.56f), _darkMaterial, radiusY * 0.42f, radiusZ * 0.34f, 6);
        }

        BuildPropeller(position.x + (length * 0.52f), position.y, position.z, (wide ? 0.55f : 0.48f) * scale, wide ? 3 : 2, index);
    }

    private void BuildVEngine(Vector3 position, float scale, int index)
    {
        BuildInlineEngine(position, scale, index, true);
    }

    private void BuildRadialEngine(Vector3 position, float scale, int index)
    {
        var length = 0.48f * scale;
        var radius = 0.29f * scale;
        CreateLoftPart(
            $"RadialCowling_{index}",
            new[] { -length * 0.5f, -length * 0.30f, length * 0.30f, length * 0.5f },
            new[] { 0.76f, 1f, 1f, 0.78f },
            new[] { 0.76f, 1f, 1f, 0.78f },
            position,
            _engineMaterial,
            radius,
            radius,
            12);

        var cylinderRadius = radius * 0.16f;
        for (var cylinderIndex = 0; cylinderIndex < 7; cylinderIndex++)
        {
            var angle = (Mathf.PI * 2f * cylinderIndex) / 7f;
            var center = position + new Vector3(
                length * 0.40f,
                Mathf.Cos(angle) * radius * 0.58f,
                Mathf.Sin(angle) * radius * 0.58f);
            CreateLoftPart(
                $"RadialCylinder_{index}_{cylinderIndex}",
                new[] { -0.07f * scale, 0.07f * scale },
                new[] { 0.85f, 0.85f },
                new[] { 0.85f, 0.85f },
                center,
                _darkMaterial,
                cylinderRadius,
                cylinderRadius,
                6);
        }

        BuildPropeller(position.x + (length * 0.56f), position.y, position.z, 0.62f * scale, 3, index);
    }

    private void BuildJetEngine(Vector3 position, float scale, int index, float radius, float length, bool showFan)
    {
        radius *= scale;
        length *= scale;
        CreateLoftPart(
            $"JetNacelle_{index}",
            new[] { -length * 0.52f, -length * 0.42f, length * 0.30f, length * 0.50f },
            new[] { 0.72f, 1f, 0.94f, 0.64f },
            new[] { 0.72f, 1f, 0.94f, 0.64f },
            position,
            _engineMaterial,
            radius,
            radius,
            12);

        CreateCylinderPart($"JetIntake_{index}", position + new Vector3(length * 0.515f, 0f, 0f), radius * 0.68f, 0.025f, Quaternion.Euler(0f, 0f, 90f), _darkMaterial, 12);
        CreateCylinderPart($"JetExhaust_{index}", position - new Vector3(length * 0.515f, 0f, 0f), radius * 0.48f, 0.025f, Quaternion.Euler(0f, 0f, 90f), _darkMaterial, 12);

        if (showFan)
        {
            BuildFan(position.x + (length * 0.53f), position.y, position.z, radius * 0.57f, index);
        }
    }

    private void BuildPropeller(float x, float y, float z, float radius, int bladeCount, int index)
    {
        CreateLoftPart($"PropHub_{index}", new[] { -0.09f, 0f, 0.13f }, new[] { 0.76f, 1f, 0.12f }, new[] { 0.76f, 1f, 0.12f }, new Vector3(x, y, z), _accentMaterial, radius * 0.15f, radius * 0.15f, 8);
        if (_packPropellerMesh != null && bladeCount >= 3)
        {
            CreateSharedMeshPart(
                $"PackPropeller_{index}",
                _packPropellerMesh,
                new Vector3(x + 0.01f, y, z),
                Vector3.one * radius,
                Quaternion.identity,
                _darkMaterial);
            return;
        }

        BuildRotorBlades($"PropBlade_{index}", x + 0.01f, y, z, radius, bladeCount, 0.09f, _darkMaterial);
    }

    private void BuildFan(float x, float y, float z, float radius, int index)
    {
        BuildRotorBlades($"FanBlade_{index}", x + 0.02f, y, z, radius, 8, 0.028f, _accentMaterial);
        CreateLoftPart($"FanHub_{index}", new[] { -0.03f, 0.04f }, new[] { 1f, 0.55f }, new[] { 1f, 0.55f }, new Vector3(x + 0.025f, y, z), _darkMaterial, radius * 0.19f, radius * 0.19f, 8);
    }

    private void BuildRotorBlades(string name, float x, float y, float z, float radius, int bladeCount, float bladeWidth, Material material)
    {
        for (var bladeIndex = 0; bladeIndex < bladeCount; bladeIndex++)
        {
            var angle = (Mathf.PI * 2f * bladeIndex) / bladeCount;
            var radial = new Vector3(0f, Mathf.Cos(angle), Mathf.Sin(angle));
            var tangent = new Vector3(0f, -Mathf.Sin(angle), Mathf.Cos(angle));
            var center = new Vector3(x, y, z);
            var polygon = new[]
            {
                center + (radial * radius * 0.16f) - (tangent * bladeWidth),
                center + (radial * radius * 0.93f) - (tangent * bladeWidth * 0.42f),
                center + (radial * radius) + (tangent * bladeWidth * 0.34f),
                center + (radial * radius * 0.18f) + (tangent * bladeWidth),
            };
            CreateExtrudedPolygonPart($"{name}_{bladeIndex}", polygon, Vector3.right * 0.018f, material);
        }
    }

    private void BuildLandingGear(LandingGearType gearType, AircraftPreviewLayout layout)
    {
        var y = -layout.FuselageRadius * 1.02f;
        var z = Mathf.Max(layout.FuselageRadius * 1.25f, 0.52f);
        if (gearType == LandingGearType.Retractable)
        {
            CreateBoxPart("GearDoorLeft", new Vector3(0.02f, y, -z), new Vector3(0.42f, 0.035f, 0.17f), Quaternion.identity, _darkMaterial);
            CreateBoxPart("GearDoorRight", new Vector3(0.02f, y, z), new Vector3(0.42f, 0.035f, 0.17f), Quaternion.identity, _darkMaterial);
            return;
        }

        var extension = gearType == LandingGearType.Fixed ? 0.54f : 0.34f;
        var wheelY = y - extension;
        BuildGearLeg("MainGearLeft", new Vector3(0.04f, y - (extension * 0.5f), -z), wheelY, -z, extension, gearType == LandingGearType.Fixed);
        BuildGearLeg("MainGearRight", new Vector3(0.04f, y - (extension * 0.5f), z), wheelY, z, extension, gearType == LandingGearType.Fixed);

        var noseX = layout.FuselageLength * 0.39f;
        BuildGearLeg("NoseGear", new Vector3(noseX, y - (extension * 0.42f), 0f), wheelY + (extension * 0.08f), 0f, extension * 0.82f, false, noseX);
    }

    private void BuildGearLeg(string name, Vector3 strutPosition, float wheelY, float z, float length, bool addFairing, float x = 0.04f)
    {
        CreateCylinderPart(name + "Strut", strutPosition, 0.025f, length, Quaternion.identity, _engineMaterial, 8);
        CreateCylinderPart(name + "Wheel", new Vector3(x, wheelY, z), 0.15f, 0.075f, Quaternion.Euler(90f, 0f, 0f), _darkMaterial, 10);
        if (addFairing)
        {
            CreateLoftPart(name + "Fairing", new[] { -0.2f, 0.2f }, new[] { 0.4f, 1f }, new[] { 0.7f, 1f }, new Vector3(x, wheelY + 0.06f, z), _accentMaterial, 0.18f, 0.10f, 6);
        }
    }

    private void CreateLoftPart(
        string name,
        IReadOnlyList<float> x,
        IReadOnlyList<float> radiusYScale,
        IReadOnlyList<float> radiusZScale,
        Vector3 center,
        Material material,
        float radius = 1f,
        float? radiusZOverride = null,
        int segments = RadialSegments)
    {
        var radiusZ = radiusZOverride ?? radius;
        var vertices = new Vector3[x.Count * segments];
        var triangles = new int[(x.Count - 1) * segments * 6];

        for (var ringIndex = 0; ringIndex < x.Count; ringIndex++)
        {
            for (var segmentIndex = 0; segmentIndex < segments; segmentIndex++)
            {
                var angle = (Mathf.PI * 2f * segmentIndex) / segments;
                vertices[(ringIndex * segments) + segmentIndex] = center + new Vector3(
                    x[ringIndex],
                    Mathf.Cos(angle) * radius * radiusYScale[ringIndex],
                    Mathf.Sin(angle) * radiusZ * radiusZScale[ringIndex]);
            }
        }

        var triangleIndex = 0;
        for (var ringIndex = 0; ringIndex < x.Count - 1; ringIndex++)
        {
            for (var segmentIndex = 0; segmentIndex < segments; segmentIndex++)
            {
                var nextSegment = (segmentIndex + 1) % segments;
                var a = (ringIndex * segments) + segmentIndex;
                var b = (ringIndex * segments) + nextSegment;
                var c = ((ringIndex + 1) * segments) + segmentIndex;
                var d = ((ringIndex + 1) * segments) + nextSegment;
                triangles[triangleIndex++] = a;
                triangles[triangleIndex++] = b;
                triangles[triangleIndex++] = c;
                triangles[triangleIndex++] = b;
                triangles[triangleIndex++] = d;
                triangles[triangleIndex++] = c;
            }
        }

        CreateMeshPart(name, vertices, triangles, material);
    }

    private void CreateExtrudedPolygonPart(string name, IReadOnlyList<Vector3> sourcePolygon, Vector3 extrusion, Material material)
    {
        var polygon = new List<Vector3>(sourcePolygon);
        if (polygon.Count < 3)
        {
            return;
        }

        var normal = Vector3.Cross(polygon[1] - polygon[0], polygon[2] - polygon[0]);
        if (Vector3.Dot(normal, extrusion) < 0f)
        {
            polygon.Reverse();
        }

        var count = polygon.Count;
        var halfExtrusion = extrusion * 0.5f;
        var vertices = new Vector3[count * 2];
        for (var index = 0; index < count; index++)
        {
            vertices[index] = polygon[index] - halfExtrusion;
            vertices[count + index] = polygon[index] + halfExtrusion;
        }

        var triangles = new List<int>((count - 2) * 6 + (count * 6));
        for (var index = 1; index < count - 1; index++)
        {
            triangles.Add(0);
            triangles.Add(index + 1);
            triangles.Add(index);

            triangles.Add(count);
            triangles.Add(count + index);
            triangles.Add(count + index + 1);
        }

        for (var index = 0; index < count; index++)
        {
            var next = (index + 1) % count;
            triangles.Add(index);
            triangles.Add(next);
            triangles.Add(count + index);
            triangles.Add(next);
            triangles.Add(count + next);
            triangles.Add(count + index);
        }

        CreateMeshPart(name, vertices, triangles.ToArray(), material);
    }

    private void CreateBoxPart(string name, Vector3 position, Vector3 scale, Quaternion rotation, Material material)
    {
        CreatePrimitivePart(PrimitiveType.Cube, name, position, scale, rotation, material);
    }

    private void CreateCylinderPart(string name, Vector3 position, float radius, float length, Quaternion rotation, Material material, int ignoredSegments)
    {
        CreatePrimitivePart(PrimitiveType.Cylinder, name, position, new Vector3(radius * 2f, length * 0.5f, radius * 2f), rotation, material);
    }

    private void CreatePrimitivePart(
        PrimitiveType primitiveType,
        string name,
        Vector3 position,
        Vector3 scale,
        Quaternion rotation,
        Material material)
    {
        var part = GameObject.CreatePrimitive(primitiveType);
        part.name = name;
        part.hideFlags = HideFlags.HideAndDontSave;
        part.layer = _layer;
        part.transform.SetParent(_root, false);
        part.transform.localPosition = position;
        part.transform.localRotation = rotation;
        part.transform.localScale = scale;

        var collider = part.GetComponent<Collider>();
        if (collider != null)
        {
            UnityEngine.Object.DestroyImmediate(collider);
        }

        var renderer = part.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
        }

        _parts.Add(part);
    }

    private void CreateMeshPart(string name, Vector3[] vertices, int[] triangles, Material material)
    {
        var mesh = new Mesh
        {
            name = name + "Mesh",
            hideFlags = HideFlags.HideAndDontSave,
        };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        _meshes.Add(mesh);

        var part = new GameObject(name)
        {
            hideFlags = HideFlags.HideAndDontSave,
            layer = _layer,
        };
        part.transform.SetParent(_root, false);
        var filter = part.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        var renderer = part.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        _parts.Add(part);
    }

    private void CreateSharedMeshPart(
        string name,
        Mesh mesh,
        Vector3 position,
        Vector3 scale,
        Quaternion rotation,
        Material material)
    {
        var part = new GameObject(name)
        {
            hideFlags = HideFlags.HideAndDontSave,
            layer = _layer,
        };
        part.transform.SetParent(_root, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        part.transform.localRotation = rotation;
        var filter = part.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        var renderer = part.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        _parts.Add(part);
    }
}
