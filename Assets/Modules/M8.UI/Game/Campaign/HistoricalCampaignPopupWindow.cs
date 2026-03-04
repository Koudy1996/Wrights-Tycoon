using UnityEngine;
using WrightTycoon.Core.Campaign;

namespace WrightTycoon.Game.Campaign;

public sealed class HistoricalCampaignPopupWindow
{
    private static readonly Vector2 DefaultSize = new(560f, 480f);
    private const float HeaderHeight = 24f;
    private static readonly Color WindowBodyColor = new(0.08f, 0.10f, 0.13f, 1f);
    private static readonly Color ContentPanelColor = new(0.11f, 0.14f, 0.18f, 1f);
    private static readonly Color HeaderColor = new(0.16f, 0.19f, 0.24f, 1f);
    private static readonly Color BorderColor = new(0.30f, 0.36f, 0.44f, 1f);

    private readonly int _windowId = $"{nameof(HistoricalCampaignPopupWindow)}_Runtime".GetHashCode();

    private Rect _windowRect = new(96f, 96f, DefaultSize.x, DefaultSize.y);
    private Vector2 _articleScroll;
    private string _title = string.Empty;
    private string _dateText = string.Empty;
    private string _articleText = string.Empty;
    private string _alternateImageFileName = string.Empty;
    private Texture2D? _image;
    private bool _isOpen;
    private GUIStyle? _headlineStyle;
    private GUIStyle? _dateStyle;
    private GUIStyle? _articleStyle;
    private GUIStyle? _placeholderStyle;
    private GUIStyle? _placeholderHintStyle;
    private GUIStyle? _windowTitleStyle;

    public bool IsOpen => _isOpen;

    public void Show(HistoricalCampaignEventDefinition definition, Texture2D? image)
    {
        _title = definition.Title;
        _dateText = $"{definition.Year:D4}-{definition.Month:D2}";
        _articleText = definition.ArticleText;
        _alternateImageFileName = HistoricalCampaignImageLoader.BuildAlternateFileName(definition.Title);
        _image = image;
        _articleScroll = Vector2.zero;
        _isOpen = true;
    }

    public void Close()
    {
        _isOpen = false;
    }

    public void Draw(Vector2 virtualScreenSize)
    {
        if (!_isOpen)
        {
            return;
        }

        ClampToScreen(virtualScreenSize);
        _windowRect = GUI.Window(_windowId, _windowRect, DrawWindowContents, "Historical Bulletin");
        ClampToScreen(virtualScreenSize);
    }

    private void DrawWindowContents(int windowId)
    {
        DrawSolidRect(new Rect(0f, HeaderHeight, _windowRect.width, _windowRect.height - HeaderHeight), WindowBodyColor);
        DrawSolidRect(new Rect(0f, 0f, _windowRect.width, HeaderHeight), HeaderColor);
        GUI.Label(new Rect(12f, 2f, _windowRect.width - 56f, 20f), "Historical Bulletin", GetWindowTitleStyle());

        var closeButtonRect = new Rect(_windowRect.width - 34f, 4f, 30f, 20f);
        if (GUI.Button(closeButtonRect, "X"))
        {
            Close();
            GUIUtility.ExitGUI();
        }

        var contentRect = new Rect(12f, 30f, _windowRect.width - 24f, _windowRect.height - 42f);
        DrawSolidRect(contentRect, ContentPanelColor);
        GUILayout.BeginArea(contentRect);

        GUILayout.Label(_title, GetHeadlineStyle());
        GUILayout.Space(2f);
        GUILayout.Label(_dateText, GetDateStyle());
        GUILayout.Space(8f);

        var imageHeight = Mathf.Clamp(_windowRect.height * 0.34f, 120f, 180f);
        var imageRect = GUILayoutUtility.GetRect(10f, imageHeight, GUILayout.ExpandWidth(true), GUILayout.Height(imageHeight));
        DrawImagePanel(imageRect);

        GUILayout.Space(8f);
        _articleScroll = GUILayout.BeginScrollView(_articleScroll, GUILayout.ExpandHeight(true));
        GUILayout.Label(_articleText, GetArticleStyle(), GUILayout.ExpandWidth(true));
        GUILayout.EndScrollView();

        GUILayout.EndArea();

        GUI.DragWindow(new Rect(0f, 0f, _windowRect.width - 40f, 24f));
        _ = windowId;
    }

