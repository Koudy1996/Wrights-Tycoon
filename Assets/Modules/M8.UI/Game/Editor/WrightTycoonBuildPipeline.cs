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
    private const string MacArchiveFileName = "WrightTycoon-macOS-Release.tar.gz";
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
        else if (target == BuildTarget.StandaloneOSX)
        {
            CreateMacDistributionArchive(buildFolder);
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

    private static void CreateMacDistributionArchive(string buildFolder)
    {
        var platformRoot = Directory.GetParent(buildFolder)?.FullName;
        if (string.IsNullOrWhiteSpace(platformRoot))
        {
            return;
        }

        var archivePath = Path.Combine(platformRoot, MacArchiveFileName);
        if (File.Exists(archivePath))
        {
            File.Delete(archivePath);
        }

        using var fileStream = File.Create(archivePath);
        using var gzipStream = new System.IO.Compression.GZipStream(
            fileStream,
            System.IO.Compression.CompressionLevel.Optimal);

        var buildDirectory = new DirectoryInfo(buildFolder);
        var relativeRootPath = buildDirectory.Parent?.FullName ?? platformRoot;
        WriteDirectoryEntry(gzipStream, buildDirectory, relativeRootPath);
        gzipStream.Write(new byte[1024], 0, 1024);
        Debug.Log($"[Build] Created macOS distribution archive: {archivePath}");
    }

    private static void WriteDirectoryEntry(Stream archiveStream, DirectoryInfo directory, string relativeRootPath)
    {
        var relativePath = Path.GetRelativePath(relativeRootPath, directory.FullName)
            .Replace('\\', '/')
            .TrimEnd('/') + "/";
        WriteTarHeader(
            archiveStream,
            relativePath,
            0,
            0x1ED,
            '5',
            directory.LastWriteTimeUtc);

        foreach (var childDirectory in directory.GetDirectories().OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
        {
            WriteDirectoryEntry(archiveStream, childDirectory, relativeRootPath);
        }

        foreach (var file in directory.GetFiles().OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
        {
            WriteFileEntry(archiveStream, file, relativeRootPath);
        }
    }

    private static void WriteFileEntry(Stream archiveStream, FileInfo file, string relativeRootPath)
    {
        var relativePath = Path.GetRelativePath(relativeRootPath, file.FullName).Replace('\\', '/');
        var mode = DetermineMacArchiveFileMode(relativePath);

        WriteTarHeader(
            archiveStream,
            relativePath,
            file.Length,
            mode,
            '0',
            file.LastWriteTimeUtc);

        using (var inputStream = file.OpenRead())
        {
            inputStream.CopyTo(archiveStream);
        }

        var padding = 512 - (file.Length % 512);
        if (padding is > 0 and < 512)
        {
            archiveStream.Write(new byte[padding], 0, (int)padding);
        }
    }

    private static int DetermineMacArchiveFileMode(string relativePath)
    {
        var normalizedPath = relativePath.Replace('\\', '/');
        if (normalizedPath.IndexOf("/WrightTycoon.app/Contents/MacOS/", StringComparison.OrdinalIgnoreCase) >= 0 ||
            normalizedPath.IndexOf("/WrightTycoon.app/Contents/Frameworks/", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return 0x1ED; // 0755
        }

        return 0x1A4; // 0644
    }

    private static void WriteTarHeader(
        Stream archiveStream,
        string entryPath,
        long size,
        int mode,
        char typeFlag,
        DateTime lastWriteTimeUtc)
    {
        var header = new byte[512];
        var normalizedPath = entryPath.Replace('\\', '/');
        WriteTarPath(header, normalizedPath);
        WriteTarOctal(header, 100, 8, mode);
        WriteTarOctal(header, 108, 8, 0);
        WriteTarOctal(header, 116, 8, 0);
        WriteTarOctal(header, 124, 12, size);
        var unixTime = new DateTimeOffset(lastWriteTimeUtc).ToUnixTimeSeconds();
        WriteTarOctal(header, 136, 12, unixTime);

        for (var index = 148; index < 156; index++)
        {
            header[index] = (byte)' ';
        }

        header[156] = (byte)typeFlag;
        WriteTarString(header, 257, 6, "ustar");
        WriteTarString(header, 263, 2, "00");

        var checksum = header.Sum(value => value);
        WriteTarChecksum(header, checksum);
        archiveStream.Write(header, 0, header.Length);
    }

    private static void WriteTarPath(byte[] header, string entryPath)
    {
        var pathBytes = Encoding.UTF8.GetBytes(entryPath);
        if (pathBytes.Length <= 100)
        {
            WriteTarBytes(header, 0, 100, pathBytes);
            return;
        }

        var splitIndex = entryPath.LastIndexOf('/');
        while (splitIndex > 0)
        {
            var prefix = entryPath[..splitIndex];
            var name = entryPath[(splitIndex + 1)..];
            var prefixBytes = Encoding.UTF8.GetBytes(prefix);
            var nameBytes = Encoding.UTF8.GetBytes(name);
            if (prefixBytes.Length <= 155 && nameBytes.Length <= 100)
            {
                WriteTarBytes(header, 0, 100, nameBytes);
                WriteTarBytes(header, 345, 155, prefixBytes);
                return;
            }

            splitIndex = entryPath.LastIndexOf('/', splitIndex - 1);
        }

        throw new IOException($"Tar entry path is too long: {entryPath}");
    }

    private static void WriteTarOctal(byte[] header, int offset, int length, long value)
    {
        var width = length - 1;
        var octal = Convert.ToString(value, 8);
        if (octal.Length > width)
        {
            throw new IOException($"Tar field overflow for value {value}.");
        }

        var padded = octal.PadLeft(width, '0');
        var bytes = Encoding.ASCII.GetBytes(padded);
        var start = offset + width - bytes.Length;
        WriteTarBytes(header, start, bytes.Length, bytes);
        header[offset + length - 1] = 0;
    }

    private static void WriteTarChecksum(byte[] header, int checksum)
    {
        var octal = Convert.ToString(checksum, 8).PadLeft(6, '0');
        var bytes = Encoding.ASCII.GetBytes(octal);
        WriteTarBytes(header, 148, bytes.Length, bytes);
        header[154] = 0;
        header[155] = (byte)' ';
    }

    private static void WriteTarString(byte[] header, int offset, int length, string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        WriteTarBytes(header, offset, length, bytes);
    }

    private static void WriteTarBytes(byte[] header, int offset, int length, byte[] bytes)
    {
        var count = Math.Min(length, bytes.Length);
        Array.Copy(bytes, 0, header, offset, count);
    }
}
