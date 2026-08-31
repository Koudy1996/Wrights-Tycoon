using System;
using System.Globalization;
using UnityEngine;
using WrightTycoon.Core.Airframes;
using WrightTycoon.Core.Engines;

namespace WrightTycoon.Game.UI;

public sealed class AircraftPreview3DRenderer : IDisposable
{
    private const int PreviewLayer = 31;
    private const float RotationSensitivity = 0.48f;
    private static readonly Quaternion DefaultModelRotation = Quaternion.Euler(6f, -12f, 0f);

    private GameObject? _root;
    private Transform? _modelRoot;
    private Camera? _camera;
    private Light? _keyLight;
    private Light? _fillLight;
    private RenderTexture? _renderTexture;
    private AircraftPreviewModuleFactory? _moduleFactory;

    private Material? _fuselageMaterial;
    private Material? _wingMaterial;
    private Material? _engineMaterial;
    private Material? _accentMaterial;
    private Material? _darkMaterial;
    private Material? _glassMaterial;

    private AircraftPreviewLayout _layout;
    private string _lastSignature = string.Empty;
    private Quaternion _modelRotation = DefaultModelRotation;

    internal Quaternion ModelRotation => _modelRotation;

    public bool Draw(
        Rect rect,
        AirframeParameters parameters,
        EngineType engineType,
        double bypassRatio,
        bool showPassengerWindows = true)
    {
        if (rect.width < 16f || rect.height < 16f)
        {
            return false;
        }

        EnsureInitialized((int)rect.width, (int)rect.height);
        if (_camera is null || _renderTexture is null || _modelRoot is null || _moduleFactory is null)
        {
            return false;
        }

        var resetRect = GetResetButtonRect(rect);
        HandleRotationInput(rect, resetRect);

        var signature = $"{BuildSignature(parameters, engineType, bypassRatio)}|{showPassengerWindows}";
        if (!string.Equals(signature, _lastSignature, StringComparison.Ordinal))
        {
            UpdateMaterialPalette(parameters.MaterialType);
            _layout = _moduleFactory.Build(parameters, engineType, bypassRatio, showPassengerWindows);
            _lastSignature = signature;
        }

        _modelRoot.localRotation = _modelRotation;
        ConfigureCamera();

        if (Event.current.type == EventType.Repaint)
        {
            _camera.targetTexture = _renderTexture;
            _camera.Render();
            GUI.DrawTexture(rect, _renderTexture, ScaleMode.StretchToFill, false);
            DrawRotationHint(rect);
        }

        if (GUI.Button(resetRect, "Reset view"))
        {
            ResetView();
        }

        return true;
    }

    public void Dispose()
    {
        _moduleFactory?.Dispose();
        _moduleFactory = null;

        if (_renderTexture != null)
        {
            UnityEngine.Object.DestroyImmediate(_renderTexture);
            _renderTexture = null;
        }

        DestroyMaterial(ref _fuselageMaterial);
        DestroyMaterial(ref _wingMaterial);
        DestroyMaterial(ref _engineMaterial);
        DestroyMaterial(ref _accentMaterial);
        DestroyMaterial(ref _darkMaterial);
        DestroyMaterial(ref _glassMaterial);

        if (_root != null)
        {
            UnityEngine.Object.DestroyImmediate(_root);
            _root = null;
            _modelRoot = null;
            _camera = null;
            _keyLight = null;
            _fillLight = null;
        }

        _lastSignature = string.Empty;
        _modelRotation = DefaultModelRotation;
    }

