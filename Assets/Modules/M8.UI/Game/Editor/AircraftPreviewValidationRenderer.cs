using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using WrightTycoon.Core.Airframes;
using WrightTycoon.Core.Engines;
using WrightTycoon.Game.UI;

namespace WrightTycoon.Game.Editor;

public static class AircraftPreviewValidationRenderer
{
    private const int Width = 640;
    private const int Height = 400;
    private const int PreviewLayer = 31;

    [MenuItem("WrightTycoon/Validation/Render Aircraft Preview Set")]
    public static void RenderFromMenu()
    {
        var outputDirectory = RenderPreviewSet();
        EditorUtility.RevealInFinder(outputDirectory);
    }

    public static void RenderAndExit()
    {
        try
        {
            RenderPreviewSet();
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static string RenderPreviewSet()
    {
        var projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
        var outputDirectory = Path.Combine(projectRoot, "Logs", "Workflow", "AircraftPreviewValidation");
        Directory.CreateDirectory(outputDirectory);

        var engineCases = new[]
        {
            new PreviewCase("engine-inline-1", EngineType.Inline, 0d, 1, TailType.Classic, WingPosition.Low, WingShape.Straight),
            new PreviewCase("engine-v-2", EngineType.V, 0d, 2, TailType.Classic, WingPosition.Low, WingShape.Straight),
            new PreviewCase("engine-radial-3", EngineType.Radial, 0d, 3, TailType.Classic, WingPosition.Low, WingShape.Straight),
            new PreviewCase("engine-center-jet-1", EngineType.Turbojet, 0d, 1, TailType.Classic, WingPosition.Low, WingShape.Swept),
            new PreviewCase("engine-center-jet-1-side", EngineType.Turbojet, 0d, 1, TailType.Classic, WingPosition.Low, WingShape.Swept, sideView: true),
            new PreviewCase("engine-turbojet-4", EngineType.Turbojet, 0d, 4, TailType.Classic, WingPosition.Low, WingShape.Swept),
            new PreviewCase("engine-turbofan-low-5", EngineType.Turbofan, 6d, 5, TailType.Classic, WingPosition.Low, WingShape.Swept),
            new PreviewCase("engine-turbofan-high-6", EngineType.Turbofan, 16d, 6, TailType.Classic, WingPosition.Low, WingShape.Swept),
            new PreviewCase("cargo-no-windows", EngineType.Inline, 0d, 2, TailType.Classic, WingPosition.High, WingShape.Straight, showPassengerWindows: false),
        };
        var tailCases = new[]
        {
            new PreviewCase("tail-classic", EngineType.Radial, 0d, 2, TailType.Classic, WingPosition.High, WingShape.Straight),
            new PreviewCase("tail-t", EngineType.Turbofan, 12d, 2, TailType.TTail, WingPosition.Low, WingShape.Swept),
            new PreviewCase("tail-v", EngineType.Inline, 0d, 1, TailType.VTail, WingPosition.Low, WingShape.Straight),
            new PreviewCase("tail-twin-biplane", EngineType.Radial, 0d, 2, TailType.Twin, WingPosition.Biplane, WingShape.Straight),
            new PreviewCase("wing-aspect-4", EngineType.Turbojet, 0d, 2, TailType.Classic, WingPosition.Low, WingShape.Straight, 4d),
            new PreviewCase("wing-aspect-20", EngineType.Turbojet, 0d, 2, TailType.Classic, WingPosition.Low, WingShape.Straight, 20d),
            new PreviewCase("wing-delta", EngineType.Turbojet, 0d, 2, TailType.Classic, WingPosition.Low, WingShape.Delta, 8d),
        };
        var comparisonCases = new[]
        {
            new PreviewCase("comparison-single-piston", EngineType.Inline, 0d, 1, TailType.Classic, WingPosition.Low, WingShape.Straight, 6d, 22d, crewCount: 2, fuelAmountLiters: 620d),
            new PreviewCase("comparison-single-piston-side", EngineType.Radial, 0d, 1, TailType.Classic, WingPosition.Low, WingShape.Straight, 6d, 26d, crewCount: 2, fuelAmountLiters: 700d, sideView: true),
            new PreviewCase("comparison-single-jet", EngineType.Turbojet, 0d, 1, TailType.Classic, WingPosition.Low, WingShape.Swept, 4.8d, 25d, crewCount: 2, fuelAmountLiters: 820d),
            new PreviewCase("comparison-single-jet-side", EngineType.Turbojet, 0d, 1, TailType.Classic, WingPosition.Low, WingShape.Swept, 4.8d, 25d, crewCount: 2, fuelAmountLiters: 820d, sideView: true),
            new PreviewCase("comparison-commuter-turboprop-2", EngineType.Inline, 0d, 2, TailType.TTail, WingPosition.High, WingShape.Straight, 10d, 48d, crewCount: 5, fuelAmountLiters: 1_400d),
            new PreviewCase("comparison-regional-jet-2", EngineType.Turbofan, 8d, 2, TailType.TTail, WingPosition.Low, WingShape.Swept, 9d, 72d, crewCount: 8, fuelAmountLiters: 2_800d),
            new PreviewCase("comparison-transport-jet-4", EngineType.Turbofan, 14d, 4, TailType.Classic, WingPosition.Low, WingShape.Swept, 9d, 145d, crewCount: 14, fuelAmountLiters: 7_200d),
            new PreviewCase("comparison-transport-cargo-4", EngineType.Turbofan, 14d, 4, TailType.Classic, WingPosition.Low, WingShape.Swept, 9d, 145d, showPassengerWindows: false, crewCount: 14, fuelAmountLiters: 7_200d),
        };

        foreach (var previewCase in Combine(engineCases, tailCases))
        {
            var png = Render(previewCase);
            File.WriteAllBytes(Path.Combine(outputDirectory, previewCase.FileName + ".png"), png);
        }

        foreach (var previewCase in comparisonCases)
        {
            var png = Render(previewCase);
            File.WriteAllBytes(Path.Combine(outputDirectory, previewCase.FileName + ".png"), png);
        }

        RenderSourcePackReferences(outputDirectory);

        Debug.Log($"Aircraft preview validation renders written to: {outputDirectory}");
        return outputDirectory;
    }

    private static byte[] Render(PreviewCase previewCase)
    {
        var root = new GameObject("AircraftPreviewValidationRoot")
        {
            hideFlags = HideFlags.HideAndDontSave,
        };
        var model = new GameObject("Model")
        {
            hideFlags = HideFlags.HideAndDontSave,
        };
        model.transform.SetParent(root.transform, false);
        model.transform.rotation = previewCase.SideView
            ? Quaternion.identity
            : Quaternion.Euler(7f, -14f, 0f);

        var materials = CreateMaterials();
        var factory = new AircraftPreviewModuleFactory(
            model.transform,
            PreviewLayer,
            materials[0],
            materials[1],
            materials[2],
            materials[3],
            materials[4],
            materials[5]);

        var parameters = new AirframeParameters
        {
            EngineCount = previewCase.EngineCount,
            CrewCount = previewCase.CrewCount,
            WingAspectRatio = previewCase.WingAspectRatio,
            WingAreaSquareMeters = previewCase.WingAreaSquareMeters,
            FuelAmountLiters = previewCase.FuelAmountLiters,
            WingPosition = previewCase.WingPosition,
            WingShape = previewCase.WingShape,
            TailType = previewCase.TailType,
            LandingGearType = LandingGearType.Fixed,
            MaterialType = MaterialType.Aluminum,
        };
        var layout = factory.Build(
            parameters,
            previewCase.EngineType,
            previewCase.BypassRatio,
            previewCase.ShowPassengerWindows);

        var cameraObject = new GameObject("Camera") { hideFlags = HideFlags.HideAndDontSave };
        cameraObject.transform.SetParent(root.transform, false);
        var camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.025f, 0.063f, 0.09f, 1f);
        camera.fieldOfView = 31f;
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 100f;
        camera.cullingMask = 1 << PreviewLayer;
        camera.enabled = false;

        var distance = Mathf.Max(6.8f, Mathf.Max(layout.FuselageLength * 1.45f, layout.Span * 1.12f));
        camera.farClipPlane = Mathf.Max(100f, distance * 3f);
        camera.transform.position = previewCase.SideView
            ? new Vector3(0f, distance * 0.04f, -distance)
            : new Vector3(distance * 0.50f, distance * 0.28f, -distance);
        camera.transform.LookAt(new Vector3(0f, 0.05f, 0f));

        CreateLight(root.transform, "Key", new Color(0.92f, 0.97f, 1f), 1.25f, Quaternion.Euler(34f, 136f, 0f));
        CreateLight(root.transform, "Fill", new Color(0.28f, 0.58f, 0.94f), 0.56f, Quaternion.Euler(318f, 302f, 0f));

        var renderTexture = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
        {
            antiAliasing = 4,
            hideFlags = HideFlags.HideAndDontSave,
        };
        renderTexture.Create();
        camera.targetTexture = renderTexture;
        camera.Render();

        var previousActive = RenderTexture.active;
        RenderTexture.active = renderTexture;
        var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
        texture.ReadPixels(new Rect(0f, 0f, Width, Height), 0, 0);
        texture.Apply();
        var png = texture.EncodeToPNG();
        RenderTexture.active = previousActive;

        UnityEngine.Object.DestroyImmediate(texture);
        UnityEngine.Object.DestroyImmediate(renderTexture);
        factory.Dispose();
        foreach (var material in materials)
        {
            UnityEngine.Object.DestroyImmediate(material);
        }

        UnityEngine.Object.DestroyImmediate(root);
        return png;
    }

    private static Material[] CreateMaterials()
    {
        var shader = Shader.Find("Standard") ?? Shader.Find("Legacy Shaders/Diffuse");
        if (shader == null)
        {
            throw new InvalidOperationException("No preview shader is available.");
        }

        return new[]
        {
            CreateMaterial(shader, new Color(0.74f, 0.81f, 0.9f)),
            CreateMaterial(shader, new Color(0.62f, 0.72f, 0.84f)),
            CreateMaterial(shader, new Color(0.46f, 0.52f, 0.60f)),
            CreateMaterial(shader, new Color(0.08f, 0.42f, 0.78f)),
            CreateMaterial(shader, new Color(0.035f, 0.052f, 0.069f)),
            CreateMaterial(shader, new Color(0.055f, 0.18f, 0.28f)),
        };
    }

    private static void RenderSourcePackReferences(string outputDirectory)
    {
        for (var version = 1; version <= 3; version++)
        {
            var path = $"Assets/Polyeler/Simple Aircraft Pack/Prefabs/Passenger Aircraft V{version}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                throw new InvalidOperationException($"Source pack comparison prefab is missing: {path}");
            }

            File.WriteAllBytes(Path.Combine(outputDirectory, $"source-pack-v{version}.png"), RenderSourcePrefab(prefab, false));
            File.WriteAllBytes(Path.Combine(outputDirectory, $"source-pack-v{version}-side.png"), RenderSourcePrefab(prefab, true));
        }
    }

