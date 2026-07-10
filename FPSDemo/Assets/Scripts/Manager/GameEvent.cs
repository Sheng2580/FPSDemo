using Combat;
using Enemy;
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
    MobileSightCanceled,
    MobileSwitchWeaponPressed,
    PlayerWeaponChanged,
    PlayerInventoryChanged,
    PlayerBattleGoldChanged,
    PlayerDamaged,
    WeaponAimCameraChanged,
    EnemySpawned,
    EnemyDamaged,
    EnemyDied,
    EnemyReturnedToPool,
    WeaponHit,
    DamageResolved
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

public readonly struct PlayerDamagedEventData
{
    public readonly PlayerController player;
    public readonly int damage;
    public readonly int currentHp;
    public readonly int maxHp;

    public PlayerDamagedEventData(PlayerController player, int damage, int currentHp, int maxHp)
    {
        this.player = player;
        this.damage = Mathf.Max(0, damage);
        this.currentHp = Mathf.Max(0, currentHp);
        this.maxHp = Mathf.Max(1, maxHp);
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

public readonly struct EnemySpawnedEventData
{
    public readonly EnemyController enemy;
    public readonly int enemyId;
    public readonly string enemyName;

    public EnemySpawnedEventData(EnemyController enemy)
    {
        this.enemy = enemy;
        enemyId = enemy != null ? enemy.EnemyId : 0;
        enemyName = enemy != null ? enemy.EnemyName : string.Empty;
    }
}

public readonly struct EnemyDamagedEventData
{
    public readonly EnemyController enemy;
    public readonly DamageInfo damageInfo;
    public readonly float currentHealth;
    public readonly float maxHealth;

    public EnemyDamagedEventData(EnemyController enemy, DamageInfo damageInfo, float currentHealth, float maxHealth)
    {
        this.enemy = enemy;
        this.damageInfo = damageInfo;
        this.currentHealth = currentHealth;
        this.maxHealth = maxHealth;
    }
}

public readonly struct EnemyDiedEventData
{
    public readonly EnemyController enemy;
    public readonly DamageInfo damageInfo;
    public readonly int enemyId;
    public readonly string enemyName;
    public readonly int goldReward;

    public EnemyDiedEventData(EnemyController enemy, DamageInfo damageInfo)
    {
        this.enemy = enemy;
        this.damageInfo = damageInfo;
        enemyId = enemy != null ? enemy.EnemyId : 0;
        enemyName = enemy != null ? enemy.EnemyName : string.Empty;
        goldReward = enemy != null ? enemy.GoldReward : 0;
    }
}

public readonly struct EnemyReturnedToPoolEventData
{
    public readonly EnemyController enemy;
    public readonly int enemyId;

    public EnemyReturnedToPoolEventData(EnemyController enemy)
    {
        this.enemy = enemy;
        enemyId = enemy != null ? enemy.EnemyId : 0;
    }
}

public readonly struct WeaponHitEventData
{
    public readonly DamageInfo damageInfo;
    public readonly bool hitEnemy;

    public WeaponHitEventData(DamageInfo damageInfo, bool hitEnemy)
    {
        this.damageInfo = damageInfo;
        this.hitEnemy = hitEnemy;
    }
}

public readonly struct DamageResolvedEventData
{
    public readonly DamageInfo damageInfo;

    public DamageResolvedEventData(DamageInfo damageInfo)
    {
        this.damageInfo = damageInfo;
    }
}
