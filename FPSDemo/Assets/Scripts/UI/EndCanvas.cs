using Combat;
using DG.Tweening;
using PlayerData;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 战斗结束结算面板
/// 只负责显示结算结果和返回大厅
/// </summary>
[UICanvas(UILoadType.AssetBundle, UILayer.Popup, UseSafeArea = false)]
[DisallowMultipleComponent]
public sealed class EndCanvas : BaseCanvas
{
    private const string HallSceneName = "Hall";
    private const float NumberSequenceDuration = 3f;

    [Header("数字动画")]
    [SerializeField] private Ease numberEase = Ease.OutCubic;
    [SerializeField] private float panelFadeDuration = 0.35f;
    [SerializeField] private float resultRevealDuration = 0.32f;

    private TMP_Text _timeText;
    private TMP_Text _killText;
    private TMP_Text _goldText;
    private TMP_Text _buffText;
    private TMP_Text _pickupText;
    private TMP_Text _fireText;
    private TMP_Text _evaluationText;
    private Transform _evaluationRoot;
    private Button _saveAndExitButton;
    private Graphic[] _evaluationGraphics;
    private Graphic[] _buttonGraphics;
    private DG.Tweening.Sequence _sequence;
    private CombatRunResult _result;
    private bool _isReturning;

    public override bool UseSafeArea => false;
    public override bool NeedRaycaster => true;

    public override void Awake()
    {
        base.Awake();
        CacheReferences();
        BindButton();
    }

    private void OnDisable()
    {
        _sequence?.Kill();
        _sequence = null;
    }

    public void ShowResult(CombatRunResult result)
    {
        if (result == null)
        {
            Debug.LogError("[EndCanvas] 缺少结算数据", this);
            return;
        }

        _result = result;
        _isReturning = false;
        CacheReferences();
        BindButton();
        PlaySettlementSequence();
    }

    private void PlaySettlementSequence()
    {
        _sequence?.Kill();
        PrepareInitialState();

        _sequence = DOTween.Sequence().SetUpdate(true);
        _sequence.Append(canvasGroup.DOFade(1f, panelFadeDuration).SetEase(Ease.OutQuad));
        _sequence.Insert(
            0.2f,
            DOTween.To(
                    () => 0f,
                    value => SetTimeText(value, false),
                    _result.SurvivalSeconds,
                    1.55f)
                .SetEase(numberEase));
        _sequence.Insert(
            0.65f,
            DOTween.To(
                    () => 0f,
                    value => SetKillText(Mathf.RoundToInt(value), false),
                    _result.KillCount,
                    1.55f)
                .SetEase(numberEase));
        _sequence.Insert(
            1.05f,
            DOTween.To(
                    () => 0f,
                    value => SetGoldText(Mathf.RoundToInt(value)),
                    _result.GoldEarned,
                    1.75f)
                .SetEase(numberEase));
        InsertCountTween(0.2f, 1.55f, _result.BlessingSelectCount, SetBuffText);
        InsertCountTween(0.65f, 1.55f, _result.PickupCollectedCount, SetPickupText);
        InsertCountTween(1.05f, 1.75f, _result.WeaponFireCount, SetFireText);
        _sequence.InsertCallback(NumberSequenceDuration, ApplyFinalNumberText);
        _sequence.Insert(
            NumberSequenceDuration,
            _evaluationRoot.DOScale(1f, resultRevealDuration).SetEase(Ease.OutBack));
        InsertGraphicFade(_sequence, _evaluationGraphics, NumberSequenceDuration, resultRevealDuration);
        _sequence.Insert(
            NumberSequenceDuration + 0.3f,
            _saveAndExitButton.transform.DOScale(1f, resultRevealDuration).SetEase(Ease.OutBack));
        InsertGraphicFade(
            _sequence,
            _buttonGraphics,
            NumberSequenceDuration + 0.3f,
            resultRevealDuration);
        _sequence.InsertCallback(NumberSequenceDuration + 0.62f, EnableReturnButton);
    }

    private void PrepareInitialState()
    {
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;

        SetTimeText(0f, false);
        SetKillText(0, false);
        SetGoldText(0);
        SetBuffText(0);
        SetPickupText(0);
        SetFireText(0);
        _evaluationText.text = CombatEvaluationConfigLoader.ResolveEvaluationText(
            _result.SurvivalSeconds,
            _result.KillCount);

        _evaluationRoot.localScale = Vector3.one * 0.82f;
        _saveAndExitButton.transform.localScale = Vector3.one * 0.88f;
        SetGraphicAlpha(_evaluationGraphics, 0f);
        SetGraphicAlpha(_buttonGraphics, 0f);
        _saveAndExitButton.interactable = false;
    }

    private void ApplyFinalNumberText()
    {
        CombatRunSettlementResult settlement = _result.Settlement;
        SetTimeText(_result.SurvivalSeconds, settlement != null && settlement.isNewBestSurvivalTime);
        SetKillText(_result.KillCount, settlement != null && settlement.isNewBestKillCount);
        SetGoldText(_result.GoldEarned);
        SetBuffText(_result.BlessingSelectCount);
        SetPickupText(_result.PickupCollectedCount);
        SetFireText(_result.WeaponFireCount);
    }

    private void SetTimeText(float seconds, bool isNewRecord)
    {
        if (_timeText == null)
        {
            return;
        }

        _timeText.text = CombatTimeFormatter.Format(seconds) + (isNewRecord ? "（新纪录）" : string.Empty);
    }

