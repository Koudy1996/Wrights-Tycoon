using System;
using UnityEngine;
using WrightTycoon.Core.Airframes;
using WrightTycoon.Core.Engines;

namespace WrightTycoon.Game.UI;

public static class CartoonAircraftPainter
{
    private static Texture2D? _pixelTexture;
    private static Texture2D? _circleTexture;

    public static void Draw(Rect rect, AirframeParameters parameters, EngineType engineType, double bypassRatio = 0d)
    {
        if (Event.current.type != EventType.Repaint)
        {
            return;
        }

        var pixel = GetPixelTexture();
        var circle = GetCircleTexture();

        DrawRect(rect, new Color(0.94f, 0.97f, 1f, 1f), pixel);
        DrawRect(new Rect(rect.x + 6f, rect.y + 6f, rect.width - 12f, rect.height - 12f), new Color(0.83f, 0.90f, 0.98f, 1f), pixel);

        var centerX = rect.x + (rect.width * 0.5f);
        var centerY = rect.y + (rect.height * 0.56f);
        var wingSpan = Mathf.Clamp((float)(parameters.WingAreaSquareMeters * 2.35d), 70f, rect.width - 32f);
        var fuselageLength = Mathf.Clamp(120f + (float)(parameters.FuelAmountLiters * 0.024d), 130f, rect.width - 24f);
        var fuselageHeight = Mathf.Clamp(18f + (parameters.CrewCount * 2.1f), 20f, 40f);
        var wingThickness = Mathf.Clamp(9f + (float)(parameters.WingAreaSquareMeters * 0.07d), 10f, 17f);

        var fuselageColor = parameters.MaterialType switch
        {
            MaterialType.WoodFabric => new Color(0.86f, 0.73f, 0.54f, 1f),
            MaterialType.Aluminum => new Color(0.77f, 0.83f, 0.89f, 1f),
            MaterialType.Composite => new Color(0.66f, 0.74f, 0.80f, 1f),
            _ => new Color(0.78f, 0.80f, 0.84f, 1f),
        };
        var wingColor = new Color(0.28f, 0.54f, 0.76f, 1f);
        var accentColor = new Color(0.95f, 0.58f, 0.22f, 1f);
        var outline = new Color(0.15f, 0.21f, 0.31f, 1f);

        var fuselage = new Rect(centerX - (fuselageLength * 0.5f), centerY - (fuselageHeight * 0.5f), fuselageLength, fuselageHeight);
        DrawRect(fuselage, fuselageColor, pixel);
        DrawRect(new Rect(fuselage.x + 4f, fuselage.y + 4f, fuselage.width - 8f, 2f), new Color(1f, 1f, 1f, 0.35f), pixel);
        DrawRect(new Rect(fuselage.x, fuselage.yMax - 2f, fuselage.width, 2f), outline, pixel);

        var wingCenterOffset = parameters.WingPosition switch
        {
            WingPosition.High => -11f,
            WingPosition.Low => 7f,
            WingPosition.Biplane => -8f,
            _ => 0f,
        };
        var hasLeadingEdgeFlaps = parameters.HasLeadingEdgeFlaps || parameters.FlapType == FlapType.LeadingEdge;

        DrawWing(centerX, centerY + wingCenterOffset, wingSpan, wingThickness, parameters.WingShape, wingColor, outline, pixel);
        if (hasLeadingEdgeFlaps)
        {
            DrawRect(new Rect(centerX - (wingSpan * 0.46f), centerY + wingCenterOffset - (wingThickness * 0.5f) - 2f, wingSpan * 0.92f, 2f), accentColor, pixel);
        }

        if (parameters.WingPosition == WingPosition.Biplane)
        {
            DrawWing(centerX, centerY + 13f, wingSpan * 0.92f, wingThickness * 0.94f, parameters.WingShape, wingColor, outline, pixel);
            if (hasLeadingEdgeFlaps)
            {
                DrawRect(new Rect(centerX - (wingSpan * 0.42f), centerY + 13f - (wingThickness * 0.47f) - 2f, wingSpan * 0.84f, 2f), accentColor, pixel);
            }
            DrawRect(new Rect(centerX - 24f, centerY - 3f, 3f, 12f), outline, pixel);
            DrawRect(new Rect(centerX + 21f, centerY - 3f, 3f, 12f), outline, pixel);
        }

        DrawTail(parameters.TailType, fuselage, accentColor, outline, pixel);
        DrawEngines(parameters.EngineCount, engineType, bypassRatio, fuselage, wingCenterOffset, wingSpan, outline, pixel, circle);
        DrawLandingGear(parameters.LandingGearType, fuselage, outline, pixel, circle);
        DrawCowlingNose(parameters.EngineCowling, fuselage, accentColor, outline, pixel);
    }

