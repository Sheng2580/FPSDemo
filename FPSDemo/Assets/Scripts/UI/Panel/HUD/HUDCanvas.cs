using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using PlayerData;

/// <summary>
/// 战斗 HUD 总入口
/// 负责组织伤害数字和能量显示 不处理具体战斗数值
/// </summary>
[UICanvas(UILoadType.AssetBundle, UILayer.HUD)]
[DisallowMultipleComponent]
public class HUDCanvas : BaseCanvas
{
    private const string CombatSceneName = "Combat";

    [Header("中心")]
    [SerializeField] private DamageCentre damageCentre;
    [SerializeField] private EnergyCentre energyCentre;
    [SerializeField] private Button openCardButton;
    [SerializeField] private CanvasGroup openCardButtonGroup;
    [SerializeField] private Canvas openCardButtonCanvas;
    [SerializeField] private GraphicRaycaster openCardButtonRaycaster;
    [SerializeField] private int openCardButtonSortingOrder = 450;
    [SerializeField] private float openCardButtonBreathScale = 1.08f;
    [SerializeField] private float openCardButtonBreathDuration = 0.6f;

    public DamageCentre DamageCentre => damageCentre;
    public EnergyCentre EnergyCentre => energyCentre;
    public override bool NeedRaycaster => false;

    private Tween _openCardButtonBreathTween;
    private Vector3 _openCardButtonBaseScale = Vector3.one;
    private bool _hasOpenCardButtonBaseScale;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void OpenForCombatScene()
    {
        if (SceneManager.GetActiveScene().name != CombatSceneName)
        {
            return;
        }

        UIManager.Instance.OpenPanelAsy<HUDCanvas>();
    }

    public override void Awake()
    {
        base.Awake();
        CacheReferences();
        BindButtons();
        ConfigureOpenCardButtonLayer();
        RefreshOpenCardButtonFromRuntime();
    }

    private void OnEnable()
    {
        CacheReferences();
        BindButtons();
        ConfigureOpenCardButtonLayer();
        RefreshOpenCardButtonFromRuntime();
        EventCenter.Instance.AddEventListener<PlayerEnergyStateChangedEventData>(GameEvent.PlayerEnergyStateChanged, OnPlayerEnergyStateChanged);
    }

    private void OnDisable()
    {
        EventCenter.Instance.RemoveEventListener<PlayerEnergyStateChangedEventData>(GameEvent.PlayerEnergyStateChanged, OnPlayerEnergyStateChanged);
        if (openCardButton != null)
        {
            openCardButton.onClick.RemoveListener(OnOpenCardButtonClicked);
        }

        StopOpenCardButtonBreath(true);
    }

    public override void Show()
    {
        base.Show();
        CacheReferences();
        BindButtons();
        ConfigureOpenCardButtonLayer();
        RefreshOpenCardButtonFromRuntime();
    }

    protected override void Reset()
    {
        base.Reset();
        CacheReferences();
    }

    private void CacheReferences()
    {
        damageCentre ??= GetComponentInChildren<DamageCentre>(true);
        energyCentre ??= GetComponentInChildren<EnergyCentre>(true);
        openCardButton ??= FindButton("OpenCardButton");
        if (openCardButton != null)
        {
            openCardButtonGroup ??= openCardButton.GetComponent<CanvasGroup>();
            openCardButtonCanvas ??= openCardButton.GetComponent<Canvas>();
            openCardButtonRaycaster ??= openCardButton.GetComponent<GraphicRaycaster>();
            if (!_hasOpenCardButtonBaseScale)
            {
                _openCardButtonBaseScale = openCardButton.transform.localScale;
                _hasOpenCardButtonBaseScale = true;
            }
        }
    }

    private void BindButtons()
    {
        if (openCardButton == null)
        {
            return;
        }

        openCardButton.onClick.RemoveListener(OnOpenCardButtonClicked);
        openCardButton.onClick.AddListener(OnOpenCardButtonClicked);
    }

    private void OnOpenCardButtonClicked()
    {
        EventCenter.Instance.EventTrigger(GameEvent.PlayerEnergyBlessingSelectRequested);
    }

    private void OnPlayerEnergyStateChanged(PlayerEnergyStateChangedEventData eventData)
    {
        SetOpenCardButtonVisible(eventData.currentState == PlayerEnergyState.LevelUpReady);

        if (eventData.currentState != PlayerEnergyState.BlessingSelecting)
        {
            return;
        }

        UIManager.Instance.OpenPanelAsy<BlessingSelectCanvas>(blessingCanvas =>
        {
            blessingCanvas?.OpenForEnergySelection();
        });
    }

    private void ConfigureOpenCardButtonLayer()
    {
        if (openCardButton == null)
        {
            return;
        }

        if (openCardButtonGroup == null)
        {
            openCardButtonGroup = openCardButton.gameObject.AddComponent<CanvasGroup>();
        }

        if (openCardButtonCanvas == null)
        {
            openCardButtonCanvas = openCardButton.gameObject.AddComponent<Canvas>();
        }

        if (openCardButtonRaycaster == null)
        {
            openCardButtonRaycaster = openCardButton.gameObject.AddComponent<GraphicRaycaster>();
        }

        openCardButtonCanvas.overrideSorting = true;
        openCardButtonCanvas.sortingOrder = openCardButtonSortingOrder;
        openCardButtonRaycaster.enabled = true;
    }

    private void RefreshOpenCardButtonFromRuntime()
    {
        PlayerEnergyRuntime runtime = FindObjectOfType<PlayerEnergyRuntime>();
        bool canOpen = runtime != null && runtime.CurrentState == PlayerEnergyState.LevelUpReady;
        SetOpenCardButtonVisible(canOpen);
    }

    private void SetOpenCardButtonVisible(bool visible)
    {
        if (openCardButton == null)
        {
            return;
        }

        openCardButton.gameObject.SetActive(true);
        if (openCardButtonGroup != null)
        {
            openCardButtonGroup.alpha = visible ? 1f : 0f;
            openCardButtonGroup.interactable = visible;
            openCardButtonGroup.blocksRaycasts = visible;
        }

        openCardButton.interactable = visible;
        if (openCardButtonRaycaster != null)
        {
            openCardButtonRaycaster.enabled = visible;
        }

        if (visible)
        {
            PlayOpenCardButtonBreath();
        }
        else
        {
            StopOpenCardButtonBreath(true);
        }
    }

    private void PlayOpenCardButtonBreath()
    {
        if (openCardButton == null)
        {
            return;
        }

        StopOpenCardButtonBreath(false);
        Transform buttonTransform = openCardButton.transform;
        buttonTransform.localScale = _openCardButtonBaseScale;
        _openCardButtonBreathTween = buttonTransform
            .DOScale(_openCardButtonBaseScale * openCardButtonBreathScale, openCardButtonBreathDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetUpdate(false);
    }

    private void StopOpenCardButtonBreath(bool resetScale)
    {
        _openCardButtonBreathTween?.Kill();
        _openCardButtonBreathTween = null;

        if (resetScale && openCardButton != null)
        {
            openCardButton.transform.localScale = _openCardButtonBaseScale;
        }
    }

    private Button FindButton(string targetName)
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button != null && button.name == targetName)
            {
                return button;
            }
        }

        return null;
    }
}
