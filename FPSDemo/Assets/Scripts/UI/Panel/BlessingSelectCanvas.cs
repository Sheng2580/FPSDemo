using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 祝福选择界面
/// </summary>
[UICanvas(UILoadType.AssetBundle, UILayer.Popup, UseSafeArea = false)]
public class BlessingSelectCanvas : BaseCanvas
{
    private const string CombatSceneName = "Combat";

    [Header("调试")]
    [SerializeField] private bool enableDebugKeys;
    [SerializeField] private KeyCode openKey = KeyCode.J;
    [SerializeField] private KeyCode closeKey = KeyCode.K;
    [SerializeField] private KeyCode confirmKey = KeyCode.L;

    [Header("界面引用")]
    [SerializeField] private RectTransform cardRoot;
    [SerializeField] private RectTransform cardButton;
    [SerializeField] private CanvasGroup cardRootGroup;
    [SerializeField] private Button confirmButton;
    [SerializeField] private CanvasGroup confirmButtonGroup;
    [SerializeField] private Button cardButtonClickArea;
    [SerializeField] private Text buffCountText;

    [Header("卡片引用")]
    [SerializeField] private RectTransform[] cards;
    [SerializeField] private BlessingCardItem[] cardItems;

    [Header("卡片布局")]
    [SerializeField] private BlessingCardDeckSettings cardDeckSettings = BlessingCardDeckSettings.CreateDefault();

    [Header("Buff计数")]
    [SerializeField] private float buffCountPunchScale = 0.18f;

    private readonly BlessingSelectionRuntime _selectionRuntime = new BlessingSelectionRuntime();
    private readonly BlessingCardDeck _cardDeck = new BlessingCardDeck();
    private BlessingCandidateProvider _candidateProvider;
    private CanvasGroup _canvasGroup;
    private DG.Tweening.Sequence _sequence;
    private Tween _buffCountTween;
    private float _timeScaleBeforeSelection = 1f;
    private bool _hasPausedGame;
    private bool _isOpened;
    private bool _isAnimating;
    private int _combatSceneHandle = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void OpenForCombatScene()
    {
        if (SceneManager.GetActiveScene().name != CombatSceneName)
        {
            return;
        }

        UIManager.Instance.OpenPanelAsy<BlessingSelectCanvas>();
    }

    public override void Awake()
    {
        base.Awake();
        CacheReferences();
        ResetRuntimeForNewCombat();
        BindButtons();
        SetClosedImmediate();
        RefreshBuffCountText(false);
    }

    public override void Show()
    {
        base.Show();
        CacheReferences();
        ResetRuntimeForNewCombat();
        BindButtons();
        SetClosedImmediate();
    }

    private void Update()
    {
        if (!enableDebugKeys)
        {
            return;
        }

        if (Input.GetKeyDown(openKey))
        {
            OpenForEnergySelection();
        }

        if (Input.GetKeyDown(closeKey))
        {
            PlayClose(true);
        }

        if (Input.GetKeyDown(confirmKey))
        {
            ConfirmCurrentCard();
        }
    }

    private void OnDisable()
    {
        KillSequence();
        _cardDeck.StopAllCardLoops(true, false);
        RestoreGameTime();
    }

    public void OpenForEnergySelection()
    {
        CacheReferences();
        _cardDeck.SetCandidates(_candidateProvider.ResolveCandidates());
        PlayOpen();
    }

    public void OpenWithCandidates(IReadOnlyList<BlessingCardViewData> candidates)
    {
        CacheReferences();
        _cardDeck.SetCandidates(candidates);
        PlayOpen();
    }

    public void SelectCard(BlessingCardItem card)
    {
        if (_isAnimating || !_isOpened)
        {
            return;
        }

        _cardDeck.SelectCard(card);
    }

    public void ConfirmCurrentCard()
    {
        if (!_cardDeck.HasSelectedCard || _isAnimating || !_isOpened)
        {
            return;
        }

        BlessingCardViewData selectedData = _cardDeck.SelectedData;
        int selectedCardIndex = _cardDeck.SelectedIndex;

        KillSequence();
        _cardDeck.SetCardsRaycast(false);
        _isAnimating = true;
        _sequence = _cardDeck.CreateConfirmSequence();
        _sequence.OnComplete(() =>
        {
            int stackCount = _selectionRuntime.AddBlessing(selectedData.BlessingId);
            _cardDeck.ClearSelectedCard(true);
            _cardDeck.SetClosedImmediate();
            RefreshBuffCountText(true);
            _isAnimating = false;
            _isOpened = false;
            SetSelectionVisible(false);
            EventCenter.Instance.EventTrigger(
                GameEvent.PlayerEnergyBlessingSelected,
                new PlayerEnergyBlessingSelectedEventData(
                    selectedCardIndex,
                    _selectionRuntime.OwnedBuffCount,
                    selectedData.BlessingId,
                    selectedData.Tier,
                    stackCount,
                    selectedData.Value,
                    selectedData.DisplayName,
                    selectedData.Description));
            RestoreGameTime();
        });
    }

    public void OpenOwnedBuffPanel()
    {
        Debug.Log($"打开本局祝福列表占位 当前数量 {_selectionRuntime.OwnedBuffCount}");
    }

    private void PlayOpen()
    {
        KillSequence();
        _cardDeck.PrepareForOpen();
        PauseGameForSelection();
        SetSelectionVisible(true);

        _isAnimating = true;
        _isOpened = true;
        _sequence = _cardDeck.CreateOpenSequence();
        _sequence.OnComplete(() =>
        {
            _isAnimating = false;
            _cardDeck.SetCardsRaycast(true);
        });
    }

