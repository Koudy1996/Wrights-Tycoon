using System;
using System.Collections.Generic;
using UnityEngine;
using WrightTycoon.Game.UI;

namespace WrightTycoon.Game.GovernmentContracts;

public sealed class GovernmentContractNotebookWindow : MonoBehaviour
{
    private static readonly Vector2 LegacyDefaultSize = new(760f, 460f);
    private static readonly Vector2 PreviousCompactDefaultSize = new(560f, 340f);
    private static readonly Vector2 CompactDefaultSize = new(448f, 272f);
    private const float WindowOpacity = 1f;
    private static readonly Color OpaqueWindowFillColor = new(0.08f, 0.11f, 0.15f, 1f);

    [SerializeField] private bool _openByDefault;
    [SerializeField] private Vector2 _defaultPosition = new(120f, -120f);
    [SerializeField] private Vector2 _defaultSize = default;
    [SerializeField] private Vector2 _minimumSize = new(400f, 240f);
    [SerializeField] private Vector2 _maximumSize = new(1800f, 1100f);

    private ContractUIState _state = new();
    private Rect _windowRect;
    private Vector2 _contentScroll;
    private bool _isResizing;
    private Vector2 _resizeStartMouseVirtual;
    private Vector2 _resizeStartSize;
    private int _windowId;
    private string _title = "Government Contract Notebook";
    private string _content = string.Empty;
    private IReadOnlyList<string> _tabLabels = Array.Empty<string>();
    private int _selectedTabIndex = -1;
    private Action<int>? _tabSelected;
    private GUIStyle? _contentLabelStyle;
    private GUIStyle? _selectedTabStyle;

    public ContractUIState State => _state;

    public static GovernmentContractNotebookWindow CreateRuntimeUi(
        Transform parent,
        ContractUIState? initialState = null,
        int sortingOrder = 140)
    {
        if (parent is null)
        {
            throw new ArgumentNullException(nameof(parent));
        }

        var windowObject = new GameObject("GovernmentContractNotebookWindowRuntime");
        windowObject.transform.SetParent(parent, false);

        var notebookWindow = windowObject.AddComponent<GovernmentContractNotebookWindow>();
        notebookWindow.InitializeDefaults();
        var state = initialState is null
            ? new ContractUIState
            {
                IsOpen = false,
                Position = new Vector2(120f, -120f),
                Size = CompactDefaultSize,
            }
            : new ContractUIState
            {
                IsOpen = initialState.IsOpen,
                Position = initialState.Position,
                Size = initialState.Size,
            };

        notebookWindow.ApplyState(state);

        _ = sortingOrder;
        return notebookWindow;
    }

    private void Awake()
    {
        InitializeDefaults();
        _windowId = $"{nameof(GovernmentContractNotebookWindow)}_{GetInstanceID()}".GetHashCode();

        if (_state.Size.x <= 0f || _state.Size.y <= 0f)
        {
            ApplyState(new ContractUIState
            {
                IsOpen = _openByDefault,
                Position = _defaultPosition,
                Size = _defaultSize,
            });
        }
    }

    public void ApplyState(ContractUIState state)
    {
        if (state is null)
        {
            return;
        }

        InitializeDefaults();
        _state = NormalizeState(state);
        _windowRect = ToWindowRect(_state);
    }

    public ContractUIState CaptureState()
    {
        _state = FromWindowRect(_windowRect, _state.IsOpen);
        return new ContractUIState
        {
            IsOpen = _state.IsOpen,
            Position = _state.Position,
            Size = _state.Size,
        };
    }

    public void SetContent(string title, string content)
    {
        _title = string.IsNullOrWhiteSpace(title)
            ? "Government Contract Notebook"
            : title.Trim();
        _content = content ?? string.Empty;
    }

    public void SetTabs(
        IReadOnlyList<string>? tabLabels,
        int selectedTabIndex,
        Action<int>? tabSelected)
    {
        _tabLabels = tabLabels ?? Array.Empty<string>();
        _selectedTabIndex = _tabLabels.Count == 0
            ? -1
            : Mathf.Clamp(selectedTabIndex, 0, _tabLabels.Count - 1);
        _tabSelected = tabSelected;
    }

    public void ClearTabs() => SetTabs(Array.Empty<string>(), -1, null);

