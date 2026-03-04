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

        var texture = TryLoadTexture(normalizedTitle);
        _cache[normalizedTitle] = texture;

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

        var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        yield return Path.Combine(projectRoot, "Data", "Campaign", "Images");
    }

    private static IEnumerable<string> EnumerateCandidatePaths(string normalizedTitle)
    {
        foreach (var directory in EnumerateSearchDirectories())
        {
            for (var index = 0; index < SupportedExtensions.Length; index++)
            {
                yield return Path.Combine(directory, normalizedTitle + SupportedExtensions[index]);
            }
        }
    }

    private static Texture2D? TryLoadTexture(string normalizedTitle)
    {
        foreach (var candidatePath in EnumerateCandidatePaths(normalizedTitle))
        {
            if (!File.Exists(candidatePath))
            {
                continue;
            }

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

                texture.name = $"HistoricalCampaign_{normalizedTitle}";
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

        return null;
    }
}
