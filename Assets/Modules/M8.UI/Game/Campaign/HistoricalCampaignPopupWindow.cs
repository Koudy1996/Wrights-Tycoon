using UnityEngine;
using WrightTycoon.Core.Campaign;

namespace WrightTycoon.Game.Campaign;

public sealed class HistoricalCampaignPopupWindow
{
    private static readonly Vector2 DefaultSize = new(560f, 480f);

    private readonly int _windowId = $"{nameof(HistoricalCampaignPopupWindow)}_Runtime".GetHashCode();

    private Rect _windowRect = new(96f, 96f, DefaultSize.x, DefaultSize.y);
    private Vector2 _articleScroll;
    private string _title = string.Empty;
    private string _dateText = string.Empty;
    private string _articleText = string.Empty;
    private Texture2D? _image;
    private bool _isOpen;
    private GUIStyle? _headlineStyle;
    private GUIStyle? _dateStyle;
    private GUIStyle? _articleStyle;
    private GUIStyle? _placeholderStyle;

    public bool IsOpen => _isOpen;

    public void Show(HistoricalCampaignEventDefinition definition, Texture2D? image)
    {
        _title = definition.Title;
        _dateText = $"{definition.Year:D4}-{definition.Month:D2}";
        _articleText = definition.ArticleText;
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

        var originalColor = GUI.color;
        GUI.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0.98f);

        try
        {
            _windowRect = GUI.Window(_windowId, _windowRect, DrawWindowContents, "Historical Bulletin");
            ClampToScreen(virtualScreenSize);
        }
        finally
        {
            GUI.color = originalColor;
        }
    }

    private void DrawWindowContents(int windowId)
    {
        var closeButtonRect = new Rect(_windowRect.width - 34f, 4f, 30f, 20f);
        if (GUI.Button(closeButtonRect, "X"))
        {
            Close();
            GUIUtility.ExitGUI();
        }

        var contentRect = new Rect(12f, 30f, _windowRect.width - 24f, _windowRect.height - 42f);
        GUILayout.BeginArea(contentRect);

        GUILayout.Label(_title, GetHeadlineStyle());
        GUILayout.Space(2f);
        GUILayout.Label(_dateText, GetDateStyle());
        GUILayout.Space(8f);

        var imageHeight = Mathf.Clamp(_windowRect.height * 0.34f, 120f, 180f);
        var imageRect = GUILayoutUtility.GetRect(10f, imageHeight, GUILayout.ExpandWidth(true), GUILayout.Height(imageHeight));
        if (_image is not null)
        {
            GUI.Box(imageRect, GUIContent.none);
            GUI.DrawTexture(imageRect, _image, ScaleMode.ScaleAndCrop);
        }
        else
        {
            GUI.Box(imageRect, "Image unavailable", GetPlaceholderStyle());
        }

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

        _placeholderStyle = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Italic,
            wordWrap = true,
        };

        return _placeholderStyle;
    }
}