    private void EnsureInitialized(int width, int height)
    {
        if (_root == null)
        {
            _root = new GameObject("WT03_AircraftPreview3DRoot")
            {
                hideFlags = HideFlags.HideAndDontSave,
            };

            var model = new GameObject("ModularAircraft")
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            model.transform.SetParent(_root.transform, false);
            _modelRoot = model.transform;

            var cameraObject = new GameObject("PreviewCamera")
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            cameraObject.transform.SetParent(_root.transform, false);
            _camera = cameraObject.AddComponent<Camera>();
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.025f, 0.063f, 0.09f, 1f);
            _camera.fieldOfView = 31f;
            _camera.nearClipPlane = 0.05f;
            _camera.farClipPlane = 100f;
            _camera.cullingMask = 1 << PreviewLayer;
            _camera.enabled = false;
            _camera.allowHDR = true;

            _keyLight = CreateDirectionalLight(
                "PreviewKeyLight",
                new Color(0.92f, 0.97f, 1f, 1f),
                1.25f,
                Quaternion.Euler(34f, 136f, 0f));
            _fillLight = CreateDirectionalLight(
                "PreviewFillLight",
                new Color(0.28f, 0.58f, 0.94f, 1f),
                0.56f,
                Quaternion.Euler(318f, 302f, 0f));

            InitializeMaterials();
            if (_modelRoot != null
                && _fuselageMaterial != null
                && _wingMaterial != null
                && _engineMaterial != null
                && _accentMaterial != null
                && _darkMaterial != null
                && _glassMaterial != null)
            {
                _moduleFactory = new AircraftPreviewModuleFactory(
                    _modelRoot,
                    PreviewLayer,
                    _fuselageMaterial,
                    _wingMaterial,
                    _engineMaterial,
                    _accentMaterial,
                    _darkMaterial,
                    _glassMaterial);
            }
        }

