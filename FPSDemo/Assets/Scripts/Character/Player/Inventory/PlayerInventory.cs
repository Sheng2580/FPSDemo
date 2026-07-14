using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Weapon.Data;

public class PlayerInventory : MonoBehaviour
{
    private const string CombatSceneName = "Combat";

    [Header("武器")]
    [SerializeField] private int startWeaponIndex;
    [SerializeField] private List<CarriedWeaponSlot> carriedWeapons = new List<CarriedWeaponSlot>();

    [Header("本局物品")]
    [SerializeField] private int battleGold;
    [SerializeField] private List<InventoryItemSlot> items = new List<InventoryItemSlot>();

    private int _currentWeaponIndex = -1;
    private bool _initialized;
    private float _battleGoldGainMultiplier = 1f;

    public int CurrentWeaponIndex => _currentWeaponIndex;
    public int WeaponCount => carriedWeapons != null ? carriedWeapons.Count : 0;
    public int BattleGold => battleGold;
    public float BattleGoldGainMultiplier => _battleGoldGainMultiplier;
    public IReadOnlyList<CarriedWeaponSlot> CarriedWeapons => carriedWeapons;
    public IReadOnlyList<InventoryItemSlot> Items => items;
    public CarriedWeaponSlot CurrentWeapon => GetWeaponSlot(_currentWeaponIndex);

    private void Awake()
    {
        InitForNewRun();
    }

    private void OnEnable()
    {
        EventCenter.Instance.AddEventListener(GameEvent.MobileSwitchWeaponPressed, OnSwitchWeaponPressed);
        EventCenter.Instance.AddEventListener<MobileWeaponSlotPressedEventData>(GameEvent.MobileWeaponSlotPressed, OnWeaponSlotPressed);
    }

    private void OnDisable()
    {
        EventCenter.Instance.RemoveEventListener(GameEvent.MobileSwitchWeaponPressed, OnSwitchWeaponPressed);
        EventCenter.Instance.RemoveEventListener<MobileWeaponSlotPressedEventData>(GameEvent.MobileWeaponSlotPressed, OnWeaponSlotPressed);
    }

    public void InitForNewRun()
    {
        if (_initialized)
        {
            return;
        }

        carriedWeapons ??= new List<CarriedWeaponSlot>();
        items ??= new List<InventoryItemSlot>();
        battleGold = 0;
        _battleGoldGainMultiplier = 1f;

        for (int i = 0; i < carriedWeapons.Count; i++)
        {
            carriedWeapons[i]?.InitForNewRun();
        }

        _initialized = true;
        SetCurrentWeaponIndex(startWeaponIndex, true);
        SendBattleGoldChangedEvent();
    }

    public void ConfigureRunWeapons(IList<CarriedWeaponSlot> runWeapons, int defaultWeaponIndex)
    {
        SetAllWeaponViewsActive(false);

        carriedWeapons = runWeapons != null
            ? new List<CarriedWeaponSlot>(runWeapons)
            : new List<CarriedWeaponSlot>();

        startWeaponIndex = Mathf.Max(0, defaultWeaponIndex);
        _currentWeaponIndex = -1;
        _initialized = false;

        InitForNewRun();
        OpenHpAndWeaponCanvasWhenReady();
    }

    public bool SwitchNextWeapon()
    {
        InitForNewRun();

        int validWeaponCount = GetValidWeaponCount();
        if (validWeaponCount <= 1)
        {
            return false;
        }

        int startIndex = Mathf.Max(0, _currentWeaponIndex);
        for (int i = 1; i <= carriedWeapons.Count; i++)
        {
            int nextIndex = (startIndex + i) % carriedWeapons.Count;
            if (IsWeaponIndexValid(nextIndex))
            {
                return SetCurrentWeaponIndex(nextIndex, false);
            }
        }

        return false;
    }

