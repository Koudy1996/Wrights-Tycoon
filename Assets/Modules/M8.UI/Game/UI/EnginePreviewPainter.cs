using System.Collections.Generic;
using UnityEngine;

namespace WrightTycoon.Game.UI;

public enum EnginePreviewKind
{
    InlinePiston = 0,
    VPiston = 1,
    RadialPiston = 2,
    Turbojet = 3,
    LowBypassTurbofan = 4,
    HighBypassTurbofan = 5,
}

public static class EnginePreviewPainter
{
    private static readonly Dictionary<EnginePreviewKind, Texture2D?> CachedTexturesByKind = new();
    private static Texture2D? _pixelTexture;
    private static Texture2D? _circleTexture;

    public static string GetLabel(EnginePreviewKind kind) =>
        kind switch
        {
            EnginePreviewKind.InlinePiston => "Inline Piston",
            EnginePreviewKind.VPiston => "V Piston",
            EnginePreviewKind.RadialPiston => "Radial Piston",
            EnginePreviewKind.Turbojet => "Turbojet",
            EnginePreviewKind.LowBypassTurbofan => "Low-bypass Turbofan",
            EnginePreviewKind.HighBypassTurbofan => "High-bypass Turbofan",
            _ => kind.ToString(),
        };

    public static void Draw(Rect rect, EnginePreviewKind kind)
    {
        if (Event.current.type != EventType.Repaint)
        {
            return;
        }

        var importedTexture = LoadImportedTexture(kind);
        if (importedTexture != null)
        {
            DrawImportedTextureCard(rect, importedTexture);
            return;
        }

        DrawFallbackCard(rect, kind);
    }

    private static Texture2D? LoadImportedTexture(EnginePreviewKind kind)
    {
        if (CachedTexturesByKind.TryGetValue(kind, out var cached) && cached != null)
        {
            return cached;
        }

        var texture = Resources.Load<Texture2D>(GetResourcePath(kind));
        CachedTexturesByKind[kind] = texture;
        return texture;
    }

    private static string GetResourcePath(EnginePreviewKind kind) =>
        kind switch
        {
            EnginePreviewKind.InlinePiston => "WT03/Previews/Engines/inline_piston",
            EnginePreviewKind.VPiston => "WT03/Previews/Engines/v_piston",
            EnginePreviewKind.RadialPiston => "WT03/Previews/Engines/radial_piston",
            EnginePreviewKind.Turbojet => "WT03/Previews/Engines/turbojet",
            EnginePreviewKind.LowBypassTurbofan => "WT03/Previews/Engines/turbofan_low_bypass",
            EnginePreviewKind.HighBypassTurbofan => "WT03/Previews/Engines/turbofan_high_bypass",
            _ => "WT03/Previews/Engines/inline_piston",
        };

