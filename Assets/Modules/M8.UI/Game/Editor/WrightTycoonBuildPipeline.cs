using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace WrightTycoon.Game.Editor;

public static class WrightTycoonBuildPipeline
{
    private const string OutputFolder = "Builds/Windows/Release";
    private const string ExecutableName = "WrightTycoon.exe";

    [MenuItem("WrightTycoon/Build/Windows Release")]
    public static void BuildWindowsReleaseMenu()
    {
        BuildWindowsRelease();
    }

    public static void BuildWindowsRelease()
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
        var buildFolder = Path.Combine(projectRoot, OutputFolder);
        Directory.CreateDirectory(buildFolder);

        var outputPath = Path.Combine(buildFolder, ExecutableName);
        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outputPath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        Debug.Log($"[Build] Starting Windows release build to: {outputPath}");
        var report = BuildPipeline.BuildPlayer(options);
        var summary = report.summary;
        Debug.Log($"[Build] Result: {summary.result} | Time: {summary.totalTime} | Size: {summary.totalSize} bytes");

        if (summary.result != BuildResult.Succeeded)
        {
            throw new Exception($"Windows release build failed: {summary.result}");
        }
    }
}
