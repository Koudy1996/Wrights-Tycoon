using UnityEngine;

namespace WrightTycoon.Game.GovernmentContracts;

public sealed class GovernmentContractNotebookWindow : MonoBehaviour
{
    [SerializeField] private RectTransform? _windowRoot;
    [SerializeField] private RectTransform? _dragHandleArea;
    [SerializeField] private RectTransform? _resizeHandleArea;
    [SerializeField] private Camera? _uiCamera;
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

    private void Awake()
    {
        if (_windowRoot is null)
        {
            _windowRoot = transform as RectTransform;
        }

        _state = new ContractUIState
        {
            IsOpen = _openByDefault,
            Position = _defaultPosition,
            Size = _defaultSize,
        };

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

    public void ApplyState(ContractUIState state)
    {
        if (state is null || _windowRoot is null)
        {
            return;
        }

        _state = new ContractUIState
        {
            IsOpen = state.IsOpen,
            Position = state.Position,
            Size = state.Size,
        };

        _windowRoot.anchoredPosition = _state.Position;
        _windowRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _state.Size.x);
        _windowRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _state.Size.y);
        _windowRoot.gameObject.SetActive(_state.IsOpen);
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
}
