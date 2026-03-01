using System;
using UnityEngine;

namespace WrightTycoon.Game.UI;

public static class WrightTycoonGuiScaling
{
    private const float ReferenceWidth = 1920f;
    private const float ReferenceHeight = 1080f;
    private const float MinimumScale = 1f;

    public static Scope Begin()
    {
        return new Scope(CalculateScale());
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

    public readonly struct Scope : IDisposable
    {
        private readonly Matrix4x4 _previousMatrix;

        public Scope(float scale)
        {
            Scale = NormalizeScale(scale);
            _previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(Scale, Scale, 1f)) * _previousMatrix;
        }

        public float Scale { get; }

        public Vector2 VirtualScreenSize => GetVirtualScreenSize(Scale);

        public void Dispose()
        {
            GUI.matrix = _previousMatrix;
        }
    }
}