    private static void DrawWing(
        float centerX,
        float centerY,
        float wingSpan,
        float wingThickness,
        WingShape wingShape,
        Color wingColor,
        Color outline,
        Texture2D pixel)
    {
        switch (wingShape)
        {
            case WingShape.Straight:
                DrawRect(new Rect(centerX - (wingSpan * 0.5f), centerY - (wingThickness * 0.5f), wingSpan, wingThickness), wingColor, pixel);
                break;
            case WingShape.Swept:
                DrawRotatedRect(new Rect(centerX - (wingSpan * 0.52f), centerY - (wingThickness * 0.5f), wingSpan * 0.54f, wingThickness), -12f, wingColor, pixel);
                DrawRotatedRect(new Rect(centerX - (wingSpan * 0.02f), centerY - (wingThickness * 0.5f), wingSpan * 0.54f, wingThickness), 12f, wingColor, pixel);
                break;
            case WingShape.Delta:
                DrawRotatedRect(new Rect(centerX - (wingSpan * 0.58f), centerY - (wingThickness * 0.5f), wingSpan * 0.6f, wingThickness * 1.05f), -20f, wingColor, pixel);
                DrawRotatedRect(new Rect(centerX - (wingSpan * 0.02f), centerY - (wingThickness * 0.5f), wingSpan * 0.6f, wingThickness * 1.05f), 20f, wingColor, pixel);
                break;
        }

        DrawRect(new Rect(centerX - (wingSpan * 0.5f), centerY + (wingThickness * 0.5f), wingSpan, 2f), outline, pixel);
    }

    private static void DrawTail(TailType tailType, Rect fuselage, Color accent, Color outline, Texture2D pixel)
    {
        var tailBaseX = fuselage.xMin + 8f;
        var tailBaseY = fuselage.yMin + 3f;

        switch (tailType)
        {
            case TailType.Classic:
                DrawRect(new Rect(tailBaseX - 14f, tailBaseY + 7f, 24f, 5f), accent, pixel);
                DrawRect(new Rect(tailBaseX + 4f, tailBaseY - 8f, 4f, 15f), accent, pixel);
                break;
            case TailType.TTail:
                DrawRect(new Rect(tailBaseX + 4f, tailBaseY - 12f, 4f, 20f), accent, pixel);
                DrawRect(new Rect(tailBaseX - 9f, tailBaseY - 12f, 28f, 5f), accent, pixel);
                break;
            case TailType.VTail:
                DrawRotatedRect(new Rect(tailBaseX - 2f, tailBaseY - 10f, 4f, 16f), -27f, accent, pixel);
                DrawRotatedRect(new Rect(tailBaseX + 9f, tailBaseY - 10f, 4f, 16f), 27f, accent, pixel);
                break;
            case TailType.Twin:
                DrawRect(new Rect(tailBaseX - 10f, tailBaseY + 7f, 26f, 4f), accent, pixel);
                DrawRect(new Rect(tailBaseX - 8f, tailBaseY - 9f, 4f, 15f), accent, pixel);
                DrawRect(new Rect(tailBaseX + 10f, tailBaseY - 9f, 4f, 15f), accent, pixel);
                break;
        }

        DrawRect(new Rect(tailBaseX - 15f, tailBaseY + 11f, 32f, 2f), outline, pixel);
    }

    private static void DrawEngines(
        int engineCount,
        EngineType engineType,
        double bypassRatio,
        Rect fuselage,
        float wingCenterOffset,
        float wingSpan,
        Color outline,
        Texture2D pixel,
        Texture2D circle)
    {
        var y = fuselage.center.y + wingCenterOffset - 1f;
        var engineXs = engineCount switch
        {
            1 => new[] { fuselage.xMax - 6f },
            2 => new[] { fuselage.center.x - (wingSpan * 0.26f), fuselage.center.x + (wingSpan * 0.26f) },
            3 => new[] { fuselage.xMax - 6f, fuselage.center.x - (wingSpan * 0.26f), fuselage.center.x + (wingSpan * 0.26f) },
            4 => new[] { fuselage.center.x - (wingSpan * 0.32f), fuselage.center.x - (wingSpan * 0.14f), fuselage.center.x + (wingSpan * 0.14f), fuselage.center.x + (wingSpan * 0.32f) },
            5 => new[] { fuselage.center.x - (wingSpan * 0.34f), fuselage.center.x - (wingSpan * 0.18f), fuselage.xMax - 6f, fuselage.center.x + (wingSpan * 0.18f), fuselage.center.x + (wingSpan * 0.34f) },
            6 => new[] { fuselage.center.x - (wingSpan * 0.36f), fuselage.center.x - (wingSpan * 0.2f), fuselage.center.x - (wingSpan * 0.04f), fuselage.center.x + (wingSpan * 0.04f), fuselage.center.x + (wingSpan * 0.2f), fuselage.center.x + (wingSpan * 0.36f) },
            _ => new[] { fuselage.xMax - 6f },
        };

        for (var index = 0; index < engineXs.Length; index++)
        {
            DrawSingleEngine(new Vector2(engineXs[index], y), engineType, bypassRatio, outline, pixel, circle);
        }

        DrawRect(new Rect(fuselage.xMin, y + 12f, fuselage.width, 1.5f), outline, pixel);
    }

