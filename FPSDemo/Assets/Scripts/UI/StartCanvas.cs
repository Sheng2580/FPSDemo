using System.Collections.Generic;
using DG.Tweening;
using PlayerData;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 开始场景入口
/// 负责选择内存会话并进入大厅
/// </summary>
[DisallowMultipleComponent]
public sealed class StartCanvas : MonoBehaviour
{
    private const string DefaultHallSceneName = "Hall";
    private const string FileItemResourcePath = "UI/FileItem";

    [Header("场景跳转")]
    [SerializeField] private string hallSceneName = DefaultHallSceneName;

    [Header("主界面")]
    [SerializeField] private CanvasGroup rootCanvasGroup;
    [SerializeField] private RectTransform titleRect;
    [SerializeField] private CanvasGroup titleCanvasGroup;
    [SerializeField] private GameObject mainButtonPanel;
    [SerializeField] private CanvasGroup mainButtonCanvasGroup;
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button continueGameButton;
    [SerializeField] private Button readFileButton;
    [SerializeField] private Button quitButton;

    [Header("存档列表")]
    [SerializeField] private GameObject filePanel;
    [SerializeField] private RectTransform filePanelRect;
    [SerializeField] private CanvasGroup filePanelCanvasGroup;
    [SerializeField] private RectTransform fileContent;
    [SerializeField] private StartSaveFileItem fileItemPrefab;
    [SerializeField] private GameObject emptyState;
    [SerializeField] private Button returnButton;

    [Header("动画")]
    [SerializeField] private float fadeDuration = 0.24f;
    [SerializeField] private float buttonInterval = 0.07f;

    private readonly List<StartSaveFileItem> _spawnedItems = new List<StartSaveFileItem>();
    private DG.Tweening.Sequence _filePanelSequence;
    private DG.Tweening.Sequence _transitionSequence;
    private bool _filePanelOpen;
    private bool _isTransitioning;

    private void Awake()
    {
        ResolveMissingReferences();
        BindButtons();
        SetFilePanelImmediate(false);
        SetRootVisible();
        RefreshSaveAvailability();
    }

    private void Start()
    {
        SetMainInput(true);
    }

    private void OnEnable()
    {
        SetRootVisible();
        if (continueGameButton != null)
        {
            RefreshSaveAvailability();
        }
    }

    private void OnDisable()
    {
        KillTweens();
    }

    public void StartNewGame()
    {
        if (_isTransitioning)
        {
            return;
        }

        MusicMgr.Instance?.PlayUISound(MusicMgr.UIConfirmSound);
        PlayerProgressSaveService.BeginNewSession();
        PlayButtonPunch(startGameButton);
        BeginHallTransition();
    }

    public void ContinueGame()
    {
        if (_isTransitioning)
        {
            return;
        }

        if (!PlayerProgressSaveService.TryContinueLatestSession(out PlayerSaveData _))
        {
            ShowEmptySaveList();
            return;
        }

        MusicMgr.Instance?.PlayUISound(MusicMgr.UIConfirmSound);
        PlayButtonPunch(continueGameButton);
        BeginHallTransition();
    }

    public void ToggleSaveList()
    {
        if (_isTransitioning)
        {
            return;
        }

        if (_filePanelOpen)
        {
            CloseSaveList();
            return;
        }

        IReadOnlyList<PlayerSaveSlotSummary> summaries = PlayerProgressSaveService.GetSaveSlotSummaries();
        if (summaries.Count <= 0)
        {
            ShowEmptySaveList();
            return;
        }

        MusicMgr.Instance?.PlayUISound(MusicMgr.UISelectSound);
        PlayButtonPunch(readFileButton);
        OpenSaveList(summaries);
    }

