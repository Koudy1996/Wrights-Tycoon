using System.IO;
using UnityEngine;

namespace WrightTycoon.Game.RuntimeData;

public static class WrightTycoonRuntimePaths
{
    public const string PlayerInfoFolderName = "Player Info";
    public const string SavesFolderName = "Saves";
    public const string GeneratedAssetsFolderName = "Generated Assets";
    public const string LogsFolderName = "Logs";
    public const string CacheFolderName = "Cache";
    public const string TempFolderName = "Temp";
    public const string ReadmeFileName = "README.txt";

    public static string GetPlayerInfoRootPath()
    {
        return ResolvePlayerInfoRootPath();
    }

    public static string GetPlayerInfoRootPath(string buildRootPath)
    {
        return Path.Combine(NormalizeBuildRootPath(buildRootPath), PlayerInfoFolderName);
    }

    public static string GetSavesDirectory()
    {
        EnsurePlayerInfoLayout();
        return Path.Combine(ResolvePlayerInfoRootPath(), SavesFolderName);
    }

    public static string GetGeneratedAssetsDirectory()
    {
        EnsurePlayerInfoLayout();
        return Path.Combine(ResolvePlayerInfoRootPath(), GeneratedAssetsFolderName);
    }

    public static string GetGeneratedAssetsDirectory(string buildRootPath)
    {
        EnsurePlayerInfoLayout(buildRootPath);
        return Path.Combine(GetPlayerInfoRootPath(buildRootPath), GeneratedAssetsFolderName);
    }

    public static string GetLogsDirectory()
    {
        EnsurePlayerInfoLayout();
        return Path.Combine(ResolvePlayerInfoRootPath(), LogsFolderName);
    }

    public static string GetLogsDirectory(string buildRootPath)
    {
        EnsurePlayerInfoLayout(buildRootPath);
        return Path.Combine(GetPlayerInfoRootPath(buildRootPath), LogsFolderName);
    }

    public static string GetCacheDirectory()
    {
        EnsurePlayerInfoLayout();
        return Path.Combine(ResolvePlayerInfoRootPath(), CacheFolderName);
    }

    public static string GetTempDirectory()
    {
        EnsurePlayerInfoLayout();
        return Path.Combine(ResolvePlayerInfoRootPath(), TempFolderName);
    }

    public static string GetSaveFilePath(string fileName)
    {
        return Path.Combine(GetSavesDirectory(), fileName);
    }

    public static string GetGeneratedAssetPath(string fileName)
    {
        return Path.Combine(GetGeneratedAssetsDirectory(), fileName);
    }

    public static string GetBuildRootPath()
    {
        return ResolveBuildRootPath();
    }

    public static void EnsurePlayerInfoLayout()
    {
        EnsurePlayerInfoLayout(ResolveBuildRootPath());
    }

    public static void EnsurePlayerInfoLayout(string buildRootPath)
    {
        var root = EnsureDirectory(GetPlayerInfoRootPath(buildRootPath));
        EnsureDirectory(Path.Combine(root, SavesFolderName));
        EnsureDirectory(Path.Combine(root, GeneratedAssetsFolderName));
        EnsureDirectory(Path.Combine(root, LogsFolderName));
        EnsureDirectory(Path.Combine(root, CacheFolderName));
        EnsureDirectory(Path.Combine(root, TempFolderName));
        EnsureReadme(root);
    }

    private static string ResolvePlayerInfoRootPath()
    {
        return Path.Combine(ResolveBuildRootPath(), PlayerInfoFolderName);
    }

    private static string ResolveBuildRootPath()
    {
        if (Application.isEditor)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }

        var dataPath = Path.GetFullPath(Application.dataPath);
        if (Application.platform == RuntimePlatform.OSXPlayer)
        {
            var contentsDirectory = Directory.GetParent(dataPath)?.FullName;
            if (!string.IsNullOrWhiteSpace(contentsDirectory))
            {
                var buildRoot = Directory.GetParent(contentsDirectory)?.FullName;
                if (!string.IsNullOrWhiteSpace(buildRoot))
                {
                    return buildRoot;
                }
            }
        }

        var parent = Directory.GetParent(dataPath)?.FullName;
        return string.IsNullOrWhiteSpace(parent) ? dataPath : parent;
    }

    private static string EnsureDirectory(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }

    private static string NormalizeBuildRootPath(string buildRootPath)
    {
        if (string.IsNullOrWhiteSpace(buildRootPath))
        {
            throw new IOException("Build root path cannot be empty.");
        }

        return Path.GetFullPath(buildRootPath);
    }

    private static void EnsureReadme(string root)
    {
        var readmePath = Path.Combine(root, ReadmeFileName);
        const string content =
            "This folder stores Wright Tycoon runtime data.\n" +
            "It is safe to delete. The game will recreate saves, logs, and generated files.\n" +
            "Deleting it resets saves and forces generated outputs to be rebuilt.\n";

        if (!File.Exists(readmePath) || File.ReadAllText(readmePath) != content)
        {
            File.WriteAllText(readmePath, content);
        }
    }
}