    public void Open()
    {
        _state.IsOpen = true;
    }

    public void Close()
    {
        _state.IsOpen = false;
        _isResizing = false;
    }

    public void NotifyContractAccepted() => Open();

    public void NotifyContractEvaluated() => Close();

    public void OnCloseButtonClicked() => Close();

    public void OnNotebookButtonClicked() => Open();

    private void OnGUI()
    {
        if (!_state.IsOpen)
        {
            return;
        }

        var uiScale = WrightTycoonGuiScaling.CalculateScale();
        var virtualScreenSize = WrightTycoonGuiScaling.GetVirtualScreenSize(uiScale);
        ClampWindowToScreen(virtualScreenSize);

        var originalGuiColor = GUI.color;
        GUI.color = new Color(originalGuiColor.r, originalGuiColor.g, originalGuiColor.b, WindowOpacity);

        try
        {
            using var _ = new WrightTycoonGuiScaling.Scope(uiScale);
            _windowRect = GUI.Window(_windowId, _windowRect, DrawWindowContents, _title);
            ClampWindowToScreen(virtualScreenSize);
            _state = FromWindowRect(_windowRect, _state.IsOpen);
        }
        finally
        {
            GUI.color = originalGuiColor;
        }
    }

    private void DrawWindowContents(int windowId)
    {
        DrawOpaqueWindowBackground();

        var closeButtonRect = new Rect(_windowRect.width - 34f, 4f, 30f, 20f);
        if (GUI.Button(closeButtonRect, "X"))
        {
            Close();
            GUIUtility.ExitGUI();
        }

        var contentTop = 28f;
        if (_tabLabels.Count > 0)
        {
            DrawTabs(contentTop);
            contentTop += 34f;
        }

        var contentRect = new Rect(10f, contentTop, _windowRect.width - 26f, _windowRect.height - contentTop - 44f);
        GUILayout.BeginArea(contentRect);
        _contentScroll = GUILayout.BeginScrollView(_contentScroll);
        GUILayout.Label(_content, GetContentLabelStyle(), GUILayout.ExpandWidth(true));
        GUILayout.EndScrollView();
        GUILayout.EndArea();

        var resizeHandleRect = new Rect(_windowRect.width - 82f, _windowRect.height - 34f, 74f, 24f);
        GUI.Box(resizeHandleRect, "Resize");

        HandleResize(resizeHandleRect);

        if (!_isResizing)
        {
            GUI.DragWindow(new Rect(0f, 0f, _windowRect.width - 40f, 24f));
        }

        _ = windowId;
    }

    private void DrawTabs(float top)
    {
        const float tabHeight = 26f;
        const float minTabWidth = 96f;
        const float maxTabWidth = 180f;
        var availableWidth = Mathf.Max(80f, _windowRect.width - 20f);
        var tabWidth = Mathf.Clamp(availableWidth / _tabLabels.Count, minTabWidth, maxTabWidth);

        GUILayout.BeginArea(new Rect(10f, top, availableWidth, tabHeight));
        GUILayout.BeginHorizontal();
        for (var index = 0; index < _tabLabels.Count; index++)
        {
            var label = _tabLabels[index];
            var style = index == _selectedTabIndex ? GetSelectedTabStyle() : GUI.skin.button;
            if (GUILayout.Button(label, style, GUILayout.Width(tabWidth), GUILayout.Height(tabHeight)))
            {
                _selectedTabIndex = index;
                _tabSelected?.Invoke(index);
                GUIUtility.ExitGUI();
            }
        }

        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }

    private void DrawOpaqueWindowBackground()
    {
        var previous = GUI.color;
        GUI.color = OpaqueWindowFillColor;
        GUI.DrawTexture(new Rect(0f, 0f, _windowRect.width, _windowRect.height), Texture2D.whiteTexture);
        GUI.color = previous;
    }

