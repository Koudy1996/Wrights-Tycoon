using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WrightTycoon.Game.GovernmentContracts;

public sealed class GovernmentContractNotebookWindow : MonoBehaviour
{
    [SerializeField] private RectTransform? _windowRoot;
    [SerializeField] private RectTransform? _dragHandleArea;
    [SerializeField] private RectTransform? _resizeHandleArea;
    [SerializeField] private Camera? _uiCamera;
    [SerializeField] private Text? _titleText;
    [SerializeField] private Text? _contentText;
    [SerializeField] private bool _openByDefault;
    [SerializeField] private Vector2 _defaultPosition = new(120f, -120f);
    [SerializeField] private Vector2 _defaultSize = new(760f, 460f);
    [SerializeField] private Vector2 _minimumSize = new(420f, 260f);
    [SerializeField] private Vector2 _maximumSize = new(1800f, 1100f);

    private ContractUIState _state = new();
    private bool _isDragging;
    private bool _isResizing;
    private Vector2 _dragOffset;
    private Vector2 _resizeStartPointerLocal;
    private Vector2 _resizeStartSize;
    private RectTransform? _parentRect;

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

        EnsureEventSystem();

        var effectiveState = initialState is null
            ? new ContractUIState
            {
                IsOpen = false,
                Position = new Vector2(120f, -120f),
                Size = new Vector2(760f, 460f),
            }
            : new ContractUIState
            {
                IsOpen = initialState.IsOpen,
                Position = initialState.Position,
                Size = initialState.Size,
            };

        if (effectiveState.Size.x <= 0f || effectiveState.Size.y <= 0f)
        {
            effectiveState.Size = new Vector2(760f, 460f);
        }

