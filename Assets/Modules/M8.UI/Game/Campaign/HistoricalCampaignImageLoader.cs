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
    private Texture2D? _fallbackTexture;

    public Texture2D? LoadForTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return GetFallbackTexture();
        }

        var normalizedTitle = title.Trim();
        if (_cache.TryGetValue(normalizedTitle, out var cachedTexture))
        {
            return cachedTexture;
        }

        var texture = TryLoadTexture(normalizedTitle);
        if (texture is not null)
        {
            _cache[normalizedTitle] = texture;
        }

        if (texture is null && _loggedMissingTitles.Add(normalizedTitle))
        {
            Debug.LogWarning($"[Campaign] No image found for historical event title '{normalizedTitle}'.");
        }

        return texture ?? GetFallbackTexture();
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

        if (_fallbackTexture is not null)
        {
            UnityEngine.Object.Destroy(_fallbackTexture);
            _fallbackTexture = null;
        }
    }

    private static IEnumerable<string> EnumerateSearchDirectories()
    {
        yield return Path.Combine(Application.streamingAssetsPath, "Data", "Campaign", "Images");
        yield return Path.Combine(Application.streamingAssetsPath, "Data", "Campaign");

        var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        yield return Path.Combine(projectRoot, "Data", "Campaign", "Images");
        yield return Path.Combine(projectRoot, "Data", "Campaign");
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
                var matchingPath = FindCaseInsensitiveTitleMatch(directory, normalizedTitle);
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

        return null;
    }

    private static string? FindCaseInsensitiveTitleMatch(string directory, string normalizedTitle)
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
            if (string.Equals(fileNameWithoutExtension, normalizedTitle, StringComparison.OrdinalIgnoreCase))
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

    private Texture2D GetFallbackTexture()
    {
        if (_fallbackTexture is not null)
        {
            return _fallbackTexture;
        }

        const int width = 16;
        const int height = 16;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false);
        var pixels = new Color[width * height];
        var topColor = new Color(0.20f, 0.28f, 0.38f, 1f);
        var bottomColor = new Color(0.08f, 0.11f, 0.16f, 1f);

        for (var y = 0; y < height; y++)
        {
            var t = y / (float)(height - 1);
            var rowColor = Color.Lerp(topColor, bottomColor, t);
            for (var x = 0; x < width; x++)
            {
                var stripe = ((x + y) % 6) == 0;
                pixels[(y * width) + x] = stripe
                    ? Color.Lerp(rowColor, Color.white, 0.08f)
                    : rowColor;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
        texture.name = "HistoricalCampaign_Fallback";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        _fallbackTexture = texture;
        return texture;
    }
}
