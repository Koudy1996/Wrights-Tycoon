using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace WrightTycoon.Game.Editor;

/// <summary>
/// Converts the monolithic Simple Aircraft Pack model into small runtime resources used by the
/// data-driven preview. The generated meshes intentionally contain no source materials: the
/// preview owns its palette so all aircraft eras and configurations remain visually consistent.
/// </summary>
public static class AircraftPreviewPackModuleImporter
{
    private const string SourcePrefabPath = "Assets/Polyeler/Simple Aircraft Pack/Prefabs/Passenger Aircraft V3.prefab";
    private const string OutputFolder = "Assets/Resources/WT03/Previews/AircraftModules";
    private const string FuselageOutputPath = OutputFolder + "/PackFuselage.asset";
    private const string PropellerOutputPath = OutputFolder + "/PackPropeller.asset";

    [MenuItem("WrightTycoon/Aircraft Preview/Rebuild Simple Aircraft Pack Modules")]
    public static void RebuildPackModules()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefabPath);
        if (prefab == null)
        {
            throw new InvalidOperationException($"Simple Aircraft Pack prefab was not found at '{SourcePrefabPath}'.");
        }

        var filters = prefab.GetComponentsInChildren<MeshFilter>(true);
        var body = filters.FirstOrDefault(filter => string.Equals(filter.name, "Body", StringComparison.Ordinal));
        var propeller = filters.FirstOrDefault(filter => filter.name.StartsWith("Propeller", StringComparison.Ordinal));
        if (body?.sharedMesh == null || propeller?.sharedMesh == null)
        {
            throw new InvalidOperationException("The imported aircraft does not contain the expected Body and Propeller modules.");
        }

        Directory.CreateDirectory(OutputFolder);
        SaveOrUpdateMesh(FuselageOutputPath, ExtractNormalizedFuselage(body.sharedMesh));
        SaveOrUpdateMesh(PropellerOutputPath, NormalizePropeller(propeller.sharedMesh));
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Aircraft preview pack modules rebuilt in {OutputFolder}.");
    }

    private static Mesh ExtractNormalizedFuselage(Mesh source)
    {
        var sourceVertices = source.vertices;
        var sourceTriangles = source.triangles;
        var selectedTriangles = new List<int>();

        // V3 is authored along Z, with X as its span. This central envelope removes the fixed
        // wings, engines and vertical tail while retaining the low-poly cabin and nose profile.
        for (var index = 0; index < sourceTriangles.Length; index += 3)
        {
            var a = sourceVertices[sourceTriangles[index]];
            var b = sourceVertices[sourceTriangles[index + 1]];
            var c = sourceVertices[sourceTriangles[index + 2]];
            if (IsInsideFuselageEnvelope(a) && IsInsideFuselageEnvelope(b) && IsInsideFuselageEnvelope(c))
            {
                selectedTriangles.Add(sourceTriangles[index]);
                selectedTriangles.Add(sourceTriangles[index + 1]);
                selectedTriangles.Add(sourceTriangles[index + 2]);
            }
        }

        selectedTriangles = SelectFuselageShell(sourceVertices, selectedTriangles);
        if (selectedTriangles.Count == 0)
        {
            throw new InvalidOperationException("The Simple Aircraft Pack fuselage extraction produced no triangles.");
        }

        var selectedBounds = CalculateBounds(sourceVertices, selectedTriangles);
        var length = Mathf.Max(0.001f, selectedBounds.size.z);
        var halfHeight = Mathf.Max(0.001f, selectedBounds.extents.y);
        var halfWidth = Mathf.Max(0.001f, selectedBounds.extents.x);
        return RemapMesh(
            "PackFuselage",
            source,
            selectedTriangles,
            vertex => new Vector3(
                (vertex.z - selectedBounds.center.z) / length,
                (vertex.y - selectedBounds.center.y) / halfHeight,
                (vertex.x - selectedBounds.center.x) / halfWidth));
    }

    private static bool IsInsideFuselageEnvelope(Vector3 vertex)
    {
        return Mathf.Abs(vertex.x) <= 1.45f && vertex.y <= 3.35f;
    }

    private static List<int> SelectFuselageShell(
        IReadOnlyList<Vector3> vertices,
        IReadOnlyList<int> selectedTriangles)
    {
        var triangleCount = selectedTriangles.Count / 3;
        var parents = Enumerable.Range(0, triangleCount).ToArray();
        var triangleByPosition = new Dictionary<Vector3Int, int>();

        for (var triangle = 0; triangle < triangleCount; triangle++)
        {
            for (var corner = 0; corner < 3; corner++)
            {
                var vertex = vertices[selectedTriangles[(triangle * 3) + corner]];
                var positionKey = new Vector3Int(
                    Mathf.RoundToInt(vertex.x * 10_000f),
                    Mathf.RoundToInt(vertex.y * 10_000f),
                    Mathf.RoundToInt(vertex.z * 10_000f));
                if (triangleByPosition.TryGetValue(positionKey, out var neighbour))
                {
                    Union(parents, triangle, neighbour);
                }
                else
                {
                    triangleByPosition.Add(positionKey, triangle);
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

            component.Add(selectedTriangles[triangle * 3]);
            component.Add(selectedTriangles[(triangle * 3) + 1]);
            component.Add(selectedTriangles[(triangle * 3) + 2]);
        }

        // The passenger shell is the only connected component spanning almost the full aircraft.
        // Seats and other cabin props are separate, shorter components; excluding them removes the
        // small protrusions that previously appeared above the generated passenger windows.
        return components.Values
            .OrderByDescending(component =>
            {
                var bounds = CalculateBounds(vertices, component);
                return bounds.size.z * component.Count;
            })
            .FirstOrDefault() ?? new List<int>();
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

    private static Mesh NormalizePropeller(Mesh source)
    {
        var bounds = source.bounds;
        var radius = Mathf.Max(0.001f, Mathf.Max(bounds.extents.x, bounds.extents.y));
        return RemapMesh(
            "PackPropeller",
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

        // Swapping the pack's X/Z axes is a reflection. Reverse every triangle so the generated
        // hull keeps outward-facing normals instead of rendering only its far side like glass.
        for (var index = 0; index < triangles.Length; index += 3)
        {
            (triangles[index + 1], triangles[index + 2]) = (triangles[index + 2], triangles[index + 1]);
        }

        var mesh = new Mesh { name = name };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
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
}
