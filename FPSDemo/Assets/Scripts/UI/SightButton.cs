using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SightButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private bool toggleAimOnClick = true;
    [SerializeField] private Graphic targetGraphic;
    [SerializeField] private Color inactiveColor = new Color(0.25f, 0.55f, 0.95f, 0.88f);
    [SerializeField] private Color activeColor = new Color(0.95f, 0.8f, 0.25f, 0.95f);
    [SerializeField] private float activeScale = 1.08f;
    [SerializeField] private bool logInput;

    private bool _isPointerDown;
    private bool _isSightActive;
    private Vector3 _defaultScale = Vector3.one;

    private void Reset()
    {
        CacheVisuals();
    }

    private void Awake()
    {
        CacheVisuals();
        UpdateVisualState();
    }

    private void OnEnable()
    {
        EventCenter.Instance.AddEventListener(GameEvent.MobileSightCanceled, OnMobileSightCanceled);
    }

    private void OnDisable()
    {
        EventCenter.Instance.RemoveEventListener(GameEvent.MobileSightCanceled, OnMobileSightCanceled);
        _isPointerDown = false;

        if (!_isSightActive)
        {
            return;
        }

        _isSightActive = false;
        UpdateVisualState();
        EventCenter.Instance.EventTrigger(GameEvent.MobileSightReleased);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (toggleAimOnClick)
        {
            SetSightActive(!_isSightActive);
            return;
        }

        if (_isPointerDown)
        {
            return;
        }

        _isPointerDown = true;
        SetSightActive(true);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (toggleAimOnClick)
        {
            return;
        }

        if (!_isPointerDown)
        {
            return;
        }

        _isPointerDown = false;
        SetSightActive(false);
    }

    private void SetSightActive(bool isActive)
    {
        if (_isSightActive == isActive)
        {
            return;
        }

        // 手机瞄准按钮只负责切换输入状态
        _isSightActive = isActive;
        UpdateVisualState();
        EventCenter.Instance.EventTrigger(isActive ? GameEvent.MobileSightPressed : GameEvent.MobileSightReleased);

        if (logInput)
        {
            Debug.Log(isActive ? "移动端瞄准开启" : "移动端瞄准关闭", this);
        }
    }

    private void OnMobileSightCanceled()
    {
        // 外部切枪时只同步按钮表现 不再重复发送释放事件
        _isPointerDown = false;
        _isSightActive = false;
        UpdateVisualState();
    }

    private void CacheVisuals()
    {
        targetGraphic ??= GetComponent<Graphic>();
        _defaultScale = transform.localScale;
    }

    private void UpdateVisualState()
    {
        if (targetGraphic != null)
        {
            targetGraphic.color = _isSightActive ? activeColor : inactiveColor;
        }

        transform.localScale = _isSightActive ? _defaultScale * activeScale : _defaultScale;
    }
}
