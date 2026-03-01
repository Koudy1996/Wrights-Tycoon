using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace WrightTycoon.Game.RuntimeData;

public sealed class WrightTycoonBuildManifest
{
    public int ManifestVersion { get; set; } = 1;

    public string GameVersion { get; set; } = string.Empty;

    public string BuildTimestampUtc { get; set; } = string.Empty;

    public string TargetPlatform { get; set; } = string.Empty;

    public string GitCommit { get; set; } = string.Empty;

    public string BalanceChecksum { get; set; } = string.Empty;

    public int BalanceFileCount { get; set; }
}

public static class WrightTycoonBuildManifestService
{
    public const string ManifestFileName = "WrightTycoon.build.json";
    private const string RuntimeCacheFileName = "build-manifest.json";

    public static WrightTycoonBuildManifest LoadOrCreateRuntimeManifest()
    {
        var manifest = TryReadFromFile(GetPackagedManifestPath())
            ?? TryReadFromFile(GetRuntimeCachePath())
            ?? CreateFallbackManifest();

        TryWriteRuntimeCache(manifest);
        return manifest;
    }

    public static string GetPackagedManifestPath()
    {
        return Path.Combine(WrightTycoonRuntimePaths.GetBuildRootPath(), ManifestFileName);
    }

    public static string GetRuntimeCachePath()
    {
        return WrightTycoonRuntimePaths.GetGeneratedAssetPath(RuntimeCacheFileName);
    }

    public static WrightTycoonBuildManifest CreateFallbackManifest()
    {
        return new WrightTycoonBuildManifest
        {
            GameVersion = string.IsNullOrWhiteSpace(Application.version) ? "0.0.0" : Application.version,
            BuildTimestampUtc = "runtime-generated",
            TargetPlatform = Application.platform.ToString(),
            GitCommit = string.Empty,
            BalanceChecksum = "runtime-generated",
            BalanceFileCount = 0,
        };
    }

    public static WrightTycoonBuildManifest? TryReadFromFile(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            var json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            return JsonConvert.DeserializeObject<WrightTycoonBuildManifest>(json);
        }
        catch
        {
            return null;
        }
    }

    public static void WriteToFile(string path, WrightTycoonBuildManifest manifest)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonConvert.SerializeObject(manifest, Formatting.Indented);
        File.WriteAllText(path, json);
    }

    public static void TryWriteRuntimeCache(WrightTycoonBuildManifest manifest)
    {
        try
        {
            WrightTycoonRuntimePaths.EnsurePlayerInfoLayout();
            WriteToFile(GetRuntimeCachePath(), manifest);
        }
        catch
        {
            // Cache mirroring should not block startup.
        }
    }
}
