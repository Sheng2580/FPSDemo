using System.Collections.Generic;
using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    [Header("武器")]
    [SerializeField] private int startWeaponIndex;
    [SerializeField] private List<CarriedWeaponSlot> carriedWeapons = new List<CarriedWeaponSlot>();

    [Header("本局物品")]
    [SerializeField] private int battleGold;
    [SerializeField] private List<InventoryItemSlot> items = new List<InventoryItemSlot>();

    private int _currentWeaponIndex = -1;
    private bool _initialized;

    public int CurrentWeaponIndex => _currentWeaponIndex;
    public int WeaponCount => carriedWeapons != null ? carriedWeapons.Count : 0;
    public int BattleGold => battleGold;
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
    }

    private void OnDisable()
    {
        EventCenter.Instance.RemoveEventListener(GameEvent.MobileSwitchWeaponPressed, OnSwitchWeaponPressed);
    }

    public void InitForNewRun()
    {
        if (_initialized)
        {
            return;
        }

        carriedWeapons ??= new List<CarriedWeaponSlot>();
        items ??= new List<InventoryItemSlot>();

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

        battleGold += amount;
        SendBattleGoldChangedEvent();
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

    private void OnSwitchWeaponPressed()
    {
        SwitchNextWeapon();
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
}
