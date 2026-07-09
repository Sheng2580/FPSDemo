using UnityEngine;
using UnityEngine.EventSystems;

public class MoveJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    // 挂在 MoveJoystick 物体上
    // TouchArea 负责接收点击范围
    // Background 只是摇杆底图
    // Handle 会跟随手指移动

    [Header("引用")]
    [SerializeField] private RectTransform touchArea;
    [SerializeField] private RectTransform background;
    [SerializeField] private RectTransform handle;

    [Header("参数")]
    [SerializeField] private float maxHandleDistance = 100f;
    [SerializeField] private Vector2 defaultCenterPadding = new Vector2(220f, 220f);
    [SerializeField] private bool moveBackgroundToPointerDown = true;
    [SerializeField] private float keepMoveShowInputY = 0.65f;
    [SerializeField] private float keepMoveTriggerRadius = 80f;
    [SerializeField] private bool logInput;

    private RectTransform _rectTransform;
    private RectTransform keepMove;
    private Vector2 _defaultCenterPosition;
    private Vector2 _centerPosition;
    private bool _isDragging;
    private bool _isKeepMoveLocked;
    private bool _isKeepMoveTargetVisible;
    private bool _isPointerInKeepMoveTarget;
    private const int NoPointer = int.MinValue;
    private int _activePointerId = NoPointer;

    public Vector2 InputValue { get; private set; }
    public bool IsKeepMoveLocked => _isKeepMoveLocked;

    private void Reset()
    {
        CacheReferences();
    }

    private void Awake()
    {
        CacheReferences();
        CacheDefaultCenterPosition();
        ResetVisualToDefaultCenter();
        ApplyKeepMoveTargetVisible(false);
        ClearInput();
    }

    private void OnDisable()
    {
        bool wasKeepMoveLocked = _isKeepMoveLocked;
        _isKeepMoveLocked = false;
        _activePointerId = NoPointer;
        ClearInput();
        SetKeepMoveTargetVisible(false);
        SetPointerInKeepMoveTarget(false);

        if (wasKeepMoveLocked)
        {
            SendMoveLockEvent(false);
        }
    }

    private void OnRectTransformDimensionsChange()
    {
        CacheReferences();
        CacheDefaultCenterPosition();

        if (!_isDragging)
        {
            ResetVisualToDefaultCenter();
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (_activePointerId != NoPointer)
        {
            return;
        }

        _activePointerId = eventData.pointerId;

        if (_isKeepMoveLocked)
        {
            CancelKeepMoveLock();
        }

        _isDragging = true;

        if (moveBackgroundToPointerDown)
        {
            MoveCenterToPointer(eventData);
        }

        // 按下时立刻计算一次方向
        UpdateInput(eventData);

        if (logInput)
        {
            Debug.Log($"摇杆按下 {InputValue}", this);
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_activePointerId != eventData.pointerId)
        {
            return;
        }

        // 拖动时持续更新摇杆方向
        UpdateInput(eventData);

        if (logInput)
        {
            Debug.Log($"摇杆拖动 {InputValue}", this);
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (_activePointerId != eventData.pointerId)
        {
            return;
        }

        if (_isKeepMoveLocked)
        {
            _isDragging = false;
            _activePointerId = NoPointer;
            return;
        }

        _isDragging = false;
        _activePointerId = NoPointer;

        if (IsPointerInKeepMoveTarget(eventData))
        {
            ActivateKeepMoveLock();
        }
        else
        {
            // 松开时让摇杆回到默认左下位置
            ClearInput();
        }

        if (logInput)
        {
            Debug.Log("摇杆松开", this);
        }
    }

    private void UpdateInput(PointerEventData eventData)
    {
        CacheReferences();

        if (_rectTransform == null)
        {
            return;
        }

        // 用 MoveJoystick 自己的坐标系计算手指位置
        // Handle 在 Background 下时只写局部偏移
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _rectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localPoint);

        Vector2 dragOffset = localPoint - _centerPosition;

        UpdateKeepMoveTargetState(localPoint, dragOffset);

        // 限制小圆点移动距离
        float distance = Mathf.Max(1f, maxHandleDistance);
        Vector2 handleOffset = Vector2.ClampMagnitude(dragOffset, distance);

        // 转成 -1 到 1 的移动输入
        InputValue = handleOffset / distance;

        SetHandleOffset(handleOffset);

        SendInputEvent();
    }

    private void MoveCenterToPointer(PointerEventData eventData)
    {
        CacheReferences();

        if (_rectTransform == null)
        {
            return;
        }

        // 把摇杆底盘移动到手指按下的位置
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _rectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out _centerPosition);

        if (background != null)
        {
            background.anchoredPosition = _centerPosition;
        }

        SetHandleOffset(Vector2.zero);
    }

    private void ClearInput()
    {
        ResetVisualToDefaultCenter();
        SetKeepMoveTargetVisible(false);

        InputValue = Vector2.zero;

        SetHandleOffset(Vector2.zero);

        SendInputEvent();
    }

    private void SendInputEvent()
    {
        // 把摇杆结果发给事件中心
        EventCenter.Instance.EventTrigger(GameEvent.MobileMoveInputChanged, InputValue);
    }

    private void SendMoveLockEvent(bool isLocked)
    {
        // 把移动锁定状态发给事件中心
        EventCenter.Instance.EventTrigger(GameEvent.MobileMoveLockChanged, isLocked);
    }

    private void SendMoveLockTargetEvent(bool isVisible)
    {
        // 把移动锁定图标显示状态发给事件中心
        EventCenter.Instance.EventTrigger(GameEvent.MobileMoveLockTargetChanged, isVisible);
    }

    private void SendMoveLockTargetHoverEvent(bool isHovering)
    {
        // 把手指是否进入移动锁定区域发给事件中心
        EventCenter.Instance.EventTrigger(GameEvent.MobileMoveLockTargetHoverChanged, isHovering);
    }

    private void UpdateKeepMoveTargetState(Vector2 localPoint, Vector2 dragOffset)
    {
        if (_isKeepMoveLocked)
        {
            return;
        }

        bool shouldShowKeepMove = dragOffset.y >= maxHandleDistance * keepMoveShowInputY;
        SetKeepMoveTargetVisible(shouldShowKeepMove);

        if (!shouldShowKeepMove || keepMove == null)
        {
            SetPointerInKeepMoveTarget(false);
            return;
        }

        Vector2 keepMovePosition = GetKeepMovePositionInJoystick();
        bool reachedKeepMove = Vector2.Distance(localPoint, keepMovePosition) <= keepMoveTriggerRadius;
        SetPointerInKeepMoveTarget(reachedKeepMove);
    }

    private void ActivateKeepMoveLock()
    {
        _isKeepMoveLocked = true;
        _isDragging = false;
        InputValue = Vector2.up;

        ResetVisualToDefaultCenter();
        SetKeepMoveTargetVisible(false);
        SetPointerInKeepMoveTarget(false);

        SetHandleOffset(Vector2.up * maxHandleDistance);

        SendMoveLockEvent(true);
        SendInputEvent();

        if (logInput)
        {
            Debug.Log("移动锁定开启", this);
        }
    }

    private void CancelKeepMoveLock()
    {
        _isKeepMoveLocked = false;
        InputValue = Vector2.zero;
        ResetVisualToDefaultCenter();
        SetKeepMoveTargetVisible(false);
        SetPointerInKeepMoveTarget(false);
        SendMoveLockEvent(false);
        SendInputEvent();

        if (logInput)
        {
            Debug.Log("移动锁定取消", this);
        }
    }

    private void SetKeepMoveTargetVisible(bool isVisible)
    {
        if (_isKeepMoveTargetVisible == isVisible)
        {
            return;
        }

        _isKeepMoveTargetVisible = isVisible;
        ApplyKeepMoveTargetVisible(isVisible);
        SendMoveLockTargetEvent(isVisible);
    }

    private void ApplyKeepMoveTargetVisible(bool isVisible)
    {
        CacheReferences();

        if (keepMove == null)
        {
            return;
        }

        if (isVisible)
        {
            SetKeepMovePositionForCurrentCenter();
        }

        if (keepMove.gameObject.activeSelf != isVisible)
        {
            keepMove.gameObject.SetActive(isVisible);
        }
    }

    private void SetKeepMovePositionForCurrentCenter()
    {
        if (keepMove == null)
        {
            return;
        }

        // KeepMove 在 Background 下时，距离由它自己的 RectTransform 位置控制
        if (background != null && keepMove.parent == background)
        {
            return;
        }

        // 如果不是 Background 子物体，就把当前位置当作相对默认中心的偏移
        Vector2 offsetFromDefault = keepMove.anchoredPosition - _defaultCenterPosition;
        keepMove.anchoredPosition = _centerPosition + offsetFromDefault;
    }

    private void SetPointerInKeepMoveTarget(bool isHovering)
    {
        if (_isPointerInKeepMoveTarget == isHovering)
        {
            return;
        }

        _isPointerInKeepMoveTarget = isHovering;
        SendMoveLockTargetHoverEvent(isHovering);
    }

    private Vector2 GetKeepMovePositionInJoystick()
    {
        if (keepMove == null)
        {
            return _centerPosition;
        }

        // KeepMove 在 Background 下时，检测位置等于底盘中心加它自己的局部位置
        if (background != null && keepMove.parent == background)
        {
            return _centerPosition + keepMove.anchoredPosition;
        }

        return keepMove.anchoredPosition;
    }

    private bool IsPointerInKeepMoveTarget(PointerEventData eventData)
    {
        CacheReferences();

        if (!_isKeepMoveTargetVisible || keepMove == null || _rectTransform == null)
        {
            return false;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _rectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localPoint);

        Vector2 keepMovePosition = GetKeepMovePositionInJoystick();
        return Vector2.Distance(localPoint, keepMovePosition) <= keepMoveTriggerRadius;
    }

    private void CacheReferences()
    {
        // 尽量按子物体名字自动补引用
        _rectTransform ??= transform as RectTransform;
        touchArea ??= FindRectTransform("TouchArea");
        background ??= FindRectTransform("Background");
        background ??= FindRectTransform("Backgound");
        handle ??= FindRectTransform("Handle");
        handle ??= FindChildRectTransform(background, "Handle");
        handle ??= FindRectTransformInChildren("Handle");
        keepMove ??= FindRectTransform("KeepMove");
        keepMove ??= FindChildRectTransform(background, "KeepMove");
        keepMove ??= FindRectTransformInChildren("KeepMove");
    }

    private void CacheCenterPosition()
    {
        // 优先以 Background 的位置作为摇杆中心
        if (background != null)
        {
            _centerPosition = background.anchoredPosition;
            return;
        }

        if (handle != null && !IsHandleUnderBackground())
        {
            _centerPosition = handle.anchoredPosition;
            return;
        }

        _centerPosition = Vector2.zero;
    }

    private void CacheDefaultCenterPosition()
    {
        if (_rectTransform == null)
        {
            _defaultCenterPosition = defaultCenterPadding;
            return;
        }

        // 默认位置按左下角加边距计算
        Rect rect = _rectTransform.rect;
        _defaultCenterPosition = new Vector2(
            rect.xMin + defaultCenterPadding.x,
            rect.yMin + defaultCenterPadding.y);
    }

    private void ResetVisualToDefaultCenter()
    {
        _centerPosition = _defaultCenterPosition;

        if (background != null)
        {
            background.anchoredPosition = _centerPosition;
        }

        SetHandleOffset(Vector2.zero);
    }

    private void SetHandleOffset(Vector2 handleOffset)
    {
        if (handle == null)
        {
            return;
        }

        if (IsHandleUnderBackground())
        {
            handle.anchoredPosition = handleOffset;
            return;
        }

        handle.anchoredPosition = _centerPosition + handleOffset;
    }

    private bool IsHandleUnderBackground()
    {
        return background != null && handle != null && handle.parent == background;
    }

    private RectTransform FindRectTransform(string childName)
    {
        Transform child = transform.Find(childName);
        return child as RectTransform;
    }

    private RectTransform FindChildRectTransform(RectTransform parent, string childName)
    {
        if (parent == null)
        {
            return null;
        }

        Transform child = parent.Find(childName);
        return child as RectTransform;
    }

    private RectTransform FindRectTransformInChildren(string childName)
    {
        RectTransform[] children = GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null && children[i].name == childName)
            {
                return children[i];
            }
        }

        return null;
    }
}
