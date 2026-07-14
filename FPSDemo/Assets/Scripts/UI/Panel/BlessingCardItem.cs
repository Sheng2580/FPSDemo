using System.Collections;
using DG.Tweening;
using Blessing.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 祝福卡片点击和选中表现
/// </summary>
public class BlessingCardItem : MonoBehaviour, IPointerClickHandler, IPointerDownHandler
{
    [Header("卡片引用")]
    [SerializeField] private RectTransform root;
    [SerializeField] private RectTransform visualRoot;
    [SerializeField] private Image colorImage;
    [SerializeField] private Text nameText;
    [SerializeField] private Text descriptionText;
    [SerializeField] private Text valueText;
    [SerializeField] private Text tierText;
    [SerializeField] private Text stackText;
    [SerializeField] private Text categoryText;

    [Header("选中颜色")]
    [SerializeField] private float selectedColorDuration = 0.22f;

    private BlessingSelectCanvas _owner;
    private CanvasGroup _canvasGroup;
    private Graphic[] _graphics;
    private Color _defaultColor = Color.white;
    private BlessingCardViewData _viewData;
    private Tween _selectedColorTween;
    private Tween _selectedScaleTween;
    private Coroutine _restoreSiblingCoroutine;
    private Vector3 _scaleBeforeSelect = Vector3.one;
    private int _originSiblingIndex;
    private bool _hasScaleBeforeSelect;
    private bool _hasOriginSiblingIndex;
    private bool _hasDefaultColor;
    private bool _pendingRestoreSibling;

    public int Index { get; private set; }
    public RectTransform Root => root;
    public BlessingCardViewData ViewData => _viewData;

    private void Awake()
    {
        CacheReferences();
    }

    private void OnDisable()
    {
        StopRestoreSiblingCoroutine();
        _pendingRestoreSibling = _hasOriginSiblingIndex;
        StopSelectedLoop(true, false);
    }

    private void OnEnable()
    {
        if (_pendingRestoreSibling)
        {
            _restoreSiblingCoroutine = StartCoroutine(RestoreSiblingAfterParentActivation());
        }
    }

    public void Init(BlessingSelectCanvas owner, int index)
    {
        _owner = owner;
        Index = index;
        CacheReferences();
    }

