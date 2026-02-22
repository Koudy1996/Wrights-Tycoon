using System;
using System.Collections.Generic;
using UnityEngine;
using WrightTycoon.Core.Airframes;
using WrightTycoon.Core.Engines;

namespace WrightTycoon.Game.UI;

public sealed class AircraftPreview3DRenderer : IDisposable
{
    private const int PreviewLayer = 31;
    private const string PackResourcesPath = "WT03/Previews/AircraftPack";

    private GameObject? _root;
    private Transform? _modelRoot;
    private Camera? _camera;
    private Light? _keyLight;
    private RenderTexture? _renderTexture;

    private Material? _fuselageMaterial;
    private Material? _wingMaterial;
    private Material? _engineMaterial;
    private Material? _detailMaterial;
    private Material? _wheelMaterial;

    private readonly List<GameObject> _spawnedParts = new();
    private string _lastSignature = string.Empty;

    public bool Draw(Rect rect, AirframeParameters parameters, EngineType engineType, double bypassRatio)
    {
        if (rect.width < 16f || rect.height < 16f)
        {
            return false;
        }

        EnsureInitialized((int)rect.width, (int)rect.height);
        if (_camera is null || _renderTexture is null || _modelRoot is null)
        {
            return false;
        }

        var signature = BuildSignature(parameters, engineType, bypassRatio);
        if (!string.Equals(signature, _lastSignature, StringComparison.Ordinal))
        {
            RebuildModel(parameters, engineType, bypassRatio);
            _lastSignature = signature;
        }

        ConfigureCamera(parameters);

        if (Event.current.type == EventType.Repaint)
        {
            _camera.targetTexture = _renderTexture;
            _camera.Render();
            GUI.DrawTexture(rect, _renderTexture, ScaleMode.StretchToFill, false);
        }

        return true;
    }

    public void Dispose()
    {
        ClearModel();

        if (_renderTexture != null)
        {
            UnityEngine.Object.DestroyImmediate(_renderTexture);
            _renderTexture = null;
        }

        DestroyMaterial(ref _fuselageMaterial);
        DestroyMaterial(ref _wingMaterial);
        DestroyMaterial(ref _engineMaterial);
        DestroyMaterial(ref _detailMaterial);
        DestroyMaterial(ref _wheelMaterial);

        if (_root != null)
        {
            UnityEngine.Object.DestroyImmediate(_root);
            _root = null;
            _modelRoot = null;
            _camera = null;
            _keyLight = null;
        }

        _lastSignature = string.Empty;
    }

    private void EnsureInitialized(int width, int height)
    {
        if (_root == null)
        {
            _root = new GameObject("WT03_AircraftPreview3DRoot")
            {
                hideFlags = HideFlags.HideAndDontSave,
            };

            var model = new GameObject("Model")
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
            _camera.backgroundColor = new Color(0.72f, 0.79f, 0.88f, 1f);
            _camera.fieldOfView = 32f;
            _camera.nearClipPlane = 0.05f;
            _camera.farClipPlane = 100f;
            _camera.cullingMask = 1 << PreviewLayer;
            _camera.enabled = false;

            var lightObject = new GameObject("PreviewLight")
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            lightObject.transform.SetParent(_root.transform, false);
            _keyLight = lightObject.AddComponent<Light>();
            _keyLight.type = LightType.Directional;
            _keyLight.intensity = 1.15f;
            _keyLight.color = new Color(1f, 0.98f, 0.94f, 1f);
            _keyLight.transform.rotation = Quaternion.Euler(38f, 142f, 0f);
            _keyLight.cullingMask = 1 << PreviewLayer;

            InitializeMaterials();
        }

        EnsureRenderTexture(width, height);
    }

    private void InitializeMaterials()
    {
        _fuselageMaterial = CreateMaterial(new Color(0.78f, 0.83f, 0.9f, 1f));
        _wingMaterial = CreateMaterial(new Color(0.30f, 0.56f, 0.78f, 1f));
        _engineMaterial = CreateMaterial(new Color(0.35f, 0.39f, 0.46f, 1f));
        _detailMaterial = CreateMaterial(new Color(0.95f, 0.59f, 0.24f, 1f));
        _wheelMaterial = CreateMaterial(new Color(0.10f, 0.11f, 0.13f, 1f));
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

        _renderTexture = new RenderTexture(targetWidth, targetHeight, 16, RenderTextureFormat.ARGB32)
        {
            antiAliasing = 2,
            hideFlags = HideFlags.HideAndDontSave,
        };
        _renderTexture.Create();
    }

