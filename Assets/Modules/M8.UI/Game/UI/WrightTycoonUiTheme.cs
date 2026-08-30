using System;
using System.Collections.Generic;
using UnityEngine;

namespace WrightTycoon.Game.UI;

/// <summary>
/// Central visual theme for every in-game screen.
/// The theme deliberately contains no game state; changing it cannot affect saves or simulation behavior.
/// </summary>
public static class WrightTycoonUiTheme
{
    public const string PageHeaderStyleName = "WT Page Header";
    public const string PageTitleStyleName = "WT Page Title";
    public const string PageSubtitleStyleName = "WT Page Subtitle";
    public const string CardStyleName = "WT Card";
    public const string SelectedCardStyleName = "WT Selected Card";
    public const string CardHeaderStyleName = "WT Card Header";
    public const string SectionHeaderStyleName = "WT Section Header";
    public const string MutedLabelStyleName = "WT Muted Label";
    public const string AccentBadgeStyleName = "WT Accent Badge";
    public const string SuccessBadgeStyleName = "WT Success Badge";
    public const string WarningBadgeStyleName = "WT Warning Badge";
    public const string DangerBadgeStyleName = "WT Danger Badge";
    public const string NeutralBadgeStyleName = "WT Neutral Badge";
    public const string PrimaryButtonStyleName = "WT Primary Button";
    public const string SecondaryButtonStyleName = "WT Secondary Button";
    public const string DangerButtonStyleName = "WT Danger Button";
    public const string KpiCardStyleName = "WT KPI Card";
    public const string DesignerKpiCardStyleName = "WT Designer KPI Card";
    public const string KpiLabelStyleName = "WT KPI Label";
    public const string KpiValueStyleName = "WT KPI Value";
    public const string DesignerKpiLabelStyleName = "WT Designer KPI Label";
    public const string DesignerKpiValueStyleName = "WT Designer KPI Value";
    public const string StepDoneStyleName = "WT Step Done";
    public const string StepActiveStyleName = "WT Step Active";
    public const string StepIdleStyleName = "WT Step Idle";
    public const string TableHeaderStyleName = "WT Table Header";

    public static readonly Color Canvas = Hex("050B12");
    public static readonly Color Panel = Hex("07111A");
    public static readonly Color Card = Hex("09141E");
    public static readonly Color CardRaised = Hex("0C1A26");
    public static readonly Color Border = Hex("182735");
    public static readonly Color BorderStrong = Hex("243847");
    public static readonly Color TextPrimary = Hex("DDE6EE");
    public static readonly Color TextMuted = Hex("8F9EAC");
    public static readonly Color Accent = Hex("2F6FB8");
    public static readonly Color AccentHover = Hex("3B82CB");
    public static readonly Color Success = Hex("55C978");
    public static readonly Color Warning = Hex("E0A94F");
    public static readonly Color Danger = Hex("E56363");

    private const int RoundedTextureSize = 20;
    private const int RoundedSlice = 7;
    private const float RoundedRadius = 7f;
    private const float RoundedBorderWidth = 1.15f;

    private static readonly Dictionary<int, GUISkin> ThemedSkins = new();

    public static Scope Begin(bool enabled)
    {
        return enabled ? new Scope(GUI.skin) : default;
    }

    public static GUIStyle GetStyle(string styleName) => GUI.skin.GetStyle(styleName);