        var canvasObject = new GameObject(
            "GovernmentContractNotebookCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(parent, false);

        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        var canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.anchorMin = Vector2.zero;
        canvasRect.anchorMax = Vector2.one;
        canvasRect.offsetMin = Vector2.zero;
        canvasRect.offsetMax = Vector2.zero;

        var windowObject = new GameObject(
            "GovernmentContractNotebookWindow",
            typeof(RectTransform),
            typeof(Image));
        windowObject.transform.SetParent(canvasObject.transform, false);
        var windowRect = windowObject.GetComponent<RectTransform>();
        windowRect.anchorMin = new Vector2(0f, 1f);
        windowRect.anchorMax = new Vector2(0f, 1f);
        windowRect.pivot = new Vector2(0f, 1f);
        windowRect.anchoredPosition = effectiveState.Position;
        windowRect.sizeDelta = effectiveState.Size;

        var windowBackground = windowObject.GetComponent<Image>();
        windowBackground.color = new Color(0.08f, 0.12f, 0.18f, 0.96f);

        var dragHandleObject = new GameObject(
            "DragHandle",
            typeof(RectTransform),
            typeof(Image));
        dragHandleObject.transform.SetParent(windowObject.transform, false);
        var dragHandleRect = dragHandleObject.GetComponent<RectTransform>();
        dragHandleRect.anchorMin = new Vector2(0f, 1f);
        dragHandleRect.anchorMax = new Vector2(1f, 1f);
        dragHandleRect.pivot = new Vector2(0.5f, 1f);
        dragHandleRect.anchoredPosition = Vector2.zero;
        dragHandleRect.sizeDelta = new Vector2(0f, 42f);
        var dragHandleBackground = dragHandleObject.GetComponent<Image>();
        dragHandleBackground.color = new Color(0.14f, 0.20f, 0.30f, 0.98f);

        var contentPanelObject = new GameObject(
            "ContentPanel",
            typeof(RectTransform),
            typeof(Image));
        contentPanelObject.transform.SetParent(windowObject.transform, false);
        var contentPanelRect = contentPanelObject.GetComponent<RectTransform>();
        contentPanelRect.anchorMin = new Vector2(0f, 0f);
        contentPanelRect.anchorMax = new Vector2(1f, 1f);
        contentPanelRect.offsetMin = new Vector2(12f, 12f);
        contentPanelRect.offsetMax = new Vector2(-12f, -52f);
        var contentPanelBackground = contentPanelObject.GetComponent<Image>();
        contentPanelBackground.color = new Color(0.07f, 0.09f, 0.14f, 0.92f);

        var resizeHandleObject = new GameObject(
            "ResizeHandle",
            typeof(RectTransform),
            typeof(Image));
        resizeHandleObject.transform.SetParent(windowObject.transform, false);
        var resizeHandleRect = resizeHandleObject.GetComponent<RectTransform>();
        resizeHandleRect.anchorMin = new Vector2(1f, 0f);
        resizeHandleRect.anchorMax = new Vector2(1f, 0f);
        resizeHandleRect.pivot = new Vector2(1f, 0f);
        resizeHandleRect.anchoredPosition = new Vector2(-4f, 4f);
        resizeHandleRect.sizeDelta = new Vector2(28f, 28f);
        var resizeHandleBackground = resizeHandleObject.GetComponent<Image>();
        resizeHandleBackground.color = new Color(0.45f, 0.55f, 0.68f, 1f);

        var titleFont = ResolveDefaultFont();
        var titleText = CreateTextElement(
            "TitleText",
            dragHandleRect,
            titleFont,
            18,
            TextAnchor.MiddleLeft,
            new Vector2(8f, 0f),
            new Vector2(-58f, 0f),
            "Government Contract Notebook");

        var closeButtonObject = new GameObject(
            "CloseButton",
            typeof(RectTransform),
            typeof(Image),
            typeof(Button));
        closeButtonObject.transform.SetParent(dragHandleRect, false);
        var closeButtonRect = closeButtonObject.GetComponent<RectTransform>();
        closeButtonRect.anchorMin = new Vector2(1f, 0f);
        closeButtonRect.anchorMax = new Vector2(1f, 1f);
        closeButtonRect.pivot = new Vector2(1f, 0.5f);
        closeButtonRect.anchoredPosition = Vector2.zero;
        closeButtonRect.sizeDelta = new Vector2(44f, 0f);

        var closeButtonBackground = closeButtonObject.GetComponent<Image>();
        closeButtonBackground.color = new Color(0.56f, 0.20f, 0.20f, 0.98f);
        var closeButton = closeButtonObject.GetComponent<Button>();

        var closeLabel = CreateTextElement(
            "CloseButtonLabel",
            closeButtonRect,
            titleFont,
            18,
            TextAnchor.MiddleCenter,
            Vector2.zero,
            Vector2.zero,
            "X");
        closeLabel.raycastTarget = false;

        var contentText = CreateTextElement(
            "ContentText",
            contentPanelRect,
            titleFont,
            14,
            TextAnchor.UpperLeft,
            Vector2.zero,
            Vector2.zero,
            string.Empty);
        contentText.horizontalOverflow = HorizontalWrapMode.Wrap;
        contentText.verticalOverflow = VerticalWrapMode.Overflow;

        var notebookWindow = windowObject.AddComponent<GovernmentContractNotebookWindow>();
        notebookWindow.ConfigureRuntime(
            windowRect,
            dragHandleRect,
            resizeHandleRect,
            uiCamera: null,
            titleText,
            contentText,
            effectiveState);
        closeButton.onClick.AddListener(notebookWindow.OnCloseButtonClicked);

        return notebookWindow;
    }

    private void Awake()
    {
        if (_windowRoot is null)
        {
            _windowRoot = transform as RectTransform;
        }

        _state = NormalizeState(new ContractUIState
        {
            IsOpen = _openByDefault,
            Position = _defaultPosition,
            Size = _defaultSize,
        });

        if (_dragHandleArea is null)
        {
            _dragHandleArea = _windowRoot;
        }

        if (_resizeHandleArea is null)
        {
            _resizeHandleArea = _windowRoot;
        }

        _parentRect = _windowRoot?.parent as RectTransform;
        ApplyState(_state);
    }

