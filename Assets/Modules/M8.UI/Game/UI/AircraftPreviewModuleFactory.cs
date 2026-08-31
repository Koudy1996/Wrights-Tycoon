using System;
using System.Collections.Generic;
using UnityEngine;
using WrightTycoon.Core.Airframes;
using WrightTycoon.Core.Engines;

namespace WrightTycoon.Game.UI;

internal readonly struct AircraftPreviewLayout
{
    private const float PreviewMetersScale = 0.28f;

    public AircraftPreviewLayout(float span, float fuselageLength, float fuselageRadius, float wingChord, float wingArea)
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
        if (parameters.EngineCount == 1)
        {
            fuselageLength = Mathf.Clamp(fuselageLength * 0.80f, 3.2f, 6.4f);
            fuselageRadius = Mathf.Clamp(fuselageRadius * 1.08f, 0.36f, 0.74f);
        }
        var tipChordRatio = ResolveTipChordRatio(parameters.WingShape);
        var rootOffset = Mathf.Min(fuselageRadius * 0.68f, span * 0.21f);
        var exposedSpan = Mathf.Max(0.05f, span - (rootOffset * 2f));
        var wingChord = (wingArea * 2f) / (exposedSpan * (1f + tipChordRatio));
        return new AircraftPreviewLayout(span, fuselageLength, fuselageRadius, wingChord, wingArea);
    }

    internal static float ResolveTipChordRatio(WingShape wingShape) =>
        wingShape switch
        {
            WingShape.Straight => 0.70f,
            WingShape.Swept => 0.42f,
            WingShape.Delta => 0.035f,
            _ => 0.70f,
        };
}

internal enum AircraftPreviewArchetype
{
    SingleSeat,
    Commuter,
    Regional,
    Transport,
}

/// <summary>
/// Builds a socket-based aircraft from pack-derived fuselage families and reusable aerodynamic
/// modules. The aircraft points along +X, spans along Z and uses +Y as up.
/// </summary>
internal sealed class AircraftPreviewModuleFactory : IDisposable
{
    private const int RadialSegments = 18;
    private const int WingAirfoilSegments = 12;