    private static GUISkin GetOrCreateSkin(GUISkin source)
    {
        var sourceId = source.GetInstanceID();
        if (ThemedSkins.TryGetValue(sourceId, out var cached) && cached != null)
        {
            return cached;
        }

        var skin = ScriptableObject.Instantiate(source);
        skin.name = $"Wright Tycoon Dark Industrial ({source.name})";

        var panelTexture = CreateRoundedPanelTexture(Panel, Border);
        var cardTexture = CreateRoundedPanelTexture(Card, Border);
        var raisedTexture = CreateRoundedPanelTexture(CardRaised, BorderStrong);
        var selectedTexture = CreateRoundedPanelTexture(Hex("0E2B45"), Accent);
        var inputTexture = CreateRoundedPanelTexture(Hex("050D15"), Border);
        var buttonTexture = CreateRoundedPanelTexture(Hex("0D1822"), BorderStrong);
        var buttonHoverTexture = CreateRoundedPanelTexture(Hex("112534"), Hex("315168"));
        var buttonActiveTexture = CreateRoundedPanelTexture(Hex("0A1A29"), Accent);
        var accentTexture = CreateRoundedPanelTexture(Hex("183F68"), Accent);
        var accentHoverTexture = CreateRoundedPanelTexture(Hex("1F548A"), AccentHover);
        var dangerTexture = CreateRoundedPanelTexture(Hex("3D1D22"), Hex("9D4149"));
        var dangerHoverTexture = CreateRoundedPanelTexture(Hex("52242A"), Danger);

        ConfigureStyle(skin.label, null, null, null, TextPrimary);
        skin.label.wordWrap = true;
        skin.label.padding = new RectOffset(3, 3, 2, 2);

        ConfigureStyle(skin.box, cardTexture, raisedTexture, selectedTexture, TextPrimary);
        skin.box.border = CreateRoundedSlice();
        skin.box.padding = new RectOffset(10, 10, 8, 8);
        skin.box.margin = new RectOffset(3, 3, 3, 3);

        ConfigureStyle(skin.button, buttonTexture, buttonHoverTexture, buttonActiveTexture, TextPrimary);
        skin.button.onNormal.background = accentTexture;
        skin.button.onNormal.textColor = Color.white;
        skin.button.onHover.background = accentHoverTexture;
        skin.button.onHover.textColor = Color.white;
        skin.button.onActive.background = accentHoverTexture;
        skin.button.onActive.textColor = Color.white;
        skin.button.border = CreateRoundedSlice();
        skin.button.padding = new RectOffset(10, 10, 6, 6);
        skin.button.margin = new RectOffset(2, 2, 2, 2);

        ConfigureStyle(skin.textField, inputTexture, raisedTexture, selectedTexture, TextPrimary);
        skin.textField.border = CreateRoundedSlice();
        skin.textField.padding = new RectOffset(8, 8, 5, 5);
        skin.textField.focused.background = selectedTexture;
        skin.textField.focused.textColor = Color.white;

        ConfigureStyle(skin.textArea, inputTexture, raisedTexture, selectedTexture, TextPrimary);
        skin.textArea.border = CreateRoundedSlice();
        skin.textArea.padding = new RectOffset(8, 8, 6, 6);

        ConfigureStyle(skin.toggle, null, null, null, TextPrimary);
        ConfigureStyle(skin.window, panelTexture, panelTexture, panelTexture, TextPrimary);
        skin.window.border = CreateRoundedSlice();
        skin.window.padding = new RectOffset(12, 12, 24, 12);

        ConfigureStyle(skin.horizontalSlider, inputTexture, inputTexture, inputTexture, TextPrimary);
        skin.horizontalSlider.border = CreateRoundedSlice();
        ConfigureStyle(skin.horizontalSliderThumb, accentTexture, accentHoverTexture, accentHoverTexture, TextPrimary);
        skin.horizontalSliderThumb.border = CreateRoundedSlice();
        ConfigureStyle(skin.verticalScrollbar, inputTexture, inputTexture, inputTexture, TextPrimary);
        skin.verticalScrollbar.border = CreateRoundedSlice();
        ConfigureStyle(skin.verticalScrollbarThumb, buttonTexture, buttonHoverTexture, buttonActiveTexture, TextPrimary);
        skin.verticalScrollbarThumb.border = CreateRoundedSlice();
        ConfigureStyle(skin.horizontalScrollbar, inputTexture, inputTexture, inputTexture, TextPrimary);
        skin.horizontalScrollbar.border = CreateRoundedSlice();
        ConfigureStyle(skin.horizontalScrollbarThumb, buttonTexture, buttonHoverTexture, buttonActiveTexture, TextPrimary);
        skin.horizontalScrollbarThumb.border = CreateRoundedSlice();

        var customStyles = new List<GUIStyle>(skin.customStyles ?? Array.Empty<GUIStyle>())
        {
            CreateBoxStyle(PageHeaderStyleName, panelTexture, BorderStrong, 14, 12),
            CreateLabelStyle(PageTitleStyleName, TextPrimary, 24, FontStyle.Bold, TextAnchor.MiddleLeft),
            CreateLabelStyle(PageSubtitleStyleName, TextMuted, 14, FontStyle.Normal, TextAnchor.MiddleLeft),
            CreateBoxStyle(CardStyleName, cardTexture, Border, 12, 10),
            CreateBoxStyle(SelectedCardStyleName, selectedTexture, Accent, 12, 10),
            CreateLabelStyle(CardHeaderStyleName, TextPrimary, 18, FontStyle.Bold, TextAnchor.MiddleLeft),
            CreateBoxStyle(SectionHeaderStyleName, raisedTexture, BorderStrong, 10, 7, 16, FontStyle.Bold),
            CreateLabelStyle(MutedLabelStyleName, TextMuted, 13, FontStyle.Normal, TextAnchor.MiddleLeft),
            CreateBadgeStyle(AccentBadgeStyleName, Hex("173F69"), Accent, Hex("A9D0FF")),
            CreateBadgeStyle(SuccessBadgeStyleName, Hex("173D2A"), Success, Hex("A8F0BD")),
            CreateBadgeStyle(WarningBadgeStyleName, Hex("4A341B"), Warning, Hex("FFD994")),
            CreateBadgeStyle(DangerBadgeStyleName, Hex("4B2227"), Danger, Hex("FFB0B0")),
            CreateBadgeStyle(NeutralBadgeStyleName, Hex("1A2A38"), BorderStrong, TextMuted),
            CreateButtonStyle(PrimaryButtonStyleName, accentTexture, accentHoverTexture, Accent, Color.white),
            CreateButtonStyle(SecondaryButtonStyleName, buttonTexture, buttonHoverTexture, BorderStrong, TextPrimary),
            CreateButtonStyle(DangerButtonStyleName, dangerTexture, dangerHoverTexture, Danger, Color.white),
            CreateBoxStyle(KpiCardStyleName, raisedTexture, BorderStrong, 12, 10),
            CreateBoxStyle(DesignerKpiCardStyleName, accentTexture, Accent, 12, 10),
            CreateLabelStyle(KpiLabelStyleName, TextMuted, 13, FontStyle.Bold, TextAnchor.MiddleLeft),
            CreateLabelStyle(KpiValueStyleName, TextPrimary, 24, FontStyle.Bold, TextAnchor.MiddleLeft),
            CreateLabelStyle(DesignerKpiLabelStyleName, Hex("BFDFFF"), 14, FontStyle.Bold, TextAnchor.MiddleLeft),
            CreateLabelStyle(DesignerKpiValueStyleName, Color.white, 26, FontStyle.Bold, TextAnchor.MiddleLeft),
            CreateBadgeStyle(StepDoneStyleName, Hex("173D2A"), Success, Hex("B6F3C6")),
            CreateBadgeStyle(StepActiveStyleName, Hex("173F69"), Accent, Color.white),
            CreateBadgeStyle(StepIdleStyleName, Hex("111E2A"), Border, TextMuted),
            CreateBoxStyle(TableHeaderStyleName, raisedTexture, BorderStrong, 8, 6, 13, FontStyle.Bold),
        };

        skin.customStyles = customStyles.ToArray();
        ThemedSkins[sourceId] = skin;
        return skin;
    }