    public bool SetCurrentWeaponIndex(int index, bool force)
    {
        int safeIndex = GetSafeWeaponIndex(index);
        if (safeIndex < 0)
        {
            SetAllWeaponViewsActive(false);
            _currentWeaponIndex = -1;
            SendWeaponChangedEvent(-1, -1);
            return false;
        }

        if (!force && _currentWeaponIndex == safeIndex)
        {
            return false;
        }

        int previousIndex = _currentWeaponIndex;
        CarriedWeaponSlot previousWeapon = GetWeaponSlot(previousIndex);
        previousWeapon?.SetViewActive(false);

        _currentWeaponIndex = safeIndex;
        CarriedWeaponSlot currentWeapon = GetWeaponSlot(_currentWeaponIndex);
        currentWeapon?.EnsureRuntimeReady();
        currentWeapon?.SetViewActive(true);

        SendWeaponChangedEvent(previousIndex, _currentWeaponIndex);
        return true;
    }

    public void AddBattleGold(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        int finalAmount = Mathf.Max(1, Mathf.RoundToInt(amount * Mathf.Max(0f, _battleGoldGainMultiplier)));
        battleGold += finalAmount;
        SendBattleGoldChangedEvent();
    }

    public void ApplyBattleGoldGainMultiplier(float multiplier)
    {
        _battleGoldGainMultiplier = Mathf.Max(0f, _battleGoldGainMultiplier * Mathf.Max(0f, multiplier));
    }

    public void AddItem(int itemId, int count)
    {
        if (itemId <= 0 || count <= 0)
        {
            return;
        }

        InventoryItemSlot slot = FindItemSlot(itemId);
        if (slot == null)
        {
            slot = new InventoryItemSlot(itemId, 0);
            items.Add(slot);
        }

        slot.count += count;
        SendInventoryChangedEvent(slot);
    }

    public bool TryConsumeItem(int itemId, int count)
    {
        if (itemId <= 0 || count <= 0)
        {
            return false;
        }

        InventoryItemSlot slot = FindItemSlot(itemId);
        if (slot == null || slot.count < count)
        {
            return false;
        }

        slot.count -= count;
        SendInventoryChangedEvent(slot);

        if (slot.count <= 0)
        {
            items.Remove(slot);
        }

        return true;
    }

    public int AddReserveAmmoToAllWeapons(int amount)
    {
        if (amount <= 0)
        {
            return 0;
        }

        InitForNewRun();
        if (carriedWeapons == null || carriedWeapons.Count <= 0)
        {
            return 0;
        }

        int totalAddedAmount = 0;
        for (int i = 0; i < carriedWeapons.Count; i++)
        {
            CarriedWeaponSlot slot = carriedWeapons[i];
            if (slot == null)
            {
                continue;
            }

            slot.EnsureRuntimeReady();
            WeaponConfig config = slot.RuntimeConfig;
            WeaponRuntimeData runtimeData = slot.RuntimeData;
            if (config == null || runtimeData == null)
            {
                continue;
            }

            int previousReserveAmmo = runtimeData.currentReserveAmmo;
            int maxReserveAmmo = Mathf.Max(0, config.maxReserveAmmo);
            runtimeData.currentReserveAmmo = Mathf.Clamp(runtimeData.currentReserveAmmo + amount, 0, maxReserveAmmo);

            int addedAmount = runtimeData.currentReserveAmmo - previousReserveAmmo;
            if (addedAmount <= 0)
            {
                continue;
            }

            totalAddedAmount += addedAmount;
            SendWeaponAmmoChangedEvent(i, slot);
        }

        return totalAddedAmount;
    }

    private void OnSwitchWeaponPressed()
    {
        SwitchNextWeapon();
    }

    private void OnWeaponSlotPressed(MobileWeaponSlotPressedEventData eventData)
    {
        SetCurrentWeaponIndex(eventData.weaponIndex, false);
    }

    private CarriedWeaponSlot GetWeaponSlot(int index)
    {
        if (carriedWeapons == null || index < 0 || index >= carriedWeapons.Count)
        {
            return null;
        }

        return carriedWeapons[index];
    }

    private int GetSafeWeaponIndex(int index)
    {
        if (IsWeaponIndexValid(index))
        {
            return index;
        }

        if (carriedWeapons == null)
        {
            return -1;
        }

        for (int i = 0; i < carriedWeapons.Count; i++)
        {
            if (IsWeaponIndexValid(i))
            {
                return i;
            }
        }

        return -1;
    }

