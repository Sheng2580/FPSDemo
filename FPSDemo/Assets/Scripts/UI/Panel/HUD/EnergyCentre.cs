using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// HUD 能量显示中心
/// 能量数值由数据层事件提供 这里只负责两位小数缓动表现
/// </summary>
[DisallowMultipleComponent]
public class EnergyCentre : MonoBehaviour
{
    [Header("文本")]
    [SerializeField] private TMP_Text energyText;
    [SerializeField] private TMP_Text levelText;

    [Header("动画")]
    [SerializeField] private float valueTweenDuration = 0.28f;
    [SerializeField] private float resetTweenDuration = 0.45f;
    [SerializeField] private Ease valueEase = Ease.OutQuad;
    [SerializeField] private Ease resetEase = Ease.InOutQuad;
    [SerializeField] private float punchScale = 0.12f;
    [SerializeField] private float punchDuration = 0.16f;
    [SerializeField] private float levelReadyScale = 1.25f;
    [SerializeField] private float levelReadyDuration = 0.42f;
    [SerializeField] private Ease levelReadyEase = Ease.InOutSine;

    [Header("格式")]
    [SerializeField] private string energyFormat = "0.00";
    [SerializeField] private string levelPrefix = "Lv.";

    private RectTransform _energyRect;
    private Tweener _valueTweener;
    private Tweener _punchTweener;
    private Tweener _levelReadyTweener;
    private Vector3 _levelBaseScale = Vector3.one;
    private float _displayEnergy;
    private float _targetEnergy;
    private int _level = 1;

    public RectTransform EnergyRect => _energyRect;
    public float DisplayEnergy => _displayEnergy;
    public float TargetEnergy => _targetEnergy;
    public int Level => _level;

    private void Awake()
    {
        CacheReferences();
        CacheBaseScale();
        RefreshText();
    }

    private void Reset()
    {
        CacheReferences();
    }

    private void OnEnable()
    {
        EventCenter.Instance.AddEventListener<PlayerEnergyChangedEventData>(GameEvent.PlayerEnergyChanged, OnPlayerEnergyChanged);
        EventCenter.Instance.AddEventListener<PlayerEnergyLevelUpEventData>(GameEvent.PlayerEnergyLevelUpReady, OnPlayerEnergyLevelUpReady);
        EventCenter.Instance.AddEventListener<PlayerEnergyLevelUpEventData>(GameEvent.PlayerEnergyLevelUp, OnPlayerEnergyLevelUp);
    }

    private void OnDisable()
    {
        EventCenter.Instance.RemoveEventListener<PlayerEnergyChangedEventData>(GameEvent.PlayerEnergyChanged, OnPlayerEnergyChanged);
        EventCenter.Instance.RemoveEventListener<PlayerEnergyLevelUpEventData>(GameEvent.PlayerEnergyLevelUpReady, OnPlayerEnergyLevelUpReady);
        EventCenter.Instance.RemoveEventListener<PlayerEnergyLevelUpEventData>(GameEvent.PlayerEnergyLevelUp, OnPlayerEnergyLevelUp);
        KillTweens();
    }

    public bool TryGetLocalPosition(RectTransform targetRect, Camera uiCamera, out Vector2 localPosition)
    {
        localPosition = Vector2.zero;
        CacheReferences();

        if (_energyRect == null || targetRect == null)
        {
            return false;
        }

        Vector3 worldPosition = _energyRect.TransformPoint(_energyRect.rect.center);
        Vector2 screenPosition = RectTransformUtility.WorldToScreenPoint(uiCamera, worldPosition);
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(targetRect, screenPosition, uiCamera, out localPosition);
    }