    private void ConfigureCamera(AirframeParameters parameters)
    {
        if (_camera == null)
        {
            return;
        }

        var span = Mathf.Clamp((float)(parameters.WingAreaSquareMeters * 0.18d), 2.8f, 9.5f);
        var distance = Mathf.Clamp(5.8f + (span * 0.55f), 6.2f, 11.5f);
        _camera.transform.position = new Vector3(distance * 0.42f, 2.1f, -distance);
        _camera.transform.LookAt(new Vector3(0f, 0.35f, 0f));
    }

    private void RebuildModel(AirframeParameters parameters, EngineType engineType, double bypassRatio)
    {
        if (_modelRoot == null)
        {
            return;
        }

        ClearModel();
        UpdateFuselageColor(parameters.MaterialType);

        var span = Mathf.Clamp((float)(parameters.WingAreaSquareMeters * 0.18d), 2.8f, 9.5f);
        var fuselageLength = Mathf.Clamp(2.9f + (float)(parameters.FuelAmountLiters * 0.0011d) + (parameters.CrewCount * 0.2f), 3.2f, 8.6f);
        var fuselageRadius = Mathf.Clamp(0.3f + (parameters.CrewCount * 0.035f), 0.32f, 0.78f);

        if (TryBuildFromSimpleAircraftPack(parameters, engineType, bypassRatio, span, fuselageLength))
        {
            // Keep param-driven behavior visible even when using a single pack prefab.
            BuildTail(parameters.TailType, fuselageLength * 0.72f, 0.65f);
            BuildEngines(parameters.EngineCount, engineType, bypassRatio, span * 0.84f, fuselageLength * 0.76f, 0.7f);
            BuildLandingGear(parameters.LandingGearType, fuselageLength * 0.76f, fuselageRadius * 0.84f, 0.72f);
            BuildWingHint(parameters, span * 0.78f);
            return;
        }

        BuildProceduralAircraft(parameters, engineType, bypassRatio, span, fuselageLength, fuselageRadius);
    }

    private void BuildProceduralAircraft(
        AirframeParameters parameters,
        EngineType engineType,
        double bypassRatio,
        float span,
        float fuselageLength,
        float fuselageRadius)
    {
        var fuselage = CreatePart(
            PrimitiveType.Capsule,
            "Fuselage",
            Vector3.zero,
            new Vector3(fuselageRadius * 2f, fuselageLength * 0.45f, fuselageRadius * 2f),
            Quaternion.Euler(0f, 0f, 90f),
            _fuselageMaterial);

        CreatePart(
            PrimitiveType.Sphere,
            "Nose",
            new Vector3((fuselageLength * 0.5f) + (fuselageRadius * 0.35f), 0f, 0f),
            Vector3.one * fuselageRadius * 1.45f,
            Quaternion.identity,
            _detailMaterial);

        BuildWings(parameters, span);
        BuildTail(parameters.TailType, fuselageLength);
        BuildEngines(parameters.EngineCount, engineType, bypassRatio, span, fuselageLength);
        BuildLandingGear(parameters.LandingGearType, fuselageLength, fuselageRadius);

        if (fuselage != null)
        {
            fuselage.transform.SetParent(_modelRoot, false);
        }
    }

