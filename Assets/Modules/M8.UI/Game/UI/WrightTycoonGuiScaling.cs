using System;
using System.Collections.Generic;
using UnityEngine;

namespace WrightTycoon.Game.UI;

public static class WrightTycoonGuiScaling
{
    private const float ReferenceWidth = 1920f;
    private const float ReferenceHeight = 1080f;
    private const float MinimumScale = 1f;
    private const int MinimumTextPixelSize = 16;
    private static readonly Dictionary<int, GUISkin> ScaledSkinsByKey = new();

    public static Scope Begin()
    {
        return new Scope(CalculateScale(), MinimumTextPixelSize);
    }

    public static Scope Begin(int minimumTextPixelSize)
    {
        return new Scope(CalculateScale(), minimumTextPixelSize);
    }

    public static float CalculateScale()
    {
        var widthRatio = Screen.width / ReferenceWidth;
        var heightRatio = Screen.height / ReferenceHeight;
        var scale = Mathf.Min(widthRatio, heightRatio);

        if (float.IsNaN(scale) || float.IsInfinity(scale))
        {
            return MinimumScale;
        }

        return Mathf.Max(MinimumScale, scale);
    }

    public static Vector2 GetVirtualScreenSize(float scale)
    {
        var safeScale = NormalizeScale(scale);
        return new Vector2(Screen.width / safeScale, Screen.height / safeScale);
    }

    public static Vector2 GetVirtualMousePosition(float scale)
    {
        var currentEvent = Event.current;
        if (currentEvent is null)
        {
            return Vector2.zero;
        }

        var safeScale = NormalizeScale(scale);
        return currentEvent.mousePosition / safeScale;
    }

    private static float NormalizeScale(float scale)
    {
        if (float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0f)
        {
            return MinimumScale;
        }

        return scale;
    }

    private static GUISkin GetScaledSkin(GUISkin sourceSkin, float scale, int minimumTextPixelSize)
    {
        var safeScale = NormalizeScale(scale);
        var safeMinimumTextPixelSize = Mathf.Max(1, minimumTextPixelSize);
        var scaleBucket = Mathf.Max(100, Mathf.RoundToInt(safeScale * 100f));
        var cacheKey = (scaleBucket * 1000) + safeMinimumTextPixelSize;

        if (ScaledSkinsByKey.TryGetValue(cacheKey, out var cachedSkin) && cachedSkin != null)
        {
            return cachedSkin;
        }

        var scaledSkin = ScriptableObject.Instantiate(sourceSkin);
        var minimumLogicalFontSize = Mathf.CeilToInt(safeMinimumTextPixelSize / safeScale);

        ApplyMinimumFontSize(scaledSkin.box, minimumLogicalFontSize);
        ApplyMinimumFontSize(scaledSkin.button, minimumLogicalFontSize);
        ApplyMinimumFontSize(scaledSkin.label, minimumLogicalFontSize);
        ApplyMinimumFontSize(scaledSkin.textField, minimumLogicalFontSize);
        ApplyMinimumFontSize(scaledSkin.textArea, minimumLogicalFontSize);
        ApplyMinimumFontSize(scaledSkin.toggle, minimumLogicalFontSize);
        ApplyMinimumFontSize(scaledSkin.window, minimumLogicalFontSize);

        if (scaledSkin.customStyles != null)
        {
            for (var index = 0; index < scaledSkin.customStyles.Length; index++)
            {
                ApplyMinimumFontSize(scaledSkin.customStyles[index], minimumLogicalFontSize);
            }
        }

        ScaledSkinsByKey[cacheKey] = scaledSkin;
        return scaledSkin;
    }

    private static void ApplyMinimumFontSize(GUIStyle style, int minimumLogicalFontSize)
    {
        if (style is null)
        {
            return;
        }

        if (style.fontSize < minimumLogicalFontSize)
        {
            style.fontSize = minimumLogicalFontSize;
        }
    }

    public readonly struct Scope : IDisposable
    {
        private readonly Matrix4x4 _previousMatrix;
        private readonly GUISkin _previousSkin;

        public Scope(float scale, int minimumTextPixelSize = MinimumTextPixelSize)
        {
            Scale = NormalizeScale(scale);
            _previousMatrix = GUI.matrix;
            _previousSkin = GUI.skin;
            GUI.matrix = Matrix4x4.Scale(new Vector3(Scale, Scale, 1f)) * _previousMatrix;
            GUI.skin = GetScaledSkin(_previousSkin, Scale, minimumTextPixelSize);
        }

        public float Scale { get; }

        public Vector2 VirtualScreenSize => GetVirtualScreenSize(Scale);

        public void Dispose()
        {
            GUI.skin = _previousSkin;
            GUI.matrix = _previousMatrix;
        }
    }
}
