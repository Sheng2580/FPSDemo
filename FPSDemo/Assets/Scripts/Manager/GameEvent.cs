using UnityEngine;

public enum GameEvent
{
    MobileMoveInputChanged,
    MobileMoveLockChanged,
    MobileMoveLockTargetChanged,
    MobileMoveLockTargetHoverChanged,
    MobileLookDeltaChanged,
    MobileFirePressed,
    MobileFireReleased,
    MobileFireHolding,
    MobileReloadPressed,
    MobileSightPressed,
    MobileSightReleased,
    MobileSwitchWeaponPressed,
    PlayerWeaponChanged,
    PlayerInventoryChanged,
    PlayerBattleGoldChanged,
    WeaponAimCameraChanged
}

public readonly struct PlayerWeaponChangedEventData
{
    public readonly int previousIndex;
    public readonly int currentIndex;
    public readonly int weaponId;
    public readonly string weaponName;
    public readonly int currentAmmo;
    public readonly int reserveAmmo;

    public PlayerWeaponChangedEventData(
        int previousIndex,
        int currentIndex,
        int weaponId,
        string weaponName,
        int currentAmmo,
        int reserveAmmo)
    {
        this.previousIndex = previousIndex;
        this.currentIndex = currentIndex;
        this.weaponId = weaponId;
        this.weaponName = weaponName;
        this.currentAmmo = currentAmmo;
        this.reserveAmmo = reserveAmmo;
    }
}

public readonly struct PlayerInventoryChangedEventData
{
    public readonly int itemId;
    public readonly int count;

    public PlayerInventoryChangedEventData(int itemId, int count)
    {
        this.itemId = itemId;
        this.count = count;
    }
}

public readonly struct PlayerBattleGoldChangedEventData
{
    public readonly int battleGold;

    public PlayerBattleGoldChangedEventData(int battleGold)
    {
        this.battleGold = battleGold;
    }
}

public readonly struct WeaponAimCameraEventData
{
    public readonly float aimAmount;
    public readonly float targetFov;

    public WeaponAimCameraEventData(float aimAmount, float targetFov)
    {
        this.aimAmount = Mathf.Clamp01(aimAmount);
        this.targetFov = Mathf.Max(1f, targetFov);
    }
}
