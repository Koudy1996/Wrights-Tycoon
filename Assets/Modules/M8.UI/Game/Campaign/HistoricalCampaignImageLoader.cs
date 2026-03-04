using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace WrightTycoon.Game.Campaign;

public sealed class HistoricalCampaignImageLoader : IDisposable
{
    private static readonly string[] SupportedExtensions = { ".png", ".jpg", ".jpeg" };

    private readonly Dictionary<string, Texture2D?> _cache = new(StringComparer.Ordinal);
    private readonly HashSet<string> _loggedMissingTitles = new(StringComparer.Ordinal);

    public Texture2D? LoadForTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var normalizedTitle = title.Trim();
        if (_cache.TryGetValue(normalizedTitle, out var cachedTexture))
        {
            return cachedTexture;
        }

        var candidateBaseNames = BuildCandidateBaseNames(normalizedTitle);
        var texture = TryLoadTexture(candidateBaseNames);
        if (texture is not null)
        {
            _cache[normalizedTitle] = texture;
        }

        if (texture is null && _loggedMissingTitles.Add(normalizedTitle))
        {
            Debug.LogWarning($"[Campaign] No image found for historical event title '{normalizedTitle}'.");
        }

        return texture;
    }

    public void Dispose()
    {
        foreach (var texture in _cache.Values)
        {
            if (texture is not null)
            {
                UnityEngine.Object.Destroy(texture);
            }
        }

        _cache.Clear();
        _loggedMissingTitles.Clear();
    }

    private static IEnumerable<string> EnumerateSearchDirectories()
    {
        yield return Path.Combine(Application.streamingAssetsPath, "Data", "Campaign", "Images");
        yield return Path.Combine(Application.streamingAssetsPath, "Data", "Campaign");

        var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        yield return Path.Combine(projectRoot, "Data", "Campaign", "Images");
        yield return Path.Combine(projectRoot, "Data", "Campaign");
    }

    private static IEnumerable<string> EnumerateCandidatePaths(IReadOnlyList<string> candidateBaseNames)
    {
        foreach (var directory in EnumerateSearchDirectories())
        {
            for (var nameIndex = 0; nameIndex < candidateBaseNames.Count; nameIndex++)
            {
                var baseName = candidateBaseNames[nameIndex];
                for (var extensionIndex = 0; extensionIndex < SupportedExtensions.Length; extensionIndex++)
                {
                    yield return Path.Combine(directory, baseName + SupportedExtensions[extensionIndex]);
                }
            }
        }
    }

    private static Texture2D? TryLoadTexture(IReadOnlyList<string> candidateBaseNames)
    {
        foreach (var candidatePath in EnumerateCandidatePaths(candidateBaseNames))
        {
            if (!File.Exists(candidatePath))
            {
                continue;
            }

            return LoadTextureFromPath(candidatePath);
        }

        foreach (var directory in EnumerateSearchDirectories())
        {
            if (!Directory.Exists(directory))
            {
                continue;
            }

            try
            {
                var matchingPath = FindCaseInsensitiveTitleMatch(directory, candidateBaseNames);
                if (!string.IsNullOrWhiteSpace(matchingPath))
                {
                    return LoadTextureFromPath(matchingPath);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Campaign] Failed while scanning historical event image directory '{directory}': {ex.Message}");
            }
        }

        foreach (var rootDirectory in EnumerateBroadSearchRoots())
        {
            if (!Directory.Exists(rootDirectory))
            {
                continue;
            }

            try
            {
                var matchingPath = FindCaseInsensitiveTitleMatchRecursive(rootDirectory, candidateBaseNames);
                if (!string.IsNullOrWhiteSpace(matchingPath))
                {
                    return LoadTextureFromPath(matchingPath);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Campaign] Failed while recursively scanning '{rootDirectory}': {ex.Message}");
            }
        }

        return null;
    }

    public static string BuildAlternateFileName(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return "event_image";
        }

        return ToSeparatedLowercase(title.Trim(), separator: '_');
    }

    private static string[] BuildCandidateBaseNames(string normalizedTitle)
    {
        var names = new List<string>(capacity: 4);
        AddCandidateName(names, normalizedTitle);
        AddCandidateName(names, ToSeparatedLowercase(normalizedTitle, separator: '_'));
        AddCandidateName(names, ToSeparatedLowercase(normalizedTitle, separator: '-'));
        AddCandidateName(names, ToConcatenatedLowercase(normalizedTitle));
        return names.ToArray();
    }

    private static IEnumerable<string> EnumerateBroadSearchRoots()
    {
        yield return Application.streamingAssetsPath;
        yield return Application.dataPath;

        var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        yield return Path.Combine(projectRoot, "Data");
    }

    private static string? FindCaseInsensitiveTitleMatch(string directory, IReadOnlyList<string> candidateBaseNames)
    {
        var files = Directory.GetFiles(directory);
        for (var index = 0; index < files.Length; index++)
        {
            var candidatePath = files[index];
            var extension = Path.GetExtension(candidatePath);
            if (!IsSupportedExtension(extension))
            {
                continue;
            }

            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(candidatePath);
            if (MatchesCandidateBaseName(fileNameWithoutExtension, candidateBaseNames))
            {
                return candidatePath;
            }
        }

        return null;
    }

    private static string? FindCaseInsensitiveTitleMatchRecursive(string rootDirectory, IReadOnlyList<string> candidateBaseNames)
    {
        var files = Directory.GetFiles(rootDirectory, "*.*", SearchOption.AllDirectories);
        for (var index = 0; index < files.Length; index++)
        {
            var candidatePath = files[index];
            var extension = Path.GetExtension(candidatePath);
            if (!IsSupportedExtension(extension))
            {
                continue;
            }

            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(candidatePath);
            if (MatchesCandidateBaseName(fileNameWithoutExtension, candidateBaseNames))
            {
                return candidatePath;
            }
        }

        return null;
    }

    private static bool IsSupportedExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return false;
        }

        for (var index = 0; index < SupportedExtensions.Length; index++)
        {
            if (string.Equals(extension, SupportedExtensions[index], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static Texture2D? LoadTextureFromPath(string candidatePath)
    {
        try
        {
            var bytes = File.ReadAllBytes(candidatePath);
            var texture = new Texture2D(2, 2);
            if (!texture.LoadImage(bytes))
            {
                UnityEngine.Object.Destroy(texture);
                Debug.LogWarning($"[Campaign] Failed to decode historical event image '{candidatePath}'.");
                return null;
            }

            texture.name = $"HistoricalCampaign_{Path.GetFileNameWithoutExtension(candidatePath)}";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            return texture;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Campaign] Failed to load historical event image '{candidatePath}': {ex.Message}");
            return null;
        }
    }

    private static bool MatchesCandidateBaseName(string fileNameWithoutExtension, IReadOnlyList<string> candidateBaseNames)
    {
        for (var index = 0; index < candidateBaseNames.Count; index++)
        {
            if (string.Equals(fileNameWithoutExtension, candidateBaseNames[index], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void AddCandidateName(List<string> names, string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return;
        }

        for (var index = 0; index < names.Count; index++)
        {
            if (string.Equals(names[index], candidate, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        names.Add(candidate);
    }

    private static string ToSeparatedLowercase(string value, char separator)
    {
        var characters = new List<char>(value.Length);
        var lastWasSeparator = false;
        for (var index = 0; index < value.Length; index++)
        {
            var current = value[index];
            if (char.IsLetterOrDigit(current))
            {
                characters.Add(char.ToLowerInvariant(current));
                lastWasSeparator = false;
            }
            else if (!lastWasSeparator)
            {
                characters.Add(separator);
                lastWasSeparator = true;
            }
        }

        while (characters.Count > 0 && characters[0] == separator)
        {
            characters.RemoveAt(0);
        }

        while (characters.Count > 0 && characters[^1] == separator)
        {
            characters.RemoveAt(characters.Count - 1);
        }

        return new string(characters.ToArray());
    }

    private static string ToConcatenatedLowercase(string value)
    {
        var characters = new List<char>(value.Length);
        for (var index = 0; index < value.Length; index++)
        {
            var current = value[index];
            if (char.IsLetterOrDigit(current))
            {
                characters.Add(char.ToLowerInvariant(current));
            }
        }

        return new string(characters.ToArray());
    }
}