    private bool TryBuildFromSimpleAircraftPack(
        AirframeParameters parameters,
        EngineType engineType,
        double bypassRatio,
        float span,
        float fuselageLength)
    {
        var prefab = SelectPackPrefab(parameters, engineType, bypassRatio);
        if (prefab == null || _modelRoot == null)
        {
            return false;
        }

        var holder = new GameObject("PackModel")
        {
            hideFlags = HideFlags.HideAndDontSave,
        };
        holder.transform.SetParent(_modelRoot, false);
        _spawnedParts.Add(holder);

        var instance = UnityEngine.Object.Instantiate(prefab, holder.transform, false);
        instance.name = "PackModelInstance";
        instance.hideFlags = HideFlags.HideAndDontSave;
        SetLayerRecursively(instance, PreviewLayer);
        SetHideFlagsRecursively(instance, HideFlags.HideAndDontSave);

        NormalizePackModelOrientation(instance.transform);
        CenterPackModel(instance.transform);

        var boundsSize = GetCombinedBoundsSize(instance);
        var baseLength = Mathf.Max(boundsSize.x, boundsSize.z, 0.001f);
        var targetLength = Mathf.Clamp(fuselageLength * 0.95f + (span * 0.18f), 3.6f, 10.5f);
        var uniformScale = Mathf.Clamp(targetLength / baseLength, 0.45f, 2.2f);
        holder.transform.localScale = Vector3.one * uniformScale;

        CenterPackModel(instance.transform);
        TintPackModel(instance, parameters.MaterialType);
        return true;
    }

    private GameObject? SelectPackPrefab(AirframeParameters parameters, EngineType engineType, double bypassRatio)
    {
        var candidates = Resources.LoadAll<GameObject>(PackResourcesPath);
        if (candidates == null || candidates.Length == 0)
        {
            return null;
        }

        var bestScore = int.MinValue;
        GameObject? best = null;

        for (var index = 0; index < candidates.Length; index++)
        {
            var candidate = candidates[index];
            if (candidate == null)
            {
                continue;
            }

            var score = ScorePackPrefab(candidate.name, parameters, engineType, bypassRatio);
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best;
    }

    private int ScorePackPrefab(string prefabName, AirframeParameters parameters, EngineType engineType, double bypassRatio)
    {
        var name = prefabName.ToLowerInvariant();
        var isJet = engineType is EngineType.Jet or EngineType.Turbojet or EngineType.Turbofan;
        var isHighBypass = engineType == EngineType.Turbofan && bypassRatio > 10d;
        var score = 0;

        if (isJet)
        {
            if (ContainsAny(name, "jet", "fighter", "airliner", "turbofan"))
            {
                score += 12;
            }

            if (ContainsAny(name, "prop", "piston", "biplane"))
            {
                score -= 6;
            }

            if (isHighBypass && ContainsAny(name, "airliner", "transport", "wide"))
            {
                score += 4;
            }
        }
        else
        {
            if (ContainsAny(name, "prop", "piston", "trainer", "cessna", "general"))
            {
                score += 12;
            }

            if (ContainsAny(name, "jet", "fighter", "airliner", "turbofan"))
            {
                score -= 5;
            }
        }

        if (parameters.EngineCount >= 2 && ContainsAny(name, "twin", "dual", "multi"))
        {
            score += 3;
        }

        if (parameters.EngineCount == 1 && ContainsAny(name, "single"))
        {
            score += 2;
        }

        if (parameters.WingPosition == WingPosition.Biplane && name.Contains("biplane"))
        {
            score += 4;
        }

        if (parameters.LandingGearType == LandingGearType.Retractable && ContainsAny(name, "fighter", "jet"))
        {
            score += 2;
        }

        return score;
    }

    private static bool ContainsAny(string value, params string[] tokens)
    {
        for (var index = 0; index < tokens.Length; index++)
        {
            if (value.Contains(tokens[index]))
            {
                return true;
            }
        }

        return false;
    }

    private static void NormalizePackModelOrientation(Transform instanceTransform)
    {
        var size = GetCombinedBoundsSize(instanceTransform.gameObject);
        if (size.z > size.x * 1.2f)
        {
            instanceTransform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            return;
        }

        if (size.y > size.x * 1.2f)
        {
            instanceTransform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        }
    }

    private static void CenterPackModel(Transform instanceTransform)
    {
        var boundsCenter = GetCombinedBoundsCenter(instanceTransform.gameObject);
        instanceTransform.localPosition -= boundsCenter;
    }

    private static Vector3 GetCombinedBoundsCenter(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return Vector3.zero;
        }

        var bounds = renderers[0].bounds;
        for (var index = 1; index < renderers.Length; index++)
        {
            bounds.Encapsulate(renderers[index].bounds);
        }

        return bounds.center;
    }

