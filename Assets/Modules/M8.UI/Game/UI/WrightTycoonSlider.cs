using System.Collections.Generic;
using UnityEngine;

namespace WrightTycoon.Game.UI;

/// <summary>One input path for all horizontal sliders, including the invisible thumb hit halo.</summary>
public static class WrightTycoonSlider
{
    public const float VisualScale = 1.5f;
    public const float HitScale = 1.2f;
    private sealed class DragState { public DragState() { } public float Offset; }
    public static float ThumbWidth => Mathf.Max(10f, GUI.skin.horizontalSliderThumb.fixedWidth) * VisualScale;
    public static float ThumbHeight => Mathf.Max(10f, GUI.skin.horizontalSliderThumb.fixedHeight) * VisualScale;
    public static Rect ThumbRect(Rect rect, float value, float min, float max)
    {
        var width = Mathf.Min(ThumbWidth, rect.width);
        var center = Mathf.Lerp(rect.x + width * .5f, rect.xMax - width * .5f,
            Mathf.InverseLerp(min, max, value));
        return new Rect(center - width * .5f, rect.center.y - ThumbHeight * .5f, width, ThumbHeight);
    }
    public static Rect HitRect(Rect thumb) => new(thumb.center.x - thumb.width * HitScale * .5f,
        thumb.center.y - thumb.height * HitScale * .5f, thumb.width * HitScale, thumb.height * HitScale);
    public static float Layout(float value, float min, float max, params GUILayoutOption[] options)
    {
        var layout = new List<GUILayoutOption>(options) { GUILayout.ExpandWidth(true), GUILayout.MinHeight(ThumbHeight * HitScale) };
        return Horizontal(GUILayoutUtility.GetRect(1f, ThumbHeight * HitScale, layout.ToArray()), value, min, max);
    }
    public static float Horizontal(Rect rect, float value, float min, float max)
    {
        value = Mathf.Clamp(value, min, max);
        var id = GUIUtility.GetControlID("WT accessible slider".GetHashCode(), FocusType.Keyboard, rect);
        var state = (DragState)GUIUtility.GetStateObject(typeof(DragState), id);
        var thumb = ThumbRect(rect, value, min, max);
        var hit = HitRect(thumb);
        var evt = Event.current;
        float PositionValue(float x) => Mathf.Lerp(min, max, Mathf.Clamp01(
            (x - rect.x - thumb.width * .5f) / Mathf.Max(1f, rect.width - thumb.width)));
        switch (evt.GetTypeForControl(id))
        {
            case EventType.MouseDown when GUI.enabled && evt.button == 0 && (hit.Contains(evt.mousePosition) || rect.Contains(evt.mousePosition)):
                GUIUtility.hotControl = GUIUtility.keyboardControl = id;
                state.Offset = hit.Contains(evt.mousePosition) ? evt.mousePosition.x - thumb.center.x : 0f;
                value = PositionValue(evt.mousePosition.x - state.Offset);
                GUI.changed = true;
                evt.Use();
                break;
            case EventType.MouseDrag when GUIUtility.hotControl == id:
                if (GUI.enabled) { value = PositionValue(evt.mousePosition.x - state.Offset); GUI.changed = true; }
                evt.Use();
                break;
            case EventType.MouseUp when GUIUtility.hotControl == id && evt.button == 0:
                GUIUtility.hotControl = 0;
                evt.Use();
                break;
            case EventType.KeyDown when GUI.enabled && GUIUtility.keyboardControl == id:
                if (evt.keyCode == KeyCode.LeftArrow || evt.keyCode == KeyCode.RightArrow
                    || evt.keyCode == KeyCode.Home || evt.keyCode == KeyCode.End)
                {
                    value = evt.keyCode == KeyCode.Home ? min : evt.keyCode == KeyCode.End ? max
                        : Mathf.Clamp(value + (evt.keyCode == KeyCode.LeftArrow ? -1f : 1f) * (max - min) * (evt.shift ? .1f : .01f), min, max);
                    GUI.changed = true;
                    evt.Use();
                }
                break;
            case EventType.Repaint:
                var track = new Rect(rect.x, rect.center.y - 3f, rect.width, 6f);
                GUI.skin.horizontalSlider.Draw(track, GUIContent.none, rect.Contains(evt.mousePosition), false, false, false);
                GUI.skin.horizontalSliderThumb.Draw(thumb, GUIContent.none, hit.Contains(evt.mousePosition), GUIUtility.hotControl == id,
                    false, GUIUtility.keyboardControl == id);
                break;
        }
        return value;
    }
}