    private static void DrawSingleEngine(
        Vector2 center,
        EngineType engineType,
        double bypassRatio,
        Color outline,
        Texture2D pixel,
        Texture2D circle)
    {
        var isJet = engineType is EngineType.Jet or EngineType.Turbojet or EngineType.Turbofan;
        var isHighBypassTurbofan = engineType == EngineType.Turbofan && bypassRatio > 10d;

        if (!isJet)
        {
            var nacelleColor = new Color(0.79f, 0.24f, 0.22f, 1f);
            var pistonRadius = 7f;
            DrawCircle(center, pistonRadius, nacelleColor, circle);
            DrawRect(new Rect(center.x + pistonRadius - 1f, center.y - 8f, 2f, 16f), outline, pixel);
            DrawRect(new Rect(center.x + pistonRadius - 7f, center.y - 1f, 14f, 2f), outline, pixel);
            return;
        }

        var nacelleBaseColor = new Color(0.33f, 0.38f, 0.45f, 1f);
        var fanColor = new Color(0.72f, 0.77f, 0.84f, 1f);

        if (engineType == EngineType.Turbojet || engineType == EngineType.Jet)
        {
            var length = 20f;
            var height = 10f;
            DrawRect(new Rect(center.x - (length * 0.5f), center.y - (height * 0.5f), length, height), nacelleBaseColor, pixel);
            DrawCircle(new Vector2(center.x + (length * 0.5f), center.y), 5.5f, fanColor, circle);
            DrawCircle(new Vector2(center.x - (length * 0.5f), center.y), 3.2f, new Color(0.14f, 0.17f, 0.22f, 1f), circle);
            DrawRect(new Rect(center.x - (length * 0.5f) - 8f, center.y - 2f, 7f, 4f), new Color(0.93f, 0.56f, 0.18f, 1f), pixel);
            return;
        }

        var turbofanRadius = isHighBypassTurbofan ? 10.5f : 8.2f;
        DrawCircle(center, turbofanRadius, nacelleBaseColor, circle);
        DrawCircle(center, turbofanRadius * 0.58f, fanColor, circle);
        DrawCircle(center, turbofanRadius * 0.28f, new Color(0.14f, 0.18f, 0.24f, 1f), circle);

        if (isHighBypassTurbofan)
        {
            DrawRect(new Rect(center.x - 3f, center.y + turbofanRadius + 1f, 6f, 2f), outline, pixel);
        }
    }

    private static void DrawLandingGear(LandingGearType gearType, Rect fuselage, Color outline, Texture2D pixel, Texture2D circle)
    {
        var baseY = fuselage.yMax + 13f;
        var leftX = fuselage.center.x - 28f;
        var rightX = fuselage.center.x + 20f;
        var noseX = fuselage.xMax - 8f;

        switch (gearType)
        {
            case LandingGearType.Fixed:
                DrawRect(new Rect(leftX, fuselage.yMax, 2f, 12f), outline, pixel);
                DrawRect(new Rect(rightX, fuselage.yMax, 2f, 12f), outline, pixel);
                DrawCircle(new Vector2(leftX + 1f, baseY), 5f, new Color(0.08f, 0.08f, 0.1f, 1f), circle);
                DrawCircle(new Vector2(rightX + 1f, baseY), 5f, new Color(0.08f, 0.08f, 0.1f, 1f), circle);
                DrawCircle(new Vector2(noseX, baseY - 1f), 3.8f, new Color(0.08f, 0.08f, 0.1f, 1f), circle);
                break;
            case LandingGearType.SemiRetractable:
                DrawRect(new Rect(leftX + 2f, fuselage.yMax + 2f, 2f, 7f), outline, pixel);
                DrawRect(new Rect(rightX - 2f, fuselage.yMax + 2f, 2f, 7f), outline, pixel);
                DrawCircle(new Vector2(leftX + 3f, baseY - 2f), 4.5f, new Color(0.10f, 0.10f, 0.12f, 1f), circle);
                DrawCircle(new Vector2(rightX - 1f, baseY - 2f), 4.5f, new Color(0.10f, 0.10f, 0.12f, 1f), circle);
                break;
            case LandingGearType.Retractable:
                DrawRect(new Rect(leftX - 3f, fuselage.yMax + 3f, 11f, 2f), new Color(0.20f, 0.24f, 0.30f, 1f), pixel);
                DrawRect(new Rect(rightX - 5f, fuselage.yMax + 3f, 11f, 2f), new Color(0.20f, 0.24f, 0.30f, 1f), pixel);
                break;
        }
    }

    private static void DrawCowlingNose(double cowling, Rect fuselage, Color accent, Color outline, Texture2D pixel)
    {
        var cowlingAmount = Mathf.Clamp01((float)cowling);
        var width = Mathf.Lerp(16f, 7f, cowlingAmount);
        var height = Mathf.Lerp(20f, 13f, cowlingAmount);
        var nose = new Rect(fuselage.xMax - 2f, fuselage.center.y - (height * 0.5f), width, height);

        DrawRect(nose, accent, pixel);
        DrawRect(new Rect(nose.x + 1f, nose.y + 1f, nose.width - 2f, 2f), new Color(1f, 1f, 1f, 0.35f), pixel);
        DrawRect(new Rect(nose.x, nose.yMax - 2f, nose.width, 2f), outline, pixel);
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


