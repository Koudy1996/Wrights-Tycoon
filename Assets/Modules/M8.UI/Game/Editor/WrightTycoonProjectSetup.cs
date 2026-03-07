using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using WrightTycoon.Game.Launch;

namespace WrightTycoon.Game.Editor;

public static class WrightTycoonProjectSetup
{
    private const string SceneFolder = "Assets/WrightTycoon/Scenes";
    private const string ScenePath = "Assets/WrightTycoon/Scenes/WrightTycoonLaunch.unity";
    private const string BuildSceneFolder = "Assets/Modules/M8.UI/Game/Scenes";
    private const string BuildScenePath = "Assets/Modules/M8.UI/Game/Scenes/WrightTycoonBuildLaunch.unity";
    private const string StreamingExamplesPath = "Assets/StreamingAssets/Data/Examples";
    private const string TempDiagnosticsScenePrefix = "Assets/TempBuildDiagnostics/";

    public static string RuntimeLaunchScenePath => BuildScenePath;

    [MenuItem("WrightTycoon/Prepare Project For Launch")]
    public static void PrepareProjectForLaunch()
    {
        EnsureStreamingDefinitions();
        CreateOrUpdateLaunchScene();
        CreateOrUpdateBuildLaunchScene();
        EnsureBuildSettings();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (!Application.isBatchMode)
        {
            EditorUtility.DisplayDialog(
                "Wright Tycoon",
                "Project prepared. Open scene Assets/WrightTycoon/Scenes/WrightTycoonLaunch.unity and press Play.",
                "OK");
        }
    }

    [MenuItem("WrightTycoon/Open Launch Scene")]
    public static void OpenLaunchScene()
    {
        PrepareProjectForLaunch();
        EditorSceneManager.OpenScene(BuildScenePath);
    }

    [MenuItem("WrightTycoon/Fix Missing Scripts (Open Scene)")]
    public static void FixMissingScriptsInOpenScene()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog("Wright Tycoon", "No active scene found.", "OK");
            }

            return;
        }

        var missingCount = 0;
        foreach (var root in scene.GetRootGameObjects())
        {
            missingCount += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(root);
        }

        if (missingCount == 0)
        {
            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog("Wright Tycoon", "No missing scripts found in the active scene.", "OK");
            }

            return;
        }

        foreach (var root in scene.GetRootGameObjects())
        {
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(root);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        if (!Application.isBatchMode)
        {
            EditorUtility.DisplayDialog("Wright Tycoon", $"Removed {missingCount} missing script component(s).", "OK");
        }
    }

    private static void EnsureStreamingDefinitions()
    {
        Directory.CreateDirectory(StreamingExamplesPath);

        var root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        var sourceEngine = Path.Combine(root, "Data", "Examples", "engine-definitions.json");
        var sourceAirframe = Path.Combine(root, "Data", "Examples", "airframe-definitions.json");

        var targetEngine = Path.Combine(StreamingExamplesPath, "engine-definitions.json");
        var targetAirframe = Path.Combine(StreamingExamplesPath, "airframe-definitions.json");

        if (File.Exists(sourceEngine))
        {
            File.Copy(sourceEngine, targetEngine, true);
        }

        if (File.Exists(sourceAirframe))
        {
            File.Copy(sourceAirframe, targetAirframe, true);
        }
    }

    private static void CreateOrUpdateLaunchScene()
    {
        Directory.CreateDirectory(SceneFolder);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    private static void CreateOrUpdateBuildLaunchScene()
    {
        Directory.CreateDirectory(BuildSceneFolder);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene, BuildScenePath);
    }

    private static void EnsureBuildSettings()
    {
        var existing = EditorBuildSettings.scenes;
        var updated = new List<EditorBuildSettingsScene>(existing.Length + 2);
        var hasLegacyScene = false;
        var hasBuildScene = false;

        foreach (var scene in existing)
        {
            if (string.IsNullOrWhiteSpace(scene.path))
            {
                continue;
            }

            if (scene.path.StartsWith(TempDiagnosticsScenePrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(scene.path, ScenePath, StringComparison.OrdinalIgnoreCase))
            {
                hasLegacyScene = true;
                updated.Add(new EditorBuildSettingsScene(ScenePath, false));
                continue;
            }

            if (string.Equals(scene.path, BuildScenePath, StringComparison.OrdinalIgnoreCase))
            {
                hasBuildScene = true;
                updated.Add(new EditorBuildSettingsScene(BuildScenePath, true));
                continue;
            }

            updated.Add(scene);
        }

        if (!hasLegacyScene)
        {
            updated.Add(new EditorBuildSettingsScene(ScenePath, false));
        }

        if (!hasBuildScene)
        {
            updated.Add(new EditorBuildSettingsScene(BuildScenePath, true));
        }

        if (existing.Length == updated.Count)
        {
            var changed = false;
            for (var index = 0; index < existing.Length; index++)
            {
                if (!string.Equals(existing[index].path, updated[index].path, StringComparison.OrdinalIgnoreCase) ||
                    existing[index].enabled != updated[index].enabled)
                {
                    changed = true;
                    break;
                }
            }

            if (!changed)
            {
                return;
            }
        }

        EditorBuildSettings.scenes = updated.ToArray();
    }
}