    private void OnPlayerEnergyChanged(PlayerEnergyChangedEventData eventData)
    {
        _level = Mathf.Max(1, eventData.level);
        float duration = eventData.targetEnergy < _displayEnergy ? resetTweenDuration : valueTweenDuration;
        Ease ease = eventData.targetEnergy < _displayEnergy ? resetEase : valueEase;

        if (eventData.targetEnergy < eventData.maxEnergy)
        {
            StopLevelReadyLoop();
        }

        TweenEnergyTo(eventData.targetEnergy, duration, ease);
        RefreshLevelText();

        if (eventData.deltaEnergy > 0f)
        {
            PlayPunch();
        }
    }

    private void OnPlayerEnergyLevelUpReady(PlayerEnergyLevelUpEventData eventData)
    {
        _level = Mathf.Max(1, eventData.level);
        RefreshLevelText();
        PlayLevelReadyLoop();
        PlayPunch();
    }

    private void OnPlayerEnergyLevelUp(PlayerEnergyLevelUpEventData eventData)
    {
        _level = Mathf.Max(1, eventData.level);
        StopLevelReadyLoop();
        TweenEnergyTo(eventData.currentEnergy, resetTweenDuration, resetEase);
        RefreshLevelText();
        PlayPunch();
    }

    private void TweenEnergyTo(float targetEnergy, float duration, Ease ease)
    {
        _targetEnergy = Mathf.Max(0f, targetEnergy);
        _valueTweener?.Kill();

        if (duration <= 0f)
        {
            _displayEnergy = _targetEnergy;
            RefreshEnergyText();
            return;
        }

        _valueTweener = DOTween
            .To(() => _displayEnergy, value =>
            {
                _displayEnergy = value;
                RefreshEnergyText();
            }, _targetEnergy, duration)
            .SetEase(ease)
            .SetUpdate(false);
    }

    private void PlayPunch()
    {
        Transform target = energyText != null ? energyText.transform : transform;
        _punchTweener?.Kill();
        target.localScale = Vector3.one;
        _punchTweener = target
            .DOPunchScale(Vector3.one * punchScale, punchDuration, 6, 0.6f)
            .SetUpdate(false);
    }

    private void PlayLevelReadyLoop()
    {
        if (levelText == null)
        {
            return;
        }

        StopLevelReadyLoop();
        CacheBaseScale();
        levelText.transform.localScale = _levelBaseScale;
        _levelReadyTweener = levelText.transform
            .DOScale(_levelBaseScale * levelReadyScale, levelReadyDuration)
            .SetEase(levelReadyEase)
            .SetLoops(-1, LoopType.Yoyo)
            .SetUpdate(false);
    }

    private void StopLevelReadyLoop(bool resetScale = true)
    {
        _levelReadyTweener?.Kill();
        _levelReadyTweener = null;

        if (resetScale && levelText != null)
        {
            levelText.transform.localScale = _levelBaseScale;
        }
    }

    private void RefreshText()
    {
        RefreshEnergyText();
        RefreshLevelText();
    }

    private void RefreshEnergyText()
    {
        if (energyText != null)
        {
            energyText.text = _displayEnergy.ToString(energyFormat);
        }
    }

    private void RefreshLevelText()
    {
        if (levelText != null)
        {
            levelText.text = $"{levelPrefix}{_level}";
        }
    }

    private void CacheReferences()
    {
        energyText ??= FindText("EnergyText");
        levelText ??= FindText("LevelText");
        levelText ??= FindText("LVTest");
        _energyRect = energyText != null ? energyText.rectTransform : transform as RectTransform;
    }

    private void CacheBaseScale()
    {
        if (levelText != null)
        {
            _levelBaseScale = levelText.transform.localScale;
        }
    }

    private TMP_Text FindText(string targetName)
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child != null && child.name == targetName)
            {
                return child.GetComponent<TMP_Text>();
            }
        }

        return null;
    }

    private void KillTweens()
    {
        _valueTweener?.Kill();
        _valueTweener = null;
        _punchTweener?.Kill();
        _punchTweener = null;
        StopLevelReadyLoop();
    }
}
