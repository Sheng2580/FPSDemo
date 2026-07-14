using System;
using DG.Tweening;
using PlayerData;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 开始界面的单条存档显示
/// </summary>
[DisallowMultipleComponent]
public sealed class StartSaveFileItem : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Text legacyText;
    [SerializeField] private TMP_Text tmpText;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform itemRect;

    private PlayerSaveSlotSummary _summary;
    private UnityAction<StartSaveFileItem, PlayerSaveSlotSummary> _onClicked;
    private DG.Tweening.Sequence _sequence;

    private void Awake()
    {
        ResolveMissingReferences();
        BindButton();
    }

    private void OnDisable()
    {
        KillTweens();
    }

    public void Bind(
        PlayerSaveSlotSummary summary,
        UnityAction<StartSaveFileItem, PlayerSaveSlotSummary> onClicked)
    {
        ResolveMissingReferences();
        _summary = summary;
        _onClicked = onClicked;
        SetLabel(BuildLabel(summary));
        SetInteractable(true);

        if (itemRect != null)
        {
            itemRect.localScale = Vector3.one;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
        }
    }

    public void PlayEnter(float delay)
    {
        KillTweens();
        if (itemRect != null)
        {
            itemRect.localScale = Vector3.one * 0.9f;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }

        _sequence = DOTween.Sequence().SetUpdate(true);
        if (delay > 0f)
        {
            _sequence.AppendInterval(delay);
        }

        if (itemRect != null)
        {
            _sequence.Join(itemRect.DOScale(1f, 0.22f).SetEase(Ease.OutBack));
        }

        if (canvasGroup != null)
        {
            _sequence.Join(canvasGroup.DOFade(1f, 0.16f).SetEase(Ease.OutQuad));
        }
    }

    public void PlaySelected(UnityAction onComplete)
    {
        SetInteractable(false);
        KillTweens();
        _sequence = DOTween.Sequence().SetUpdate(true);
        if (itemRect != null)
        {
            _sequence.Append(itemRect.DOScale(1.07f, 0.14f).SetEase(Ease.OutBack));
            _sequence.Append(itemRect.DOScale(1.02f, 0.08f).SetEase(Ease.OutQuad));
        }
        else
        {
            _sequence.AppendInterval(0.22f);
        }

        _sequence.OnComplete(() => onComplete?.Invoke());
    }

    public void SetInteractable(bool interactable)
    {
        if (button != null)
        {
            button.interactable = interactable;
        }
    }

    public void KillTweens()
    {
        _sequence?.Kill();
        _sequence = null;
        itemRect?.DOKill();
        canvasGroup?.DOKill();
    }

    private void BindButton()
    {
        if (button == null)
        {
            return;
        }

        UIManager.Instance?.RegisterButtonAudio(button, MusicMgr.UISelectSound);
        button.onClick.RemoveListener(HandleClicked);
        button.onClick.AddListener(HandleClicked);
    }

    private void HandleClicked()
    {
        if (_summary == null)
        {
            return;
        }

        _onClicked?.Invoke(this, _summary);
    }

    private void ResolveMissingReferences()
    {
        button = button != null ? button : GetComponent<Button>();
        legacyText = legacyText != null ? legacyText : GetComponentInChildren<Text>(true);
        tmpText = tmpText != null ? tmpText : GetComponentInChildren<TMP_Text>(true);
        canvasGroup = canvasGroup != null ? canvasGroup : GetComponent<CanvasGroup>();
        itemRect = itemRect != null ? itemRect : transform as RectTransform;
    }

    private void SetLabel(string value)
    {
        if (tmpText != null)
        {
            tmpText.text = value;
        }

        if (legacyText != null)
        {
            legacyText.text = value;
        }
    }

    private static string BuildLabel(PlayerSaveSlotSummary summary)
    {
        if (summary == null)
        {
            return "无效存档";
        }

        string legacyLabel = summary.IsLegacy ? "  旧存档" : string.Empty;
        return $"{summary.SavedAt:yyyy-MM-dd HH:mm:ss}{legacyLabel}\n"
               + $"金币 {summary.Gold}  最长 {FormatTime(summary.BestSurvivalTime)}  击杀 {summary.BestKillCount}\n"
               + $"玩家等级 LV.{summary.PlayerUpgradeLevel}";
    }

    private static string FormatTime(float seconds)
    {
        int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(seconds));
        int minutes = totalSeconds / 60;
        int remainingSeconds = totalSeconds % 60;
        return $"{minutes:00}:{remainingSeconds:00}";
    }
}
