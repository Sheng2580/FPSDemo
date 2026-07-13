using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Weapon.Data;

[UICanvas(UILoadType.AssetBundle, UILayer.Touch)]
public class HpAndWeaponCanvas : BaseCanvas
{
    private const string WeaponItemBundleName = "uiitem";
    private const string WeaponItemAssetName = "WeaponItem";

    [Header("武器")]
    [SerializeField] private RectTransform weaponCentre;

    [Header("血量")]
    [SerializeField] private Slider hpSlider;
    [SerializeField] private Text hpText;

    private readonly List<WeaponItem> _items = new List<WeaponItem>();
    private readonly List<GameObject> _runtimeItemObjects = new List<GameObject>();
    private PlayerController _player;
    private PlayerInventory _inventory;
    private bool _isBuildingItems;
    private Coroutine _buildCoroutine;

    public override bool NeedRaycaster => true;

    public override void Awake()
    {
        base.Awake();
        CacheReferences();
        DisablePassiveRaycasts();
    }

    private void OnEnable()
    {
        CacheReferences();
        DisablePassiveRaycasts();
        EventCenter.Instance.AddEventListener<PlayerWeaponChangedEventData>(GameEvent.PlayerWeaponChanged, OnPlayerWeaponChanged);
        EventCenter.Instance.AddEventListener<PlayerWeaponAmmoChangedEventData>(GameEvent.PlayerWeaponAmmoChanged, OnPlayerWeaponAmmoChanged);
        EventCenter.Instance.AddEventListener<PlayerHealthChangedEventData>(GameEvent.PlayerHealthChanged, OnPlayerHealthChanged);
        RefreshHpFromRuntime();
    }

    private void OnDisable()
    {
        EventCenter.Instance.RemoveEventListener<PlayerWeaponChangedEventData>(GameEvent.PlayerWeaponChanged, OnPlayerWeaponChanged);
        EventCenter.Instance.RemoveEventListener<PlayerWeaponAmmoChangedEventData>(GameEvent.PlayerWeaponAmmoChanged, OnPlayerWeaponAmmoChanged);
        EventCenter.Instance.RemoveEventListener<PlayerHealthChangedEventData>(GameEvent.PlayerHealthChanged, OnPlayerHealthChanged);
    }

    public override void Show()
    {
        base.Show();
        CacheReferences();
        DisablePassiveRaycasts();
        RefreshHpFromRuntime();
    }

    protected override void Reset()
    {
        base.Reset();
        CacheReferences();
    }

    public void RebuildForInventory(PlayerInventory inventory)
    {
        if (inventory == null)
        {
            return;
        }

        _inventory = inventory;
        _player = inventory.GetComponent<PlayerController>();
        StartRebuildWeaponItems();
        RefreshHpFromRuntime();
    }

    private void StartRebuildWeaponItems()
    {
        if (_buildCoroutine != null)
        {
            StopCoroutine(_buildCoroutine);
            _buildCoroutine = null;
        }

        _isBuildingItems = false;
        _buildCoroutine = StartCoroutine(BuildWeaponItemsRoutine());
    }