    private static byte[] RenderSourcePrefab(GameObject prefab, bool sideView)
    {
        var root = new GameObject("SourcePackReferenceRoot") { hideFlags = HideFlags.HideAndDontSave };
        var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject
            ?? throw new InvalidOperationException($"Could not instantiate source prefab '{prefab.name}'.");
        instance.hideFlags = HideFlags.HideAndDontSave;
        instance.transform.SetParent(root.transform, false);
        SetLayerRecursively(instance, PreviewLayer);
        var bounds = CalculateRendererBounds(instance);
        instance.transform.position -= bounds.center;
        bounds = CalculateRendererBounds(instance);
        var extent = Mathf.Max(bounds.extents.x, Mathf.Max(bounds.extents.y, bounds.extents.z));
        var distance = extent * (sideView ? 3.0f : 2.7f);

        var cameraObject = new GameObject("SourceCamera") { hideFlags = HideFlags.HideAndDontSave };
        cameraObject.transform.SetParent(root.transform, false);
        var camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.025f, 0.063f, 0.09f, 1f);
        camera.fieldOfView = 31f;
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = distance * 4f;
        camera.cullingMask = 1 << PreviewLayer;
        camera.enabled = false;
        camera.transform.position = sideView
            ? new Vector3(distance, extent * 0.08f, 0f)
            : new Vector3(distance * 0.74f, extent * 0.38f, distance * 0.72f);
        camera.transform.LookAt(Vector3.zero);
        CreateLight(root.transform, "SourceKey", new Color(0.95f, 0.98f, 1f), 1.35f, Quaternion.Euler(32f, 145f, 0f));
        CreateLight(root.transform, "SourceFill", new Color(0.35f, 0.55f, 0.9f), 0.55f, Quaternion.Euler(320f, 310f, 0f));

