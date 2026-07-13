using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 局内提示面板
/// 监听提示事件并播放淡入淡出
/// </summary>
[UICanvas(UILoadType.AssetBundle, UILayer.Tip)]
[DisallowMultipleComponent]
public class TipCanvas : BaseCanvas
{
    private const string HideTimerId = "TipCanvas_HideTimer";

    [Header("文本")]
    [SerializeField] private TMP_Text tipText;
    [SerializeField] private Text legacyTipText;

    [Header("动画")]
    [SerializeField] private float fadeInTime = 0.16f;
    [SerializeField] private float fadeOutTime = 0.22f;
    [SerializeField] private float showOffsetY = 18f;
    [SerializeField] private Ease showEase = Ease.OutQuad;
    [SerializeField] private Ease hideEase = Ease.InQuad;

    [Header("调试")]
    [SerializeField] private bool debugTip = true;

    private RectTransform _tipRoot;
    private Vector2 _baseAnchoredPosition;
    private DG.Tweening.Sequence _sequence;
    private Timer _hideTimer;

    public override bool NeedRaycaster => false;

    public override void Awake()
    {
        base.Awake();
        CacheReferences();
        HideInstant();
    }

    public override void Show()
    {
        base.Show();
        HideInstant();
    }

    private void OnEnable()
    {
        CacheReferences();
        EventCenter.Instance.AddEventListener<PickupTipEventData>(GameEvent.PickupTipRequested, OnPickupTipRequested);
    }

    private void OnDisable()
    {
        EventCenter.Instance.RemoveEventListener<PickupTipEventData>(GameEvent.PickupTipRequested, OnPickupTipRequested);
        StopHideTimer();
        KillSequence();
    }

    private void OnPickupTipRequested(PickupTipEventData eventData)
    {
        ShowTip(eventData);
    }

    private void ShowTip(PickupTipEventData eventData)
    {
        CacheReferences();
        if (tipText != null)
        {
            tipText.text = string.IsNullOrEmpty(eventData.itemName)
                ? eventData.description
                : $"{eventData.itemName}\n{eventData.description}";
            tipText.color = ResolveColor(eventData.tipColorKey);
        }
        else if (legacyTipText != null)
        {
            legacyTipText.text = string.IsNullOrEmpty(eventData.itemName)
                ? eventData.description
                : $"{eventData.itemName}\n{eventData.description}";
            legacyTipText.color = ResolveColor(eventData.tipColorKey);
        }

        if (debugTip)
        {
            Debug.Log(
                $"[TipCanvas] 收到提示 Item={eventData.itemName} Desc={eventData.description} TMP={(tipText != null)} Text={(legacyTipText != null)}",
                this);
        }

        StopHideTimer();
        KillSequence();

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }

        if (_tipRoot != null)
        {
            _tipRoot.anchoredPosition = _baseAnchoredPosition + Vector2.down * showOffsetY;
        }

        _sequence = DOTween.Sequence().SetUpdate(true);
        if (canvasGroup != null)
        {
            _sequence.Join(canvasGroup.DOFade(1f, fadeInTime).SetEase(showEase));
        }

        if (_tipRoot != null)
        {
            _sequence.Join(_tipRoot.DOAnchorPos(_baseAnchoredPosition, fadeInTime).SetEase(showEase));
        }

        StartHideTimer(eventData.duration);
    }

    private void HideTip()
    {
        KillSequence();
        _sequence = DOTween.Sequence().SetUpdate(true);
        if (canvasGroup != null)
        {
            _sequence.Join(canvasGroup.DOFade(0f, fadeOutTime).SetEase(hideEase));
        }

        if (_tipRoot != null)
        {
            _sequence.Join(_tipRoot.DOAnchorPos(_baseAnchoredPosition + Vector2.up * showOffsetY, fadeOutTime).SetEase(hideEase));
        }
    }

    private void HideInstant()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
    }

    private void StartHideTimer(float duration)
    {
        MultiTimerManager timerManager = MultiTimerManager.Instance;
        if (timerManager == null)
        {
            return;
        }

        _hideTimer = timerManager.CreateTimer(HideTimerId, true);
        _hideTimer.SetTargetTime(Mathf.Max(0.1f, duration));
        _hideTimer.OnTimeUp += OnHideTimerUp;
        _hideTimer.Start();
    }

    private void StopHideTimer()
    {
        if (_hideTimer == null)
        {
            return;
        }

        _hideTimer.OnTimeUp -= OnHideTimerUp;
        _hideTimer = null;
        MultiTimerManager timerManager = MultiTimerManager.Instance;
        if (timerManager != null)
        {
            timerManager.RemoveTimer(HideTimerId);
        }
    }

    private void OnHideTimerUp()
    {
        StopHideTimer();
        HideTip();
    }

    private void CacheReferences()
    {
        tipText ??= FindTipText();
        legacyTipText ??= FindLegacyTipText();
        if (_tipRoot == null)
        {
            if (tipText != null)
            {
                _tipRoot = tipText.transform.parent as RectTransform;
            }
            else if (legacyTipText != null)
            {
                _tipRoot = legacyTipText.transform.parent as RectTransform;
            }
            else
            {
                _tipRoot = transform as RectTransform;
            }
        }

        if (_tipRoot != null && _baseAnchoredPosition == Vector2.zero)
        {
            _baseAnchoredPosition = _tipRoot.anchoredPosition;
        }
    }

    private TMP_Text FindTipText()
    {
        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        if (texts == null || texts.Length == 0)
        {
            return null;
        }

        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text != null && text.name == "TipText")
            {
                return text;
            }
        }

        return texts[0];
    }

    private Text FindLegacyTipText()
    {
        Text[] texts = GetComponentsInChildren<Text>(true);
        if (texts == null || texts.Length == 0)
        {
            return null;
        }

        for (int i = 0; i < texts.Length; i++)
        {
            Text text = texts[i];
            if (text != null && text.name == "TipText")
            {
                return text;
            }
        }

        return texts[0];
    }

    private Color ResolveColor(string colorKey)
    {
        switch ((colorKey ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "heal":
                return new Color(0.42f, 1f, 0.58f, 1f);
            case "ammo":
                return new Color(0.58f, 0.82f, 1f, 1f);
            case "grenade":
                return new Color(1f, 0.78f, 0.42f, 1f);
            case "berserk":
                return new Color(1f, 0.34f, 0.28f, 1f);
            default:
                return Color.white;
        }
    }

    private void KillSequence()
    {
        _sequence?.Kill();
        _sequence = null;
    }
}
