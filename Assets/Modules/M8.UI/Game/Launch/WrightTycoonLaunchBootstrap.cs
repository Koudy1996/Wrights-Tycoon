using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WrightTycoon.Game.Launch;

public static class WrightTycoonLaunchBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureLaunchRunnerExists()
    {
        var activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid())
        {
            return;
        }

        if (!IsLaunchScene(activeScene) && !IsBackupScene(activeScene))
        {
            return;
        }

        var existing = UnityEngine.Object.FindObjectsByType<WrightTycoonLaunchRunner>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        if (existing is { Length: > 0 })
        {
            return;
        }

        var host = new GameObject("WrightTycoonLaunchRunner");
        host.AddComponent<WrightTycoonLaunchRunner>();
        Debug.LogWarning("[LaunchBootstrap] Launch runner was missing and has been recreated at runtime.");
    }

    private static bool IsLaunchScene(Scene scene)
    {
        if (string.Equals(scene.path, "Assets/WrightTycoon/Scenes/WrightTycoonLaunch.unity", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(scene.name, "WrightTycoonLaunch", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBackupScene(Scene scene)
    {
        if (string.IsNullOrWhiteSpace(scene.path))
        {
            return false;
        }

        return scene.path.IndexOf("__Backupscenes", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
