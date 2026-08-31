using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace WrightTycoon.Game.Editor;

/// <summary>
/// Builds a reusable visual kit from the three Simple Aircraft Pack aircraft. The source Body
/// meshes are monolithic, so the importer keeps only long central shell components and removes
/// fixed wings, engines, seats and tail surfaces. Runtime code deforms the resulting shells by
/// longitudinal stations and attaches socket-based wings, engines, tails and landing gear.
/// </summary>
public static class AircraftPreviewPackModuleImporter
{
    private const string SourceFolder = "Assets/Polyeler/Simple Aircraft Pack/Prefabs";
    private const string OutputFolder = "Assets/Resources/WT03/Previews/AircraftModules";

    private static readonly SourceDefinition[] Sources =
    {
        new(1, 2.05f, 7.05f),
        new(2, 1.95f, 6.65f),
        new(3, 1.25f, 3.42f),
    };

    [MenuItem("WrightTycoon/Aircraft Preview/Rebuild Simple Aircraft Pack Modules")]
    public static void RebuildPackModules()
    {
        Directory.CreateDirectory(OutputFolder);
        foreach (var source in Sources)
        {
            var prefab = LoadPrefab(source.Version);
            SaveOrUpdateMesh(
                $"{OutputFolder}/PackFuselageV{source.Version}.asset",
                ExtractNormalizedFuselage(FindMesh(prefab, "Body"), source));
            SaveOrUpdateMaterial(
                $"{OutputFolder}/PackSurfaceV{source.Version}.mat",
                FindRenderer(prefab, "Body").sharedMaterial);
        }

        var transport = LoadPrefab(1);
        var commuter = LoadPrefab(3);
        SaveOrUpdateMesh($"{OutputFolder}/PackFan.asset", NormalizeRotor(FindFirstRotor(transport), "PackFan"));
        SaveOrUpdateMesh($"{OutputFolder}/PackPropeller.asset", NormalizeRotor(FindFirstRotor(commuter), "PackPropeller"));
        SaveOrUpdateMesh($"{OutputFolder}/PackWheelLarge.asset", NormalizeWheel(FindMesh(transport, "Front Wheel"), "PackWheelLarge"));
        SaveOrUpdateMesh($"{OutputFolder}/PackWheelSmall.asset", NormalizeWheel(FindMesh(commuter, "Front Wheel"), "PackWheelSmall"));

        // Keep the original resource name as a compatibility alias for older tests and builds.
        SaveOrUpdateMesh(
            $"{OutputFolder}/PackFuselage.asset",
            ExtractNormalizedFuselage(FindMesh(commuter, "Body"), Sources[2], "PackFuselage"));

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Modular Simple Aircraft Pack visual kit rebuilt in {OutputFolder}.");
    }