    private IEnumerator BuildWeaponItemsRoutine()
    {
        _isBuildingItems = true;
        CacheReferences();
        ClearRuntimeItems();
        HidePreviewItems();

        if (weaponCentre == null)
        {
            Debug.LogError("[HpAndWeaponCanvas] 找不到 WeaponCentre 无法生成武器 Item", this);
            FinishBuild();
            yield break;
        }

        if (_inventory == null || _inventory.CarriedWeapons == null || _inventory.CarriedWeapons.Count <= 0)
        {
            Debug.LogWarning("[HpAndWeaponCanvas] 玩家背包没有武器数据 不生成武器 Item", this);
            FinishBuild();
            yield break;
        }

        bool templateLoaded = false;
        GameObject templatePrefab = null;
        LoadWeaponItemTemplate(loadedPrefab =>
        {
            templatePrefab = loadedPrefab;
            templateLoaded = true;
        });

        yield return new WaitUntil(() => templateLoaded);

        if (templatePrefab == null)
        {
            Debug.LogError("[HpAndWeaponCanvas] 武器 Item 模板加载失败", this);
            FinishBuild();
            yield break;
        }

        int weaponCount = _inventory.CarriedWeapons.Count;
        for (int i = 0; i < weaponCount; i++)
        {
            GameObject itemObject = Instantiate(templatePrefab, weaponCentre, false);
            itemObject.name = $"{WeaponItemAssetName}_{i}";
            itemObject.SetActive(true);
            _runtimeItemObjects.Add(itemObject);

            WeaponItem item = itemObject.GetComponent<WeaponItem>();
            if (item == null)
            {
                Debug.LogError("[HpAndWeaponCanvas] WeaponItem 预制体缺少 WeaponItem 脚本", itemObject);
                continue;
            }

            _items.Add(item);
            BindWeaponItem(i, item);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(weaponCentre);
        RefreshWeaponSelection(_inventory.CurrentWeaponIndex, true);
        RefreshAllAmmoFromRuntime();
        FinishBuild();
    }

    private void LoadWeaponItemTemplate(Action<GameObject> onLoaded)
    {
        if (ABManager.Instance == null)
        {
            Debug.LogError("[HpAndWeaponCanvas] ABManager 不存在 无法加载 WeaponItem", this);
            onLoaded?.Invoke(null);
            return;
        }

        ABManager.Instance.LoadAssetAsync<GameObject>(
            WeaponItemBundleName,
            WeaponItemAssetName,
            loadedPrefab => onLoaded?.Invoke(loadedPrefab));
    }

    private void FinishBuild()
    {
        _isBuildingItems = false;
        _buildCoroutine = null;
    }

    private void BindWeaponItem(int index, WeaponItem item)
    {
        if (item == null || _inventory == null || index < 0 || index >= _inventory.CarriedWeapons.Count)
        {
            return;
        }

        CarriedWeaponSlot slot = _inventory.CarriedWeapons[index];
        slot?.EnsureRuntimeReady();
        WeaponConfig config = slot?.RuntimeConfig;
        WeaponRuntimeData runtimeData = slot?.RuntimeData;
        item.Bind(
            index,
            slot?.DisplayName ?? config?.weaponName ?? "武器",
            runtimeData != null ? runtimeData.currentAmmoInMagazine : 0,
            runtimeData != null ? runtimeData.currentReserveAmmo : 0,
            index == _inventory.CurrentWeaponIndex,
            OnWeaponItemClicked);
    }

    private void OnWeaponItemClicked(int weaponIndex)
    {
        EventCenter.Instance.EventTrigger(GameEvent.MobileWeaponSlotPressed, new MobileWeaponSlotPressedEventData(weaponIndex));
    }

    private void OnPlayerWeaponChanged(PlayerWeaponChangedEventData eventData)
    {
        CacheRuntimeReferences();
        if (_items.Count <= 0)
        {
            if (_inventory != null && !_isBuildingItems)
            {
                StartRebuildWeaponItems();
            }

            return;
        }

        RefreshWeaponSelection(eventData.currentIndex, false);
        RefreshWeaponAmmo(eventData.currentIndex, eventData.currentAmmo, eventData.reserveAmmo);
    }

    private void OnPlayerWeaponAmmoChanged(PlayerWeaponAmmoChangedEventData eventData)
    {
        RefreshWeaponAmmo(eventData.weaponIndex, eventData.currentAmmo, eventData.reserveAmmo);
    }

    private void OnPlayerHealthChanged(PlayerHealthChangedEventData eventData)
    {
        RefreshHp(eventData.currentHp, eventData.maxHp);
    }

    private void RefreshWeaponSelection(int currentIndex, bool immediate)
    {
        for (int i = 0; i < _items.Count; i++)
        {
            WeaponItem item = _items[i];
            if (item != null)
            {
                item.SetSelected(item.WeaponIndex == currentIndex, immediate);
            }
        }
    }

    private void RefreshAllAmmoFromRuntime()
    {
        if (_inventory == null || _inventory.CarriedWeapons == null)
        {
            return;
        }

        for (int i = 0; i < _items.Count && i < _inventory.CarriedWeapons.Count; i++)
        {
            CarriedWeaponSlot slot = _inventory.CarriedWeapons[i];
            slot?.EnsureRuntimeReady();
            WeaponRuntimeData runtimeData = slot?.RuntimeData;
            if (runtimeData != null)
            {
                RefreshWeaponAmmo(i, runtimeData.currentAmmoInMagazine, runtimeData.currentReserveAmmo);
            }
        }
    }

    private void RefreshWeaponAmmo(int weaponIndex, int currentAmmo, int reserveAmmo)
    {
        for (int i = 0; i < _items.Count; i++)
        {
            WeaponItem item = _items[i];
            if (item != null && item.WeaponIndex == weaponIndex)
            {
                item.RefreshAmmo(currentAmmo, reserveAmmo);
                return;
            }
        }
    }

    private void RefreshHpFromRuntime()
    {
        CacheRuntimeReferences();
        if (_player == null || _player.Stats == null || _player.Stats.RuntimeData == null)
        {
            return;
        }

        RefreshHp(_player.Stats.RuntimeData.currentHp, _player.Stats.RuntimeData.maxHp);
    }

    private void RefreshHp(int currentHp, int maxHp)
    {
        int safeMaxHp = Mathf.Max(1, maxHp);
        int safeCurrentHp = Mathf.Clamp(currentHp, 0, safeMaxHp);
        if (hpSlider != null)
        {
            hpSlider.minValue = 0f;
            hpSlider.maxValue = safeMaxHp;
            hpSlider.value = safeCurrentHp;
        }

        if (hpText != null)
        {
            hpText.text = $"{safeCurrentHp}/{safeMaxHp}";
        }
    }

    private void ClearRuntimeItems()
    {
        for (int i = 0; i < _runtimeItemObjects.Count; i++)
        {
            if (_runtimeItemObjects[i] != null)
            {
                Destroy(_runtimeItemObjects[i]);
            }
        }

        _runtimeItemObjects.Clear();
        _items.Clear();
    }

    private void HidePreviewItems()
    {
        if (weaponCentre == null)
        {
            return;
        }

        for (int i = weaponCentre.childCount - 1; i >= 0; i--)
        {
            Transform child = weaponCentre.GetChild(i);
            if (child != null && child.GetComponent<WeaponItem>() != null)
            {
                child.gameObject.SetActive(false);
            }
        }
    }

    private void CacheReferences()
    {
        if (weaponCentre == null)
        {
            weaponCentre = FindChildRect("WeaponCentre");
        }

        if (hpSlider == null)
        {
            hpSlider = GetComponentInChildren<Slider>(true);
        }

        if (hpText == null)
        {
            Text[] texts = GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null && texts[i].name == "Text (Legacy)")
                {
                    hpText = texts[i];
                    break;
                }
            }
        }

        CacheRuntimeReferences();
    }

    private void CacheRuntimeReferences()
    {
        if (_player == null)
        {
            _player = FindObjectOfType<PlayerController>();
        }

        if (_inventory == null)
        {
            _inventory = _player != null ? _player.Inventory : FindObjectOfType<PlayerInventory>();
        }
    }

    private RectTransform FindChildRect(string childName)
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child != null && child.name == childName)
            {
                return child as RectTransform;
            }
        }

        return null;
    }

    private void DisablePassiveRaycasts()
    {
        Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];
            if (graphic == null || graphic.GetComponentInParent<WeaponItem>() != null)
            {
                continue;
            }

            graphic.raycastTarget = false;
        }
    }
}
