using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace WrightTycoon.Game.Editor;

public static class WrightTycoonBuildPipeline
{
    private const string WindowsOutputFolder = "Builds/Windows/Release";
    private const string MacOutputFolder = "Builds/Mac/Release";
    private const string WindowsExecutableName = "WrightTycoon.exe";
    private const string MacApplicationName = "WrightTycoon.app";

    [MenuItem("WrightTycoon/Build/Windows Release")]
    public static void BuildWindowsReleaseMenu()
    {
        BuildWindowsRelease();
    }

    [MenuItem("WrightTycoon/Build/macOS Release")]
    public static void BuildMacReleaseMenu()
    {
        BuildMacRelease();
    }

    public static void BuildWindowsRelease()
    {
        BuildRelease(
            BuildTarget.StandaloneWindows64,
            WindowsOutputFolder,
            WindowsExecutableName,
            "Windows release");
    }

    public static void BuildMacRelease()
    {
        var originalBackend = PlayerSettings.GetScriptingBackend(BuildTargetGroup.Standalone);
        var switchedToMono = false;

        try
        {
            if (originalBackend != ScriptingImplementation.Mono2x)
            {
                Debug.Log("[Build] Switching Standalone scripting backend to Mono for macOS build.");
                PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);
                AssetDatabase.SaveAssets();
                switchedToMono = true;
            }

            BuildRelease(
                BuildTarget.StandaloneOSX,
                MacOutputFolder,
                MacApplicationName,
                "macOS release");
        }
        finally
        {
            if (switchedToMono)
            {
                PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, originalBackend);
                AssetDatabase.SaveAssets();
                Debug.Log($"[Build] Restored Standalone scripting backend to {originalBackend}.");
            }
        }
    }

    private static void BuildRelease(
        BuildTarget target,
        string outputFolder,
        string playerFileName,
        string buildLabel)
    {
        WrightTycoonProjectSetup.PrepareProjectForLaunch();

        var scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();

        if (scenes.Length == 0)
        {
            throw new InvalidOperationException("No enabled scenes found in Build Settings.");
        }

        var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        var buildFolder = Path.Combine(projectRoot, outputFolder);
        CleanBuildFolder(buildFolder);

        var outputPath = Path.Combine(buildFolder, playerFileName);
        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outputPath,
            target = target,
            options = BuildOptions.None
        };

        Debug.Log($"[Build] Starting {buildLabel} build to: {outputPath}");
        var report = BuildPipeline.BuildPlayer(options);
        var summary = report.summary;
        Debug.Log($"[Build] Result: {summary.result} | Time: {summary.totalTime} | Size: {summary.totalSize} bytes");

        if (summary.result != BuildResult.Succeeded)
        {
            throw new Exception($"{buildLabel} build failed: {summary.result}");
        }

        if (!File.Exists(outputPath) && !Directory.Exists(outputPath))
        {
            throw new FileNotFoundException($"Build finished but output was not found at '{outputPath}'.");
        }
    }

    private static void CleanBuildFolder(string buildFolder)
    {
        Directory.CreateDirectory(buildFolder);

        var directoryInfo = new DirectoryInfo(buildFolder);
        foreach (var directory in directoryInfo.GetDirectories())
        {
            directory.Delete(true);
        }

        foreach (var file in directoryInfo.GetFiles())
        {
            file.Delete();
        }
    }
}
