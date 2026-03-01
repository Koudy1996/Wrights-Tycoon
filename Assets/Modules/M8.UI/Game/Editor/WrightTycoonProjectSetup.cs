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
        if (File.Exists(ScenePath))
        {
            return;
        }

        Directory.CreateDirectory(SceneFolder);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        var bootstrapObject = new GameObject("WrightTycoonLaunchRunner");
        bootstrapObject.AddComponent<WrightTycoonLaunchRunner>();

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
        var hasLegacyScene = false;
        var hasBuildScene = false;

        foreach (var scene in existing)
        {
            if (scene.path == ScenePath)
            {
                hasLegacyScene = true;
            }

            if (scene.path == BuildScenePath)
            {
                hasBuildScene = true;
            }
        }

        var additionalCount = (hasLegacyScene ? 0 : 1) + (hasBuildScene ? 0 : 1);
        if (additionalCount == 0)
        {
            return;
        }

        var updated = new EditorBuildSettingsScene[existing.Length + additionalCount];
        for (var index = 0; index < existing.Length; index++)
        {
            updated[index] = existing[index];
        }

        var next = existing.Length;
        if (!hasLegacyScene)
        {
            updated[next++] = new EditorBuildSettingsScene(ScenePath, false);
        }

        if (!hasBuildScene)
        {
            updated[next] = new EditorBuildSettingsScene(BuildScenePath, true);
        }

        EditorBuildSettings.scenes = updated;
    }
}
