using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WrightTycoon.EditorTools;

[InitializeOnLoad]
public static class MissingScriptCleaner
{
    static MissingScriptCleaner()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    [MenuItem("Tools/Diagnostics/Clean Missing Scripts In Open Scenes")]
    public static void CleanOpenScenesFromMenu()
    {
        var removed = CleanOpenScenes();
        if (removed > 0)
        {
            Debug.LogWarning($"[MissingScriptCleaner] Removed {removed} missing script component(s).");
            return;
        }

        Debug.Log("[MissingScriptCleaner] No missing scripts found in open scenes.");
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingEditMode)
        {
            return;
        }

        var removed = CleanOpenScenes();
        if (removed > 0)
        {
            Debug.LogWarning($"[MissingScriptCleaner] Removed {removed} missing script component(s) before Play.");
        }
    }

    private static int CleanOpenScenes()
    {
        var totalRemoved = 0;

        for (var sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            var scene = SceneManager.GetSceneAt(sceneIndex);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                continue;
            }

            var sceneRemoved = 0;
            var roots = scene.GetRootGameObjects();
            for (var rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                sceneRemoved += CleanRecursive(roots[rootIndex].transform);
            }

            if (sceneRemoved > 0)
            {
                totalRemoved += sceneRemoved;
                EditorSceneManager.MarkSceneDirty(scene);
            }
        }

        return totalRemoved;
    }

    private static int CleanRecursive(Transform current)
    {
        var removed = 0;
        var gameObject = current.gameObject;

        var missingCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject);
        if (missingCount > 0)
        {
            Undo.RegisterCompleteObjectUndo(gameObject, "Remove missing scripts");
            var removedNow = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(gameObject);
            if (removedNow > 0)
            {
                removed += removedNow;
            }
        }

        for (var childIndex = 0; childIndex < current.childCount; childIndex++)
        {
            removed += CleanRecursive(current.GetChild(childIndex));
        }

        return removed;
    }
}