    private void PlayClose(bool notifyCancel)
    {
        if (!_isOpened && !_isAnimating)
        {
            SetClosedImmediate();
            RestoreGameTime();
            return;
        }

        KillSequence();
        _cardDeck.ClearSelectedCard(true);
        _cardDeck.SetCardsRaycast(false);
        _isAnimating = true;

        _sequence = _cardDeck.CreateCloseSequence();
        _sequence.OnComplete(() =>
        {
            _isAnimating = false;
            _isOpened = false;
            SetSelectionVisible(false);
            RestoreGameTime();
            if (notifyCancel)
            {
                EventCenter.Instance.EventTrigger(GameEvent.PlayerEnergyBlessingSelectCanceled);
            }
        });
    }

    private void CacheReferences()
    {
        _canvasGroup ??= GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
        {
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        cardRoot ??= FindChildRect("CardRoot");
        cardButton ??= FindChildRect("CardButton");
        confirmButton ??= FindButton("Button (Legacy)");
        cardButtonClickArea ??= cardButton != null ? cardButton.GetComponent<Button>() : null;

        if (cardRootGroup == null && cardRoot != null)
        {
            cardRootGroup = cardRoot.GetComponent<CanvasGroup>();
            if (cardRootGroup == null)
            {
                cardRootGroup = cardRoot.gameObject.AddComponent<CanvasGroup>();
            }
        }

        if (confirmButtonGroup == null && confirmButton != null)
        {
            confirmButtonGroup = confirmButton.GetComponent<CanvasGroup>();
            if (confirmButtonGroup == null)
            {
                confirmButtonGroup = confirmButton.gameObject.AddComponent<CanvasGroup>();
            }
        }

        if (buffCountText == null && cardButton != null)
        {
            Transform count = cardButton.Find("CardNum");
            buffCountText = count != null ? count.GetComponent<Text>() : null;
        }

        _cardDeck.Configure(this, transform, cardRoot, cardButton, ref cards, ref cardItems, cardDeckSettings);
        _candidateProvider ??= new BlessingCandidateProvider(_selectionRuntime);
    }

    private void BindButtons()
    {
        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveListener(ConfirmCurrentCard);
            confirmButton.onClick.AddListener(ConfirmCurrentCard);
        }

        if (cardButtonClickArea != null)
        {
            cardButtonClickArea.onClick.RemoveListener(OpenOwnedBuffPanel);
            cardButtonClickArea.onClick.AddListener(OpenOwnedBuffPanel);
        }
    }

    private void SetClosedImmediate()
    {
        KillSequence();
        _cardDeck.ClearSelectedCard(true);
        _cardDeck.SetClosedImmediate();
        _isAnimating = false;
        _isOpened = false;
        SetSelectionVisible(false);
    }

    private void SetSelectionVisible(bool visible)
    {
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 1f;
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;
        }

        SetConfirmButtonVisible(visible);
        SetCardRootVisible(visible);
    }

    private void SetCardRootVisible(bool visible)
    {
        if (cardRootGroup == null)
        {
            return;
        }

        // 卡片区单独隐藏 不影响右侧祝福入口按钮
        cardRootGroup.alpha = visible ? 1f : 0f;
        cardRootGroup.interactable = visible;
        cardRootGroup.blocksRaycasts = visible;
    }

    private void SetConfirmButtonVisible(bool visible)
    {
        if (confirmButtonGroup == null)
        {
            return;
        }

        confirmButtonGroup.alpha = visible ? 1f : 0f;
        confirmButtonGroup.interactable = visible;
        confirmButtonGroup.blocksRaycasts = visible;

        if (!visible)
        {
            _cardDeck.SetCardsRaycast(false);
        }
    }

    private void PauseGameForSelection()
    {
        if (_hasPausedGame)
        {
            return;
        }

        _timeScaleBeforeSelection = Time.timeScale;
        Time.timeScale = 0f;
        _hasPausedGame = true;
    }

    private void RestoreGameTime()
    {
        if (!_hasPausedGame)
        {
            return;
        }

        Time.timeScale = _timeScaleBeforeSelection;
        _hasPausedGame = false;
    }

    private void RefreshBuffCountText(bool playTween)
    {
        if (buffCountText == null)
        {
            return;
        }

        buffCountText.text = _selectionRuntime.OwnedBuffCount.ToString();
        if (!playTween)
        {
            return;
        }

        _buffCountTween?.Kill();
        Transform countTransform = buffCountText.transform;
        countTransform.localScale = Vector3.one;
        _buffCountTween = countTransform.DOPunchScale(Vector3.one * buffCountPunchScale, 0.2f, 6, 0.8f).SetUpdate(true);
    }

    private void ResetRuntimeForNewCombat()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.name != CombatSceneName || activeScene.handle == _combatSceneHandle)
        {
            return;
        }

        _combatSceneHandle = activeScene.handle;
        _selectionRuntime.Reset();
        RefreshBuffCountText(false);
    }

    private RectTransform FindChildRect(string childName)
    {
        Transform child = transform.Find(childName);
        return child != null ? child as RectTransform : null;
    }

    private Button FindButton(string childName)
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null && buttons[i].name == childName)
            {
                return buttons[i];
            }
        }

        return null;
    }

    private void KillSequence()
    {
        _sequence?.Kill();
        _sequence = null;
        _buffCountTween?.Kill();
        _buffCountTween = null;
    }
}
