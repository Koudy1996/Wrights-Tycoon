using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Process = System.Diagnostics.Process;
using ProcessStartInfo = System.Diagnostics.ProcessStartInfo;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using WrightTycoon.Game.RuntimeData;

namespace WrightTycoon.Game.Editor;

public static class WrightTycoonBuildPipeline
{
    private const string WindowsOutputFolder = "Builds/Windows/Release";
    private const string MacOutputFolder = "Builds/Mac/Release";
    private const string WindowsExecutableName = "WrightTycoon.exe";
    private const string MacApplicationName = "WrightTycoon.app";
    private const string LegacyLaunchScenePath = "Assets/WrightTycoon/Scenes/WrightTycoonLaunch.unity";

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

        var runtimeLaunchScenePath = WrightTycoonProjectSetup.RuntimeLaunchScenePath;
        var additionalScenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .Where(path => !string.Equals(path, runtimeLaunchScenePath, StringComparison.OrdinalIgnoreCase))
            .Where(path => !string.Equals(path, LegacyLaunchScenePath, StringComparison.OrdinalIgnoreCase))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var scenes = new[] { runtimeLaunchScenePath }
            .Concat(additionalScenes)
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

        FinalizeReleaseLayout(buildFolder, target);
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

    private static void FinalizeReleaseLayout(string buildFolder, BuildTarget target)
    {
        WrightTycoonRuntimePaths.EnsurePlayerInfoLayout(buildFolder);

        var manifest = CreateBuildManifest(target);
        var packagedManifestPath = Path.Combine(buildFolder, WrightTycoonBuildManifestService.ManifestFileName);
        WrightTycoonBuildManifestService.WriteToFile(packagedManifestPath, manifest);

        var generatedAssetsDirectory = WrightTycoonRuntimePaths.GetGeneratedAssetsDirectory(buildFolder);
        var cachedManifestPath = Path.Combine(generatedAssetsDirectory, "build-manifest.json");
        WrightTycoonBuildManifestService.WriteToFile(cachedManifestPath, manifest);

        if (target == BuildTarget.StandaloneWindows64)
        {
            MoveWindowsDeveloperBackupOutOfRelease(buildFolder);
        }
    }

    private static WrightTycoonBuildManifest CreateBuildManifest(BuildTarget target)
    {
        var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        var (balanceChecksum, balanceFileCount) = ComputeBalanceChecksum(projectRoot);
        var gitCommit = TryGetGitCommit(projectRoot) ?? string.Empty;
        var gameVersion = string.IsNullOrWhiteSpace(PlayerSettings.bundleVersion)
            ? Application.version
            : PlayerSettings.bundleVersion;

        return new WrightTycoonBuildManifest
        {
            GameVersion = string.IsNullOrWhiteSpace(gameVersion) ? "0.0.0" : gameVersion,
            BuildTimestampUtc = DateTimeOffset.UtcNow.ToString("O"),
            TargetPlatform = target.ToString(),
            GitCommit = gitCommit,
            BalanceChecksum = string.IsNullOrWhiteSpace(balanceChecksum) ? "unavailable" : balanceChecksum,
            BalanceFileCount = balanceFileCount,
        };
    }

    private static (string Checksum, int FileCount) ComputeBalanceChecksum(string projectRoot)
    {
        var files = new List<string>();

        var modulesRoot = Path.Combine(projectRoot, "Assets", "Modules");
        if (Directory.Exists(modulesRoot))
        {
            foreach (var file in Directory.EnumerateFiles(modulesRoot, "*.cs", SearchOption.AllDirectories))
            {
                var normalizedPath = file.Replace('\\', '/');
                if (normalizedPath.IndexOf("/balance/", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    files.Add(file);
                }
            }
        }

        var dataExamplesRoot = Path.Combine(projectRoot, "Data", "Examples");
        if (Directory.Exists(dataExamplesRoot))
        {
            files.AddRange(Directory.EnumerateFiles(dataExamplesRoot, "*.json", SearchOption.AllDirectories));
        }

        var orderedFiles = files
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (orderedFiles.Length == 0)
        {
            return (string.Empty, 0);
        }

        using var content = new MemoryStream();
        for (var index = 0; index < orderedFiles.Length; index++)
        {
            var file = orderedFiles[index];
            var relativePath = Path.GetRelativePath(projectRoot, file).Replace('\\', '/');
            var relativePathBytes = Encoding.UTF8.GetBytes(relativePath);
            content.Write(relativePathBytes, 0, relativePathBytes.Length);
            content.WriteByte((byte)'\n');

            var fileBytes = File.ReadAllBytes(file);
            content.Write(fileBytes, 0, fileBytes.Length);
            content.WriteByte((byte)'\n');
        }

        using var sha = SHA256.Create();
        var checksum = sha.ComputeHash(content.ToArray());
        return (BitConverter.ToString(checksum).Replace("-", string.Empty).ToLowerInvariant(), orderedFiles.Length);
    }

    private static string? TryGetGitCommit(string workingDirectory)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "rev-parse --short HEAD",
                    WorkingDirectory = workingDirectory,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                }
            };

            process.Start();
            if (!process.WaitForExit(3000))
            {
                try
                {
                    process.Kill();
                }
                catch
                {
                    // Ignore cleanup failures.
                }

                return null;
            }

            var output = process.StandardOutput.ReadToEnd().Trim();
            return process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output)
                ? output
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static void MoveWindowsDeveloperBackupOutOfRelease(string buildFolder)
    {
        var source = Path.Combine(buildFolder, "WrightTycoon_BackUpThisFolder_ButDontShipItWithYourGame");
        if (!Directory.Exists(source))
        {
            return;
        }

        var platformRoot = Directory.GetParent(buildFolder)?.FullName;
        if (string.IsNullOrWhiteSpace(platformRoot))
        {
            return;
        }

        var developerArtifactsRoot = Path.Combine(platformRoot, "Developer");
        Directory.CreateDirectory(developerArtifactsRoot);

        var target = Path.Combine(developerArtifactsRoot, Path.GetFileName(source));
        if (Directory.Exists(target))
        {
            Directory.Delete(target, true);
        }

        Directory.Move(source, target);
        Debug.Log($"[Build] Moved developer-only backup payload to: {target}");
    }
}
