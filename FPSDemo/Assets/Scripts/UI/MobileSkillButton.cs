using PlayerData;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MobileSkillButton : MonoBehaviour, IPointerDownHandler
{
    [SerializeField] private SkillType skillType = SkillType.Dodge;
    [SerializeField] private Graphic targetGraphic;
    [SerializeField] private Text label;
    [SerializeField] private Color readyColor = new Color(1f, 1f, 1f, 0.86f);
    [SerializeField] private Color cooldownColor = new Color(0.45f, 0.45f, 0.45f, 0.65f);
    [SerializeField] private Color emptyColor = new Color(0.25f, 0.25f, 0.25f, 0.5f);

    private float _cooldownRemaining;
    private int _currentCount = -1;
    private int _maxCount;
    private PlayerSkillController _skillController;

    public SkillType SkillType => skillType;

    private void Awake()
    {
        CacheReferences();
        UpdateVisual();
    }

    private void OnEnable()
    {
        EventCenter.Instance.AddEventListener<SkillCooldownEventData>(GameEvent.SkillCooldownChanged, OnSkillCooldownChanged);
        EventCenter.Instance.AddEventListener<SkillChargeEventData>(GameEvent.SkillChargeChanged, OnSkillChargeChanged);
        ResetRuntimeState();
        SyncRuntimeState();
        UpdateVisual();
    }

    private void OnDisable()
    {
        EventCenter.Instance.RemoveEventListener<SkillCooldownEventData>(GameEvent.SkillCooldownChanged, OnSkillCooldownChanged);
        EventCenter.Instance.RemoveEventListener<SkillChargeEventData>(GameEvent.SkillChargeChanged, OnSkillChargeChanged);
    }

    public void Configure(SkillType type, string displayText, Graphic graphic, Text text)
    {
        skillType = type;
        targetGraphic = graphic;
        label = text;
        if (label != null)
        {
            label.text = displayText;
        }

        SyncRuntimeState();
        UpdateVisual();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        switch (skillType)
        {
            case SkillType.Push:
                EventCenter.Instance.EventTrigger(GameEvent.MobilePushPressed);
                break;
            case SkillType.Grenade:
                EventCenter.Instance.EventTrigger(GameEvent.MobileGrenadePressed);
                break;
            case SkillType.Dodge:
            default:
                EventCenter.Instance.EventTrigger(GameEvent.MobileDodgePressed);
                break;
        }
    }

    private void OnSkillCooldownChanged(SkillCooldownEventData eventData)
    {
        if (eventData.skillType != skillType)
        {
            return;
        }

        _cooldownRemaining = eventData.remaining;
        UpdateVisual();
    }

    private void OnSkillChargeChanged(SkillChargeEventData eventData)
    {
        if (eventData.skillType != skillType)
        {
            return;
        }

        _currentCount = eventData.currentCount;
        _maxCount = eventData.maxCount;
        UpdateVisual();
    }

    private void CacheReferences()
    {
        targetGraphic ??= GetComponent<Graphic>();
        label ??= GetComponentInChildren<Text>(true);
    }

    private void ResetRuntimeState()
    {
        _cooldownRemaining = 0f;
        _currentCount = -1;
        _maxCount = 0;
    }

    private void SyncRuntimeState()
    {
        if (_skillController == null)
        {
            _skillController = FindObjectOfType<PlayerSkillController>();
        }

        if (_skillController == null
            || !_skillController.TryGetRuntimeState(
                skillType,
                out float cooldownRemaining,
                out _,
                out int currentCount,
                out int maxCount))
        {
            return;
        }

        _cooldownRemaining = cooldownRemaining;
        _currentCount = currentCount;
        _maxCount = maxCount;
    }

    private void UpdateVisual()
    {
        CacheReferences();

        bool hasChargeLimit = skillType == SkillType.Grenade && _maxCount > 0;
        bool isEmpty = hasChargeLimit && _currentCount <= 0;
        bool isCooling = _cooldownRemaining > 0.01f;

        if (targetGraphic != null)
        {
            targetGraphic.color = isEmpty ? emptyColor : isCooling ? cooldownColor : readyColor;
        }

        if (label == null)
        {
            return;
        }

        if (isCooling)
        {
            label.text = Mathf.CeilToInt(_cooldownRemaining).ToString();
            return;
        }

        label.text = GetDefaultLabel();
        if (hasChargeLimit)
        {
            label.text = $"{label.text}{_currentCount}";
        }
    }

    private string GetDefaultLabel()
    {
        switch (skillType)
        {
            case SkillType.Push:
                return "推";
            case SkillType.Grenade:
                return "雷";
            case SkillType.Dodge:
            default:
                return "闪";
        }
    }

}