        var renderTexture = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
        {
            antiAliasing = 4,
            hideFlags = HideFlags.HideAndDontSave,
        };
        renderTexture.Create();
        camera.targetTexture = renderTexture;
        camera.Render();
        var previousActive = RenderTexture.active;
        RenderTexture.active = renderTexture;
        var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
        texture.ReadPixels(new Rect(0f, 0f, Width, Height), 0, 0);
        texture.Apply();
        var png = texture.EncodeToPNG();
        RenderTexture.active = previousActive;
        UnityEngine.Object.DestroyImmediate(texture);
        UnityEngine.Object.DestroyImmediate(renderTexture);
        UnityEngine.Object.DestroyImmediate(root);
        return png;
    }

    private static Bounds CalculateRendererBounds(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return new Bounds(Vector3.zero, Vector3.one);
        }

        var bounds = renderers[0].bounds;
        for (var index = 1; index < renderers.Length; index++)
        {
            bounds.Encapsulate(renderers[index].bounds);
        }

        return bounds;
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        root.layer = layer;
        foreach (Transform child in root.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    private static Material CreateMaterial(Shader shader, Color color)
    {
        var material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        material.color = color;
        if (material.HasProperty("_Glossiness"))
        {
            material.SetFloat("_Glossiness", 0.52f);
        }

        return material;
    }

    private static void CreateLight(Transform root, string name, Color color, float intensity, Quaternion rotation)
    {
        var lightObject = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
        lightObject.transform.SetParent(root, false);
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = intensity;
        light.color = color;
        light.transform.rotation = rotation;
        light.cullingMask = 1 << PreviewLayer;
    }

    private static IEnumerable<PreviewCase> Combine(PreviewCase[] first, PreviewCase[] second)
    {
        foreach (var value in first)
        {
            yield return value;
        }

        foreach (var value in second)
        {
            yield return value;
        }
    }

    private readonly struct PreviewCase
    {
        public PreviewCase(
            string fileName,
            EngineType engineType,
            double bypassRatio,
            int engineCount,
            TailType tailType,
            WingPosition wingPosition,
            WingShape wingShape,
            double wingAspectRatio = 8d,
            double wingAreaSquareMeters = 48d,
            bool showPassengerWindows = true,
            bool sideView = false,
            int crewCount = 4,
            double fuelAmountLiters = 1_400d)
        {
            FileName = fileName;
            EngineType = engineType;
            BypassRatio = bypassRatio;
            EngineCount = engineCount;
            TailType = tailType;
            WingPosition = wingPosition;
            WingShape = wingShape;
            WingAspectRatio = wingAspectRatio;
            WingAreaSquareMeters = wingAreaSquareMeters;
            ShowPassengerWindows = showPassengerWindows;
            SideView = sideView;
            CrewCount = crewCount;
            FuelAmountLiters = fuelAmountLiters;
        }

        public string FileName { get; }

        public EngineType EngineType { get; }

        public double BypassRatio { get; }

        public int EngineCount { get; }

        public TailType TailType { get; }

        public WingPosition WingPosition { get; }

        public WingShape WingShape { get; }

        public double WingAspectRatio { get; }

        public double WingAreaSquareMeters { get; }

        public bool ShowPassengerWindows { get; }

        public bool SideView { get; }

        public int CrewCount { get; }

        public double FuelAmountLiters { get; }
    }
}
