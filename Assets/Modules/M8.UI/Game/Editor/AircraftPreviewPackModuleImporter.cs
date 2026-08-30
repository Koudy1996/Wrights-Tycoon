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

        if (selectedTriangles.Count == 0)
        {
            throw new InvalidOperationException("The Simple Aircraft Pack fuselage extraction produced no triangles.");
        }

        var selectedBounds = CalculateBounds(sourceVertices, selectedTriangles);
        var length = Mathf.Max(0.001f, selectedBounds.size.z);
        var halfHeight = Mathf.Max(0.001f, selectedBounds.extents.y);
        var halfWidth = Mathf.Max(0.001f, selectedBounds.extents.x);
        return RemapMesh(
            "SimpleAircraftPack_Fuselage",
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

    private static Mesh NormalizePropeller(Mesh source)
    {
        var bounds = source.bounds;
        var radius = Mathf.Max(0.001f, Mathf.Max(bounds.extents.x, bounds.extents.y));
        return RemapMesh(
            "SimpleAircraftPack_Propeller",
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