    public static void RebuildPackModulesAndExit()
    {
        try
        {
            RebuildPackModules();
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static GameObject LoadPrefab(int version)
    {
        var path = $"{SourceFolder}/Passenger Aircraft V{version}.prefab";
        return AssetDatabase.LoadAssetAtPath<GameObject>(path)
            ?? throw new InvalidOperationException($"Simple Aircraft Pack prefab was not found at '{path}'.");
    }

    private static Mesh FindMesh(GameObject prefab, string name)
    {
        var filter = prefab.GetComponentsInChildren<MeshFilter>(true)
            .FirstOrDefault(value => string.Equals(value.name, name, StringComparison.Ordinal));
        return filter?.sharedMesh
            ?? throw new InvalidOperationException($"'{prefab.name}' does not contain mesh '{name}'.");
    }

    private static MeshRenderer FindRenderer(GameObject prefab, string name)
    {
        var renderer = prefab.GetComponentsInChildren<MeshRenderer>(true)
            .FirstOrDefault(value => string.Equals(value.name, name, StringComparison.Ordinal));
        return renderer
            ?? throw new InvalidOperationException($"'{prefab.name}' does not contain renderer '{name}'.");
    }

    private static Mesh FindFirstRotor(GameObject prefab)
    {
        var filter = prefab.GetComponentsInChildren<MeshFilter>(true)
            .FirstOrDefault(value => value.name.StartsWith("Propeller", StringComparison.Ordinal));
        return filter?.sharedMesh
            ?? throw new InvalidOperationException($"'{prefab.name}' does not contain a rotor mesh.");
    }

    private static Mesh ExtractNormalizedFuselage(
        Mesh source,
        SourceDefinition definition,
        string? outputName = null)
    {
        var vertices = source.vertices;
        var candidateTriangles = new List<int>();
        for (var index = 0; index < source.triangles.Length; index += 3)
        {
            var a = vertices[source.triangles[index]];
            var b = vertices[source.triangles[index + 1]];
            var c = vertices[source.triangles[index + 2]];
            if (IsInsideCentralEnvelope(a, definition)
                && IsInsideCentralEnvelope(b, definition)
                && IsInsideCentralEnvelope(c, definition))
            {
                candidateTriangles.Add(source.triangles[index]);
                candidateTriangles.Add(source.triangles[index + 1]);
                candidateTriangles.Add(source.triangles[index + 2]);
            }
        }

        if (candidateTriangles.Count == 0)
        {
            throw new InvalidOperationException($"Fuselage V{definition.Version} envelope produced no triangles.");
        }

        var candidateBounds = CalculateBounds(vertices, candidateTriangles);
        var components = BuildConnectedComponents(vertices, candidateTriangles);
        var minimumLongitudinalSpan = candidateBounds.size.z * 0.27f;
        var selectedTriangles = components
            .Where(component => CalculateBounds(vertices, component).size.z >= minimumLongitudinalSpan)
            .SelectMany(component => component)
            .ToList();
        if (selectedTriangles.Count == 0)
        {
            selectedTriangles = components
                .OrderByDescending(component => CalculateBounds(vertices, component).size.z * component.Count)
                .First();
        }

        var selectedBounds = CalculateBounds(vertices, selectedTriangles);
        var length = Mathf.Max(0.001f, selectedBounds.size.z);
        var halfHeight = Mathf.Max(0.001f, selectedBounds.extents.y);
        var halfWidth = Mathf.Max(0.001f, selectedBounds.extents.x);
        return RemapMesh(
            outputName ?? $"PackFuselageV{definition.Version}",
            source,
            selectedTriangles,
            vertex => new Vector3(
                (vertex.z - selectedBounds.center.z) / length,
                (vertex.y - selectedBounds.center.y) / halfHeight,
                (vertex.x - selectedBounds.center.x) / halfWidth));
    }

    private static bool IsInsideCentralEnvelope(Vector3 vertex, SourceDefinition definition) =>
        Mathf.Abs(vertex.x) <= definition.HalfWidth && vertex.y <= definition.MaximumY;

    private static List<List<int>> BuildConnectedComponents(
        IReadOnlyList<Vector3> vertices,
        IReadOnlyList<int> triangles)
    {
        var triangleCount = triangles.Count / 3;
        var parents = Enumerable.Range(0, triangleCount).ToArray();
        var triangleByPosition = new Dictionary<Vector3Int, int>();
        for (var triangle = 0; triangle < triangleCount; triangle++)
        {
            for (var corner = 0; corner < 3; corner++)
            {
                var vertex = vertices[triangles[(triangle * 3) + corner]];
                var key = new Vector3Int(
                    Mathf.RoundToInt(vertex.x * 10_000f),
                    Mathf.RoundToInt(vertex.y * 10_000f),
                    Mathf.RoundToInt(vertex.z * 10_000f));
                if (triangleByPosition.TryGetValue(key, out var neighbour))
                {
                    Union(parents, triangle, neighbour);
                }
                else
                {
                    triangleByPosition.Add(key, triangle);
                }
            }
        }

        var components = new Dictionary<int, List<int>>();
        for (var triangle = 0; triangle < triangleCount; triangle++)
        {
            var root = FindRoot(parents, triangle);
            if (!components.TryGetValue(root, out var component))
            {
                component = new List<int>();
                components.Add(root, component);
            }

            component.Add(triangles[triangle * 3]);
            component.Add(triangles[(triangle * 3) + 1]);
            component.Add(triangles[(triangle * 3) + 2]);
        }

        return components.Values.ToList();
    }

    private static int FindRoot(int[] parents, int index)
    {
        while (parents[index] != index)
        {
            parents[index] = parents[parents[index]];
            index = parents[index];
        }

        return index;
    }

    private static void Union(int[] parents, int first, int second)
    {
        var firstRoot = FindRoot(parents, first);
        var secondRoot = FindRoot(parents, second);
        if (firstRoot != secondRoot)
        {
            parents[secondRoot] = firstRoot;
        }
    }

    private static Mesh NormalizeRotor(Mesh source, string name)
    {
        var bounds = source.bounds;
        var radius = Mathf.Max(0.001f, Mathf.Max(bounds.extents.x, bounds.extents.y));
        return RemapMesh(
            name,
            source,
            source.triangles,
            vertex => new Vector3(
                (vertex.z - bounds.center.z) / radius,
                (vertex.y - bounds.center.y) / radius,
                (vertex.x - bounds.center.x) / radius));
    }

    private static Mesh NormalizeWheel(Mesh source, string name)
    {
        var bounds = source.bounds;
        var radius = Mathf.Max(0.001f, Mathf.Max(bounds.extents.y, bounds.extents.z));
        return RemapMesh(
            name,
            source,
            source.triangles,
            vertex => new Vector3(
                (vertex.z - bounds.center.z) / radius,
                (vertex.y - bounds.center.y) / radius,
                (vertex.x - bounds.center.x) / radius));
    }

    private static Bounds CalculateBounds(IReadOnlyList<Vector3> vertices, IReadOnlyList<int> triangles)
    {
        var bounds = new Bounds(vertices[triangles[0]], Vector3.zero);
        for (var index = 1; index < triangles.Count; index++)
        {
            bounds.Encapsulate(vertices[triangles[index]]);
        }

        return bounds;
    }

    private static Mesh RemapMesh(
        string name,
        Mesh source,
        IReadOnlyList<int> sourceTriangles,
        Func<Vector3, Vector3> remap)
    {
        var sourceVertices = source.vertices;
        var sourceUvs = source.uv;
        var vertexMap = new Dictionary<int, int>();
        var vertices = new List<Vector3>();
        var uvs = new List<Vector2>();
        var triangles = new int[sourceTriangles.Count];
        for (var index = 0; index < sourceTriangles.Count; index++)
        {
            var sourceIndex = sourceTriangles[index];
            if (!vertexMap.TryGetValue(sourceIndex, out var targetIndex))
            {
                targetIndex = vertices.Count;
                vertexMap.Add(sourceIndex, targetIndex);
                vertices.Add(remap(sourceVertices[sourceIndex]));
                uvs.Add(sourceUvs.Length == sourceVertices.Length ? sourceUvs[sourceIndex] : Vector2.zero);
            }

            triangles[index] = targetIndex;
        }

        // Z-to-X remapping reflects the coordinate system, therefore triangle winding changes.
        for (var index = 0; index < triangles.Length; index += 3)
        {
            (triangles[index + 1], triangles[index + 2]) = (triangles[index + 2], triangles[index + 1]);
        }

        var mesh = new Mesh { name = name };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void SaveOrUpdateMesh(string path, Mesh generated)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(generated, path);
            return;
        }

        EditorUtility.CopySerialized(generated, existing);
        UnityEngine.Object.DestroyImmediate(generated);
        EditorUtility.SetDirty(existing);
    }

    private static void SaveOrUpdateMaterial(string path, Material source)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(new Material(source), path);
            return;
        }

        EditorUtility.CopySerialized(source, existing);
        EditorUtility.SetDirty(existing);
    }

    private readonly struct SourceDefinition
    {
        public SourceDefinition(int version, float halfWidth, float maximumY)
        {
            Version = version;
            HalfWidth = halfWidth;
            MaximumY = maximumY;
        }

        public int Version { get; }
        public float HalfWidth { get; }
        public float MaximumY { get; }
    }
}