    private static Vector3 GetCombinedBoundsSize(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return Vector3.one;
        }

        var bounds = renderers[0].bounds;
        for (var index = 1; index < renderers.Length; index++)
        {
            bounds.Encapsulate(renderers[index].bounds);
        }

        return bounds.size;
    }

    private static void TintPackModel(GameObject root, MaterialType materialType)
    {
        var tint = materialType switch
        {
            MaterialType.WoodFabric => new Color(0.89f, 0.76f, 0.59f, 1f),
            MaterialType.Aluminum => new Color(0.80f, 0.86f, 0.94f, 1f),
            MaterialType.Composite => new Color(0.72f, 0.79f, 0.87f, 1f),
            _ => new Color(0.80f, 0.86f, 0.94f, 1f),
        };

        var renderers = root.GetComponentsInChildren<Renderer>(true);
        for (var rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            var renderer = renderers[rendererIndex];
            var materials = renderer.materials;
            for (var materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                var material = materials[materialIndex];
                if (material == null)
                {
                    continue;
                }

                if (material.HasProperty("_BaseColor"))
                {
                    var baseColor = material.GetColor("_BaseColor");
                    material.SetColor("_BaseColor", Color.Lerp(baseColor, tint, 0.32f));
                }
                else if (material.HasProperty("_Color"))
                {
                    material.color = Color.Lerp(material.color, tint, 0.32f);
                }
            }
        }
    }

    private void BuildWingHint(AirframeParameters parameters, float span)
    {
        var y = parameters.WingPosition switch
        {
            WingPosition.High => 0.34f,
            WingPosition.Low => -0.2f,
            WingPosition.Biplane => 0.1f,
            _ => 0f,
        };

        var hintLength = Mathf.Clamp(span * 0.36f, 1.4f, 3.6f);
        var sweep = parameters.WingShape switch
        {
            WingShape.Straight => 0f,
            WingShape.Swept => 16f,
            WingShape.Delta => 28f,
            _ => 0f,
        };

        CreatePart(
            PrimitiveType.Cube,
            "WingHint",
            new Vector3(0f, y, 0f),
            new Vector3(0.06f, 0.04f, hintLength),
            Quaternion.Euler(0f, sweep, 0f),
            _detailMaterial);

        if (parameters.WingPosition == WingPosition.Biplane)
        {
            CreatePart(
                PrimitiveType.Cube,
                "WingHintUpper",
                new Vector3(0f, y + 0.32f, 0f),
                new Vector3(0.06f, 0.04f, hintLength * 0.88f),
                Quaternion.Euler(0f, sweep, 0f),
                _detailMaterial);
        }
    }

    private void BuildWings(AirframeParameters parameters, float span)
    {
        var wingY = parameters.WingPosition switch
        {
            WingPosition.High => 0.42f,
            WingPosition.Low => -0.26f,
            WingPosition.Biplane => 0.20f,
            _ => 0f,
        };

        var chord = Mathf.Clamp((float)(parameters.WingAreaSquareMeters / (span * 0.8d)), 0.7f, 2.5f);
        var thickness = Mathf.Clamp(0.09f + (chord * 0.07f), 0.1f, 0.22f);
        var halfSpan = span * 0.5f;

        if (parameters.WingShape == WingShape.Straight)
        {
            CreatePart(
                PrimitiveType.Cube,
                "WingMain",
                new Vector3(0f, wingY, 0f),
                new Vector3(chord, thickness, span),
                Quaternion.identity,
                _wingMaterial);
        }
        else
        {
            var sweepAngle = parameters.WingShape == WingShape.Delta ? 28f : 16f;
            var sectionScale = parameters.WingShape == WingShape.Delta
                ? new Vector3(chord * 1.25f, thickness, halfSpan * 1.12f)
                : new Vector3(chord, thickness, halfSpan * 1.02f);

            CreatePart(
                PrimitiveType.Cube,
                "WingLeft",
                new Vector3(0f, wingY, -halfSpan * 0.47f),
                sectionScale,
                Quaternion.Euler(0f, -sweepAngle, 0f),
                _wingMaterial);
            CreatePart(
                PrimitiveType.Cube,
                "WingRight",
                new Vector3(0f, wingY, halfSpan * 0.47f),
                sectionScale,
                Quaternion.Euler(0f, sweepAngle, 0f),
                _wingMaterial);
        }

        if (parameters.WingPosition == WingPosition.Biplane)
        {
            CreatePart(
                PrimitiveType.Cube,
                "WingUpper",
                new Vector3(0f, wingY + 0.52f, 0f),
                new Vector3(chord * 0.95f, thickness, span * 0.92f),
                Quaternion.identity,
                _wingMaterial);
        }

        if (parameters.HasLeadingEdgeFlaps || parameters.FlapType == FlapType.LeadingEdge)
        {
            CreatePart(
                PrimitiveType.Cube,
                "LeadingEdge",
                new Vector3(-0.22f, wingY + 0.04f, 0f),
                new Vector3(0.08f, thickness * 0.65f, span * 0.92f),
                Quaternion.identity,
                _detailMaterial);
        }
    }

    private void BuildTail(TailType tailType, float fuselageLength, float scale = 1f)
    {
        var tailX = (-fuselageLength * 0.5f) + (0.25f * scale);

        switch (tailType)
        {
            case TailType.Classic:
                CreatePart(PrimitiveType.Cube, "TailVertical", new Vector3(tailX, 0.45f * scale, 0f), new Vector3(0.12f, 0.6f, 0.45f) * scale, Quaternion.identity, _detailMaterial);
                CreatePart(PrimitiveType.Cube, "TailHorizontal", new Vector3(tailX - (0.1f * scale), 0.12f * scale, 0f), new Vector3(0.35f, 0.08f, 1.4f) * scale, Quaternion.identity, _detailMaterial);
                break;
            case TailType.TTail:
                CreatePart(PrimitiveType.Cube, "TailVertical", new Vector3(tailX, 0.62f * scale, 0f), new Vector3(0.12f, 0.9f, 0.42f) * scale, Quaternion.identity, _detailMaterial);
                CreatePart(PrimitiveType.Cube, "TailTop", new Vector3(tailX - (0.06f * scale), 0.95f * scale, 0f), new Vector3(0.32f, 0.08f, 1.25f) * scale, Quaternion.identity, _detailMaterial);
                break;
            case TailType.VTail:
                CreatePart(PrimitiveType.Cube, "TailVLeft", new Vector3(tailX - (0.03f * scale), 0.5f * scale, -0.42f * scale), new Vector3(0.1f, 0.58f, 0.36f) * scale, Quaternion.Euler(0f, 0f, -34f), _detailMaterial);
                CreatePart(PrimitiveType.Cube, "TailVRight", new Vector3(tailX - (0.03f * scale), 0.5f * scale, 0.42f * scale), new Vector3(0.1f, 0.58f, 0.36f) * scale, Quaternion.Euler(0f, 0f, 34f), _detailMaterial);
                break;
            case TailType.Twin:
                CreatePart(PrimitiveType.Cube, "TailBeam", new Vector3(tailX - (0.1f * scale), 0.16f * scale, 0f), new Vector3(0.36f, 0.08f, 1.4f) * scale, Quaternion.identity, _detailMaterial);
                CreatePart(PrimitiveType.Cube, "TailTwinLeft", new Vector3(tailX + (0.02f * scale), 0.56f * scale, -0.54f * scale), new Vector3(0.1f, 0.75f, 0.18f) * scale, Quaternion.identity, _detailMaterial);
                CreatePart(PrimitiveType.Cube, "TailTwinRight", new Vector3(tailX + (0.02f * scale), 0.56f * scale, 0.54f * scale), new Vector3(0.1f, 0.75f, 0.18f) * scale, Quaternion.identity, _detailMaterial);
                break;
        }
    }

    private void BuildEngines(
        int engineCount,
        EngineType engineType,
        double bypassRatio,
        float span,
        float fuselageLength,
        float scale = 1f)
    {
        var offsets = GetEngineOffsets(engineCount, span * scale);
        var isJet = engineType is EngineType.Jet or EngineType.Turbojet or EngineType.Turbofan;
        var isHighBypass = engineType == EngineType.Turbofan && bypassRatio > 10d;

        var radius = isJet
            ? (isHighBypass ? 0.28f : 0.21f)
            : 0.16f;
        var length = isJet
            ? (isHighBypass ? 0.95f : 0.72f)
            : 0.46f;
        radius *= scale;
        length *= scale;

        var x = engineCount == 1 ? (fuselageLength * 0.45f) : 0.22f * scale;
        var y = -0.07f * scale;

        for (var index = 0; index < offsets.Length; index++)
        {
            var position = new Vector3(x, y, offsets[index]);
            CreatePart(
                PrimitiveType.Cylinder,
                $"Engine_{index}",
                position,
                new Vector3(radius * 2f, length * 0.5f, radius * 2f),
                Quaternion.Euler(0f, 0f, 90f),
                _engineMaterial);

            if (!isJet)
            {
                CreatePart(
                    PrimitiveType.Cylinder,
                    $"Prop_{index}",
                    new Vector3(position.x + (length * 0.5f) + (0.08f * scale), y, offsets[index]),
                    new Vector3(0.02f * scale, 0.38f * scale, 0.02f * scale),
                    Quaternion.Euler(0f, 0f, 90f),
                    _detailMaterial);
            }
            else
            {
                CreatePart(
                    PrimitiveType.Sphere,
                    $"Fan_{index}",
                    new Vector3(position.x + (length * 0.5f), y, offsets[index]),
                    Vector3.one * radius * 1.15f,
                    Quaternion.identity,
                    _detailMaterial);
            }
        }
    }

    private void BuildLandingGear(LandingGearType gearType, float fuselageLength, float fuselageRadius, float scale = 1f)
    {
        var leftZ = -0.52f * scale;
        var rightZ = 0.52f * scale;
        var axleY = -fuselageRadius - (0.24f * scale);
        var strutY = axleY + (0.24f * scale);
        var mainX = 0.1f * scale;

        if (gearType == LandingGearType.Retractable)
        {
            CreatePart(PrimitiveType.Cube, "GearDoorLeft", new Vector3(mainX, -fuselageRadius - (0.02f * scale), leftZ), new Vector3(0.35f, 0.03f, 0.22f) * scale, Quaternion.identity, _engineMaterial);
            CreatePart(PrimitiveType.Cube, "GearDoorRight", new Vector3(mainX, -fuselageRadius - (0.02f * scale), rightZ), new Vector3(0.35f, 0.03f, 0.22f) * scale, Quaternion.identity, _engineMaterial);
            return;
        }

        var strutLength = gearType == LandingGearType.Fixed ? 0.38f : 0.26f;
        strutLength *= scale;

        CreatePart(PrimitiveType.Cylinder, "StrutLeft", new Vector3(mainX, strutY, leftZ), new Vector3(0.03f, strutLength * 0.5f, 0.03f) * scale, Quaternion.identity, _engineMaterial);
        CreatePart(PrimitiveType.Cylinder, "StrutRight", new Vector3(mainX, strutY, rightZ), new Vector3(0.03f, strutLength * 0.5f, 0.03f) * scale, Quaternion.identity, _engineMaterial);
        CreatePart(PrimitiveType.Sphere, "WheelLeft", new Vector3(mainX, axleY, leftZ), Vector3.one * 0.18f * scale, Quaternion.identity, _wheelMaterial);
        CreatePart(PrimitiveType.Sphere, "WheelRight", new Vector3(mainX, axleY, rightZ), Vector3.one * 0.18f * scale, Quaternion.identity, _wheelMaterial);

        CreatePart(PrimitiveType.Cylinder, "NoseStrut", new Vector3((fuselageLength * 0.46f), strutY - (0.04f * scale), 0f), new Vector3(0.024f, (strutLength * 0.44f), 0.024f) * scale, Quaternion.identity, _engineMaterial);
        CreatePart(PrimitiveType.Sphere, "NoseWheel", new Vector3((fuselageLength * 0.46f), axleY + (0.02f * scale), 0f), Vector3.one * 0.13f * scale, Quaternion.identity, _wheelMaterial);
    }

    private float[] GetEngineOffsets(int engineCount, float span)
    {
        var normalizedSpan = span * 0.42f;
        return engineCount switch
        {
            <= 1 => new[] { 0f },
            2 => new[] { -normalizedSpan * 0.55f, normalizedSpan * 0.55f },
            3 => new[] { -normalizedSpan * 0.58f, 0f, normalizedSpan * 0.58f },
            4 => new[] { -normalizedSpan * 0.72f, -normalizedSpan * 0.3f, normalizedSpan * 0.3f, normalizedSpan * 0.72f },
            5 => new[] { -normalizedSpan * 0.78f, -normalizedSpan * 0.4f, 0f, normalizedSpan * 0.4f, normalizedSpan * 0.78f },
            _ => new[] { -normalizedSpan * 0.84f, -normalizedSpan * 0.5f, -normalizedSpan * 0.17f, normalizedSpan * 0.17f, normalizedSpan * 0.5f, normalizedSpan * 0.84f },
        };
    }

    private void UpdateFuselageColor(MaterialType materialType)
    {
        if (_fuselageMaterial == null)
        {
            return;
        }

        var color = materialType switch
        {
            MaterialType.WoodFabric => new Color(0.86f, 0.72f, 0.53f, 1f),
            MaterialType.Aluminum => new Color(0.78f, 0.83f, 0.9f, 1f),
            MaterialType.Composite => new Color(0.67f, 0.74f, 0.82f, 1f),
            _ => new Color(0.78f, 0.83f, 0.9f, 1f),
        };
        SetMaterialColor(_fuselageMaterial, color);
    }

    private GameObject? CreatePart(
        PrimitiveType primitiveType,
        string name,
        Vector3 localPosition,
        Vector3 localScale,
        Quaternion localRotation,
        Material? material)
    {
        if (_modelRoot == null)
        {
            return null;
        }

        var part = GameObject.CreatePrimitive(primitiveType);
        part.name = name;
        part.hideFlags = HideFlags.HideAndDontSave;
        part.transform.SetParent(_modelRoot, false);
        part.transform.localPosition = localPosition;
        part.transform.localRotation = localRotation;
        part.transform.localScale = localScale;

        SetLayerRecursively(part, PreviewLayer);

        var collider = part.GetComponent<Collider>();
        if (collider != null)
        {
            UnityEngine.Object.DestroyImmediate(collider);
        }

        var renderer = part.GetComponent<Renderer>();
        if (renderer != null && material != null)
        {
            renderer.sharedMaterial = material;
        }

        _spawnedParts.Add(part);
        return part;
    }

    private void ClearModel()
    {
        for (var index = 0; index < _spawnedParts.Count; index++)
        {
            if (_spawnedParts[index] != null)
            {
                UnityEngine.Object.DestroyImmediate(_spawnedParts[index]);
            }
        }

        _spawnedParts.Clear();
    }

    private static string BuildSignature(AirframeParameters parameters, EngineType engineType, double bypassRatio)
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
            parameters.WingAreaSquareMeters.ToString("0.00"),
            parameters.FuelAmountLiters.ToString("0.0"),
            parameters.EngineCowling.ToString("0.00"),
            engineType,
            bypassRatio.ToString("0.00"));
    }

    private static Material CreateMaterial(Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit")
            ?? Shader.Find("Standard")
            ?? Shader.Find("Legacy Shaders/Diffuse");

        var material = new Material(shader)
        {
            hideFlags = HideFlags.HideAndDontSave,
        };
        SetMaterialColor(material, color);
        return material;
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

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        root.layer = layer;
        var transform = root.transform;
        for (var index = 0; index < transform.childCount; index++)
        {
            SetLayerRecursively(transform.GetChild(index).gameObject, layer);
        }
    }

    private static void SetHideFlagsRecursively(GameObject root, HideFlags hideFlags)
    {
        root.hideFlags = hideFlags;
        var transform = root.transform;
        for (var index = 0; index < transform.childCount; index++)
        {
            SetHideFlagsRecursively(transform.GetChild(index).gameObject, hideFlags);
        }
    }
}