    private int GetValidWeaponCount()
    {
        if (carriedWeapons == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < carriedWeapons.Count; i++)
        {
            if (IsWeaponIndexValid(i))
            {
                count++;
            }
        }

        return count;
    }

    private bool IsWeaponIndexValid(int index)
    {
        CarriedWeaponSlot slot = GetWeaponSlot(index);
        return slot != null && slot.HasWeaponView;
    }

    private InventoryItemSlot FindItemSlot(int itemId)
    {
        if (items == null)
        {
            return null;
        }

        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] != null && items[i].itemId == itemId)
            {
                return items[i];
            }
        }

        return null;
    }

    private void SetAllWeaponViewsActive(bool isActive)
    {
        if (carriedWeapons == null)
        {
            return;
        }

        for (int i = 0; i < carriedWeapons.Count; i++)
        {
            carriedWeapons[i]?.SetViewActive(isActive);
        }
    }

    private void SendWeaponChangedEvent(int previousIndex, int currentIndex)
    {
        CarriedWeaponSlot slot = GetWeaponSlot(currentIndex);
        slot?.EnsureRuntimeReady();

        EventCenter.Instance.EventTrigger(
            GameEvent.PlayerWeaponChanged,
            new PlayerWeaponChangedEventData(
                previousIndex,
                currentIndex,
                slot?.RuntimeConfig?.weaponId ?? 0,
                slot?.DisplayName ?? string.Empty,
                slot?.RuntimeData?.currentAmmoInMagazine ?? 0,
                slot?.RuntimeData?.currentReserveAmmo ?? 0));
    }

    private void SendWeaponAmmoChangedEvent(int weaponIndex, CarriedWeaponSlot slot)
    {
        slot?.EnsureRuntimeReady();
        WeaponConfig config = slot?.RuntimeConfig;
        WeaponRuntimeData runtimeData = slot?.RuntimeData;
        if (config == null || runtimeData == null)
        {
            return;
        }

        EventCenter.Instance.EventTrigger(
            GameEvent.PlayerWeaponAmmoChanged,
            new PlayerWeaponAmmoChangedEventData(
                weaponIndex,
                config.weaponId,
                config.weaponName,
                runtimeData.currentAmmoInMagazine,
                runtimeData.currentReserveAmmo,
                config.magazineSize,
                config.maxReserveAmmo));
    }

    private void SendInventoryChangedEvent(InventoryItemSlot slot)
    {
        EventCenter.Instance.EventTrigger(
            GameEvent.PlayerInventoryChanged,
            new PlayerInventoryChangedEventData(slot != null ? slot.itemId : 0, slot != null ? slot.count : 0));
    }

    private void SendBattleGoldChangedEvent()
    {
        EventCenter.Instance.EventTrigger(
            GameEvent.PlayerBattleGoldChanged,
            new PlayerBattleGoldChangedEventData(battleGold));
    }

    private void OpenHpAndWeaponCanvasWhenReady()
    {
        if (SceneManager.GetActiveScene().name != CombatSceneName)
        {
            return;
        }

        int validWeaponCount = GetValidWeaponCount();
        if (validWeaponCount <= 0)
        {
            Debug.LogWarning("[PlayerInventory] 武器列表还没准备好 跳过 HpAndWeaponCanvas 打开", this);
            return;
        }

        UIManager.Instance.OpenPanelAsy<HpAndWeaponCanvas>(canvas =>
        {
            if (canvas == null)
            {
                Debug.LogError("[PlayerInventory] HpAndWeaponCanvas 打开失败", this);
                return;
            }

            HideOtherHpAndWeaponCanvases(canvas);
            canvas.RebuildForInventory(this);
        });
    }

    private static void HideOtherHpAndWeaponCanvases(HpAndWeaponCanvas activeCanvas)
    {
        HpAndWeaponCanvas[] canvases = FindObjectsOfType<HpAndWeaponCanvas>(true);
        if (canvases == null)
        {
            return;
        }

        for (int i = 0; i < canvases.Length; i++)
        {
            HpAndWeaponCanvas canvas = canvases[i];
            if (canvas == null || canvas == activeCanvas)
            {
                continue;
            }

            canvas.Hide();
        }
    }
}