    private static void DrawImportedTextureCard(Rect rect, Texture2D texture)
    {
        var pixel = GetPixelTexture();
        DrawRect(rect, new Color(0.93f, 0.95f, 0.99f, 1f), pixel);
        DrawRect(new Rect(rect.x + 4f, rect.y + 4f, rect.width - 8f, rect.height - 8f), new Color(0.2f, 0.25f, 0.34f, 1f), pixel);
        GUI.DrawTexture(new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, rect.height - 16f), texture, ScaleMode.ScaleToFit, true);
    }

    private static void DrawFallbackCard(Rect rect, EnginePreviewKind kind)
    {
        var pixel = GetPixelTexture();
        var circle = GetCircleTexture();

        DrawRect(rect, new Color(0.94f, 0.97f, 1f, 1f), pixel);
        DrawRect(new Rect(rect.x + 6f, rect.y + 6f, rect.width - 12f, rect.height - 12f), new Color(0.84f, 0.90f, 0.98f, 1f), pixel);

        var center = rect.center;
        var baseColor = new Color(0.28f, 0.35f, 0.45f, 1f);
        var accent = new Color(0.94f, 0.56f, 0.22f, 1f);
        var detail = new Color(0.76f, 0.81f, 0.88f, 1f);

        switch (kind)
        {
            case EnginePreviewKind.InlinePiston:
                DrawInlinePiston(center, baseColor, accent, detail, pixel, circle);
                break;
            case EnginePreviewKind.VPiston:
                DrawVPiston(center, baseColor, accent, detail, pixel, circle);
                break;
            case EnginePreviewKind.RadialPiston:
                DrawRadialPiston(center, baseColor, accent, detail, pixel, circle);
                break;
            case EnginePreviewKind.Turbojet:
                DrawTurbojet(center, baseColor, accent, pixel, circle);
                break;
            case EnginePreviewKind.LowBypassTurbofan:
                DrawTurbofan(center, baseColor, detail, accent, false, pixel, circle);
                break;
            case EnginePreviewKind.HighBypassTurbofan:
                DrawTurbofan(center, baseColor, detail, accent, true, pixel, circle);
                break;
        }
    }

    private static void DrawInlinePiston(
        Vector2 center,
        Color baseColor,
        Color accent,
        Color detail,
        Texture2D pixel,
        Texture2D circle)
    {
        var block = new Rect(center.x - 86f, center.y - 24f, 172f, 48f);
        DrawRect(block, baseColor, pixel);
        DrawRect(new Rect(block.x + 8f, block.y + 8f, block.width - 16f, 7f), detail, pixel);

        for (var i = 0; i < 4; i++)
        {
            var x = block.x + 16f + (i * 36f);
            DrawRect(new Rect(x, block.y - 12f, 24f, 12f), accent, pixel);
        }

        DrawCircle(new Vector2(block.xMax + 14f, center.y), 14f, new Color(0.15f, 0.19f, 0.24f, 1f), circle);
        DrawRect(new Rect(block.xMax + 22f, center.y - 2f, 18f, 4f), detail, pixel);
    }

    private static void DrawVPiston(
        Vector2 center,
        Color baseColor,
        Color accent,
        Color detail,
        Texture2D pixel,
        Texture2D circle)
    {
        var core = new Rect(center.x - 24f, center.y - 20f, 48f, 40f);
        DrawRect(core, baseColor, pixel);

        DrawRotatedRect(new Rect(center.x - 96f, center.y - 13f, 80f, 26f), -22f, baseColor, pixel);
        DrawRotatedRect(new Rect(center.x + 16f, center.y - 13f, 80f, 26f), 22f, baseColor, pixel);

        for (var i = 0; i < 3; i++)
        {
            DrawRect(new Rect(center.x - 79f + (i * 22f), center.y - 35f, 14f, 8f), accent, pixel);
            DrawRect(new Rect(center.x + 14f + (i * 22f), center.y + 27f, 14f, 8f), accent, pixel);
        }

        DrawCircle(new Vector2(center.x + 108f, center.y), 13f, new Color(0.16f, 0.2f, 0.25f, 1f), circle);
        DrawRect(new Rect(center.x + 119f, center.y - 2f, 16f, 4f), detail, pixel);
    }

    private static void DrawRadialPiston(
        Vector2 center,
        Color baseColor,
        Color accent,
        Color detail,
        Texture2D pixel,
        Texture2D circle)
    {
        DrawCircle(center, 42f, baseColor, circle);
        DrawCircle(center, 18f, detail, circle);

        for (var i = 0; i < 7; i++)
        {
            var angle = (i / 7f) * Mathf.PI * 2f;
            var x = center.x + (Mathf.Cos(angle) * 52f);
            var y = center.y + (Mathf.Sin(angle) * 52f);
            DrawCircle(new Vector2(x, y), 12f, accent, circle);
        }

        DrawRect(new Rect(center.x + 52f, center.y - 3f, 38f, 6f), detail, pixel);
    }

    private static void DrawTurbojet(
        Vector2 center,
        Color baseColor,
        Color accent,
        Texture2D pixel,
        Texture2D circle)
    {
        var body = new Rect(center.x - 96f, center.y - 22f, 192f, 44f);
        DrawRect(body, baseColor, pixel);
        DrawCircle(new Vector2(body.xMax, center.y), 22f, new Color(0.66f, 0.72f, 0.8f, 1f), circle);
        DrawCircle(new Vector2(body.xMin + 8f, center.y), 13f, new Color(0.14f, 0.18f, 0.24f, 1f), circle);

        DrawRotatedRect(new Rect(body.x - 28f, center.y - 6f, 24f, 12f), -8f, accent, pixel);
        DrawRotatedRect(new Rect(body.x - 40f, center.y - 3f, 14f, 6f), -8f, new Color(1f, 0.82f, 0.28f, 1f), pixel);
    }

    private static void DrawTurbofan(
        Vector2 center,
        Color baseColor,
        Color detail,
        Color accent,
        bool highBypass,
        Texture2D pixel,
        Texture2D circle)
    {
        var nacelleLength = highBypass ? 220f : 190f;
        var nacelleHeight = highBypass ? 52f : 44f;
        var fanRadius = highBypass ? 28f : 20f;

        var nacelle = new Rect(center.x - (nacelleLength * 0.56f), center.y - (nacelleHeight * 0.5f), nacelleLength, nacelleHeight);
        DrawRect(nacelle, baseColor, pixel);
        DrawCircle(new Vector2(nacelle.xMax, center.y), fanRadius, detail, circle);
        DrawCircle(new Vector2(nacelle.xMax, center.y), fanRadius * 0.45f, new Color(0.14f, 0.19f, 0.25f, 1f), circle);

        DrawRect(new Rect(nacelle.x + 18f, nacelle.y + nacelle.height - 7f, nacelle.width - 44f, 4f), new Color(0.16f, 0.2f, 0.27f, 1f), pixel);
        DrawRect(new Rect(nacelle.x + 16f, nacelle.y + 3f, nacelle.width - 36f, 5f), new Color(1f, 1f, 1f, 0.28f), pixel);

        if (highBypass)
        {
            DrawRect(new Rect(nacelle.x + 60f, nacelle.y - 10f, 72f, 8f), accent, pixel);
        }
    }

    private static void DrawRect(Rect rect, Color color, Texture2D texture)
    {
        var original = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, texture);
        GUI.color = original;
    }

    private static void DrawRotatedRect(Rect rect, float angleDeg, Color color, Texture2D texture)
    {
        var matrix = GUI.matrix;
        var pivot = rect.center;
        GUIUtility.RotateAroundPivot(angleDeg, pivot);
        DrawRect(rect, color, texture);
        GUI.matrix = matrix;
    }

    private static void DrawCircle(Vector2 center, float radius, Color color, Texture2D texture)
    {
        var rect = new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f);
        DrawRect(rect, color, texture);
    }

    private static Texture2D GetPixelTexture()
    {
        if (_pixelTexture != null)
        {
            return _pixelTexture;
        }

        _pixelTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };
        _pixelTexture.SetPixel(0, 0, Color.white);
        _pixelTexture.Apply();
        return _pixelTexture;
    }

    private static Texture2D GetCircleTexture()
    {
        if (_circleTexture != null)
        {
            return _circleTexture;
        }

        const int size = 64;
        const float radius = size * 0.5f;
        const float edge = radius - 1f;

        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var dx = x - radius + 0.5f;
                var dy = y - radius + 0.5f;
                var distance = Mathf.Sqrt((dx * dx) + (dy * dy));
                if (distance <= edge)
                {
                    texture.SetPixel(x, y, Color.white);
                }
                else if (distance <= radius)
                {
                    var alpha = 1f - Mathf.InverseLerp(edge, radius, distance);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
                else
                {
                    texture.SetPixel(x, y, Color.clear);
                }
            }
        }

        texture.Apply();
        _circleTexture = texture;
        return _circleTexture;
    }
}
