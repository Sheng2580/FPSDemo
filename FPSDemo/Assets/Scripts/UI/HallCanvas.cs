using System.Collections.Generic;
using DG.Tweening;
using PlayerData;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class HallCanvas : MonoBehaviour
{
    private const string DefaultCombatSceneName = "Combat";
    private const int PistolWeaponId = 1;
    private const int RifleWeaponId = 2;
    private const int ShotgunWeaponId = 3;

    [SerializeField] private GameObject playerPanel;
    [SerializeField] private GameObject weaponPanel;
    [SerializeField] private GameObject gamePanel;
    [SerializeField] private Graphic playerTabGraphic;
    [SerializeField] private Graphic weaponTabGraphic;
    [SerializeField] private Graphic gameTabGraphic;
    [SerializeField] private Text goldText;
    [SerializeField] private string combatSceneName = DefaultCombatSceneName;
    [SerializeField] private bool enableKeyboardTest = true;
    [SerializeField] private Color normalTabColor = Color.white;
    [SerializeField] private Color selectedTabColor = new Color(1f, 0.82f, 0.36f, 1f);

    [Header("武器选择表现")]
    [SerializeField] private Color fixedWeaponColor = new Color(1f, 0.78f, 0.78f, 0.92f);
    [SerializeField] private Color selectedWeaponColor = new Color(1f, 0.82f, 0.36f, 0.92f);
    [SerializeField] private Color normalWeaponColor = new Color(1f, 1f, 1f, 0.35f);
    [SerializeField] private float selectedWeaponScale = 1.035f;
    [SerializeField] private float tweenDuration = 0.18f;

    private readonly List<HallWeaponOption> _weaponOptions = new List<HallWeaponOption>();
    private PlayerSaveData _saveData;
    private int _selectedSecondWeaponId;
    private Button _saveButton;
    private Button _startButton;
    private Text _saveButtonText;
    private string _saveButtonDefaultText = "保存游戏";
    private DG.Tweening.Sequence _pageSequence;
    private Tween _saveTextTween;
    private Tween _saveButtonBreathTween;
    private Tween _goldTween;
    private float _displayGold;
    private GameObject _currentPanel;
    private HallUpgradePresenter _upgradePresenter;
    private bool _isStartingCombat;
    private bool _lastSaveDirtyState;

    private void Awake()
    {
        Time.timeScale = 1f;
        ResolveMissingReferences();
        LoadProgress();
        SetGold(_saveData != null ? _saveData.gold : 0, true);
        CacheGamePanelReferences();
        BindTabButtons();
        BindGamePanelButtons();
        RefreshGameWeaponSelection(true);
        RefreshStartButtonState(true);
        RefreshSaveButtonDirtyState(true);
        _upgradePresenter = new HallUpgradePresenter(
            playerPanel,
            weaponPanel,
            selectedWeaponColor,
            normalWeaponColor,
            tweenDuration);
    }

    private void Start()
    {
        ShowOnly(gamePanel, true);
    }

    private void OnEnable()
    {
        EventCenter.Instance.AddEventListener<PlayerGoldChangedEventData>(
            GameEvent.PlayerGoldChanged,
            OnPlayerGoldChanged);
    }

    private void OnDisable()
    {
        EventCenter.Instance.RemoveEventListener<PlayerGoldChangedEventData>(
            GameEvent.PlayerGoldChanged,
            OnPlayerGoldChanged);
        KillTweens();
        _upgradePresenter?.Dispose();
    }

    private void Update()
    {
        RefreshSaveButtonDirtyState(false);

        if (!enableKeyboardTest)
        {
            return;
        }

        // 测试键只用于大厅本地预览
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            ShowPlayerPanel();
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            ShowWeaponPanel();
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            ShowGamePanel();
        }
    }

    public void ShowPlayerPanel()
    {
        ShowOnly(playerPanel, false);
        _upgradePresenter?.RefreshAll(false);
    }

    public void ShowWeaponPanel()
    {
        ShowOnly(weaponPanel, false);
        _upgradePresenter?.RefreshAll(false);
    }

    public void ShowGamePanel()
    {
        ShowOnly(gamePanel, false);
    }

    public void StartCombat()
    {
        if (_isStartingCombat)
        {
            return;
        }

        if (!HasSelectedSecondWeapon())
        {
            PlayButtonPunch(_startButton);
            HallTipNotifier.Show(
                "请选择武器",
                "手枪固定携带 还需要选择步枪或霰弹枪",
                "grenade");
            return;
        }

        UpdateCurrentSelection();
        PlayButtonPunch(_startButton);
        _isStartingCombat = true;
        if (_startButton != null)
        {
            _startButton.interactable = false;
        }

        string sceneName = string.IsNullOrWhiteSpace(combatSceneName)
            ? DefaultCombatSceneName
            : combatSceneName;
        LoadSceneCanvas.LoadSceneWithTransition(sceneName);
    }

    public void SaveGameForTest()
    {
        UpdateCurrentSelection();
        bool saved = PlayerProgressSaveService.CommitCurrentSession(out PlayerSaveSlotSummary summary);
        _saveData = PlayerProgressSaveService.Load();
        RefreshSaveButtonDirtyState(true);

        if (!saved)
        {
            PlayButtonPunch(_saveButton);
            HallTipNotifier.Show(
                "存档失败",
                "无法写入存档文件 请检查设备存储空间",
                "grenade");
            return;
        }

        PlaySaveFeedback();
        string savedAt = summary != null
            ? summary.SavedAt.ToString("yyyy-MM-dd HH:mm:ss")
            : "刚刚";
        HallTipNotifier.Show(
            "存档成功",
            $"保存时间 {savedAt}",
            "ammo");
    }

    private void LoadProgress()
    {
        _saveData = PlayerProgressSaveService.Load();
        // 每次进入大厅都由玩家重新选择本局第二把武器
        _selectedSecondWeaponId = 0;
    }

    private void OnPlayerGoldChanged(PlayerGoldChangedEventData eventData)
    {
        SetGold(eventData.gold, false);
    }

    private void SetGold(int value, bool immediate)
    {
        int targetGold = Mathf.Max(0, value);
        _goldTween?.Kill();
        _goldTween = null;

        if (goldText == null)
        {
            _displayGold = targetGold;
            return;
        }

        if (immediate)
        {
            _displayGold = targetGold;
            goldText.text = targetGold.ToString();
            return;
        }

        _goldTween = DOTween.To(
                () => _displayGold,
                displayValue =>
                {
                    _displayGold = displayValue;
                    goldText.text = Mathf.RoundToInt(displayValue).ToString();
                },
                targetGold,
                0.3f)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true);

        RectTransform rect = goldText.transform as RectTransform;
        if (rect != null)
        {
            rect.DOKill();
            rect.DOPunchScale(Vector3.one * 0.08f, 0.22f, 6, 0.7f).SetUpdate(true);
        }
    }

    private void UpdateCurrentSelection()
    {
        _selectedSecondWeaponId = PlayerProgressSaveService.NormalizeSecondWeaponId(_selectedSecondWeaponId);
        PlayerProgressSaveService.SaveSelectedSecondWeapon(_selectedSecondWeaponId);
        _saveData = PlayerProgressSaveService.Load();
        RefreshSaveButtonDirtyState(false);
    }

    private void RefreshSaveButtonDirtyState(bool immediate)
    {
        if (_saveButton == null)
        {
            return;
        }

        bool isDirty = PlayerProgressSaveService.IsCurrentSessionDirty;
        if (!immediate && _lastSaveDirtyState == isDirty)
        {
            return;
        }

        _lastSaveDirtyState = isDirty;
        _saveButtonBreathTween?.Kill();
        _saveButtonBreathTween = null;

        RectTransform rect = _saveButton.transform as RectTransform;
        if (rect == null)
        {
            return;
        }

        rect.DOKill();
        rect.localScale = Vector3.one;
        if (!isDirty)
        {
            return;
        }

        _saveButtonBreathTween = rect
            .DOScale(1.045f, 0.55f)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetUpdate(true);
    }

    private void ShowOnly(GameObject activePanel, bool immediate)
    {
        if (activePanel == null)
        {
            return;
        }

        if (_currentPanel == activePanel && !immediate)
        {
            PlayPanelPulse(activePanel);
            return;
        }

        GameObject outgoingPanel = FindVisiblePanel(activePanel);
        _pageSequence?.Kill();
        KillPanelTweens(playerPanel);
        KillPanelTweens(weaponPanel);
        KillPanelTweens(gamePanel);
        _currentPanel = activePanel;

        if (immediate || outgoingPanel == null || outgoingPanel == activePanel)
        {
            SetPanelImmediate(playerPanel, playerPanel == activePanel);
            SetPanelImmediate(weaponPanel, weaponPanel == activePanel);
            SetPanelImmediate(gamePanel, gamePanel == activePanel);
            RefreshTabState(activePanel, true);
            return;
        }

        DeactivatePanelExcept(playerPanel, outgoingPanel);
        DeactivatePanelExcept(weaponPanel, outgoingPanel);
        DeactivatePanelExcept(gamePanel, outgoingPanel);

        CanvasGroup outgoingGroup = outgoingPanel.GetComponent<CanvasGroup>();
        RectTransform outgoingRect = outgoingPanel.transform as RectTransform;
        SetPanelInput(outgoingGroup, false);

        _pageSequence = DOTween.Sequence();
        float exitDuration = tweenDuration * 0.45f;
        if (outgoingGroup != null)
        {
            _pageSequence.Append(outgoingGroup.DOFade(0f, exitDuration).SetEase(Ease.InQuad));
        }
        else
        {
            _pageSequence.AppendInterval(exitDuration);
        }

        if (outgoingRect != null)
        {
            _pageSequence.Join(outgoingRect.DOScale(0.985f, exitDuration).SetEase(Ease.InQuad));
        }

        _pageSequence.AppendCallback(() =>
        {
            if (outgoingPanel != null && outgoingPanel != _currentPanel)
            {
                SetPanelImmediate(outgoingPanel, false);
            }

            PrepareIncomingPanel(activePanel);
        });

        CanvasGroup incomingGroup = activePanel.GetComponent<CanvasGroup>();
        RectTransform incomingRect = activePanel.transform as RectTransform;
        if (incomingGroup != null)
        {
            _pageSequence.Append(incomingGroup.DOFade(1f, tweenDuration).SetEase(Ease.OutQuad));
        }
        else
        {
            _pageSequence.AppendInterval(tweenDuration);
        }

        if (incomingRect != null)
        {
            _pageSequence.Join(incomingRect.DOScale(1f, tweenDuration).SetEase(Ease.OutBack));
        }

        _pageSequence.OnComplete(() =>
        {
            if (_currentPanel == activePanel)
            {
                SetPanelInput(incomingGroup, true);
            }
        });
        RefreshTabState(activePanel, immediate);
    }

    private GameObject FindVisiblePanel(GameObject incomingPanel)
    {
        if (_currentPanel != null && _currentPanel != incomingPanel && _currentPanel.activeSelf)
        {
            return _currentPanel;
        }

        GameObject[] panels = { playerPanel, weaponPanel, gamePanel };
        for (int i = 0; i < panels.Length; i++)
        {
            GameObject panel = panels[i];
            if (panel != null && panel != incomingPanel && panel.activeSelf)
            {
                return panel;
            }
        }

        return null;
    }

    private static void PrepareIncomingPanel(GameObject panel)
    {
        if (panel == null)
        {
            return;
        }

        panel.SetActive(true);
        CanvasGroup canvasGroup = panel.GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            SetPanelInput(canvasGroup, false);
        }

        RectTransform rect = panel.transform as RectTransform;
        if (rect != null)
        {
            rect.localScale = Vector3.one * 0.97f;
        }
    }

    private static void SetPanelImmediate(GameObject panel, bool active)
    {
        if (panel == null)
        {
            return;
        }

        CanvasGroup canvasGroup = panel.GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            canvasGroup.alpha = active ? 1f : 0f;
            SetPanelInput(canvasGroup, active);
        }

        RectTransform rect = panel.transform as RectTransform;
        if (rect != null)
        {
            rect.localScale = Vector3.one;
        }

        panel.SetActive(active);
    }

    private static void DeactivatePanelExcept(GameObject panel, GameObject exception)
    {
        if (panel != null && panel != exception)
        {
            SetPanelImmediate(panel, false);
        }
    }

    private static void SetPanelInput(CanvasGroup canvasGroup, bool enabled)
    {
        if (canvasGroup == null)
        {
            return;
        }

        canvasGroup.interactable = enabled;
        canvasGroup.blocksRaycasts = enabled;
    }

    private static void KillPanelTweens(GameObject panel)
    {
        if (panel == null)
        {
            return;
        }

        panel.GetComponent<CanvasGroup>()?.DOKill();
        (panel.transform as RectTransform)?.DOKill();
    }

    private void PlayPanelPulse(GameObject panel)
    {
        RectTransform rect = panel != null ? panel.transform as RectTransform : null;
        if (rect == null)
        {
            return;
        }

        rect.DOKill();
        rect.localScale = Vector3.one;
        rect.DOPunchScale(Vector3.one * 0.018f, 0.18f, 5, 0.6f);
    }

    private void RefreshTabState(GameObject activePanel, bool immediate)
    {
        SetTabColor(playerTabGraphic, playerPanel == activePanel, immediate);
        SetTabColor(weaponTabGraphic, weaponPanel == activePanel, immediate);
        SetTabColor(gameTabGraphic, gamePanel == activePanel, immediate);
    }

    private void SetTabColor(Graphic graphic, bool selected, bool immediate)
    {
        if (graphic == null)
        {
            return;
        }

        Color targetColor = selected ? selectedTabColor : normalTabColor;
        graphic.DOKill();
        if (immediate)
        {
            graphic.color = targetColor;
            return;
        }

        graphic.DOColor(targetColor, tweenDuration);
        RectTransform rect = graphic.transform as RectTransform;
        if (selected && rect != null)
        {
            rect.DOKill();
            rect.DOPunchScale(Vector3.one * 0.04f, 0.18f, 5, 0.7f);
        }
    }

    private void CacheGamePanelReferences()
    {
        _weaponOptions.Clear();
        RectTransform selectionRoot = FindChildRecursive(gamePanel != null ? gamePanel.transform : null, "WeaponSelection")?.transform as RectTransform;
        if (selectionRoot != null)
        {
            for (int i = 0; i < selectionRoot.childCount; i++)
            {
                RectTransform item = selectionRoot.GetChild(i) as RectTransform;
                if (item == null)
                {
                    continue;
                }

                int weaponId = i + 1;
                HallWeaponOption option = new HallWeaponOption(item, weaponId);
                _weaponOptions.Add(option);
            }
        }

        _saveButton = FindButtonByText(gamePanel != null ? gamePanel.transform : null, "保存游戏");
        _startButton = FindButtonByText(gamePanel != null ? gamePanel.transform : null, "开始游戏");
        _saveButtonText = _saveButton != null ? _saveButton.GetComponentInChildren<Text>(true) : null;
        if (_saveButtonText != null)
        {
            _saveButtonDefaultText = _saveButtonText.text;
        }
    }

    private void BindGamePanelButtons()
    {
        for (int i = 0; i < _weaponOptions.Count; i++)
        {
            HallWeaponOption option = _weaponOptions[i];
            if (option.Button == null)
            {
                continue;
            }

            int weaponId = option.WeaponId;
            option.PrepareButton();
            option.Button.onClick.RemoveAllListeners();
            option.Button.onClick.AddListener(() => SelectWeaponForRun(weaponId));
        }

        if (_saveButton != null)
        {
            _saveButton.onClick.RemoveAllListeners();
            _saveButton.onClick.AddListener(SaveGameForTest);
        }

        if (_startButton != null)
        {
            _startButton.onClick.RemoveAllListeners();
            _startButton.onClick.AddListener(StartCombat);
        }
    }

    private void BindTabButtons()
    {
        BindTabButton(playerTabGraphic, ShowPlayerPanel);
        BindTabButton(weaponTabGraphic, ShowWeaponPanel);
        BindTabButton(gameTabGraphic, ShowGamePanel);
    }

    private static void BindTabButton(Graphic graphic, UnityAction action)
    {
        Button button = graphic != null ? graphic.GetComponent<Button>() : null;
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    private void SelectWeaponForRun(int weaponId)
    {
        HallWeaponOption option = FindWeaponOption(weaponId);
        if (weaponId == PistolWeaponId)
        {
            PlayWeaponOptionPunch(option);
            return;
        }

        _selectedSecondWeaponId = PlayerProgressSaveService.NormalizeSecondWeaponId(weaponId);
        RefreshGameWeaponSelection(false);
        RefreshStartButtonState(false);
        PlayWeaponOptionPunch(FindWeaponOption(_selectedSecondWeaponId));
    }

    private bool HasSelectedSecondWeapon()
    {
        return _selectedSecondWeaponId == RifleWeaponId || _selectedSecondWeaponId == ShotgunWeaponId;
    }

    private void RefreshStartButtonState(bool immediate)
    {
        if (_startButton == null)
        {
            return;
        }

        bool canStart = HasSelectedSecondWeapon();
        CanvasGroup canvasGroup = _startButton.GetComponent<CanvasGroup>();
        canvasGroup?.DOKill();

        if (!canStart)
        {
            _startButton.interactable = false;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            _startButton.gameObject.SetActive(false);
            return;
        }

        _startButton.gameObject.SetActive(true);
        _startButton.interactable = true;
        if (canvasGroup == null)
        {
            PlayButtonPunch(_startButton);
            return;
        }

        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
        if (immediate)
        {
            canvasGroup.alpha = 1f;
            return;
        }

        canvasGroup.alpha = 0f;
        canvasGroup.DOFade(1f, tweenDuration);
        PlayButtonPunch(_startButton);
    }

    private void RefreshGameWeaponSelection(bool immediate)
    {
        for (int i = 0; i < _weaponOptions.Count; i++)
        {
            HallWeaponOption option = _weaponOptions[i];
            bool selected = option.WeaponId == PistolWeaponId || option.WeaponId == _selectedSecondWeaponId;
            bool fixedWeapon = option.WeaponId == PistolWeaponId;
            Color color = fixedWeapon
                ? fixedWeaponColor
                : selected
                    ? selectedWeaponColor
                    : normalWeaponColor;

            option.SetSelected(selected, color, selectedWeaponScale, tweenDuration, immediate);
        }
    }

    private HallWeaponOption FindWeaponOption(int weaponId)
    {
        for (int i = 0; i < _weaponOptions.Count; i++)
        {
            if (_weaponOptions[i].WeaponId == weaponId)
            {
                return _weaponOptions[i];
            }
        }

        return null;
    }

    private void PlayWeaponOptionPunch(HallWeaponOption option)
    {
        if (option?.Rect == null)
        {
            return;
        }

        option.Rect.DOKill();
        option.Rect.DOPunchScale(Vector3.one * 0.04f, 0.16f, 5, 0.75f);
    }

    private void PlayButtonPunch(Button button)
    {
        RectTransform rect = button != null ? button.transform as RectTransform : null;
        if (rect == null)
        {
            return;
        }

        rect.DOKill();
        rect.localScale = Vector3.one;
        rect.DOPunchScale(Vector3.one * 0.08f, 0.18f, 6, 0.7f);
    }

    private void PlaySaveFeedback()
    {
        PlayButtonPunch(_saveButton);

        if (_saveButtonText == null)
        {
            return;
        }

        _saveTextTween?.Kill();
        _saveButtonText.text = "已保存";
        _saveButtonText.DOKill();
        _saveButtonText.color = selectedTabColor;
        RectTransform textRect = _saveButtonText.transform as RectTransform;
        if (textRect != null)
        {
            textRect.DOKill();
            textRect.DOPunchScale(Vector3.one * 0.08f, 0.2f, 6, 0.7f);
        }

        _saveTextTween = DOVirtual.DelayedCall(0.7f, () =>
        {
            if (_saveButtonText != null)
            {
                _saveButtonText.text = _saveButtonDefaultText;
                _saveButtonText.DOColor(Color.black, tweenDuration);
            }
        });
    }

    private void ResolveMissingReferences()
    {
        Transform root = transform.Find("Root");
        if (root == null)
        {
            root = transform.childCount > 0 ? transform.GetChild(0) : null;
        }

        if (root == null)
        {
            return;
        }

        playerPanel = playerPanel != null ? playerPanel : FindChildRecursive(root, "PlayerUPPanel");
        weaponPanel = weaponPanel != null ? weaponPanel : FindChildRecursive(root, "WeapponUPPanel");
        gamePanel = gamePanel != null ? gamePanel : FindChildRecursive(root, "GamePanel");

        if (goldText == null)
        {
            Transform goldRoot = FindTransformRecursive(root, "Gold");
            Transform goldNumber = goldRoot != null ? goldRoot.Find("Num") : null;
            goldText = goldNumber != null ? goldNumber.GetComponent<Text>() : null;
        }

        Transform topNav = root.childCount > 0 ? root.GetChild(0) : null;
        playerTabGraphic = playerTabGraphic != null ? playerTabGraphic : GetChildGraphic(topNav, 0);
        weaponTabGraphic = weaponTabGraphic != null ? weaponTabGraphic : GetChildGraphic(topNav, 1);
        gameTabGraphic = gameTabGraphic != null ? gameTabGraphic : GetChildGraphic(topNav, 2);
    }

    private static Button FindButtonByText(Transform root, string label)
    {
        if (root == null)
        {
            return null;
        }

        Text[] texts = root.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            Text text = texts[i];
            if (text == null || text.text.Trim() != label)
            {
                continue;
            }

            return text.GetComponentInParent<Button>(true);
        }

        return null;
    }

    private static GameObject FindChildRecursive(Transform root, string childName)
    {
        Transform child = FindTransformRecursive(root, childName);
        return child != null ? child.gameObject : null;
    }

    private static Transform FindTransformRecursive(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        Transform child = root.Find(childName);
        if (child != null)
        {
            return child;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform result = FindTransformRecursive(root.GetChild(i), childName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private static Graphic GetChildGraphic(Transform parent, int index)
    {
        if (parent == null || parent.childCount <= index)
        {
            return null;
        }

        return parent.GetChild(index).GetComponent<Graphic>();
    }

    private void KillTweens()
    {
        _pageSequence?.Kill();
        _saveTextTween?.Kill();
        _saveButtonBreathTween?.Kill();
        _goldTween?.Kill();
        goldText?.DOKill();
        for (int i = 0; i < _weaponOptions.Count; i++)
        {
            _weaponOptions[i]?.KillTweens();
        }
    }

    private sealed class HallWeaponOption
    {
        public readonly RectTransform Rect;
        public readonly int WeaponId;
        public readonly Button Button;
        private readonly Graphic _background;
        private readonly Vector3 _baseScale;

        public HallWeaponOption(RectTransform rect, int weaponId)
        {
            Rect = rect;
            WeaponId = weaponId;
            Button = rect != null ? rect.GetComponent<Button>() : null;
            _background = rect != null ? rect.GetComponent<Graphic>() : null;
            _baseScale = rect != null ? rect.localScale : Vector3.one;
        }

        public void SetSelected(bool selected, Color color, float selectedScale, float duration, bool immediate)
        {
            if (Rect == null)
            {
                return;
            }

            Vector3 targetScale = _baseScale * (selected ? selectedScale : 1f);
            Rect.DOKill();
            if (immediate)
            {
                Rect.localScale = targetScale;
                if (_background != null)
                {
                    _background.canvasRenderer.SetColor(Color.white);
                    _background.color = color;
                }

                return;
            }

            Rect.DOScale(targetScale, duration).SetEase(Ease.OutQuad);
            if (_background != null)
            {
                _background.DOKill();
                _background.canvasRenderer.SetColor(Color.white);
                _background.DOColor(color, duration)
                    .OnComplete(() =>
                    {
                        if (_background != null)
                        {
                            _background.canvasRenderer.SetColor(Color.white);
                            _background.color = color;
                        }
                    });
            }
        }

        public void PrepareButton()
        {
            if (Button == null)
            {
                return;
            }

            // 选中颜色由 Hall 的 DG 动画统一控制 避免 Button ColorTint 覆盖结果
            Button.interactable = true;
            Button.transition = Selectable.Transition.None;
            Button.navigation = new Navigation { mode = Navigation.Mode.None };
            if (Button.targetGraphic != null)
            {
                Button.targetGraphic.canvasRenderer.SetColor(Color.white);
            }
        }

        public void KillTweens()
        {
            Rect?.DOKill();
            _background?.DOKill();
        }
    }
}
