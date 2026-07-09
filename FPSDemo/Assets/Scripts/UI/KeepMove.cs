using UnityEngine;

public class KeepMove : MonoBehaviour
{
    [SerializeField] private bool hideWhenMoveLocked = true;
    [SerializeField] private float hoverScale = 1.15f;

    private CanvasGroup _canvasGroup;
    private Vector3 _defaultScale;

    private void Awake()
    {
        CacheComponents();
        _defaultScale = transform.localScale;
    }

    private void OnEnable()
    {
        EventCenter.Instance.AddEventListener<bool>(GameEvent.MobileMoveLockTargetHoverChanged, OnMoveLockTargetHoverChanged);
        EventCenter.Instance.AddEventListener<bool>(GameEvent.MobileMoveLockChanged, OnMoveLockChanged);
        SetVisible(true);
    }

    private void OnDisable()
    {
        EventCenter.Instance.RemoveEventListener<bool>(GameEvent.MobileMoveLockTargetHoverChanged, OnMoveLockTargetHoverChanged);
        EventCenter.Instance.RemoveEventListener<bool>(GameEvent.MobileMoveLockChanged, OnMoveLockChanged);
    }

    private void OnMoveLockChanged(bool isLocked)
    {
        if (hideWhenMoveLocked && isLocked)
        {
            gameObject.SetActive(false);
        }
    }

    private void OnMoveLockTargetHoverChanged(bool isHovering)
    {
        // 后续移动锁定图标的进入效果写这里
        transform.localScale = isHovering ? _defaultScale * hoverScale : _defaultScale;
    }

    public void SetVisible(bool isVisible)
    {
        CacheComponents();

        if (_canvasGroup == null)
        {
            return;
        }

        _canvasGroup.alpha = isVisible ? 1f : 0f;
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;
    }

    private void CacheComponents()
    {
        if (_canvasGroup == null)
        {
            _canvasGroup = GetComponent<CanvasGroup>();
        }

        if (_canvasGroup == null)
        {
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }
}