    private static GUIStyle CreateLabelStyle(
        string name,
        Color textColor,
        int fontSize,
        FontStyle fontStyle,
        TextAnchor alignment)
    {
        var style = new GUIStyle(GUI.skin.label)
        {
            name = name,
            fontSize = fontSize,
            fontStyle = fontStyle,
            alignment = alignment,
            wordWrap = true,
            padding = new RectOffset(3, 3, 2, 2),
        };
        SetAllTextColors(style, textColor);
        return style;
    }

    private static GUIStyle CreateBoxStyle(
        string name,
        Texture2D background,
        Color borderColor,
        int horizontalPadding,
        int verticalPadding,
        int fontSize = 0,
        FontStyle fontStyle = FontStyle.Normal)
    {
        var style = new GUIStyle(GUI.skin.box)
        {
            name = name,
            border = CreateRoundedSlice(),
            padding = new RectOffset(horizontalPadding, horizontalPadding, verticalPadding, verticalPadding),
            margin = new RectOffset(3, 3, 3, 3),
            fontSize = fontSize,
            fontStyle = fontStyle,
            alignment = TextAnchor.MiddleLeft,
            wordWrap = true,
        };
        style.normal.background = background;
        style.hover.background = CreateRoundedPanelTexture(CardRaised, borderColor);
        style.active.background = style.hover.background;
        SetAllTextColors(style, TextPrimary);
        return style;
    }

    private static GUIStyle CreateBadgeStyle(string name, Color fill, Color border, Color text)
    {
        var texture = CreateRoundedPanelTexture(fill, border);
        var style = new GUIStyle(GUI.skin.box)
        {
            name = name,
            border = CreateRoundedSlice(),
            padding = new RectOffset(9, 9, 5, 5),
            margin = new RectOffset(2, 2, 2, 2),
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            wordWrap = false,
        };
        style.normal.background = texture;
        style.hover.background = texture;
        style.active.background = texture;
        SetAllTextColors(style, text);
        return style;
    }