    public void QuitGame()
    {
        if (_isTransitioning)
        {
            return;
        }

        _isTransitioning = true;
        SetMainInput(false);
        MusicMgr.Instance?.PlayUISound(MusicMgr.UICloseSound);
        PlayButtonPunch(quitButton);

        _transitionSequence?.Kill();
        _transitionSequence = DOTween.Sequence().SetUpdate(true);
        if (rootCanvasGroup != null)
        {
            _transitionSequence.Append(rootCanvasGroup.DOFade(0f, fadeDuration).SetEase(Ease.InQuad));
        }

        _transitionSequence.OnComplete(() =>
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        });
    }

    private void OpenSaveList(IReadOnlyList<PlayerSaveSlotSummary> summaries)
    {
        RebuildSaveItems(summaries);

        if (filePanel == null)
        {
            return;
        }

        _filePanelOpen = true;
        filePanel.SetActive(true);
        _filePanelSequence?.Kill();

        if (filePanelCanvasGroup != null)
        {
            filePanelCanvasGroup.DOKill();
            filePanelCanvasGroup.alpha = 0f;
            filePanelCanvasGroup.interactable = false;
            filePanelCanvasGroup.blocksRaycasts = false;
        }

        if (mainButtonCanvasGroup != null)
        {
            mainButtonCanvasGroup.DOKill();
            mainButtonCanvasGroup.interactable = false;
            mainButtonCanvasGroup.blocksRaycasts = false;
        }

        _filePanelSequence = DOTween.Sequence().SetUpdate(true);
        if (mainButtonCanvasGroup != null)
        {
            _filePanelSequence.Append(
                mainButtonCanvasGroup.DOFade(0f, fadeDuration * 0.55f).SetEase(Ease.InQuad));
        }

        _filePanelSequence.AppendCallback(() =>
        {
            if (mainButtonPanel != null)
            {
                mainButtonPanel.SetActive(false);
            }
        });

        if (filePanelCanvasGroup != null)
        {
            _filePanelSequence.Append(
                filePanelCanvasGroup.DOFade(1f, fadeDuration * 0.75f).SetEase(Ease.OutQuad));
        }

        _filePanelSequence.OnComplete(() => SetFilePanelInput(true));
    }

    private void CloseSaveList()
    {
        if (filePanel == null)
        {
            return;
        }

        MusicMgr.Instance?.PlayUISound(MusicMgr.UICloseSound);
        _filePanelOpen = false;
        _filePanelSequence?.Kill();
        SetFilePanelInput(false);

        if (mainButtonPanel != null)
        {
            mainButtonPanel.SetActive(true);
        }

        if (mainButtonCanvasGroup != null)
        {
            mainButtonCanvasGroup.alpha = 0f;
            mainButtonCanvasGroup.interactable = false;
            mainButtonCanvasGroup.blocksRaycasts = false;
        }

        _filePanelSequence = DOTween.Sequence().SetUpdate(true);
        if (filePanelCanvasGroup != null)
        {
            _filePanelSequence.Append(
                filePanelCanvasGroup.DOFade(0f, fadeDuration * 0.55f).SetEase(Ease.InQuad));
        }

        _filePanelSequence.AppendCallback(() =>
        {
            if (filePanel != null)
            {
                filePanel.SetActive(false);
            }
        });

        if (mainButtonCanvasGroup != null)
        {
            _filePanelSequence.Append(
                mainButtonCanvasGroup.DOFade(1f, fadeDuration * 0.75f).SetEase(Ease.OutQuad));
        }

        _filePanelSequence.OnComplete(() => SetMainInput(true));
    }

    private void ShowEmptySaveList()
    {
        PlayButtonPunch(readFileButton != null ? readFileButton : continueGameButton);
        HallTipNotifier.Show(
            "暂无存档",
            "请先开始新游戏并在大厅保存",
            "ammo");
    }

    private void RebuildSaveItems(IReadOnlyList<PlayerSaveSlotSummary> summaries)
    {
        ClearSpawnedItems();

        int count = summaries != null ? summaries.Count : 0;
        if (emptyState != null)
        {
            emptyState.SetActive(count == 0);
        }

        if (count == 0 || fileContent == null || fileItemPrefab == null)
        {
            return;
        }

        for (int i = 0; i < count; i++)
        {
            PlayerSaveSlotSummary summary = summaries[i];
            StartSaveFileItem item = Instantiate(fileItemPrefab, fileContent, false);
            item.name = $"FileItem_{i + 1}";
            item.Bind(summary, OnSaveItemClicked);
            item.PlayEnter(i * buttonInterval);
            _spawnedItems.Add(item);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(fileContent);
    }

    private void OnSaveItemClicked(StartSaveFileItem selectedItem, PlayerSaveSlotSummary summary)
    {
        if (_isTransitioning || selectedItem == null || summary == null)
        {
            return;
        }

        SetSaveItemsInteractable(false);
        selectedItem.PlaySelected(() =>
        {
            if (PlayerProgressSaveService.TryLoadSession(summary, out PlayerSaveData _))
            {
                BeginHallTransition();
                return;
            }

            RefreshSaveAvailability();
            RebuildSaveItems(PlayerProgressSaveService.GetSaveSlotSummaries());
            SetSaveItemsInteractable(true);
        });
    }

    private void BeginHallTransition()
    {
        if (_isTransitioning)
        {
            return;
        }

        _isTransitioning = true;
        SetMainInput(false);
        SetSaveItemsInteractable(false);
        _transitionSequence?.Kill();
        _transitionSequence = DOTween.Sequence().SetUpdate(true);

        if (rootCanvasGroup != null)
        {
            _transitionSequence.Append(rootCanvasGroup.DOFade(0f, fadeDuration).SetEase(Ease.InQuad));
        }
        else
        {
            _transitionSequence.AppendInterval(fadeDuration);
        }

        _transitionSequence.OnComplete(() =>
        {
            string targetScene = string.IsNullOrWhiteSpace(hallSceneName)
                ? DefaultHallSceneName
                : hallSceneName;
            LoadSceneCanvas.LoadSceneWithTransition(targetScene);
        });
    }

    private void SetRootVisible()
    {
        if (rootCanvasGroup != null)
        {
            rootCanvasGroup.DOKill();
            rootCanvasGroup.alpha = 1f;
            rootCanvasGroup.interactable = true;
            rootCanvasGroup.blocksRaycasts = true;
        }
    }

    private void RefreshSaveAvailability()
    {
        if (continueGameButton != null)
        {
            // 无存档时仍允许点击 由 ContinueGame 统一弹出提示
            continueGameButton.interactable = !_isTransitioning;
        }
    }

    private void BindButtons()
    {
        BindButton(startGameButton, StartNewGame);
        BindButton(continueGameButton, ContinueGame);
        BindButton(readFileButton, ToggleSaveList);
        BindButton(quitButton, QuitGame);
        BindButton(returnButton, CloseSaveList);
    }

    private static void BindButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private void ResolveMissingReferences()
    {
        rootCanvasGroup = rootCanvasGroup != null ? rootCanvasGroup : GetComponent<CanvasGroup>();

        Transform safeRoot = FindTransformRecursive(transform, "SafeAreaRoot");
        if (safeRoot == null && transform.childCount > 0)
        {
            Transform background = transform.GetChild(0);
            safeRoot = background.childCount > 0 ? background.GetChild(0) : background;
        }

        TMP_Text title = safeRoot != null ? safeRoot.GetComponentInChildren<TMP_Text>(true) : null;
        titleRect = titleRect != null ? titleRect : title != null ? title.rectTransform : null;
        titleCanvasGroup = titleCanvasGroup != null
            ? titleCanvasGroup
            : titleRect != null
                ? titleRect.GetComponent<CanvasGroup>()
                : null;

        mainButtonPanel = mainButtonPanel != null
            ? mainButtonPanel
            : FindGameObjectRecursive(safeRoot, "StartButtonCenter");
        mainButtonCanvasGroup = mainButtonCanvasGroup != null
            ? mainButtonCanvasGroup
            : mainButtonPanel != null
                ? mainButtonPanel.GetComponent<CanvasGroup>()
                : null;

        startGameButton = startGameButton != null
            ? startGameButton
            : FindButtonInNamedRoot(safeRoot, "StartGameButton");
        continueGameButton = continueGameButton != null
            ? continueGameButton
            : FindButtonInNamedRoot(safeRoot, "ContinueGameButton");
        readFileButton = readFileButton != null
            ? readFileButton
            : FindButtonInNamedRoot(safeRoot, "ReadFileButton");
        quitButton = quitButton != null
            ? quitButton
            : FindButtonInNamedRoot(safeRoot, "QuitButton");

        filePanel = filePanel != null ? filePanel : FindGameObjectRecursive(safeRoot, "FilePanel");
        filePanelRect = filePanelRect != null
            ? filePanelRect
            : filePanel != null
                ? filePanel.transform as RectTransform
                : null;
        filePanelCanvasGroup = filePanelCanvasGroup != null
            ? filePanelCanvasGroup
            : filePanel != null
                ? filePanel.GetComponent<CanvasGroup>()
                : null;
        fileContent = fileContent != null
            ? fileContent
            : FindTransformRecursive(filePanel != null ? filePanel.transform : null, "Content") as RectTransform;
        emptyState = emptyState != null
            ? emptyState
            : FindGameObjectRecursive(filePanel != null ? filePanel.transform : null, "EmptyStateText");
        returnButton = returnButton != null
            ? returnButton
            : FindButtonInNamedRoot(filePanel != null ? filePanel.transform : null, "ReturnButton");

        if (fileItemPrefab == null)
        {
            GameObject prefab = Resources.Load<GameObject>(FileItemResourcePath);
            fileItemPrefab = prefab != null ? prefab.GetComponent<StartSaveFileItem>() : null;
        }
    }

    private void SetFilePanelImmediate(bool visible)
    {
        _filePanelOpen = visible;

        if (mainButtonPanel != null)
        {
            mainButtonPanel.SetActive(!visible);
        }

        if (mainButtonCanvasGroup != null)
        {
            mainButtonCanvasGroup.alpha = visible ? 0f : 1f;
            mainButtonCanvasGroup.interactable = !visible;
            mainButtonCanvasGroup.blocksRaycasts = !visible;
        }

        if (filePanelCanvasGroup != null)
        {
            filePanelCanvasGroup.alpha = visible ? 1f : 0f;
            filePanelCanvasGroup.interactable = visible;
            filePanelCanvasGroup.blocksRaycasts = visible;
        }

        if (filePanel != null)
        {
            filePanel.SetActive(visible);
        }
    }

    private void SetFilePanelInput(bool enabled)
    {
        if (filePanelCanvasGroup != null)
        {
            filePanelCanvasGroup.interactable = enabled;
            filePanelCanvasGroup.blocksRaycasts = enabled;
        }

        if (returnButton != null)
        {
            returnButton.interactable = enabled;
        }

        SetSaveItemsInteractable(enabled);
    }

    private void SetMainInput(bool enabled)
    {
        if (rootCanvasGroup != null)
        {
            rootCanvasGroup.interactable = enabled;
            rootCanvasGroup.blocksRaycasts = enabled;
        }

        if (_filePanelOpen)
        {
            SetFilePanelInput(enabled);
            return;
        }

        if (mainButtonCanvasGroup != null)
        {
            mainButtonCanvasGroup.interactable = enabled;
            mainButtonCanvasGroup.blocksRaycasts = enabled;
        }

        if (startGameButton != null)
        {
            startGameButton.interactable = enabled;
        }

        if (readFileButton != null)
        {
            readFileButton.interactable = enabled;
        }

        if (quitButton != null)
        {
            quitButton.interactable = enabled;
        }

        if (continueGameButton != null)
        {
            continueGameButton.interactable = enabled;
        }
    }

    private void SetSaveItemsInteractable(bool interactable)
    {
        for (int i = 0; i < _spawnedItems.Count; i++)
        {
            if (_spawnedItems[i] != null)
            {
                _spawnedItems[i].SetInteractable(interactable);
            }
        }
    }

    private void ClearSpawnedItems()
    {
        for (int i = 0; i < _spawnedItems.Count; i++)
        {
            StartSaveFileItem item = _spawnedItems[i];
            if (item != null)
            {
                item.gameObject.SetActive(false);
                Destroy(item.gameObject);
            }
        }

        _spawnedItems.Clear();
    }

    private static void PlayButtonPunch(Button button)
    {
        TMP_Text label = button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
        if (label == null)
        {
            return;
        }

        Color originalColor = label.color;
        label.DOKill();
        label.DOColor(new Color(1f, 0.68f, 0.08f, originalColor.a), 0.08f)
            .SetLoops(2, LoopType.Yoyo)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                if (label != null)
                {
                    label.color = originalColor;
                }
            });
    }

    private Button[] GetMainButtons()
    {
        return new[] { startGameButton, continueGameButton, readFileButton, quitButton };
    }

    private void KillTweens()
    {
        _filePanelSequence?.Kill();
        _transitionSequence?.Kill();
        titleRect?.DOKill();
        titleCanvasGroup?.DOKill();
        filePanelRect?.DOKill();
        filePanelCanvasGroup?.DOKill();
        mainButtonCanvasGroup?.DOKill();

        Button[] buttons = GetMainButtons();
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null)
            {
                continue;
            }

            buttons[i].GetComponentInChildren<TMP_Text>(true)?.DOKill();
            buttons[i].GetComponent<CanvasGroup>()?.DOKill();
        }

        for (int i = 0; i < _spawnedItems.Count; i++)
        {
            _spawnedItems[i]?.KillTweens();
        }
    }

    private static Button FindButtonInNamedRoot(Transform root, string objectName)
    {
        Transform namedRoot = FindTransformRecursive(root, objectName);
        if (namedRoot == null)
        {
            return null;
        }

        Button rootButton = namedRoot.GetComponent<Button>();
        return rootButton != null ? rootButton : namedRoot.GetComponentInChildren<Button>(true);
    }

    private static GameObject FindGameObjectRecursive(Transform root, string objectName)
    {
        Transform result = FindTransformRecursive(root, objectName);
        return result != null ? result.gameObject : null;
    }

    private static Transform FindTransformRecursive(Transform root, string objectName)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == objectName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform result = FindTransformRecursive(root.GetChild(i), objectName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }
}