    private void ClampToScreen(Vector2 virtualScreenSize)
    {
        var maxWidth = Mathf.Max(320f, virtualScreenSize.x - 24f);
        var maxHeight = Mathf.Max(240f, virtualScreenSize.y - 24f);
        var minWidth = Mathf.Min(420f, maxWidth);
        var minHeight = Mathf.Min(320f, maxHeight);
        var width = Mathf.Clamp(_windowRect.width, minWidth, maxWidth);
        var height = Mathf.Clamp(_windowRect.height, minHeight, maxHeight);
        _windowRect.width = width;
        _windowRect.height = height;

        var maxX = Mathf.Max(0f, virtualScreenSize.x - _windowRect.width);
        var maxY = Mathf.Max(0f, virtualScreenSize.y - _windowRect.height);
        _windowRect.x = Mathf.Clamp(_windowRect.x, 0f, maxX);
        _windowRect.y = Mathf.Clamp(_windowRect.y, 0f, maxY);
    }

    private void DrawImagePanel(Rect imageRect)
    {
        DrawSolidRect(imageRect, new Color(0.05f, 0.06f, 0.08f, 1f));
        DrawBorder(imageRect, BorderColor);

        if (_image is not null)
        {
            var insetRect = new Rect(imageRect.x + 2f, imageRect.y + 2f, imageRect.width - 4f, imageRect.height - 4f);
            GUI.DrawTexture(insetRect, _image, ScaleMode.ScaleAndCrop);
            return;
        }

        var messageRect = new Rect(imageRect.x + 16f, imageRect.y + 24f, imageRect.width - 32f, 32f);
        var hintRect = new Rect(imageRect.x + 16f, imageRect.y + 62f, imageRect.width - 32f, imageRect.height - 78f);
        GUI.Label(messageRect, "No event image found", GetPlaceholderStyle());
        GUI.Label(
            hintRect,
            $"Accepted: {_title}.png\nAlso works: {_alternateImageFileName}.png",
            GetPlaceholderHintStyle());
    }

    private static void DrawBorder(Rect rect, Color color)
    {
        DrawSolidRect(new Rect(rect.x, rect.y, rect.width, 1f), color);
        DrawSolidRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), color);
        DrawSolidRect(new Rect(rect.x, rect.y, 1f, rect.height), color);
        DrawSolidRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), color);
    }

    private static void DrawSolidRect(Rect rect, Color color)
    {
        var previousColor = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill);
        GUI.color = previousColor;
    }

    private GUIStyle GetHeadlineStyle()
    {
        if (_headlineStyle is not null)
        {
            return _headlineStyle;
        }

        _headlineStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 17,
            fontStyle = FontStyle.Bold,
            wordWrap = true,
        };

        return _headlineStyle;
    }

    private GUIStyle GetDateStyle()
    {
        if (_dateStyle is not null)
        {
            return _dateStyle;
        }

        _dateStyle = new GUIStyle(GUI.skin.label)
        {
            fontStyle = FontStyle.Italic,
            wordWrap = false,
        };

        return _dateStyle;
    }

    private GUIStyle GetArticleStyle()
    {
        if (_articleStyle is not null)
        {
            return _articleStyle;
        }

        _articleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            wordWrap = true,
            richText = false,
        };

        return _articleStyle;
    }

    private GUIStyle GetPlaceholderStyle()
    {
        if (_placeholderStyle is not null)
        {
            return _placeholderStyle;
        }

        _placeholderStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            wordWrap = true,
        };

        return _placeholderStyle;
    }

    private GUIStyle GetPlaceholderHintStyle()
    {
        if (_placeholderHintStyle is not null)
        {
            return _placeholderHintStyle;
        }

        _placeholderHintStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.UpperCenter,
            fontStyle = FontStyle.Italic,
            fontSize = 13,
            wordWrap = true,
        };

        return _placeholderHintStyle;
    }

    private GUIStyle GetWindowTitleStyle()
    {
        if (_windowTitleStyle is not null)
        {
            return _windowTitleStyle;
        }

        _windowTitleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = false,
        };

        return _windowTitleStyle;
    }
}