    private void SetKillText(int value, bool isNewRecord)
    {
        if (_killText == null)
        {
            return;
        }

        _killText.text = Mathf.Max(0, value) + (isNewRecord ? "（新纪录）" : string.Empty);
    }

    private void SetGoldText(int value)
    {
        if (_goldText != null)
        {
            _goldText.text = Mathf.Max(0, value).ToString();
        }
    }

    private void SetBuffText(int value)
    {
        SetCountText(_buffText, value);
    }

    private void SetPickupText(int value)
    {
        SetCountText(_pickupText, value);
    }

    private void SetFireText(int value)
    {
        SetCountText(_fireText, value);
    }

    private void InsertCountTween(float at, float duration, int targetValue, System.Action<int> setter)
    {
        _sequence.Insert(
            at,
            DOTween.To(
                    () => 0f,
                    value => setter(Mathf.RoundToInt(value)),
                    Mathf.Max(0, targetValue),
                    duration)
                .SetEase(numberEase));
    }

    private static void SetCountText(TMP_Text text, int value)
    {
        if (text != null)
        {
            text.text = Mathf.Max(0, value).ToString();
        }
    }

    private void EnableReturnButton()
    {
        if (!_isReturning && _saveAndExitButton != null)
        {
            _saveAndExitButton.interactable = true;
        }
    }

    private void OnSaveAndExitClicked()
    {
        if (_isReturning)
        {
            return;
        }

        _isReturning = true;
        _saveAndExitButton.interactable = false;
        _saveAndExitButton.transform
            .DOPunchScale(Vector3.one * 0.08f, 0.18f, 6, 0.5f)
            .SetUpdate(true);

        if (!PlayerProgressSaveService.CommitCurrentSession(out PlayerSaveSlotSummary _))
        {
            _isReturning = false;
            _saveAndExitButton.interactable = true;
            HallTipNotifier.Show(
                "存档失败",
                "结算数据仍保留在当前会话 请重试保存",
                "grenade");
            return;
        }

        LoadSceneCanvas.LoadSceneWithTransition(HallSceneName, () =>
        {
            CloseCombatPanels();
            Time.timeScale = 1f;
        });
    }

    private static void CloseCombatPanels()
    {
        UIManager uiManager = UIManager.Instance;
        if (uiManager == null)
        {
            return;
        }

        // 保留正在执行过渡的 LoadSceneCanvas
        CloseAndDeactivate<TounchControllerCanvas>(uiManager);
        CloseAndDeactivate<HUDCanvas>(uiManager);
        CloseAndDeactivate<CombatCanvas>(uiManager);
        CloseAndDeactivate<TipCanvas>(uiManager);
        CloseAndDeactivate<EnemyLifebarCanvas>(uiManager);
        CloseAndDeactivate<HpAndWeaponCanvas>(uiManager);
        CloseAndDeactivate<BlessingSelectCanvas>(uiManager);
        CloseAndDeactivate<EndCanvas>(uiManager);
    }

    private static void CloseAndDeactivate<T>(UIManager uiManager) where T : BaseCanvas
    {
        T panel = uiManager.GetPanel<T>();
        uiManager.ClosePanel<T>();
        if (panel != null)
        {
            panel.gameObject.SetActive(false);
        }
    }

    private void CacheReferences()
    {
        _timeText ??= FindValueText("Time");
        _killText ??= FindValueText("StruckDown");
        _goldText ??= FindValueText("Dold") ?? FindValueText("Gold");
        _buffText ??= FindValueText("Buff");
        _pickupText ??= FindValueText("Prop");
        _fireText ??= FindValueText("Bullet");
        _evaluationRoot ??= FindChild("Evaluate");
        _evaluationText ??= FindText(_evaluationRoot, "Content");

        Transform buttonRoot = FindChild("Save&Exit");
        _saveAndExitButton ??= buttonRoot != null ? buttonRoot.GetComponent<Button>() : null;
        _evaluationGraphics = _evaluationRoot != null
            ? _evaluationRoot.GetComponentsInChildren<Graphic>(true)
            : new Graphic[0];
        _buttonGraphics = buttonRoot != null
            ? buttonRoot.GetComponentsInChildren<Graphic>(true)
            : new Graphic[0];
    }

    private void BindButton()
    {
        if (_saveAndExitButton == null)
        {
            return;
        }

        _saveAndExitButton.onClick.RemoveListener(OnSaveAndExitClicked);
        _saveAndExitButton.onClick.AddListener(OnSaveAndExitClicked);
    }

    private TMP_Text FindValueText(string parentName)
    {
        Transform parent = FindChild(parentName);
        return FindText(parent, "num");
    }

    private Transform FindChild(string targetName)
    {
        return FindChild(transform, targetName);
    }

    private static Transform FindChild(Transform root, string targetName)
    {
        if (root == null)
        {
            return null;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == targetName)
            {
                return child;
            }

            Transform nested = FindChild(child, targetName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    private static TMP_Text FindText(Transform root, string targetName)
    {
        Transform target = FindChild(root, targetName);
        return target != null ? target.GetComponent<TMP_Text>() : null;
    }

    private static void SetGraphicAlpha(Graphic[] graphics, float alpha)
    {
        if (graphics == null)
        {
            return;
        }

        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] != null)
            {
                Color color = graphics[i].color;
                color.a = alpha;
                graphics[i].color = color;
            }
        }
    }

    private static void InsertGraphicFade(DG.Tweening.Sequence sequence, Graphic[] graphics, float at, float duration)
    {
        if (sequence == null || graphics == null)
        {
            return;
        }

        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] != null)
            {
                sequence.Insert(at, graphics[i].DOFade(1f, duration));
            }
        }
    }
}