    private readonly Transform _root;
    private readonly int _layer;
    private readonly Material _fuselageMaterial;
    private readonly Material _wingMaterial;
    private readonly Material _engineMaterial;
    private readonly Material _accentMaterial;
    private readonly Material _darkMaterial;
    private readonly Material _glassMaterial;
    private readonly Mesh? _packFuselageV1;
    private readonly Mesh? _packFuselageV2;
    private readonly Mesh? _packFuselageV3;
    private readonly Mesh? _packPropeller;
    private readonly Mesh? _packFan;
    private readonly Mesh? _packWheelLarge;
    private readonly Mesh? _packWheelSmall;
    private readonly Material? _packSurfaceV1;
    private readonly Material? _packSurfaceV2;
    private readonly Material? _packSurfaceV3;
    private readonly List<GameObject> _parts = new();
    private readonly List<Mesh> _meshes = new();
    private readonly List<Material> _ownedMaterials = new();

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
        _packFuselageV1 = Resources.Load<Mesh>("WT03/Previews/AircraftModules/PackFuselageV1");
        _packFuselageV2 = Resources.Load<Mesh>("WT03/Previews/AircraftModules/PackFuselageV2");
        _packFuselageV3 = Resources.Load<Mesh>("WT03/Previews/AircraftModules/PackFuselageV3")
            ?? Resources.Load<Mesh>("WT03/Previews/AircraftModules/PackFuselage");
        _packPropeller = Resources.Load<Mesh>("WT03/Previews/AircraftModules/PackPropeller");
        _packFan = Resources.Load<Mesh>("WT03/Previews/AircraftModules/PackFan");
        _packWheelLarge = Resources.Load<Mesh>("WT03/Previews/AircraftModules/PackWheelLarge");
        _packWheelSmall = Resources.Load<Mesh>("WT03/Previews/AircraftModules/PackWheelSmall");
        _packSurfaceV1 = Resources.Load<Material>("WT03/Previews/AircraftModules/PackSurfaceV1");
        _packSurfaceV2 = Resources.Load<Material>("WT03/Previews/AircraftModules/PackSurfaceV2");
        _packSurfaceV3 = Resources.Load<Material>("WT03/Previews/AircraftModules/PackSurfaceV3");
    }

    public AircraftPreviewLayout Build(
        AirframeParameters parameters,
        EngineType engineType,
        double bypassRatio,
        bool showPassengerWindows = true)
    {
        Clear();
        var layout = AircraftPreviewLayout.From(parameters);
        var archetype = ResolveArchetype(parameters, engineType, layout);

        BuildFuselage(parameters, engineType, layout, archetype, showPassengerWindows);
        BuildWings(parameters, layout, archetype);
        BuildTail(parameters.TailType, layout, archetype);
        BuildEngines(parameters, engineType, bypassRatio, layout);
        BuildLandingGear(parameters.LandingGearType, layout, archetype);
        return layout;
    }

    public void Dispose() => Clear();

    public void Clear()
    {
        foreach (var part in _parts)
        {
            if (part != null)
            {
                UnityEngine.Object.DestroyImmediate(part);
            }
        }

        _parts.Clear();
        foreach (var mesh in _meshes)
        {
            if (mesh != null)
            {
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        _meshes.Clear();
        foreach (var material in _ownedMaterials)
        {
            if (material != null)
            {
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        _ownedMaterials.Clear();
    }

    internal static AircraftPreviewArchetype ResolveArchetype(
        AirframeParameters parameters,
        EngineType engineType,
        AircraftPreviewLayout layout)
    {
        if (parameters.EngineCount == 1)
        {
            return AircraftPreviewArchetype.SingleSeat;
        }

        var isPropeller = engineType is EngineType.Piston or EngineType.Inline or EngineType.V or EngineType.Radial;
        if (isPropeller && parameters.EngineCount <= 2)
        {
            return AircraftPreviewArchetype.Commuter;
        }

        if (parameters.EngineCount <= 2 && layout.FuselageLength < 5.7f)
        {
            return AircraftPreviewArchetype.Regional;
        }

        return AircraftPreviewArchetype.Transport;
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

    internal static string ResolveEngineModuleName(EngineType engineType, double bypassRatio) =>
        engineType switch
        {
            EngineType.Radial => "Radial",
            EngineType.V => "V",
            EngineType.Piston or EngineType.Inline => "Inline",
            EngineType.Turbojet => "Turbojet",
            EngineType.Turbofan when bypassRatio >= 10d => "HighBypassTurbofan",
            EngineType.Turbofan => "LowBypassTurbofan",
            _ => "Inline",
        };

    private void BuildFuselage(
        AirframeParameters parameters,
        EngineType engineType,
        AircraftPreviewLayout layout,
        AircraftPreviewArchetype archetype,
        bool showPassengerWindows)
    {
        if (archetype == AircraftPreviewArchetype.SingleSeat)
        {
            BuildSingleSeatFuselage(layout, engineType);
            BuildSingleSeatCanopy(layout, engineType);
            BuildLivery(layout, archetype);
            return;
        }

        var source = archetype switch
        {
            AircraftPreviewArchetype.Transport => _packFuselageV1,
            AircraftPreviewArchetype.Regional => _packFuselageV2,
            _ => _packFuselageV3,
        };
        var packSurface = archetype switch
        {
            AircraftPreviewArchetype.Transport => _packSurfaceV1,
            AircraftPreviewArchetype.Regional => _packSurfaceV2,
            _ => _packSurfaceV3,
        };
        var usesPackSurface = packSurface != null
            && showPassengerWindows
            && archetype != AircraftPreviewArchetype.SingleSeat;
        if (source != null)
        {
            CreateSharedMeshPart(
                "Fuselage",
                CreateStationDeformedFuselage(source, layout, archetype),
                Vector3.zero,
                Vector3.one,
                Quaternion.identity,
                usesPackSurface ? CreatePackSurfaceMaterial(packSurface) : _fuselageMaterial);
        }
        else
        {
            BuildFallbackFuselage(layout);
        }

        BuildTransportCockpit(layout, archetype);

        if (!showPassengerWindows)
        {
            BuildCargoWindowBlankingPanels(layout.FuselageLength, layout.FuselageRadius);
        }
        else if (showPassengerWindows && !usesPackSurface)
        {
            BuildWindowSkinPanels("SourceWindowSkin", layout.FuselageLength, layout.FuselageRadius);
            BuildPassengerWindows(parameters, layout, archetype);
        }
        if (!usesPackSurface)
        {
            BuildLivery(layout, archetype);
        }
    }

    private Material CreatePackSurfaceMaterial(Material? source)
    {
        if (source == null)
        {
            return _fuselageMaterial;
        }

        var material = new Material(source) { hideFlags = HideFlags.HideAndDontSave };
        var requestedColor = ReadMaterialColor(_fuselageMaterial);
        var tint = Color.Lerp(Color.white, requestedColor, 0.34f);
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", tint);
        }
        else if (material.HasProperty("_Color"))
        {
            material.color = tint;
        }

        _ownedMaterials.Add(material);
        return material;
    }

    private void BuildSingleSeatFuselage(AircraftPreviewLayout layout, EngineType engineType)
    {
        var isJet = engineType is EngineType.Turbojet or EngineType.Turbofan;
        if (isJet)
        {
            BuildSingleSeatJetFuselage(layout);
            return;
        }

        BuildSingleSeatPistonFuselage(layout);
    }

    private void BuildSingleSeatPistonFuselage(AircraftPreviewLayout layout)
    {
        var length = layout.FuselageLength;
        var radius = layout.FuselageRadius;
        CreateProfiledLoftPart(
            "Fuselage",
            new[]
            {
                -length * 0.50f,
                -length * 0.44f,
                -length * 0.34f,
                -length * 0.18f,
                length * 0.02f,
                length * 0.18f,
                length * 0.27f,
            },
            new[]
            {
                -radius * 0.02f,
                -radius * 0.04f,
                -radius * 0.07f,
                -radius * 0.07f,
                -radius * 0.02f,
                radius * 0.01f,
                0f,
            },
            new[]
            {
                radius * 0.12f,
                radius * 0.34f,
                radius * 0.56f,
                radius * 0.74f,
                radius * 0.84f,
                radius * 0.90f,
                radius * 0.82f,
            },
            new[]
            {
                radius * 0.11f,
                radius * 0.32f,
                radius * 0.54f,
                radius * 0.76f,
                radius * 0.94f,
                radius,
                radius * 0.92f,
            },
            0f,
            _fuselageMaterial,
            RadialSegments);
    }

    private void BuildSingleSeatJetFuselage(AircraftPreviewLayout layout)
    {
        var length = layout.FuselageLength;
        var radius = layout.FuselageRadius;
        CreateProfiledLoftPart(
            "Fuselage",
            new[]
            {
                -length * 0.50f,
                -length * 0.46f,
                -length * 0.38f,
                -length * 0.24f,
                length * 0.02f,
                length * 0.24f,
                length * 0.39f,
                length * 0.47f,
                length * 0.50f,
            },
            new[]
            {
                0f,
                -radius * 0.01f,
                -radius * 0.02f,
                -radius * 0.03f,
                0f,
                radius * 0.02f,
                -radius * 0.01f,
                -radius * 0.02f,
                -radius * 0.02f,
            },
            new[]
            {
                radius * 0.36f,
                radius * 0.47f,
                radius * 0.64f,
                radius * 0.82f,
                radius,
                radius * 0.96f,
                radius * 0.84f,
                radius * 0.67f,
                radius * 0.58f,
            },
            new[]
            {
                radius * 0.34f,
                radius * 0.45f,
                radius * 0.63f,
                radius * 0.84f,
                radius,
                radius * 0.98f,
                radius * 0.88f,
                radius * 0.70f,
                radius * 0.60f,
            },
            0f,
            _fuselageMaterial,
            RadialSegments);
    }

    private static Color ReadMaterialColor(Material material)
    {
        if (material.HasProperty("_BaseColor"))
        {
            return material.GetColor("_BaseColor");
        }

        return material.HasProperty("_Color") ? material.color : Color.white;
    }

    private Mesh CreateStationDeformedFuselage(
        Mesh source,
        AircraftPreviewLayout layout,
        AircraftPreviewArchetype archetype)
    {
        var mesh = UnityEngine.Object.Instantiate(source);
        mesh.name = $"{source.name}_{archetype}_StationDeformed";
        mesh.hideFlags = HideFlags.HideAndDontSave;
        var vertices = mesh.vertices;
        var bounds = source.bounds;
        var tailLength = Mathf.Clamp(
            layout.FuselageRadius * (archetype == AircraftPreviewArchetype.SingleSeat ? 3.1f : 3.5f),
            layout.FuselageLength * 0.17f,
            layout.FuselageLength * 0.27f);
        var noseLength = Mathf.Clamp(
            layout.FuselageRadius * (archetype == AircraftPreviewArchetype.SingleSeat ? 3.2f : 2.7f),
            layout.FuselageLength * 0.18f,
            layout.FuselageLength * 0.30f);
        var cabinStartX = (-layout.FuselageLength * 0.5f) + tailLength;
        var cabinEndX = (layout.FuselageLength * 0.5f) - noseLength;
        const float sourceTailJoin = 0.25f;
        const float sourceNoseJoin = 0.72f;
        var heightScale = archetype == AircraftPreviewArchetype.SingleSeat ? 0.78f : 0.88f;

        for (var index = 0; index < vertices.Length; index++)
        {
            var sourceU = Mathf.InverseLerp(bounds.min.x, bounds.max.x, vertices[index].x);
            var x = sourceU switch
            {
                <= sourceTailJoin => Mathf.Lerp(
                    -layout.FuselageLength * 0.5f,
                    cabinStartX,
                    sourceU / sourceTailJoin),
                >= sourceNoseJoin => Mathf.Lerp(
                    cabinEndX,
                    layout.FuselageLength * 0.5f,
                    (sourceU - sourceNoseJoin) / (1f - sourceNoseJoin)),
                _ => Mathf.Lerp(
                    cabinStartX,
                    cabinEndX,
                    (sourceU - sourceTailJoin) / (sourceNoseJoin - sourceTailJoin)),
            };
            var normalizedY = Mathf.InverseLerp(bounds.min.y, bounds.max.y, vertices[index].y) * 2f - 1f;
            var normalizedZ = Mathf.InverseLerp(bounds.min.z, bounds.max.z, vertices[index].z) * 2f - 1f;
            vertices[index] = new Vector3(
                x,
                normalizedY * layout.FuselageRadius * heightScale,
                normalizedZ * layout.FuselageRadius);
        }

        mesh.vertices = vertices;
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        _meshes.Add(mesh);
        return mesh;
    }

    private void BuildFallbackFuselage(AircraftPreviewLayout layout)
    {
        var length = layout.FuselageLength;
        CreateLoftPart(
            "Fuselage",
            new[] { -length * 0.5f, -length * 0.42f, -length * 0.28f, length * 0.24f, length * 0.39f, length * 0.5f },
            new[] { 0.12f, 0.72f, 1f, 1f, 0.74f, 0.08f },
            new[] { 0.12f, 0.78f, 1f, 1f, 0.76f, 0.08f },
            Vector3.zero,
            _fuselageMaterial,
            layout.FuselageRadius * 0.88f,
            layout.FuselageRadius,
            RadialSegments);
    }

    private void BuildSingleSeatCanopy(AircraftPreviewLayout layout, EngineType engineType)
    {
        var isJet = engineType is EngineType.Turbojet or EngineType.Turbofan;
        var length = layout.FuselageLength;
        var radius = layout.FuselageRadius;
        var x = isJet
            ? new[] { length * 0.00f, length * 0.035f, length * 0.11f, length * 0.20f, length * 0.27f, length * 0.30f }
            : new[] { -length * 0.12f, -length * 0.08f, length * 0.01f, length * 0.09f, length * 0.15f, length * 0.17f };
        var halfWidths = isJet
            ? new[] { radius * 0.10f, radius * 0.50f, radius * 0.68f, radius * 0.66f, radius * 0.48f, radius * 0.08f }
            : new[] { radius * 0.08f, radius * 0.45f, radius * 0.60f, radius * 0.58f, radius * 0.42f, radius * 0.06f };
        var heights = isJet
            ? new[] { radius * 0.05f, radius * 0.50f, radius * 0.92f, radius * 0.88f, radius * 0.58f, radius * 0.04f }
            : new[] { radius * 0.04f, radius * 0.48f, radius * 0.78f, radius * 0.80f, radius * 0.55f, radius * 0.04f };
        var baseY = isJet ? radius * 0.54f : radius * 0.49f;
        CreateProfiledCanopyPart("CockpitCanopy", x, halfWidths, heights, baseY, _glassMaterial, 10);

        var sillCenterX = (x[0] + x[^1]) * 0.5f;
        var sillLength = x[^1] - x[0];
        for (var sideIndex = 0; sideIndex < 2; sideIndex++)
        {
            var side = sideIndex == 0 ? -1f : 1f;
            CreateRoundedPanel(
                side < 0f ? "CockpitSillLeft" : "CockpitSillRight",
                new Vector3(sillCenterX, baseY, side * radius * 0.58f),
                sillLength * 0.94f,
                radius * 0.13f,
                side * 0.025f,
                _fuselageMaterial,
                12);
        }
    }

    private void BuildTransportCockpit(AircraftPreviewLayout layout, AircraftPreviewArchetype archetype)
    {
        var length = Mathf.Clamp(layout.FuselageLength * 0.16f, 0.72f, 1.30f);
        var centerX = layout.FuselageLength * 0.38f;
        CreateCanopyPart(
            "CockpitCanopy",
            centerX,
            layout.FuselageRadius * 0.42f,
            length,
            layout.FuselageRadius * 0.72f,
            layout.FuselageRadius * (archetype == AircraftPreviewArchetype.Commuter ? 0.46f : 0.34f),
            _glassMaterial);
    }

    private void BuildPassengerWindows(
        AirframeParameters parameters,
        AircraftPreviewLayout layout,
        AircraftPreviewArchetype archetype)
    {
        var length = layout.FuselageLength;
        var radius = layout.FuselageRadius;
        var requestedRows = archetype == AircraftPreviewArchetype.Transport
            ? parameters.CrewCount + 5
            : parameters.CrewCount + 2;
        var windowCount = Mathf.Clamp(requestedRows, 4, 18);
        var firstX = length * 0.23f;
        var lastX = -length * 0.25f;
        var windowWidth = Mathf.Clamp(length / (windowCount * 4.0f), 0.085f, 0.125f);
        for (var index = 0; index < windowCount; index++)
        {
            var x = Mathf.Lerp(firstX, lastX, windowCount == 1 ? 0f : index / (float)(windowCount - 1));
            CreateCabinWindow(index, x, radius, -1f, windowWidth);
            CreateCabinWindow(index, x, radius, 1f, windowWidth);
        }

    }

    private void CreateCabinWindow(int index, float x, float radius, float side, float width)
    {
        CreateRoundedPanel(
            $"CabinWindow_{index}_{(side < 0f ? "L" : "R")}",
            new Vector3(x, radius * 0.18f, side * radius * 0.985f),
            width,
            radius * 0.23f,
            side * 0.018f,
            _glassMaterial,
            10);
    }

    private void BuildCabinDoor(string name, float x, float radius, float side)
    {
        CreateRoundedPanel(
            $"{name}_{(side < 0f ? "L" : "R")}",
            new Vector3(x, -radius * 0.02f, side * radius * 0.99f),
            radius * 0.34f,
            radius * 0.72f,
            side * 0.012f,
            _darkMaterial,
            12);
        CreateRoundedPanel(
            $"{name}Inset_{(side < 0f ? "L" : "R")}",
            new Vector3(x, -radius * 0.02f, side * radius * 1.002f),
            radius * 0.29f,
            radius * 0.65f,
            side * 0.012f,
            _fuselageMaterial,
            12);
    }

    private void BuildCargoWindowBlankingPanels(float fuselageLength, float radius)
    {
        for (var sideIndex = 0; sideIndex < 2; sideIndex++)
        {
            var side = sideIndex == 0 ? -1f : 1f;
            CreateRoundedPanel(
                side < 0f ? "CargoWindowBlankLeft" : "CargoWindowBlankRight",
                new Vector3(-fuselageLength * 0.01f, radius * 0.36f, side * radius * 1.025f),
                fuselageLength * 0.56f,
                radius * 0.46f,
                side * 0.020f,
                _fuselageMaterial,
                14);
        }
    }

    private void BuildWindowSkinPanels(string prefix, float fuselageLength, float radius)
    {
        for (var sideIndex = 0; sideIndex < 2; sideIndex++)
        {
            var side = sideIndex == 0 ? -1f : 1f;
            CreateRoundedPanel(
                $"{prefix}_{(side < 0f ? "L" : "R")}",
                new Vector3(-fuselageLength * 0.015f, radius * 0.36f, side * radius * 1.028f),
                fuselageLength * 0.59f,
                radius * 0.46f,
                side * 0.020f,
                _fuselageMaterial,
                16);
        }
    }

    private void BuildLivery(AircraftPreviewLayout layout, AircraftPreviewArchetype archetype)
    {
        var stripeLength = layout.FuselageLength * (archetype == AircraftPreviewArchetype.SingleSeat ? 0.52f : 0.66f);
        for (var sideIndex = 0; sideIndex < 2; sideIndex++)
        {
            var side = sideIndex == 0 ? -1f : 1f;
            CreateRoundedPanel(
                side < 0f ? "FuselageStripeLeft" : "FuselageStripeRight",
                new Vector3(-layout.FuselageLength * 0.015f, -layout.FuselageRadius * 0.20f, side * layout.FuselageRadius * 0.995f),
                stripeLength,
                Mathf.Clamp(layout.FuselageRadius * 0.10f, 0.035f, 0.075f),
                side * 0.014f,
                _accentMaterial,
                14);
        }
    }

    private void BuildWings(
        AirframeParameters parameters,
        AircraftPreviewLayout layout,
        AircraftPreviewArchetype archetype)
    {
        var wingY = parameters.WingPosition switch
        {
            WingPosition.High => layout.FuselageRadius * 0.54f,
            WingPosition.Low => -layout.FuselageRadius * 0.50f,
            WingPosition.Biplane => -layout.FuselageRadius * 0.16f,
            _ => 0f,
        };
        var tipChordRatio = AircraftPreviewLayout.ResolveTipChordRatio(parameters.WingShape);
        var sweepRatio = parameters.WingShape switch
        {
            WingShape.Straight => 0.035f,
            WingShape.Swept => 0.23f,
            WingShape.Delta => 0.40f,
            _ => 0.035f,
        };
        var mainWingChord = ResolveMainWingChord(parameters, layout);
        var rootX = archetype == AircraftPreviewArchetype.SingleSeat
            ? -mainWingChord * 0.04f
            : -mainWingChord * 0.08f;
        var thickness = ResolveWingThickness(mainWingChord);
        var dihedral = parameters.WingPosition == WingPosition.High ? -0.045f : 0.095f;
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
        BuildWingRootFairing("MainWingRootFairing", rootX, wingY, mainWingChord, thickness, layout.FuselageRadius);

        if (parameters.WingPosition == WingPosition.Biplane)
        {
            BuildWingPair(
                "UpperWing",
                rootX - (mainWingChord * 0.04f),
                wingY + (layout.FuselageRadius * 1.50f),
                ResolveUpperWingRootOffset(layout),
                layout.Span * 0.47f,
                mainWingChord * 0.90f,
                ResolveUpperWingTipChordRatio(parameters.WingShape),
                sweepRatio * 0.65f,
                thickness * 0.88f,
                0.055f,
                _wingMaterial);
            BuildBiplaneStruts(layout, wingY);
        }

        if (parameters.HasLeadingEdgeFlaps || parameters.FlapType == FlapType.LeadingEdge)
        {
            CreateBoxPart(
                "LeadingEdgeAccent",
                new Vector3(rootX + (mainWingChord * 0.43f), wingY + (thickness * 0.55f), 0f),
                new Vector3(0.055f, 0.025f, layout.Span * 0.64f),
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
            ? 0.035f
            : Mathf.Max(0.46f, AircraftPreviewLayout.ResolveTipChordRatio(wingShape));

    private static float ResolveWingThickness(float rootChord) =>
        Mathf.Clamp(rootChord * 0.11f, 0.045f, 0.22f);

    private static float ResolveWingRootOffset(AircraftPreviewLayout layout) =>
        Mathf.Min(layout.FuselageRadius * 0.68f, layout.Span * 0.21f);

    private static float ResolveUpperWingRootOffset(AircraftPreviewLayout layout) =>
        Mathf.Min(layout.FuselageRadius * 0.58f, layout.Span * 0.19f);

    private void BuildWingRootFairing(
        string name,
        float rootX,
        float rootY,
        float chord,
        float thickness,
        float fuselageRadius)
    {
        CreateLoftPart(
            name,
            new[] { -chord * 0.46f, -chord * 0.31f, chord * 0.28f, chord * 0.48f },
            new[] { 0.05f, 0.82f, 1f, 0.05f },
            new[] { 0.05f, 1f, 0.90f, 0.05f },
            new Vector3(rootX, rootY, 0f),
            _fuselageMaterial,
            thickness * 0.92f,
            fuselageRadius * 1.10f,
            16);
    }

    private void BuildBiplaneStruts(AircraftPreviewLayout layout, float lowerWingY)
    {
        var height = layout.FuselageRadius * 1.50f;
        var z = layout.Span * 0.27f;
        CreateCylinderBetween("BiplaneStrutLeft", new Vector3(0f, lowerWingY, -z), new Vector3(-0.08f, lowerWingY + height, -z), 0.022f, _darkMaterial);
        CreateCylinderBetween("BiplaneStrutRight", new Vector3(0f, lowerWingY, z), new Vector3(-0.08f, lowerWingY + height, z), 0.022f, _darkMaterial);
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
        var spanProgress = new[] { 0f, 0.34f, 0.72f, 1f };
        var vertices = new Vector3[spanProgress.Length * WingAirfoilSegments];
        for (var station = 0; station < spanProgress.Length; station++)
        {
            var progress = spanProgress[station];
            var easedProgress = progress * progress * (3f - (2f * progress));
            var chord = rootChord * Mathf.Lerp(1f, tipChordRatio, easedProgress);
            var centerX = rootX - (halfSpan * sweepRatio * progress);
            var centerY = rootY + (halfSpan * dihedralRatio * 0.12f * progress);
            var z = side * Mathf.Lerp(rootOffset, halfSpan, progress);
            var localThickness = thickness * Mathf.Lerp(1f, 0.38f, progress);
            for (var segment = 0; segment < WingAirfoilSegments; segment++)
            {
                var angle = (Mathf.PI * 2f * segment) / WingAirfoilSegments;
                var chordPosition = Mathf.Cos(angle) * chord * 0.5f;
                var thicknessProfile = Mathf.Sin(angle) * localThickness * 0.5f;
                vertices[(station * WingAirfoilSegments) + segment] = new Vector3(
                    centerX + chordPosition,
                    centerY + thicknessProfile,
                    z);
            }
        }

        var triangles = new List<int>();
        for (var station = 0; station < spanProgress.Length - 1; station++)
        {
            for (var segment = 0; segment < WingAirfoilSegments; segment++)
            {
                var next = (segment + 1) % WingAirfoilSegments;
                var a = (station * WingAirfoilSegments) + segment;
                var b = (station * WingAirfoilSegments) + next;
                var c = ((station + 1) * WingAirfoilSegments) + segment;
                var d = ((station + 1) * WingAirfoilSegments) + next;
                triangles.Add(a);
                triangles.Add(b);
                triangles.Add(c);
                triangles.Add(b);
                triangles.Add(d);
                triangles.Add(c);
            }
        }

        CapRing(triangles, 0, WingAirfoilSegments, reverse: side < 0f);
        CapRing(triangles, (spanProgress.Length - 1) * WingAirfoilSegments, WingAirfoilSegments, reverse: side > 0f);
        CreateMeshPart(name, vertices, triangles.ToArray(), material);
    }

    private static void CapRing(List<int> triangles, int start, int count, bool reverse)
    {
        for (var index = 1; index < count - 1; index++)
        {
            if (reverse)
            {
                triangles.Add(start);
                triangles.Add(start + index + 1);
                triangles.Add(start + index);
            }
            else
            {
                triangles.Add(start);
                triangles.Add(start + index);
                triangles.Add(start + index + 1);
            }
        }
    }

    private void BuildTail(TailType tailType, AircraftPreviewLayout layout, AircraftPreviewArchetype archetype)
    {
        var tailX = -layout.FuselageLength * 0.39f;
        var tailSpan = Mathf.Clamp(layout.Span * 0.28f, 1.35f, 3.0f);
        var tailChord = Mathf.Clamp(layout.WingChord * 0.58f, 0.5f, 1.25f);
        var finHeight = Mathf.Clamp(layout.FuselageRadius * (archetype == AircraftPreviewArchetype.SingleSeat ? 2.5f : 2.25f), 0.9f, 1.85f);
        BuildTailRootFairing(tailX, layout.FuselageRadius, tailChord);
        switch (tailType)
        {
            case TailType.Classic:
                BuildHorizontalTail(tailX, layout.FuselageRadius * 0.16f, tailSpan, tailChord);
                BuildVerticalFin("ClassicFin", tailX, layout.FuselageRadius * 0.06f, 0f, finHeight, tailChord * 0.95f);
                break;
            case TailType.TTail:
                BuildVerticalFin("TFin", tailX, layout.FuselageRadius * 0.02f, 0f, finHeight * 1.18f, tailChord);
                BuildHorizontalTail(tailX - (tailChord * 0.08f), layout.FuselageRadius * 0.02f + (finHeight * 1.08f), tailSpan, tailChord);
                break;
            case TailType.VTail:
                BuildVTail(tailX, layout.FuselageRadius * 0.12f, tailSpan * 0.62f, tailChord);
                break;
            case TailType.Twin:
                BuildHorizontalTail(tailX, layout.FuselageRadius * 0.14f, tailSpan, tailChord);
                BuildVerticalFin("TwinFinLeft", tailX, layout.FuselageRadius * 0.14f, -tailSpan * 0.38f, finHeight * 0.82f, tailChord * 0.75f);
                BuildVerticalFin("TwinFinRight", tailX, layout.FuselageRadius * 0.14f, tailSpan * 0.38f, finHeight * 0.82f, tailChord * 0.75f);
                break;
        }
    }

    private void BuildTailRootFairing(float tailX, float radius, float chord)
    {
        CreateLoftPart(
            "TailRootFairing",
            new[] { -chord * 0.55f, -chord * 0.25f, chord * 0.35f, chord * 0.55f },
            new[] { 0.04f, 0.62f, 0.82f, 0.04f },
            new[] { 0.04f, 0.72f, 0.82f, 0.04f },
            new Vector3(tailX, radius * 0.10f, 0f),
            _fuselageMaterial,
            radius * 0.32f,
            radius * 0.55f,
            14);
    }

    private void BuildHorizontalTail(float rootX, float y, float span, float chord)
    {
        BuildWingPair("HorizontalTail", rootX, y, 0.045f, span * 0.5f, chord, 0.42f, 0.20f, Mathf.Max(0.045f, chord * 0.075f), 0.04f, _wingMaterial);
    }

    private void BuildVerticalFin(string name, float rootX, float rootY, float z, float height, float chord)
    {
        var polygon = new[]
        {
            new Vector3(rootX + (chord * 0.48f), rootY, z),
            new Vector3(rootX + (chord * 0.12f), rootY + (height * 0.82f), z),
            new Vector3(rootX - (chord * 0.16f), rootY + height, z),
            new Vector3(rootX - (chord * 0.48f), rootY + (height * 0.08f), z),
        };
        CreateExtrudedPolygonPart(name, polygon, Vector3.forward * Mathf.Clamp(chord * 0.08f, 0.055f, 0.11f), _wingMaterial);
    }

    private void BuildVTail(float rootX, float rootY, float halfSpan, float chord)
    {
        for (var sideIndex = 0; sideIndex < 2; sideIndex++)
        {
            var side = sideIndex == 0 ? -1f : 1f;
            var root = new Vector3(rootX, rootY, side * 0.04f);
            var tip = new Vector3(rootX - (chord * 0.20f), rootY + (halfSpan * 0.72f), side * halfSpan);
            var forward = Vector3.right * (chord * 0.5f);
            CreateExtrudedPolygonPart(
                side < 0f ? "VTailLeft" : "VTailRight",
                new[] { root + forward, tip + (forward * 0.40f), tip - (forward * 0.40f), root - forward },
                Vector3.up * 0.065f,
                _wingMaterial);
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
                BuildCenterlineJet(bypassRatio, layout, 0, true);
            }
            else
            {
                BuildNosePropellerEngine(moduleName, layout, 0, true);
            }

            return;
        }

        var stations = CalculateWingEngineStations(engineCount);
        var mainWingChord = ResolveMainWingChord(parameters, layout);
        for (var index = 0; index < stations.Length; index++)
        {
            var station = stations[index];
            var z = station * layout.Span * 0.5f;
            var x = (mainWingChord * 0.34f) - (Mathf.Abs(station) * layout.Span * 0.033f);
            var scale = isJet ? 0.94f : 1.02f;
            var engineRadius = ResolveEngineRadius(moduleName, bypassRatio) * scale;
            var wingBottomY = CalculateWingBottomY(parameters, layout, station);
            var position = new Vector3(x, wingBottomY - (engineRadius * 0.82f), z);
            BuildEngineModule(moduleName, engineType, bypassRatio, position, scale, index);
            BuildEnginePylon(index, position, engineRadius, wingBottomY);
        }

        if ((engineCount % 2) != 0)
        {
            if (isJet)
            {
                BuildCenterlineJet(bypassRatio, layout, engineCount - 1, false);
            }
            else
            {
                BuildNosePropellerEngine(moduleName, layout, engineCount - 1, false);
            }
        }
    }

    private void BuildNosePropellerEngine(
        string moduleName,
        AircraftPreviewLayout layout,
        int index,
        bool isSingleEngine)
    {
        if (isSingleEngine)
        {
            BuildSingleSeatPistonNose(moduleName, layout, index);
            return;
        }

        var isRadial = moduleName == "Radial";
        var cowlingLength = isRadial
            ? Mathf.Clamp(layout.FuselageRadius * 2.0f, 0.70f, 1.32f)
            : Mathf.Clamp(layout.FuselageRadius * 3.35f, 1.12f, 2.05f);
        var radiusY = layout.FuselageRadius * (isRadial ? 0.86f : 0.67f);
        var radiusZ = layout.FuselageRadius * (isRadial ? 0.90f : 0.80f);
        var noseX = layout.FuselageLength * 0.5f;
        var center = new Vector3(noseX - (cowlingLength * 0.28f), -layout.FuselageRadius * 0.04f, 0f);
        CreateLoftPart(
            $"NoseCowling_{index}",
            new[] { -cowlingLength * 0.58f, -cowlingLength * 0.38f, -cowlingLength * 0.06f, cowlingLength * 0.30f, cowlingLength * 0.52f },
            new[] { 0.90f, 1f, 0.96f, 0.82f, 0.58f },
            new[] { 0.94f, 1f, 0.98f, 0.86f, 0.60f },
            center,
            _fuselageMaterial,
            radiusY,
            radiusZ,
            RadialSegments);

        if (!isRadial)
        {
            CreateRoundedPanel(
                $"NoseCowlingTop_{index}",
                new Vector3(center.x - (cowlingLength * 0.05f), center.y + (radiusY * 0.96f), 0f),
                cowlingLength * 0.72f,
                radiusZ * 0.74f,
                0.018f,
                _darkMaterial,
                14,
                panelInHorizontalPlane: true);
            CreateLoftPart(
                $"NoseCoolingIntake_{index}",
                new[] { -0.06f, 0.06f },
                new[] { 0.82f, 1f },
                new[] { 0.82f, 1f },
                new Vector3(center.x + (cowlingLength * 0.30f), center.y - (radiusY * 0.57f), 0f),
                _darkMaterial,
                radiusY * 0.16f,
                radiusZ * 0.36f,
                12);
        }

        var aircraftSize = Mathf.Sqrt(layout.WingArea);
        var propellerRadius = Mathf.Clamp(0.24f + (aircraftSize * 0.19f), layout.FuselageRadius * 0.92f, 1.28f);
        BuildPropeller(center.x + (cowlingLength * 0.57f), center.y, 0f, propellerRadius, isRadial || moduleName == "V" ? 3 : 2, index);
    }

    private void BuildSingleSeatPistonNose(
        string moduleName,
        AircraftPreviewLayout layout,
        int index)
    {
        var isRadial = moduleName == "Radial";
        var length = layout.FuselageLength;
        var radius = layout.FuselageRadius;
        var rearJoinX = length * (isRadial ? 0.20f : 0.16f);
        var noseX = length * 0.50f;
        var centerY = -radius * 0.015f;
        CreateProfiledLoftPart(
            $"NoseCowling_{index}",
            new[]
            {
                rearJoinX,
                length * 0.26f,
                length * 0.37f,
                length * 0.46f,
                noseX,
            },
            new[]
            {
                radius * 0.01f,
                radius * 0.02f,
                0f,
                -radius * 0.01f,
                centerY,
            },
            isRadial
                ? new[] { radius * 0.86f, radius * 0.92f, radius * 0.91f, radius * 0.78f, radius * 0.62f }
                : new[] { radius * 0.84f, radius * 0.86f, radius * 0.80f, radius * 0.69f, radius * 0.56f },
            isRadial
                ? new[] { radius * 0.91f, radius * 0.96f, radius * 0.95f, radius * 0.82f, radius * 0.65f }
                : new[] { radius * 0.96f, radius * 0.98f, radius * 0.90f, radius * 0.76f, radius * 0.58f },
            0f,
            _fuselageMaterial,
            RadialSegments);

        if (!isRadial)
        {
            CreateRoundedPanel(
                $"NoseCowlingTop_{index}",
                new Vector3(length * 0.34f, radius * 0.80f, 0f),
                length * 0.31f,
                radius * 1.22f,
                0.022f,
                _darkMaterial,
                14,
                panelInHorizontalPlane: true);
            CreateProfiledLoftPart(
                $"SinglePistonChinIntake_{index}",
                new[] { length * 0.25f, length * 0.31f, length * 0.38f },
                new[] { -radius * 0.67f, -radius * 0.74f, -radius * 0.69f },
                new[] { radius * 0.06f, radius * 0.14f, radius * 0.08f },
                new[] { radius * 0.20f, radius * 0.31f, radius * 0.22f },
                0f,
                _darkMaterial,
                10);
        }

        for (var sideIndex = 0; sideIndex < 2; sideIndex++)
        {
            var side = sideIndex == 0 ? -1f : 1f;
            CreateRoundedPanel(
                side < 0f ? $"PistonExhaustStackLeft_{index}" : $"PistonExhaustStackRight_{index}",
                new Vector3(length * 0.31f, radius * 0.22f, side * radius * 0.88f),
                length * 0.15f,
                radius * 0.10f,
                side * 0.026f,
                _engineMaterial,
                10);
        }

        var aircraftSize = Mathf.Sqrt(layout.WingArea);
        var propellerRadius = Mathf.Clamp(
            0.24f + (aircraftSize * 0.19f),
            radius * 0.92f,
            1.28f);
        BuildPropeller(
            noseX + 0.035f,
            centerY,
            0f,
            propellerRadius,
            isRadial || moduleName == "V" ? 3 : 2,
            index);
    }

    private void BuildCenterlineJet(double bypassRatio, AircraftPreviewLayout layout, int index, bool isSingleEngine)
    {
        var bypassScale = Mathf.InverseLerp(0f, 20f, (float)bypassRatio);
        var standardIntakeRadius = Mathf.Clamp(layout.FuselageRadius * Mathf.Lerp(0.58f, 0.66f, bypassScale), 0.22f, 0.54f);
        var intakeRadius = standardIntakeRadius * (isSingleEngine ? 0.70f : 1f);
        var exhaustRadius = standardIntakeRadius * 0.66f;
        var noseX = layout.FuselageLength * 0.5f;
        var tailX = -layout.FuselageLength * 0.5f;
        if (isSingleEngine)
        {
            BuildSingleSeatJetFlowPath(
                layout,
                index,
                intakeRadius,
                exhaustRadius,
                noseX,
                tailX);
            return;
        }

        var fairingLength = Mathf.Clamp(layout.FuselageRadius * (isSingleEngine ? 1.10f : 1.40f), 0.42f, 0.88f);
        var alongX = Quaternion.Euler(0f, 0f, 90f);

        CreateLoftPart(
            $"CenterJetIntakeFairing_{index}",
            new[] { -fairingLength * 0.62f, -fairingLength * 0.24f, fairingLength * 0.18f, fairingLength * 0.44f },
            new[] { 1.34f, 1.20f, 1.05f, 1f },
            new[] { 1.34f, 1.20f, 1.05f, 1f },
            new Vector3(noseX - (fairingLength * 0.42f), -layout.FuselageRadius * 0.02f, 0f),
            _fuselageMaterial,
            intakeRadius,
            intakeRadius,
            RadialSegments);
        CreateCylinderPart($"CenterJetIntakeLip_{index}", new Vector3(noseX + 0.012f, -layout.FuselageRadius * 0.02f, 0f), intakeRadius, 0.050f, alongX, _engineMaterial, RadialSegments);
        CreateCylinderPart($"CenterJetIntake_{index}", new Vector3(noseX + 0.040f, -layout.FuselageRadius * 0.02f, 0f), intakeRadius * 0.80f, 0.055f, alongX, _darkMaterial, RadialSegments);

        var exhaustFairingLength = Mathf.Clamp(layout.FuselageRadius * 1.45f, 0.58f, 1.12f);
        var exhaustCenterX = tailX + (exhaustFairingLength * 0.43f);
        CreateLoftPart(
            $"CenterJetExhaustFairing_{index}",
            new[] { -exhaustFairingLength * 0.43f, -exhaustFairingLength * 0.12f, exhaustFairingLength * 0.28f, exhaustFairingLength * 0.57f },
            new[] { 1f, 1.10f, 1.28f, 1.45f },
            new[] { 1f, 1.10f, 1.28f, 1.45f },
            new Vector3(exhaustCenterX, 0f, 0f),
            _fuselageMaterial,
            exhaustRadius,
            exhaustRadius,
            RadialSegments);
        CreateCylinderPart($"CenterJetExhaustLip_{index}", new Vector3(tailX - 0.008f, 0f, 0f), exhaustRadius, 0.045f, alongX, _engineMaterial, RadialSegments);
        CreateCylinderPart($"CenterJetExhaust_{index}", new Vector3(tailX - 0.032f, 0f, 0f), exhaustRadius * 0.76f, 0.05f, alongX, _darkMaterial, RadialSegments);
    }

    private void BuildSingleSeatJetFlowPath(
        AircraftPreviewLayout layout,
        int index,
        float intakeRadius,
        float exhaustRadius,
        float noseX,
        float tailX)
    {
        var radius = layout.FuselageRadius;
        var intakeCenterY = -radius * 0.02f;
        var fairingLength = Mathf.Clamp(layout.FuselageLength * 0.12f, 0.38f, 0.62f);
        CreateProfiledLoftPart(
            $"CenterJetIntakeFairing_{index}",
            new[]
            {
                noseX - fairingLength,
                noseX - (fairingLength * 0.64f),
                noseX - (fairingLength * 0.26f),
                noseX,
            },
            new[] { -radius * 0.01f, -radius * 0.015f, intakeCenterY, intakeCenterY },
            new[] { radius * 0.82f, radius * 0.72f, radius * 0.58f, intakeRadius * 1.04f },
            new[] { radius * 0.86f, radius * 0.75f, radius * 0.60f, intakeRadius * 1.04f },
            0f,
            _fuselageMaterial,
            RadialSegments);

        var alongX = Quaternion.Euler(0f, 0f, 90f);
        CreateCylinderPart(
            $"CenterJetIntakeLip_{index}",
            new Vector3(noseX + 0.012f, intakeCenterY, 0f),
            intakeRadius,
            0.050f,
            alongX,
            _engineMaterial,
            RadialSegments);
        CreateCylinderPart(
            $"CenterJetIntake_{index}",
            new Vector3(noseX + 0.038f, intakeCenterY, 0f),
            intakeRadius * 0.80f,
            0.055f,
            alongX,
            _darkMaterial,
            RadialSegments);

        var exhaustFairingLength = Mathf.Clamp(layout.FuselageLength * 0.14f, 0.45f, 0.72f);
        CreateProfiledLoftPart(
            $"CenterJetExhaustFairing_{index}",
            new[]
            {
                tailX,
                tailX + (exhaustFairingLength * 0.24f),
                tailX + (exhaustFairingLength * 0.67f),
                tailX + exhaustFairingLength,
            },
            new[] { 0f, 0f, -radius * 0.01f, -radius * 0.02f },
            new[] { exhaustRadius, radius * 0.44f, radius * 0.58f, radius * 0.68f },
            new[] { exhaustRadius, radius * 0.43f, radius * 0.57f, radius * 0.66f },
            0f,
            _fuselageMaterial,
            RadialSegments);
        CreateCylinderPart(
            $"CenterJetExhaustLip_{index}",
            new Vector3(tailX - 0.008f, 0f, 0f),
            exhaustRadius,
            0.045f,
            alongX,
            _engineMaterial,
            RadialSegments);
        CreateCylinderPart(
            $"CenterJetExhaust_{index}",
            new Vector3(tailX - 0.032f, 0f, 0f),
            exhaustRadius * 0.76f,
            0.050f,
            alongX,
            _darkMaterial,
            RadialSegments);
    }

    private static float ResolveEngineRadius(string moduleName, double bypassRatio) =>
        moduleName switch
        {
            "Radial" => 0.29f,
            "V" => 0.22f,
            "Turbojet" => 0.19f,
            "HighBypassTurbofan" => Mathf.Lerp(0.30f, 0.40f, Mathf.InverseLerp(10f, 20f, (float)bypassRatio)),
            "LowBypassTurbofan" => 0.25f,
            _ => 0.19f,
        };

    private static float CalculateWingBottomY(AirframeParameters parameters, AircraftPreviewLayout layout, float normalizedStation)
    {
        var wingY = parameters.WingPosition switch
        {
            WingPosition.High => layout.FuselageRadius * 0.54f,
            WingPosition.Low => -layout.FuselageRadius * 0.50f,
            WingPosition.Biplane => -layout.FuselageRadius * 0.16f,
            _ => 0f,
        };
        var station = Mathf.Abs(normalizedStation) * layout.Span * 0.5f;
        var progress = Mathf.InverseLerp(ResolveWingRootOffset(layout), layout.Span * 0.5f, station);
        var centerY = wingY + (layout.Span * 0.5f * (parameters.WingPosition == WingPosition.High ? -0.045f : 0.095f) * 0.12f * progress);
        return centerY - (ResolveWingThickness(ResolveMainWingChord(parameters, layout)) * 0.5f);
    }

    private void BuildEnginePylon(int index, Vector3 enginePosition, float engineRadius, float wingBottomY)
    {
        var topY = wingBottomY + 0.018f;
        var nacelleTopY = enginePosition.y + (engineRadius * 0.82f);
        var thickness = Mathf.Clamp(engineRadius * 0.42f, 0.07f, 0.16f);
        CreateExtrudedPolygonPart(
            $"EnginePylon_{index}",
            new[]
            {
                new Vector3(enginePosition.x + (engineRadius * 0.75f), nacelleTopY, enginePosition.z - (thickness * 0.5f)),
                new Vector3(enginePosition.x + (engineRadius * 0.34f), topY, enginePosition.z - (thickness * 0.5f)),
                new Vector3(enginePosition.x - (engineRadius * 0.70f), topY, enginePosition.z - (thickness * 0.5f)),
                new Vector3(enginePosition.x - (engineRadius * 0.82f), nacelleTopY, enginePosition.z - (thickness * 0.5f)),
            },
            Vector3.forward * thickness,
            _engineMaterial);
        CreateLoftPart(
            $"PylonNacelleFairing_{index}",
            new[] { -engineRadius * 0.85f, -engineRadius * 0.42f, engineRadius * 0.48f, engineRadius * 0.82f },
            new[] { 0.05f, 0.85f, 1f, 0.05f },
            new[] { 0.05f, 1f, 0.92f, 0.05f },
            new Vector3(enginePosition.x, nacelleTopY, enginePosition.z),
            _engineMaterial,
            engineRadius * 0.20f,
            thickness * 0.75f,
            12);
    }

    private void BuildEngineModule(string moduleName, EngineType engineType, double bypassRatio, Vector3 position, float scale, int index)
    {
        switch (moduleName)
        {
            case "Radial":
                BuildRadialEngine(position, scale, index);
                break;
            case "V":
                BuildInlineEngine(position, scale, index, true);
                break;
            case "Turbojet":
                BuildJetEngine(position, scale, index, 0.19f, 1.02f, false);
                break;
            case "HighBypassTurbofan":
                BuildJetEngine(position, scale, index, Mathf.Lerp(0.30f, 0.40f, Mathf.InverseLerp(10f, 20f, (float)bypassRatio)), 1.08f, true);
                break;
            case "LowBypassTurbofan":
                BuildJetEngine(position, scale, index, 0.25f, 1.04f, true);
                break;
            default:
                BuildInlineEngine(position, scale, index, engineType == EngineType.V);
                break;
        }
    }

    private void BuildInlineEngine(Vector3 position, float scale, int index, bool wide)
    {
        var length = (wide ? 0.86f : 0.78f) * scale;
        var radiusY = (wide ? 0.23f : 0.19f) * scale;
        var radiusZ = (wide ? 0.25f : 0.18f) * scale;
        CreateLoftPart(
            $"InlineCowling_{index}",
            new[] { -length * 0.52f, -length * 0.38f, -length * 0.02f, length * 0.34f, length * 0.50f },
            new[] { 0.72f, 0.94f, 1f, 0.92f, 0.64f },
            new[] { 0.70f, 0.96f, 1f, 0.94f, 0.66f },
            position,
            _engineMaterial,
            radiusY,
            radiusZ,
            RadialSegments);
        BuildPistonNacelleTail(position, length, radiusY, radiusZ, scale, index);
        if (wide)
        {
            CreateLoftPart($"VBankLeft_{index}", new[] { -0.24f, 0.24f }, new[] { 0.92f, 0.74f }, new[] { 0.92f, 0.74f }, position + new Vector3(0f, radiusY * 0.66f, -radiusZ * 0.54f), _darkMaterial, radiusY * 0.40f, radiusZ * 0.32f, 8);
            CreateLoftPart($"VBankRight_{index}", new[] { -0.24f, 0.24f }, new[] { 0.92f, 0.74f }, new[] { 0.92f, 0.74f }, position + new Vector3(0f, radiusY * 0.66f, radiusZ * 0.54f), _darkMaterial, radiusY * 0.40f, radiusZ * 0.32f, 8);
        }

        BuildPropeller(position.x + (length * 0.54f), position.y, position.z, (wide ? 0.58f : 0.52f) * scale, wide ? 3 : 2, index);
    }

    private void BuildRadialEngine(Vector3 position, float scale, int index)
    {
        var length = 0.58f * scale;
        var radius = 0.30f * scale;
        CreateLoftPart(
            $"RadialCowling_{index}",
            new[] { -length * 0.50f, -length * 0.34f, length * 0.26f, length * 0.48f },
            new[] { 0.76f, 1f, 1f, 0.80f },
            new[] { 0.76f, 1f, 1f, 0.80f },
            position,
            _engineMaterial,
            radius,
            radius,
            RadialSegments);
        BuildPistonNacelleTail(position, length, radius, radius, scale, index);
        var cylinderRadius = radius * 0.15f;
        for (var cylinderIndex = 0; cylinderIndex < 7; cylinderIndex++)
        {
            var angle = (Mathf.PI * 2f * cylinderIndex) / 7f;
            var center = position + new Vector3(length * 0.40f, Mathf.Cos(angle) * radius * 0.58f, Mathf.Sin(angle) * radius * 0.58f);
            CreateLoftPart($"RadialCylinder_{index}_{cylinderIndex}", new[] { -0.07f * scale, 0.07f * scale }, new[] { 0.85f, 0.85f }, new[] { 0.85f, 0.85f }, center, _darkMaterial, cylinderRadius, cylinderRadius, 8);
        }

        BuildPropeller(position.x + (length * 0.56f), position.y, position.z, 0.64f * scale, 3, index);
    }

    private void BuildPistonNacelleTail(Vector3 enginePosition, float cowlingLength, float radiusY, float radiusZ, float scale, int index)
    {
        var tailLength = 0.82f * scale;
        var rearX = enginePosition.x - (cowlingLength * 0.47f);
        CreateLoftPart(
            $"PistonNacelleTail_{index}",
            new[] { -tailLength * 0.58f, -tailLength * 0.24f, tailLength * 0.18f, tailLength * 0.50f },
            new[] { 0.10f, 0.48f, 0.82f, 1f },
            new[] { 0.10f, 0.48f, 0.84f, 1f },
            new Vector3(rearX - (tailLength * 0.42f), enginePosition.y, enginePosition.z),
            _engineMaterial,
            radiusY,
            radiusZ,
            14);
    }

    private void BuildJetEngine(Vector3 position, float scale, int index, float radius, float length, bool showFan)
    {
        radius *= scale;
        length *= scale;
        CreateLoftPart(
            $"JetNacelle_{index}",
            new[] { -length * 0.55f, -length * 0.43f, -length * 0.16f, length * 0.18f, length * 0.40f, length * 0.52f },
            new[] { 0.58f, 0.74f, 0.90f, 1f, 0.96f, 0.82f },
            new[] { 0.58f, 0.74f, 0.90f, 1f, 0.96f, 0.82f },
            position,
            _engineMaterial,
            radius,
            radius,
            RadialSegments);
        var alongX = Quaternion.Euler(0f, 0f, 90f);
        CreateLoftPart(
            $"JetIntakeLip_{index}",
            new[] { -0.045f, 0f, 0.045f },
            new[] { 0.93f, 1.05f, 0.96f },
            new[] { 0.93f, 1.05f, 0.96f },
            position + new Vector3(length * 0.53f, 0f, 0f),
            _engineMaterial,
            radius,
            radius,
            RadialSegments);
        CreateCylinderPart($"JetIntake_{index}", position + new Vector3(length * 0.56f, 0f, 0f), radius * 0.72f, 0.035f, alongX, _darkMaterial, RadialSegments);
        CreateCylinderPart($"JetExhaust_{index}", position - new Vector3(length * 0.57f, 0f, 0f), radius * 0.50f, 0.035f, alongX, _darkMaterial, RadialSegments);
        if (showFan)
        {
            BuildFan(position.x + (length * 0.575f), position.y, position.z, radius * 0.61f, index);
        }
    }

    private void BuildPropeller(float x, float y, float z, float radius, int bladeCount, int index)
    {
        CreateLoftPart($"PropHub_{index}", new[] { -0.11f, 0f, 0.16f }, new[] { 0.78f, 1f, 0.10f }, new[] { 0.78f, 1f, 0.10f }, new Vector3(x, y, z), _accentMaterial, radius * 0.16f, radius * 0.16f, 10);
        if (_packPropeller != null && bladeCount >= 3)
        {
            CreateSharedMeshPart($"PackPropeller_{index}", _packPropeller, new Vector3(x + 0.01f, y, z), Vector3.one * radius, Quaternion.identity, _darkMaterial);
            return;
        }

        BuildRotorBlades($"PropBlade_{index}", x + 0.01f, y, z, radius, bladeCount, 0.09f, _darkMaterial);
    }

    private void BuildFan(float x, float y, float z, float radius, int index)
    {
        if (_packFan != null)
        {
            CreateSharedMeshPart($"PackFan_{index}", _packFan, new Vector3(x, y, z), Vector3.one * radius, Quaternion.identity, _accentMaterial);
        }
        else
        {
            BuildRotorBlades($"FanBlade_{index}", x, y, z, radius, 12, 0.025f, _accentMaterial);
        }

        CreateLoftPart($"FanHub_{index}", new[] { -0.04f, 0.05f }, new[] { 1f, 0.52f }, new[] { 1f, 0.52f }, new Vector3(x + 0.025f, y, z), _darkMaterial, radius * 0.19f, radius * 0.19f, 10);
    }

    private void BuildRotorBlades(string name, float x, float y, float z, float radius, int bladeCount, float bladeWidth, Material material)
    {
        for (var bladeIndex = 0; bladeIndex < bladeCount; bladeIndex++)
        {
            var angle = (Mathf.PI * 2f * bladeIndex) / bladeCount;
            var radial = new Vector3(0f, Mathf.Cos(angle), Mathf.Sin(angle));
            var tangent = new Vector3(0f, -Mathf.Sin(angle), Mathf.Cos(angle));
            var center = new Vector3(x, y, z);
            CreateExtrudedPolygonPart(
                $"{name}_{bladeIndex}",
                new[]
                {
                    center + (radial * radius * 0.16f) - (tangent * bladeWidth),
                    center + (radial * radius * 0.93f) - (tangent * bladeWidth * 0.42f),
                    center + (radial * radius) + (tangent * bladeWidth * 0.34f),
                    center + (radial * radius * 0.18f) + (tangent * bladeWidth),
                },
                Vector3.right * 0.018f,
                material);
        }
    }

    private void BuildLandingGear(LandingGearType gearType, AircraftPreviewLayout layout, AircraftPreviewArchetype archetype)
    {
        var bellyY = -layout.FuselageRadius * 0.82f;
        var track = Mathf.Max(layout.FuselageRadius * 1.45f, 0.58f);
        if (gearType == LandingGearType.Retractable)
        {
            CreateRoundedPanel("GearDoorLeft", new Vector3(-0.04f, bellyY, -track * 0.72f), 0.50f, 0.17f, 0.018f, _darkMaterial, 10, panelInHorizontalPlane: true);
            CreateRoundedPanel("GearDoorRight", new Vector3(-0.04f, bellyY, track * 0.72f), 0.50f, 0.17f, 0.018f, _darkMaterial, 10, panelInHorizontalPlane: true);
            return;
        }

        var extension = gearType == LandingGearType.Fixed ? 0.58f : 0.38f;
        var wheelRadius = Mathf.Clamp(layout.FuselageRadius * 0.28f, 0.13f, 0.23f);
        var wheelY = bellyY - extension;
        var mainX = -layout.FuselageLength * 0.04f;
        BuildGearLeg("MainGearLeft", new Vector3(mainX, bellyY, -track * 0.48f), new Vector3(mainX + 0.08f, wheelY, -track), wheelRadius, gearType == LandingGearType.Fixed, archetype);
        BuildGearLeg("MainGearRight", new Vector3(mainX, bellyY, track * 0.48f), new Vector3(mainX + 0.08f, wheelY, track), wheelRadius, gearType == LandingGearType.Fixed, archetype);

        var noseX = layout.FuselageLength * 0.34f;
        BuildGearLeg("NoseGear", new Vector3(noseX, bellyY, 0f), new Vector3(noseX + 0.03f, wheelY + (extension * 0.10f), 0f), wheelRadius * 0.72f, false, archetype);
    }

    private void BuildGearLeg(
        string name,
        Vector3 top,
        Vector3 wheelCenter,
        float wheelRadius,
        bool addFairing,
        AircraftPreviewArchetype archetype)
    {
        CreateCylinderBetween(name + "Strut", top, wheelCenter + (Vector3.up * wheelRadius * 0.52f), Mathf.Max(0.018f, wheelRadius * 0.13f), _engineMaterial);
        var wheelMesh = archetype is AircraftPreviewArchetype.Transport or AircraftPreviewArchetype.Regional
            ? _packWheelLarge
            : _packWheelSmall;
        if (wheelMesh != null)
        {
            CreateSharedMeshPart(name + "Wheel", wheelMesh, wheelCenter, Vector3.one * wheelRadius, Quaternion.identity, _darkMaterial);
        }
        else
        {
            CreateCylinderPart(name + "Wheel", wheelCenter, wheelRadius, wheelRadius * 0.42f, Quaternion.Euler(90f, 0f, 0f), _darkMaterial, 14);
        }

        if (name.StartsWith("MainGear", StringComparison.Ordinal) && archetype == AircraftPreviewArchetype.Transport)
        {
            var side = Mathf.Sign(wheelCenter.z);
            CreateSharedMeshPart(name + "WheelOuter", wheelMesh!, wheelCenter + new Vector3(0f, 0f, side * wheelRadius * 0.62f), Vector3.one * wheelRadius, Quaternion.identity, _darkMaterial);
        }

        if (addFairing)
        {
            CreateLoftPart(name + "Fairing", new[] { -0.24f, -0.06f, 0.20f }, new[] { 0.25f, 1f, 0.55f }, new[] { 0.45f, 1f, 0.62f }, wheelCenter + new Vector3(0f, wheelRadius * 0.35f, 0f), _fuselageMaterial, wheelRadius * 0.88f, wheelRadius * 0.48f, 10);
        }
    }

    private void CreateCanopyPart(string name, float centerX, float baseY, float length, float halfWidth, float height, Material material)
    {
        const int longitudinalStations = 7;
        const int arcSegments = 10;
        var vertices = new Vector3[longitudinalStations * (arcSegments + 1)];
        for (var station = 0; station < longitudinalStations; station++)
        {
            var progress = station / (float)(longitudinalStations - 1);
            var longitudinal = progress * 2f - 1f;
            var envelope = Mathf.Pow(Mathf.Max(0f, 1f - (longitudinal * longitudinal)), 0.62f);
            var x = centerX + (longitudinal * length * 0.5f);
            for (var segment = 0; segment <= arcSegments; segment++)
            {
                var angle = Mathf.PI * segment / arcSegments;
                vertices[(station * (arcSegments + 1)) + segment] = new Vector3(
                    x,
                    baseY + (Mathf.Sin(angle) * height * envelope),
                    Mathf.Cos(angle) * halfWidth * envelope);
            }
        }

        var triangles = new List<int>();
        for (var station = 0; station < longitudinalStations - 1; station++)
        {
            for (var segment = 0; segment < arcSegments; segment++)
            {
                var a = (station * (arcSegments + 1)) + segment;
                var b = a + 1;
                var c = ((station + 1) * (arcSegments + 1)) + segment;
                var d = c + 1;
                triangles.Add(a); triangles.Add(b); triangles.Add(c);
                triangles.Add(b); triangles.Add(d); triangles.Add(c);
            }
        }

        CreateMeshPart(name, vertices, triangles.ToArray(), material);
    }

    private void CreateProfiledCanopyPart(
        string name,
        IReadOnlyList<float> x,
        IReadOnlyList<float> halfWidths,
        IReadOnlyList<float> heights,
        float baseY,
        Material material,
        int arcSegments)
    {
        if (x.Count != halfWidths.Count || x.Count != heights.Count)
        {
            throw new ArgumentException("Canopy profile station arrays must have equal lengths.");
        }

        var vertices = new Vector3[x.Count * (arcSegments + 1)];
        for (var station = 0; station < x.Count; station++)
        {
            for (var segment = 0; segment <= arcSegments; segment++)
            {
                var angle = Mathf.PI * segment / arcSegments;
                vertices[(station * (arcSegments + 1)) + segment] = new Vector3(
                    x[station],
                    baseY + (Mathf.Sin(angle) * heights[station]),
                    Mathf.Cos(angle) * halfWidths[station]);
            }
        }

        var triangles = new List<int>((x.Count - 1) * arcSegments * 6);
        for (var station = 0; station < x.Count - 1; station++)
        {
            for (var segment = 0; segment < arcSegments; segment++)
            {
                var a = (station * (arcSegments + 1)) + segment;
                var b = a + 1;
                var c = ((station + 1) * (arcSegments + 1)) + segment;
                var d = c + 1;
                triangles.Add(a); triangles.Add(b); triangles.Add(c);
                triangles.Add(b); triangles.Add(d); triangles.Add(c);
            }
        }

        CreateMeshPart(name, vertices, triangles.ToArray(), material);
    }

    private void CreateRoundedPanel(
        string name,
        Vector3 center,
        float width,
        float height,
        float extrusion,
        Material material,
        int segments,
        bool panelInHorizontalPlane = false)
    {
        var polygon = new Vector3[segments];
        for (var index = 0; index < segments; index++)
        {
            var angle = (Mathf.PI * 2f * index) / segments;
            var localA = Mathf.Cos(angle) * width * 0.5f;
            var localB = Mathf.Sin(angle) * height * 0.5f;
            polygon[index] = panelInHorizontalPlane
                ? center + new Vector3(localA, 0f, localB)
                : center + new Vector3(localA, localB, 0f);
        }

        CreateExtrudedPolygonPart(name, polygon, panelInHorizontalPlane ? Vector3.up * extrusion : Vector3.forward * extrusion, material);
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
        for (var ring = 0; ring < x.Count; ring++)
        {
            for (var segment = 0; segment < segments; segment++)
            {
                var angle = (Mathf.PI * 2f * segment) / segments;
                vertices[(ring * segments) + segment] = center + new Vector3(
                    x[ring],
                    Mathf.Cos(angle) * radius * radiusYScale[ring],
                    Mathf.Sin(angle) * radiusZ * radiusZScale[ring]);
            }
        }

        var triangleIndex = 0;
        for (var ring = 0; ring < x.Count - 1; ring++)
        {
            for (var segment = 0; segment < segments; segment++)
            {
                var next = (segment + 1) % segments;
                var a = (ring * segments) + segment;
                var b = (ring * segments) + next;
                var c = ((ring + 1) * segments) + segment;
                var d = ((ring + 1) * segments) + next;
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

    private void CreateProfiledLoftPart(
        string name,
        IReadOnlyList<float> x,
        IReadOnlyList<float> centerY,
        IReadOnlyList<float> radiusY,
        IReadOnlyList<float> radiusZ,
        float centerZ,
        Material material,
        int segments)
    {
        if (x.Count != centerY.Count || x.Count != radiusY.Count || x.Count != radiusZ.Count)
        {
            throw new ArgumentException("Loft profile station arrays must have equal lengths.");
        }

        var vertices = new Vector3[x.Count * segments];
        var triangles = new int[(x.Count - 1) * segments * 6];
        for (var ring = 0; ring < x.Count; ring++)
        {
            for (var segment = 0; segment < segments; segment++)
            {
                var angle = (Mathf.PI * 2f * segment) / segments;
                vertices[(ring * segments) + segment] = new Vector3(
                    x[ring],
                    centerY[ring] + (Mathf.Cos(angle) * radiusY[ring]),
                    centerZ + (Mathf.Sin(angle) * radiusZ[ring]));
            }
        }

        var triangleIndex = 0;
        for (var ring = 0; ring < x.Count - 1; ring++)
        {
            for (var segment = 0; segment < segments; segment++)
            {
                var next = (segment + 1) % segments;
                var a = (ring * segments) + segment;
                var b = (ring * segments) + next;
                var c = ((ring + 1) * segments) + segment;
                var d = ((ring + 1) * segments) + next;
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
        var vertices = new Vector3[count * 2];
        var halfExtrusion = extrusion * 0.5f;
        for (var index = 0; index < count; index++)
        {
            vertices[index] = polygon[index] - halfExtrusion;
            vertices[count + index] = polygon[index] + halfExtrusion;
        }

        var triangles = new List<int>((count - 2) * 6 + (count * 6));
        for (var index = 1; index < count - 1; index++)
        {
            triangles.Add(0); triangles.Add(index + 1); triangles.Add(index);
            triangles.Add(count); triangles.Add(count + index); triangles.Add(count + index + 1);
        }

        for (var index = 0; index < count; index++)
        {
            var next = (index + 1) % count;
            triangles.Add(index); triangles.Add(next); triangles.Add(count + index);
            triangles.Add(next); triangles.Add(count + next); triangles.Add(count + index);
        }

        CreateMeshPart(name, vertices, triangles.ToArray(), material);
    }

    private void CreateCylinderBetween(string name, Vector3 start, Vector3 end, float radius, Material material)
    {
        var direction = end - start;
        var part = CreatePrimitivePart(
            PrimitiveType.Cylinder,
            name,
            (start + end) * 0.5f,
            new Vector3(radius * 2f, direction.magnitude * 0.5f, radius * 2f),
            Quaternion.FromToRotation(Vector3.up, direction.normalized),
            material);
        part.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction.normalized);
    }

    private void CreateBoxPart(string name, Vector3 position, Vector3 scale, Quaternion rotation, Material material) =>
        CreatePrimitivePart(PrimitiveType.Cube, name, position, scale, rotation, material);

    private void CreateCylinderPart(string name, Vector3 position, float radius, float length, Quaternion rotation, Material material, int ignoredSegments) =>
        CreatePrimitivePart(PrimitiveType.Cylinder, name, position, new Vector3(radius * 2f, length * 0.5f, radius * 2f), rotation, material);

    private GameObject CreatePrimitivePart(
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

        part.GetComponent<Renderer>().sharedMaterial = material;
        _parts.Add(part);
        return part;
    }

    private void CreateMeshPart(string name, Vector3[] vertices, int[] triangles, Material material)
    {
        var mesh = new Mesh { name = name + "Mesh", hideFlags = HideFlags.HideAndDontSave };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        _meshes.Add(mesh);
        var part = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave, layer = _layer };
        part.transform.SetParent(_root, false);
        part.AddComponent<MeshFilter>().sharedMesh = mesh;
        part.AddComponent<MeshRenderer>().sharedMaterial = material;
        _parts.Add(part);
    }

    private void CreateSharedMeshPart(string name, Mesh mesh, Vector3 position, Vector3 scale, Quaternion rotation, Material material)
    {
        var part = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave, layer = _layer };
        part.transform.SetParent(_root, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        part.transform.localRotation = rotation;
        part.AddComponent<MeshFilter>().sharedMesh = mesh;
        part.AddComponent<MeshRenderer>().sharedMaterial = material;
        _parts.Add(part);
    }
}