    public void ConfigureRuntime(
        RectTransform windowRoot,
        RectTransform dragHandleArea,
        RectTransform resizeHandleArea,
        Camera? uiCamera,
        Text? titleText,
        Text? contentText,
        ContractUIState? initialState = null)
    {
        _windowRoot = windowRoot ?? throw new ArgumentNullException(nameof(windowRoot));
        _dragHandleArea = dragHandleArea ?? throw new ArgumentNullException(nameof(dragHandleArea));
        _resizeHandleArea = resizeHandleArea ?? throw new ArgumentNullException(nameof(resizeHandleArea));
        _uiCamera = uiCamera;
        _titleText = titleText;
        _contentText = contentText;
        _parentRect = _windowRoot.parent as RectTransform;

        var nextState = initialState is null
            ? new ContractUIState
            {
                IsOpen = _openByDefault,
                Position = _defaultPosition,
                Size = _defaultSize,
            }
            : initialState;
        ApplyState(nextState);
    }

    public void ApplyState(ContractUIState state)
    {
        if (state is null || _windowRoot is null)
        {
            return;
        }

        _state = NormalizeState(state);

        _windowRoot.anchoredPosition = _state.Position;
        _windowRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _state.Size.x);
        _windowRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _state.Size.y);
        _windowRoot.gameObject.SetActive(_state.IsOpen);
    }

    public void SetContent(string title, string content)
    {
        if (_titleText is not null)
        {
            _titleText.text = string.IsNullOrWhiteSpace(title)
                ? "Government Contract Notebook"
                : title.Trim();
        }

        if (_contentText is not null)
        {
            _contentText.text = content ?? string.Empty;
        }
    }

    public ContractUIState CaptureState()
    {
        if (_windowRoot is null)
        {
            return new ContractUIState();
        }

        _state.Position = _windowRoot.anchoredPosition;
        _state.Size = _windowRoot.rect.size;
        _state.IsOpen = _windowRoot.gameObject.activeSelf;
        return new ContractUIState
        {
            IsOpen = _state.IsOpen,
            Position = _state.Position,
            Size = _state.Size,
        };
    }

    public void Open()
    {
        if (_windowRoot is null)
        {
            return;
        }

        _state.IsOpen = true;
        _windowRoot.gameObject.SetActive(true);
    }

    public void Close()
    {
        if (_windowRoot is null)
        {
            return;
        }

        _state.IsOpen = false;
        _windowRoot.gameObject.SetActive(false);
        _isDragging = false;
        _isResizing = false;
    }

    public void NotifyContractAccepted() => Open();

    public void NotifyContractEvaluated() => Close();

    // Hook this method from a button OnClick in the Canvas.
    public void OnCloseButtonClicked() => Close();

    // Hook this method from a "Notebook" button near the listing.
    public void OnNotebookButtonClicked() => Open();

    private void Update()
    {
        if (_windowRoot is null || !_windowRoot.gameObject.activeSelf)
        {
            return;
        }

        var pointer = Input.mousePosition;

        if (Input.GetMouseButtonDown(0))
        {
            if (IsPointerInside(_resizeHandleArea, pointer))
            {
                BeginResize(pointer);
            }
            else if (IsPointerInside(_dragHandleArea, pointer))
            {
                BeginDrag(pointer);
            }
        }

        if (Input.GetMouseButton(0))
        {
            if (_isResizing)
            {
                ContinueResize(pointer);
            }
            else if (_isDragging)
            {
                ContinueDrag(pointer);
            }
        }

        if (Input.GetMouseButtonUp(0))
        {
            _isDragging = false;
            _isResizing = false;
        }
    }

    private void BeginDrag(Vector2 screenPointer)
    {
        if (_windowRoot is null)
        {
            return;
        }

        _isDragging = RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _windowRoot.parent as RectTransform,
            screenPointer,
            _uiCamera,
            out var localPointer);
        if (!_isDragging)
        {
            return;
        }

        _dragOffset = _windowRoot.anchoredPosition - localPointer;
        _isResizing = false;
    }

    private void ContinueDrag(Vector2 screenPointer)
    {
        if (_windowRoot is null)
        {
            return;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _windowRoot.parent as RectTransform,
                screenPointer,
                _uiCamera,
                out var localPointer))
        {
            return;
        }

        _windowRoot.anchoredPosition = localPointer + _dragOffset;
        ClampWindowToParent();
    }

    private void BeginResize(Vector2 screenPointer)
    {
        if (_windowRoot is null)
        {
            return;
        }

        _isResizing = RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _windowRoot,
            screenPointer,
            _uiCamera,
            out _resizeStartPointerLocal);
        if (!_isResizing)
        {
            return;
        }

        _resizeStartSize = _windowRoot.rect.size;
        _isDragging = false;
    }

    private void ContinueResize(Vector2 screenPointer)
    {
        if (_windowRoot is null)
        {
            return;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _windowRoot,
                screenPointer,
                _uiCamera,
                out var currentLocalPointer))
        {
            return;
        }

        var delta = currentLocalPointer - _resizeStartPointerLocal;
        var nextSize = _resizeStartSize + new Vector2(delta.x, -delta.y);
        nextSize.x = Mathf.Clamp(nextSize.x, _minimumSize.x, _maximumSize.x);
        nextSize.y = Mathf.Clamp(nextSize.y, _minimumSize.y, _maximumSize.y);

        _windowRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, nextSize.x);
        _windowRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, nextSize.y);
        ClampWindowToParent();
    }

    private bool IsPointerInside(RectTransform? area, Vector2 screenPointer)
    {
        if (area is null || !area.gameObject.activeInHierarchy)
        {
            return false;
        }

        return RectTransformUtility.RectangleContainsScreenPoint(area, screenPointer, _uiCamera);
    }

    private void ClampWindowToParent()
    {
        if (_windowRoot is null || _parentRect is null)
        {
            return;
        }

        var parent = _parentRect.rect;
        var window = _windowRoot.rect;
        var anchored = _windowRoot.anchoredPosition;

        var minX = -parent.width + (window.width * _windowRoot.pivot.x);
        var maxX = window.width * _windowRoot.pivot.x;
        var minY = -parent.height + (window.height * _windowRoot.pivot.y);
        var maxY = window.height * _windowRoot.pivot.y;

        anchored.x = Mathf.Clamp(anchored.x, minX, maxX);
        anchored.y = Mathf.Clamp(anchored.y, minY, maxY);
        _windowRoot.anchoredPosition = anchored;
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

        normalized.Size = new Vector2(
            Mathf.Clamp(normalized.Size.x, _minimumSize.x, _maximumSize.x),
            Mathf.Clamp(normalized.Size.y, _minimumSize.y, _maximumSize.y));
        return normalized;
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current is not null)
        {
            return;
        }

        _ = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private static Text CreateTextElement(
        string name,
        RectTransform parent,
        Font font,
        int fontSize,
        TextAnchor alignment,
        Vector2 offsetMin,
        Vector2 offsetMax,
        string text)
    {
        var textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        var textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = offsetMin;
        textRect.offsetMax = offsetMax;

        var uiText = textObject.GetComponent<Text>();
        uiText.font = font;
        uiText.fontSize = fontSize;
        uiText.alignment = alignment;
        uiText.color = new Color(0.90f, 0.94f, 0.99f, 1f);
        uiText.text = text;
        uiText.supportRichText = false;
        uiText.horizontalOverflow = HorizontalWrapMode.Wrap;
        uiText.verticalOverflow = VerticalWrapMode.Truncate;
        return uiText;
    }

    private static Font ResolveDefaultFont()
    {
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font is not null)
        {
            return font;
        }

        font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (font is not null)
        {
            return font;
        }

        throw new InvalidOperationException("Unable to resolve a built-in font for notebook UI.");
    }
}