    private static GUIStyle CreateButtonStyle(
        string name,
        Texture2D normal,
        Texture2D hover,
        Color border,
        Color text)
    {
        var style = new GUIStyle(GUI.skin.button)
        {
            name = name,
            border = CreateRoundedSlice(),
            padding = new RectOffset(12, 12, 7, 7),
            margin = new RectOffset(2, 2, 2, 2),
            fontStyle = FontStyle.Bold,
        };
        style.normal.background = normal;
        style.hover.background = hover;
        style.active.background = CreateRoundedPanelTexture(Card, border);
        style.onNormal.background = hover;
        style.onHover.background = hover;
        SetAllTextColors(style, text);
        return style;
    }

    private static void ConfigureStyle(
        GUIStyle style,
        Texture2D? normal,
        Texture2D? hover,
        Texture2D? active,
        Color textColor)
    {
        if (normal != null)
        {
            style.normal.background = normal;
            style.onNormal.background = active ?? normal;
        }

        if (hover != null)
        {
            style.hover.background = hover;
            style.onHover.background = hover;
        }

        if (active != null)
        {
            style.active.background = active;
            style.focused.background = active;
            style.onActive.background = active;
            style.onFocused.background = active;
        }

        SetAllTextColors(style, textColor);
    }

    private static void SetAllTextColors(GUIStyle style, Color textColor)
    {
        style.normal.textColor = textColor;
        style.hover.textColor = textColor;
        style.active.textColor = textColor;
        style.focused.textColor = textColor;
        style.onNormal.textColor = textColor;
        style.onHover.textColor = textColor;
        style.onActive.textColor = textColor;
        style.onFocused.textColor = textColor;
    }

    private static RectOffset CreateRoundedSlice()
    {
        return new RectOffset(RoundedSlice, RoundedSlice, RoundedSlice, RoundedSlice);
    }

    private static Texture2D CreateRoundedPanelTexture(Color fill, Color border)
    {
        var texture = new Texture2D(
            RoundedTextureSize,
            RoundedTextureSize,
            TextureFormat.RGBA32,
            false)
        {
            name = $"WT UI {ColorUtility.ToHtmlStringRGBA(fill)}",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
        };

        var pixels = new Color[RoundedTextureSize * RoundedTextureSize];
        for (var y = 0; y < RoundedTextureSize; y++)
        {
            for (var x = 0; x < RoundedTextureSize; x++)
            {
                var point = new Vector2(x + 0.5f, y + 0.5f);
                var outerDistance = SignedDistanceToRoundedRectangle(point, 0f, RoundedRadius);
                var innerDistance = SignedDistanceToRoundedRectangle(
                    point,
                    RoundedBorderWidth,
                    Mathf.Max(0f, RoundedRadius - RoundedBorderWidth));
                var outerAlpha = Mathf.Clamp01(0.5f - outerDistance);
                var innerAlpha = Mathf.Clamp01(0.5f - innerDistance);
                var pixel = Color.Lerp(border, fill, innerAlpha);
                pixel.a *= outerAlpha;
                pixels[(y * RoundedTextureSize) + x] = pixel;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private static float SignedDistanceToRoundedRectangle(Vector2 point, float inset, float radius)
    {
        var center = new Vector2(RoundedTextureSize * 0.5f, RoundedTextureSize * 0.5f);
        var halfSize = new Vector2(
            (RoundedTextureSize * 0.5f) - inset,
            (RoundedTextureSize * 0.5f) - inset);
        var local = new Vector2(
            Mathf.Abs(point.x - center.x),
            Mathf.Abs(point.y - center.y));
        var cornerDistance = local - (halfSize - new Vector2(radius, radius));
        var outside = new Vector2(
            Mathf.Max(cornerDistance.x, 0f),
            Mathf.Max(cornerDistance.y, 0f));
        return outside.magnitude
            + Mathf.Min(Mathf.Max(cornerDistance.x, cornerDistance.y), 0f)
            - radius;
    }

    private static Color Hex(string value)
    {
        return ColorUtility.TryParseHtmlString($"#{value}", out var color)
            ? color
            : Color.magenta;
    }

    public readonly struct Scope : IDisposable
    {
        private readonly GUISkin? _previousSkin;

        internal Scope(GUISkin previousSkin)
        {
            _previousSkin = previousSkin;
            GUI.skin = GetOrCreateSkin(previousSkin);
        }

        public void Dispose()
        {
            if (_previousSkin != null)
            {
                GUI.skin = _previousSkin;
            }
        }
    }
}
