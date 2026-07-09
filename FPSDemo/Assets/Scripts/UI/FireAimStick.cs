using UnityEngine;
using UnityEngine.EventSystems;

public class FireAimStick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("引用")]
    [SerializeField] private RectTransform fireRange;
    [SerializeField] private RectTransform fireButton;

    [Header("参数")]
    [SerializeField] private float lookSensitivity = 1f;
    [SerializeField] private float dragStartDeadZone = 3f;
    [SerializeField] private float maxLookDeltaPerFrame = 60f;
    [SerializeField] private bool resetButtonOnRelease = true;
    [SerializeField] private bool sendHoldingEventEachFrame = true;
    [SerializeField] private bool logInput;

    private const int NoPointer = int.MinValue;
    private int _activePointerId = NoPointer;
    private bool _isPressed;
    private Vector2 _lastPointerPosition;
    private Vector2 _dragOffset;
    private bool _hasIgnoredFirstDrag;
    private bool _hasPassedDeadZone;

    private void Reset()
    {
        CacheReferences();
    }

    private void Awake()
    {
        CacheReferences();
        ResetButtonPosition();
    }

    private void Update()
    {
        if (!_isPressed || !sendHoldingEventEachFrame)
        {
            return;
        }

        // 长按期间给武器和表现层持续信号
        EventCenter.Instance.EventTrigger(GameEvent.MobileFireHolding);
    }

    private void OnDisable()
    {
        if (_isPressed)
        {
            EventCenter.Instance.EventTrigger(GameEvent.MobileFireReleased);
        }

        _isPressed = false;
        _activePointerId = NoPointer;
        _lastPointerPosition = Vector2.zero;
        _dragOffset = Vector2.zero;
        _hasIgnoredFirstDrag = false;
        _hasPassedDeadZone = false;
        ResetButtonPosition();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (_activePointerId != NoPointer)
        {
            return;
        }

        CacheReferences();
        _activePointerId = eventData.pointerId;
        _isPressed = true;
        _lastPointerPosition = eventData.position;
        _dragOffset = Vector2.zero;
        _hasIgnoredFirstDrag = false;
        _hasPassedDeadZone = false;

        UpdateButtonPosition(eventData);
        EventCenter.Instance.EventTrigger(GameEvent.MobileFirePressed);

        if (logInput)
        {
            Debug.Log("移动端开火按下", this);
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_activePointerId != eventData.pointerId)
        {
            return;
        }

        UpdateButtonPosition(eventData);

        Vector2 rawDelta = eventData.position - _lastPointerPosition;
        _lastPointerPosition = eventData.position;

        if (!_hasIgnoredFirstDrag)
        {
            _hasIgnoredFirstDrag = true;
            return;
        }

        _dragOffset += rawDelta;
        if (!_hasPassedDeadZone)
        {
            if (_dragOffset.sqrMagnitude < dragStartDeadZone * dragStartDeadZone)
            {
                return;
            }

            _hasPassedDeadZone = true;
            return;
        }

        SendLookDelta(Vector2.ClampMagnitude(rawDelta, maxLookDeltaPerFrame));
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (_activePointerId != eventData.pointerId)
        {
            return;
        }

        _isPressed = false;
        _activePointerId = NoPointer;
        _lastPointerPosition = Vector2.zero;
        _dragOffset = Vector2.zero;
        _hasIgnoredFirstDrag = false;
        _hasPassedDeadZone = false;
        EventCenter.Instance.EventTrigger(GameEvent.MobileFireReleased);

        if (resetButtonOnRelease)
        {
            ResetButtonPosition();
        }

        if (logInput)
        {
            Debug.Log("移动端开火松开", this);
        }
    }

    private void UpdateButtonPosition(PointerEventData eventData)
    {
        if (fireRange == null || fireButton == null)
        {
            return;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            fireRange,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localPoint);

        fireButton.anchoredPosition = Vector2.ClampMagnitude(localPoint, GetMoveRadius());
    }

    private void SendLookDelta(Vector2 delta)
    {
        Vector2 lookDelta = delta * lookSensitivity;
        if (lookDelta.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        // 拖动开火键时同步转动视角
        EventCenter.Instance.EventTrigger(GameEvent.MobileLookDeltaChanged, lookDelta);
    }

    private float GetMoveRadius()
    {
        if (fireRange == null || fireButton == null)
        {
            return 0f;
        }

        float rangeRadius = Mathf.Min(fireRange.rect.width, fireRange.rect.height) * 0.5f;
        float buttonRadius = Mathf.Min(fireButton.rect.width, fireButton.rect.height) * 0.5f;
        return Mathf.Max(0f, rangeRadius - buttonRadius);
    }

    private void ResetButtonPosition()
    {
        if (fireButton != null)
        {
            fireButton.anchoredPosition = Vector2.zero;
        }
    }

    private void CacheReferences()
    {
        fireButton ??= transform as RectTransform;

        if (fireRange == null && fireButton != null)
        {
            fireRange = fireButton.parent as RectTransform;
        }
    }
}