    public void SetData(BlessingCardViewData viewData)
    {
        CacheReferences();
        _viewData = viewData;

        if (nameText != null)
        {
            nameText.text = viewData.DisplayName;
        }

        if (descriptionText != null)
        {
            descriptionText.text = viewData.Description;
        }

        if (valueText != null)
        {
            valueText.text = viewData.ValueText;
        }

        if (tierText != null)
        {
            tierText.text = viewData.TierText;
        }

        if (stackText != null)
        {
            stackText.text = viewData.StackText;
        }

        if (categoryText != null)
        {
            categoryText.text = viewData.CategoryText;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        _owner?.SelectCard(this);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        MusicMgr.Instance?.PlayUISound(MusicMgr.UISelectSound);
        _owner?.SelectCard(this);
    }

    public void SetPose(Vector2 anchoredPosition, float rotationZ, float scale, float alpha)
    {
        CacheReferences();
        KillSelectedScaleTween();

        root.anchoredPosition = anchoredPosition;
        root.localEulerAngles = new Vector3(0f, 0f, rotationZ);
        root.localScale = Vector3.one * scale;
        _canvasGroup.alpha = alpha;
        _hasScaleBeforeSelect = false;
    }

    public void PlaySelectedFocus(float selectedScale)
    {
        CacheReferences();
        StopSelectedLoop(false, false);

        _hasScaleBeforeSelect = true;
        _hasOriginSiblingIndex = true;
        _originSiblingIndex = root.GetSiblingIndex();
        _scaleBeforeSelect = root.localScale;
        root.SetAsLastSibling();
        _selectedScaleTween = root.DOScale(selectedScale, 0.16f).SetEase(Ease.OutBack).SetUpdate(true);
        PlayNextSelectedColor();
    }

    public void StopSelectedLoop(bool resetColor)
    {
        StopSelectedLoop(resetColor, true);
    }

    public void StopSelectedLoop(bool resetColor, bool restoreSibling)
    {
        KillSelectedColorTween();
        KillSelectedScaleTween();

        if (_hasScaleBeforeSelect && root != null)
        {
            root.localScale = _scaleBeforeSelect;
            _hasScaleBeforeSelect = false;
        }

        if (restoreSibling)
        {
            RestoreSiblingIndex();
        }

        if (resetColor)
        {
            ResetVisualColor();
        }
    }

    public void RestoreSiblingIndex()
    {
        if (!_hasOriginSiblingIndex || root == null || root.parent == null)
        {
            return;
        }

        int maxIndex = root.parent.childCount - 1;
        root.SetSiblingIndex(Mathf.Clamp(_originSiblingIndex, 0, maxIndex));
        _hasOriginSiblingIndex = false;
        _pendingRestoreSibling = false;
    }

    public Tween PlayConfirmFly(Vector2 targetPosition, float duration)
    {
        CacheReferences();
        KillSelectedScaleTween();

        DG.Tweening.Sequence sequence = DOTween.Sequence().SetUpdate(true);
        sequence.Join(root.DOAnchorPos(targetPosition, duration).SetEase(Ease.InOutCubic));
        sequence.Join(root.DOLocalRotate(Vector3.zero, duration).SetEase(Ease.InOutCubic));
        sequence.Join(root.DOScale(0.22f, duration).SetEase(Ease.InBack));
        sequence.Join(_canvasGroup.DOFade(1f, duration * 0.5f));
        return sequence;
    }

    public Tween PlayReturnToClosed(Vector2 targetPosition, float duration)
    {
        CacheReferences();
        StopSelectedLoop(true);

        DG.Tweening.Sequence sequence = DOTween.Sequence().SetUpdate(true);
        sequence.Join(root.DOAnchorPos(targetPosition, duration).SetEase(Ease.InCubic));
        sequence.Join(root.DOLocalRotate(Vector3.zero, duration).SetEase(Ease.InCubic));
        sequence.Join(root.DOScale(0.82f, duration).SetEase(Ease.InCubic));
        sequence.Join(_canvasGroup.DOFade(1f, duration));
        return sequence;
    }

    public Tween FadeTo(float alpha, float duration)
    {
        CacheReferences();
        return _canvasGroup.DOFade(alpha, duration).SetUpdate(true);
    }

    public void ResetVisualColor()
    {
        CacheReferences();
        if (colorImage != null)
        {
            colorImage.color = _defaultColor;
        }
    }

    public void SetInteractable(bool interactable)
    {
        CacheReferences();
        _canvasGroup.blocksRaycasts = interactable;
        _canvasGroup.interactable = interactable;

        for (int i = 0; i < _graphics.Length; i++)
        {
            if (_graphics[i] == null)
            {
                continue;
            }

            _graphics[i].raycastTarget = interactable;
        }
    }

    private void CacheReferences()
    {
        if (root == null)
        {
            root = transform as RectTransform;
        }

        if (visualRoot == null)
        {
            Transform card = transform.Find("Card");
            visualRoot = card != null ? card as RectTransform : root;
        }

        if (colorImage == null)
        {
            colorImage = GetComponent<Image>();
            if (colorImage == null)
            {
                colorImage = visualRoot != null ? visualRoot.GetComponent<Image>() : null;
            }
        }

        nameText ??= FindText("Name");
        descriptionText ??= FindText("Describe");
        valueText ??= FindText("Value");
        tierText ??= FindText("Tier");
        stackText ??= FindText("Stack");
        categoryText ??= FindText("Category");

        if (_canvasGroup == null)
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        if (_graphics == null || _graphics.Length == 0)
        {
            _graphics = GetComponentsInChildren<Graphic>(true);
        }

        if (colorImage != null && !_hasDefaultColor)
        {
            _defaultColor = colorImage.color;
            _hasDefaultColor = true;
        }
    }

    private void PlayNextSelectedColor()
    {
        if (colorImage == null)
        {
            return;
        }

        Color targetColor = Random.ColorHSV(0f, 0.12f, 0.78f, 1f, 0.95f, 1f);
        targetColor.a = _defaultColor.a;
        _selectedColorTween = colorImage.DOColor(targetColor, selectedColorDuration)
            .SetEase(Ease.InOutSine)
            .SetUpdate(true)
            .OnComplete(PlayNextSelectedColor);
    }

    private void KillSelectedColorTween()
    {
        _selectedColorTween?.Kill();
        _selectedColorTween = null;
    }

    private void KillSelectedScaleTween()
    {
        _selectedScaleTween?.Kill();
        _selectedScaleTween = null;
    }

    private IEnumerator RestoreSiblingAfterParentActivation()
    {
        yield return null;

        _restoreSiblingCoroutine = null;
        if (!_pendingRestoreSibling)
        {
            yield break;
        }

        RestoreSiblingIndex();
    }

    private void StopRestoreSiblingCoroutine()
    {
        if (_restoreSiblingCoroutine == null)
        {
            return;
        }

        StopCoroutine(_restoreSiblingCoroutine);
        _restoreSiblingCoroutine = null;
    }

    private Text FindText(string childName)
    {
        Text[] texts = GetComponentsInChildren<Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            Text text = texts[i];
            if (text != null && text.name == childName)
            {
                return text;
            }
        }

        return null;
    }
}
