using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class WeaponItem : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private Button button;
    [SerializeField] private Image background;
    [SerializeField] private LayoutElement layoutElement;
    [SerializeField] private Text weaponNameText;
    [SerializeField] private Text currentAmmoText;
    [SerializeField] private Text reserveAmmoText;

    [Header("选中表现")]
    [SerializeField] private Color normalColor = new Color(0.27f, 0.27f, 0.27f, 0.78f);
    [SerializeField] private Color selectedColor = new Color(0.95f, 0.78f, 0.24f, 0.92f);
    [SerializeField] private float normalWidth = 190f;
    [SerializeField] private float selectedWidth = 245f;
    [SerializeField] private float normalHeight = 82f;
    [SerializeField] private float selectedHeight = 96f;
    [SerializeField] private float selectedScale = 1.03f;
    [SerializeField] private float tweenDuration = 0.18f;
    [SerializeField] private Ease tweenEase = Ease.OutQuad;

    private Action<int> _clickHandler;
    private DG.Tweening.Sequence _selectSequence;
    private int _weaponIndex = -1;
    private bool _isSelected;

    public int WeaponIndex => _weaponIndex;

    private void Awake()
    {
        CacheReferences();
        ConfigureRaycastTargets();
    }

    private void OnEnable()
    {
        CacheReferences();
        BindButton();
        ApplySelectedState(_isSelected, true);
    }

    private void OnDisable()
    {
        KillTween();
        if (button != null)
        {
            button.onClick.RemoveListener(OnButtonClicked);
        }
    }

    public void Bind(
        int weaponIndex,
        string weaponName,
        int currentAmmo,
        int reserveAmmo,
        bool selected,
        Action<int> clickHandler)
    {
        CacheReferences();
        _weaponIndex = weaponIndex;
        _clickHandler = clickHandler;
        SetWeaponName(weaponName);
        RefreshAmmo(currentAmmo, reserveAmmo);
        BindButton();
        SetSelected(selected, true);
    }

    public void RefreshAmmo(int currentAmmo, int reserveAmmo)
    {
        if (currentAmmoText != null)
        {
            currentAmmoText.text = Mathf.Max(0, currentAmmo).ToString();
        }

        if (reserveAmmoText != null)
        {
            reserveAmmoText.text = Mathf.Max(0, reserveAmmo).ToString();
        }
    }

    public void SetSelected(bool selected, bool immediate = false)
    {
        if (_isSelected == selected && !immediate)
        {
            return;
        }

        _isSelected = selected;
        ApplySelectedState(selected, immediate);
    }

    private void SetWeaponName(string weaponName)
    {
        if (weaponNameText != null)
        {
            weaponNameText.text = ResolveDisplayName(weaponName);
        }
    }

    private string ResolveDisplayName(string weaponName)
    {
        if (string.IsNullOrWhiteSpace(weaponName))
        {
            return "武器";
        }

        string lowerName = weaponName.ToLowerInvariant();
        if (lowerName.Contains("pistol"))
        {
            return "手枪";
        }

        if (lowerName.Contains("rifle"))
        {
            return "步枪";
        }

        if (lowerName.Contains("shotgun"))
        {
            return "霰弹枪";
        }

        return weaponName.Replace("Default ", string.Empty);
    }

    private void ApplySelectedState(bool selected, bool immediate)
    {
        CacheReferences();
        float targetWidth = selected ? selectedWidth : normalWidth;
        float targetHeight = selected ? selectedHeight : normalHeight;
        Vector3 targetScale = Vector3.one * (selected ? selectedScale : 1f);
        Color targetColor = selected ? selectedColor : normalColor;

        KillTween();
        if (immediate)
        {
            SetLayoutSize(targetWidth, targetHeight);
            transform.localScale = targetScale;
            if (background != null)
            {
                background.color = targetColor;
            }

            return;
        }

        _selectSequence = DOTween.Sequence();
        _selectSequence.SetUpdate(false);
        _selectSequence.Join(DOTween.To(
            () => layoutElement != null ? layoutElement.preferredWidth : targetWidth,
            value => SetLayoutWidth(value),
            targetWidth,
            tweenDuration));
        _selectSequence.Join(DOTween.To(
            () => layoutElement != null ? layoutElement.preferredHeight : targetHeight,
            value => SetLayoutHeight(value),
            targetHeight,
            tweenDuration));
        _selectSequence.Join(transform.DOScale(targetScale, tweenDuration));
        if (background != null)
        {
            _selectSequence.Join(background.DOColor(targetColor, tweenDuration));
        }

        _selectSequence.SetEase(tweenEase);
    }

    private void SetLayoutSize(float width, float height)
    {
        SetLayoutWidth(width);
        SetLayoutHeight(height);
    }

    private void SetLayoutWidth(float width)
    {
        if (layoutElement == null)
        {
            return;
        }

        layoutElement.preferredWidth = Mathf.Max(1f, width);
        RebuildParentLayout();
    }

    private void SetLayoutHeight(float height)
    {
        if (layoutElement == null)
        {
            return;
        }

        layoutElement.preferredHeight = Mathf.Max(1f, height);
        RebuildParentLayout();
    }

    private void RebuildParentLayout()
    {
        RectTransform parentRect = transform.parent as RectTransform;
        if (parentRect != null)
        {
            LayoutRebuilder.MarkLayoutForRebuild(parentRect);
        }
    }

    private void BindButton()
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(OnButtonClicked);
        button.onClick.AddListener(OnButtonClicked);
    }

    private void OnButtonClicked()
    {
        if (_weaponIndex < 0)
        {
            return;
        }

        _clickHandler?.Invoke(_weaponIndex);
    }

    private void CacheReferences()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (background == null)
        {
            background = GetComponent<Image>();
        }

        if (layoutElement == null)
        {
            layoutElement = GetComponent<LayoutElement>();
        }

        if (layoutElement == null)
        {
            layoutElement = gameObject.AddComponent<LayoutElement>();
        }

        if (currentAmmoText == null)
        {
            currentAmmoText = FindText("Bullet");
        }

        if (reserveAmmoText == null)
        {
            reserveAmmoText = FindText("AllBullet");
        }

        if (weaponNameText == null)
        {
            weaponNameText = FindText("ClipBullet");
        }

        RectTransform rectTransform = transform as RectTransform;
        if (rectTransform != null)
        {
            if (normalWidth <= 0f)
            {
                normalWidth = rectTransform.rect.width > 0f ? rectTransform.rect.width : 190f;
            }

            if (normalHeight <= 0f)
            {
                normalHeight = rectTransform.rect.height > 0f ? rectTransform.rect.height : 82f;
            }
        }

        if (selectedWidth <= normalWidth)
        {
            selectedWidth = normalWidth * 1.28f;
        }

        if (selectedHeight <= 0f)
        {
            selectedHeight = normalHeight;
        }
    }

    private Text FindText(string targetName)
    {
        Text[] texts = GetComponentsInChildren<Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            Text text = texts[i];
            if (text != null && text.name == targetName)
            {
                return text;
            }
        }

        return null;
    }

    private void ConfigureRaycastTargets()
    {
        Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] != null)
            {
                graphics[i].raycastTarget = false;
            }
        }

        if (background != null)
        {
            background.raycastTarget = true;
        }
    }

    private void KillTween()
    {
        _selectSequence?.Kill();
        _selectSequence = null;
    }
}
