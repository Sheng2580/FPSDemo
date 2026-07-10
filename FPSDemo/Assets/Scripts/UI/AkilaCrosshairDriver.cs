using Akila.FPSFramework;
using UnityEngine;
using UnityEngine.UI;
using Weapon;
using Weapon.Data;

[DisallowMultipleComponent]
public class AkilaCrosshairDriver : MonoBehaviour
{
    [Header("准星引用")]
    [SerializeField] private RectTransform crosshairRoot;
    [SerializeField] private WeaponController weaponController;

    [Header("Akila 准星兜底参数")]
    [SerializeField] private float fallbackCrosshairSize = WeaponConfig.DefaultCrosshairSize;
    [SerializeField] private float sizeMatchingTime = 0.1f;
    [SerializeField] private Color crosshairColor = Color.white;

    [Header("动态扩散兜底参数")]
    [SerializeField] private float fallbackMinSprayAmount = WeaponConfig.DefaultCrosshairMinSprayAmount;
    [SerializeField] private float fallbackSpreadToAmountScale = WeaponConfig.DefaultCrosshairSpreadScale;
    [SerializeField] private float fallbackFireKickAmount = WeaponConfig.DefaultCrosshairFireKickAmount;
    [SerializeField] private float fallbackFireKickDecaySpeed = WeaponConfig.DefaultCrosshairFireKickDecaySpeed;

    [Header("线条尺寸")]
    [SerializeField] private Vector2 horizontalLineSize = new Vector2(20f, 4f);
    [SerializeField] private Vector2 verticalLineSize = new Vector2(4f, 20f);
    [SerializeField] private Vector2 centerDotSize = new Vector2(5f, 5f);
    [SerializeField] private bool showCenterDot = true;

    private Crosshair _crosshair;
    private RectTransform _holder;
    private int _lastAmmo = -1;
    private int _lastWeaponId = -1;
    private float _fireKick;
    private bool _isBuilt;

    private void Reset()
    {
        CacheReferences();
    }

    private void Awake()
    {
        EnsureBuilt();
    }

    private void OnEnable()
    {
        EnsureBuilt();
    }

    private void Update()
    {
        if (!_isBuilt || _crosshair == null || _holder == null)
        {
            EnsureBuilt();
        }

        UpdateWeaponState();
        DriveCrosshair();
    }

    public void EnsureBuilt()
    {
        CacheReferences();
        EnsureCrosshairRoot();
        DisableTemporaryImage();
        EnsureAkilaCrosshair();
        EnsureCrosshairVisuals();
        _isBuilt = _crosshair != null && _holder != null;
    }

    private void CacheReferences()
    {
        if (weaponController == null)
        {
            weaponController = FindObjectOfType<WeaponController>();
        }

        if (crosshairRoot == null)
        {
            crosshairRoot = transform.name == "Crosshair"
                ? transform as RectTransform
                : FindChildRecursive(transform, "Crosshair") as RectTransform;
        }
    }

    private void EnsureCrosshairRoot()
    {
        if (crosshairRoot != null)
        {
            crosshairRoot.gameObject.SetActive(true);
            crosshairRoot.SetAsLastSibling();
            return;
        }

        GameObject rootObject = new GameObject("Crosshair", typeof(RectTransform));
        rootObject.transform.SetParent(transform, false);
        crosshairRoot = rootObject.GetComponent<RectTransform>();
        crosshairRoot.anchorMin = new Vector2(0.5f, 0.5f);
        crosshairRoot.anchorMax = new Vector2(0.5f, 0.5f);
        crosshairRoot.pivot = new Vector2(0.5f, 0.5f);
        crosshairRoot.anchoredPosition = Vector2.zero;
        crosshairRoot.sizeDelta = Vector2.zero;
    }

    private void DisableTemporaryImage()
    {
        if (crosshairRoot == null)
        {
            return;
        }

        Image temporaryImage = crosshairRoot.GetComponent<Image>();
        if (temporaryImage == null)
        {
            return;
        }

        // 旧临时 Image 只作为定位占位物 不再参与显示和点击判定
        temporaryImage.enabled = false;
        temporaryImage.raycastTarget = false;
    }

    private void EnsureAkilaCrosshair()
    {
        CanvasGroup canvasGroup = crosshairRoot.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = crosshairRoot.gameObject.AddComponent<CanvasGroup>();
        }

        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        _crosshair = crosshairRoot.GetComponent<Crosshair>();
        if (_crosshair == null)
        {
            _crosshair = crosshairRoot.gameObject.AddComponent<Crosshair>();
        }