        EnsureRenderTexture(width, height);
    }

    private Light CreateDirectionalLight(string name, Color color, float intensity, Quaternion rotation)
    {
        var lightObject = new GameObject(name)
        {
            hideFlags = HideFlags.HideAndDontSave,
        };
        lightObject.transform.SetParent(_root!.transform, false);
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = intensity;
        light.color = color;
        light.transform.rotation = rotation;
        light.cullingMask = 1 << PreviewLayer;
        return light;
    }

    private void InitializeMaterials()
    {
        _fuselageMaterial = CreateMaterial(new Color(0.74f, 0.81f, 0.9f, 1f), 0.38f, 0.54f);
        _wingMaterial = CreateMaterial(new Color(0.62f, 0.72f, 0.84f, 1f), 0.30f, 0.52f);
        _engineMaterial = CreateMaterial(new Color(0.46f, 0.52f, 0.60f, 1f), 0.62f, 0.56f);
        _accentMaterial = CreateMaterial(new Color(0.08f, 0.42f, 0.78f, 1f), 0.24f, 0.55f);
        _darkMaterial = CreateMaterial(new Color(0.035f, 0.052f, 0.069f, 1f), 0.16f, 0.28f);
        _glassMaterial = CreateMaterial(new Color(0.055f, 0.18f, 0.28f, 1f), 0.34f, 0.82f);
    }

    private void UpdateMaterialPalette(MaterialType materialType)
    {
        if (_fuselageMaterial == null || _wingMaterial == null)
        {
            return;
        }

        var fuselageColor = materialType switch
        {
            MaterialType.WoodFabric => new Color(0.72f, 0.53f, 0.32f, 1f),
            MaterialType.Aluminum => new Color(0.74f, 0.81f, 0.9f, 1f),
            MaterialType.Composite => new Color(0.48f, 0.61f, 0.76f, 1f),
            _ => new Color(0.74f, 0.81f, 0.9f, 1f),
        };
        var wingColor = materialType == MaterialType.WoodFabric
            ? new Color(0.58f, 0.39f, 0.23f, 1f)
            : new Color(0.62f, 0.72f, 0.84f, 1f);

        SetMaterialColor(_fuselageMaterial, fuselageColor);
        SetMaterialColor(_wingMaterial, wingColor);
    }

    private void EnsureRenderTexture(int width, int height)
    {
        var targetWidth = Mathf.Clamp(width, 128, 2048);
        var targetHeight = Mathf.Clamp(height, 128, 2048);
        if (_renderTexture != null
            && _renderTexture.width == targetWidth
            && _renderTexture.height == targetHeight)
        {
            return;
        }

        if (_renderTexture != null)
        {
            UnityEngine.Object.DestroyImmediate(_renderTexture);
            _renderTexture = null;
        }

        _renderTexture = new RenderTexture(targetWidth, targetHeight, 24, RenderTextureFormat.ARGB32)
        {
            antiAliasing = 4,
            hideFlags = HideFlags.HideAndDontSave,
        };
        _renderTexture.Create();
    }

    private void ConfigureCamera()
    {
        if (_camera == null)
        {
            return;
        }

        var distance = Mathf.Max(
            6.8f,
            Mathf.Max(_layout.FuselageLength * 1.45f, _layout.Span * 1.12f));
        _camera.farClipPlane = Mathf.Max(100f, distance * 3f);
        _camera.transform.position = new Vector3(distance * 0.50f, distance * 0.28f, -distance);
        _camera.transform.LookAt(new Vector3(0f, 0.05f, 0f));
    }

    private void HandleRotationInput(Rect rect, Rect resetRect)
    {
        var currentEvent = Event.current;
        var controlId = GUIUtility.GetControlID("WT03AircraftPreviewRotation".GetHashCode(), FocusType.Passive, rect);
        switch (currentEvent.GetTypeForControl(controlId))
        {
            case EventType.MouseDown when currentEvent.button == 0
                && rect.Contains(currentEvent.mousePosition)
                && !resetRect.Contains(currentEvent.mousePosition):
                GUIUtility.hotControl = controlId;
                currentEvent.Use();
                break;

            case EventType.MouseDrag when GUIUtility.hotControl == controlId:
                RotateBy(currentEvent.delta);
                currentEvent.Use();
                break;

            case EventType.MouseUp when GUIUtility.hotControl == controlId:
                GUIUtility.hotControl = 0;
                currentEvent.Use();
                break;
        }
    }

    internal void RotateBy(Vector2 mouseDelta)
    {
        var yaw = Quaternion.AngleAxis(-mouseDelta.x * RotationSensitivity, Vector3.up);
        var pitch = Quaternion.AngleAxis(-mouseDelta.y * RotationSensitivity, Vector3.forward);
        _modelRotation = (yaw * pitch * _modelRotation).normalized;
    }

    internal void ResetView()
    {
        _modelRotation = DefaultModelRotation;
    }

    private static Rect GetResetButtonRect(Rect rect) =>
        new(rect.x + 8f, rect.yMax - 30f, 96f, 22f);

    private static void DrawRotationHint(Rect rect)
    {
        var hintRect = new Rect(rect.xMax - 132f, rect.yMax - 27f, 124f, 20f);
        var previousColor = GUI.color;
        GUI.color = new Color(0.82f, 0.9f, 1f, 0.78f);
        GUI.Label(hintRect, "Drag to rotate");
        GUI.color = previousColor;
    }

    private static string BuildSignature(
        AirframeParameters parameters,
        EngineType engineType,
        double bypassRatio)
    {
        return string.Join(
            "|",
            parameters.EngineCount,
            parameters.MaterialType,
            parameters.StructureType,
            parameters.WingPosition,
            parameters.WingShape,
            parameters.TailType,
            parameters.LandingGearType,
            parameters.CrewCount,
            parameters.WingAspectRatio.ToString("0.00", CultureInfo.InvariantCulture),
            parameters.WingAreaSquareMeters.ToString("0.00", CultureInfo.InvariantCulture),
            parameters.FuelAmountLiters.ToString("0.0", CultureInfo.InvariantCulture),
            parameters.EngineCowling.ToString("0.00", CultureInfo.InvariantCulture),
            engineType,
            bypassRatio.ToString("0.00", CultureInfo.InvariantCulture));
    }

    private static Material CreateMaterial(Color color, float metallic, float smoothness)
    {
        var shader = Shader.Find("Standard")
            ?? Shader.Find("Universal Render Pipeline/Lit")
            ?? Shader.Find("Legacy Shaders/Diffuse");
        var material = new Material(shader)
        {
            hideFlags = HideFlags.HideAndDontSave,
        };
        ConfigureOpaque(material);
        SetMaterialColor(material, color);
        if (material.HasProperty("_Metallic"))
        {
            material.SetFloat("_Metallic", metallic);
        }

        if (material.HasProperty("_Glossiness"))
        {
            material.SetFloat("_Glossiness", smoothness);
        }

        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", smoothness);
        }

        return material;
    }

    private static void ConfigureOpaque(Material material)
    {
        if (material.HasProperty("_Mode"))
        {
            material.SetFloat("_Mode", 0f);
        }

        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 0f);
        }

        if (material.HasProperty("_ZWrite"))
        {
            material.SetFloat("_ZWrite", 1f);
        }

        material.DisableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = -1;
    }

    private static void SetMaterialColor(Material material, Color color)
    {
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }
        else if (material.HasProperty("_Color"))
        {
            material.color = color;
        }
    }

    private static void DestroyMaterial(ref Material? material)
    {
        if (material != null)
        {
            UnityEngine.Object.DestroyImmediate(material);
            material = null;
        }
    }
}
