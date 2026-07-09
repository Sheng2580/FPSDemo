using UnityEngine;
using UnityEngine.EventSystems;

public class RightLookArea : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("参数")]
    [SerializeField] private float lookSensitivity = 1f;
    [SerializeField] private float dragStartDeadZone = 3f;
    [SerializeField] private float maxLookDeltaPerFrame = 60f;
    [SerializeField] private bool logInput;

    private const int NoPointer = int.MinValue;
    private int _activePointerId = NoPointer;
    private Vector2 _lastPointerPosition;
    private Vector2 _dragOffset;
    private bool _hasIgnoredFirstDrag;
    private bool _hasPassedDeadZone;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (_activePointerId != NoPointer)
        {
            return;
        }

        _activePointerId = eventData.pointerId;
        _lastPointerPosition = eventData.position;
        _dragOffset = Vector2.zero;
        _hasIgnoredFirstDrag = false;
        _hasPassedDeadZone = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_activePointerId != eventData.pointerId)
        {
            return;
        }

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

        Vector2 lookDelta = Vector2.ClampMagnitude(rawDelta, maxLookDeltaPerFrame) * lookSensitivity;
        if (lookDelta.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        // 右半屏滑动只负责视角转动
        EventCenter.Instance.EventTrigger(GameEvent.MobileLookDeltaChanged, lookDelta);

        if (logInput)
        {
            Debug.Log($"右侧视角滑动 {lookDelta}", this);
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (_activePointerId == eventData.pointerId)
        {
            _activePointerId = NoPointer;
            _lastPointerPosition = Vector2.zero;
            _dragOffset = Vector2.zero;
            _hasIgnoredFirstDrag = false;
            _hasPassedDeadZone = false;
        }
    }

    private void OnDisable()
    {
        _activePointerId = NoPointer;
        _lastPointerPosition = Vector2.zero;
        _dragOffset = Vector2.zero;
        _hasIgnoredFirstDrag = false;
        _hasPassedDeadZone = false;
    }
}