    private void HandleResize(Rect resizeHandleRect)
    {
        var currentEvent = Event.current;
        if (currentEvent is null)
        {
            return;
        }

        var mousePosition = currentEvent.mousePosition;
        if (currentEvent.type == EventType.MouseDown && resizeHandleRect.Contains(mousePosition))
        {
            _isResizing = true;
            _resizeStartMouseVirtual = mousePosition;
            _resizeStartSize = new Vector2(_windowRect.width, _windowRect.height);
            currentEvent.Use();
            return;
        }

        if (_isResizing && currentEvent.type == EventType.MouseDrag)
        {
            _windowRect = CalculateResizedRect(
                _windowRect,
                _resizeStartMouseVirtual,
                _resizeStartSize,
                mousePosition,
                _minimumSize,
                _maximumSize);
            currentEvent.Use();
            return;
        }

        if (_isResizing && currentEvent.type == EventType.MouseUp)
        {
            _isResizing = false;
            currentEvent.Use();
        }
    }

    public static Rect CalculateResizedRect(
        Rect currentRect,
        Vector2 startMousePosition,
        Vector2 startSize,
        Vector2 currentMousePosition,
        Vector2 minimumSize,
        Vector2 maximumSize)
    {
        var delta = currentMousePosition - startMousePosition;
        currentRect.width = Mathf.Clamp(startSize.x + delta.x, minimumSize.x, maximumSize.x);
        currentRect.height = Mathf.Clamp(startSize.y + delta.y, minimumSize.y, maximumSize.y);
        return currentRect;
    }

    private void ClampWindowToScreen(Vector2 virtualScreenSize)
    {
        _windowRect.width = Mathf.Clamp(_windowRect.width, _minimumSize.x, Mathf.Min(_maximumSize.x, virtualScreenSize.x));
        _windowRect.height = Mathf.Clamp(_windowRect.height, _minimumSize.y, Mathf.Min(_maximumSize.y, virtualScreenSize.y));

        var maxX = Mathf.Max(0f, virtualScreenSize.x - _windowRect.width);
        var maxY = Mathf.Max(0f, virtualScreenSize.y - _windowRect.height);
        _windowRect.x = Mathf.Clamp(_windowRect.x, 0f, maxX);
        _windowRect.y = Mathf.Clamp(_windowRect.y, 0f, maxY);
    }

    private ContractUIState NormalizeState(ContractUIState state)
    {
        var normalized = new ContractUIState
        {
            IsOpen = state.IsOpen,
            Position = state.Position,
            Size = state.Size,
        };

        if (normalized.Size.x <= 0f || normalized.Size.y <= 0f)
        {
            normalized.Size = _defaultSize;
        }
        else if ((Mathf.Approximately(normalized.Size.x, LegacyDefaultSize.x) &&
                  Mathf.Approximately(normalized.Size.y, LegacyDefaultSize.y)) ||
                 (Mathf.Approximately(normalized.Size.x, PreviousCompactDefaultSize.x) &&
                  Mathf.Approximately(normalized.Size.y, PreviousCompactDefaultSize.y)))
        {
            normalized.Size = _defaultSize;
        }

        normalized.Size = new Vector2(
            Mathf.Clamp(normalized.Size.x, _minimumSize.x, _maximumSize.x),
            Mathf.Clamp(normalized.Size.y, _minimumSize.y, _maximumSize.y));
        return normalized;
    }

    private Rect ToWindowRect(ContractUIState state)
    {
        var x = state.Position.x;
        var y = state.Position.y <= 0f ? -state.Position.y : state.Position.y;
        var width = Mathf.Clamp(state.Size.x, _minimumSize.x, _maximumSize.x);
        var height = Mathf.Clamp(state.Size.y, _minimumSize.y, _maximumSize.y);
        return new Rect(x, y, width, height);
    }

    private ContractUIState FromWindowRect(Rect rect, bool isOpen)
    {
        return new ContractUIState
        {
            IsOpen = isOpen,
            Position = new Vector2(rect.x, -rect.y),
            Size = new Vector2(rect.width, rect.height),
        };
    }

    private GUIStyle GetContentLabelStyle()
    {
        _contentLabelStyle ??= new GUIStyle(GUI.skin.label)
        {
            wordWrap = true,
            richText = false,
        };

        return _contentLabelStyle;
    }

    private GUIStyle GetSelectedTabStyle()
    {
        _selectedTabStyle ??= new GUIStyle(GUI.skin.button)
        {
            fontStyle = FontStyle.Bold,
        };

        return _selectedTabStyle;
    }

    private void InitializeDefaults()
    {
        if (_defaultSize.x <= 0f || _defaultSize.y <= 0f)
        {
            _defaultSize = CompactDefaultSize;
        }
    }
}