        _crosshair.size = GetCurrentCrosshairSize();
        _crosshair.sizeMatchingTime = sizeMatchingTime;
        _crosshair.color = crosshairColor;
        _crosshair.firearm = null;
    }

    private void EnsureCrosshairVisuals()
    {
        _holder = crosshairRoot.Find("AkilaHolder") as RectTransform;
        if (_holder == null)
        {
            _holder = CreateRect("AkilaHolder", crosshairRoot);
        }

        SetCenterRect(_holder, Vector2.zero);
        _holder.localScale = Vector3.one;

        CreateOrUpdateImage("Center", _holder, new Vector2(0.5f, 0.5f), Vector2.zero, centerDotSize, showCenterDot);
        CreateOrUpdateImage("Top", _holder, new Vector2(0.5f, 1f), new Vector2(0f, 10f), verticalLineSize, true);
        CreateOrUpdateImage("Bottom", _holder, new Vector2(0.5f, 0f), new Vector2(0f, -10f), verticalLineSize, true);
        CreateOrUpdateImage("Left", _holder, new Vector2(0f, 0.5f), new Vector2(-10f, 0f), horizontalLineSize, true);
        CreateOrUpdateImage("Right", _holder, new Vector2(1f, 0.5f), new Vector2(10f, 0f), horizontalLineSize, true);

        _crosshair.crosshairHolder = _holder;
        _crosshair.RefreshImages();
    }

    private void UpdateWeaponState()
    {
        if (weaponController == null)
        {
            weaponController = FindObjectOfType<WeaponController>();
        }

        if (weaponController == null || weaponController.Config == null || weaponController.RuntimeData == null)
        {
            _lastAmmo = -1;
            _lastWeaponId = -1;
            _fireKick = Mathf.MoveTowards(_fireKick, 0f, GetFireKickDecaySpeed() * Time.deltaTime);
            return;
        }

        int weaponId = weaponController.Config.weaponId;
        int currentAmmo = weaponController.RuntimeData.currentAmmoInMagazine;
        if (_lastWeaponId != weaponId)
        {
            _lastWeaponId = weaponId;
            _lastAmmo = currentAmmo;
            _fireKick = 0f;
            return;
        }

        if (_lastAmmo >= 0 && currentAmmo < _lastAmmo)
        {
            // 弹药减少代表真正开火 准星做一次 Akila 扩散
            _fireKick = Mathf.Max(_fireKick, GetFireKickAmount());
        }

        _lastAmmo = currentAmmo;
        _fireKick = Mathf.MoveTowards(_fireKick, 0f, GetFireKickDecaySpeed() * Time.deltaTime);
    }

    private void DriveCrosshair()
    {
        if (_crosshair == null)
        {
            return;
        }

        float aimProgress = weaponController != null ? weaponController.ADSAmount : 0f;
        float sprayAmount = GetCurrentSprayAmount();
        _crosshair.size = GetCurrentCrosshairSize();
        _crosshair.SetManualState(aimProgress, sprayAmount);
    }

    private float GetCurrentCrosshairSize()
    {
        WeaponConfig config = weaponController != null ? weaponController.Config : null;
        return config != null && config.crosshairSize > 0f
            ? config.crosshairSize
            : fallbackCrosshairSize;
    }

    private float GetCurrentSprayAmount()
    {
        WeaponConfig config = weaponController != null ? weaponController.Config : null;
        if (config == null)
        {
            return fallbackMinSprayAmount + _fireKick;
        }

        float minAmount = config.crosshairMinSprayAmount > 0f
            ? config.crosshairMinSprayAmount
            : fallbackMinSprayAmount;
        float spreadScale = config.crosshairSpreadScale > 0f
            ? config.crosshairSpreadScale
            : fallbackSpreadToAmountScale;
        float spreadAmount = Mathf.Max(0f, config.spreadAngle) * spreadScale;
        return Mathf.Max(minAmount, minAmount + spreadAmount + _fireKick);
    }

    private float GetFireKickAmount()
    {
        WeaponConfig config = weaponController != null ? weaponController.Config : null;
        return config != null && config.crosshairFireKickAmount > 0f
            ? config.crosshairFireKickAmount
            : fallbackFireKickAmount;
    }

    private float GetFireKickDecaySpeed()
    {
        WeaponConfig config = weaponController != null ? weaponController.Config : null;
        return config != null && config.crosshairFireKickDecaySpeed > 0f
            ? config.crosshairFireKickDecaySpeed
            : fallbackFireKickDecaySpeed;
    }

    private static RectTransform CreateRect(string objectName, Transform parent)
    {
        GameObject rectObject = new GameObject(objectName, typeof(RectTransform));
        rectObject.transform.SetParent(parent, false);
        return rectObject.GetComponent<RectTransform>();
    }

    private void CreateOrUpdateImage(
        string objectName,
        RectTransform parent,
        Vector2 anchor,
        Vector2 anchoredPosition,
        Vector2 size,
        bool visible)
    {
        RectTransform rect = parent.Find(objectName) as RectTransform;
        if (rect == null)
        {
            rect = CreateRect(objectName, parent);
        }

        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;

        Image image = rect.GetComponent<Image>();
        if (image == null)
        {
            image = rect.gameObject.AddComponent<Image>();
        }

        image.color = crosshairColor;
        image.raycastTarget = false;
        image.enabled = visible;
    }

    private static void SetCenterRect(RectTransform rect, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
    }

    private static Transform FindChildRecursive(Transform parent, string objectName)
    {
        if (parent == null)
        {
            return null;
        }

        foreach (Transform child in parent)
        {
            if (child.name == objectName)
            {
                return child;
            }

            Transform result = FindChildRecursive(child, objectName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }
}
