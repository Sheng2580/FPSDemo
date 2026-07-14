using Combat;
using Blessing.Data;
using Enemy;
using Enemy.Data;
using Pickup.Data;
using PlayerData;
using UnityEngine;
using Weapon;
using Weapon.Data;

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
    MobileWeaponSlotPressed,
    PlayerWeaponChanged,
    PlayerWeaponAmmoChanged,
    PlayerInventoryChanged,
    PlayerBattleGoldChanged,
    PlayerDamaged,
    PlayerHealthChanged,
    PlayerDied,
    CombatTimeChanged,
    WeaponAimCameraChanged,
    EnemySpawned,
    EnemyDamaged,
    EnemyDied,
    EnemyReturnedToPool,
    EnemyWavePreparing,
    EnemyWaveStarted,
    EnemyWaveProgressChanged,
    EnemyWaveCleared,
    WeaponFired,
    WeaponHit,
    DamageResolved,
    MobileJumpPressed,
    MobileDodgePressed,
    MobilePushPressed,
    MobileGrenadePressed,
    PlayerActionLockChanged,
    SkillCastStarted,
    SkillCastCompleted,
    SkillHitEnemy,
    SkillVisualStarted,
    SkillCooldownChanged,
    SkillChargeChanged,
    PlayerEnergyChanged,
    PlayerEnergyLevelUpReady,
    PlayerEnergyLevelUp,
    PlayerEnergyStateChanged,
    PlayerEnergyBlessingSelectRequested,
    PlayerEnergyBlessingSelectCanceled,
    PlayerEnergyBlessingSelected,
    PickupSpawned,
    PickupCollected,
    PickupExpired,
    PickupTipRequested,
    PlayerBerserkChanged,
    PlayerGoldChanged
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

public readonly struct MobileWeaponSlotPressedEventData
{
    public readonly int weaponIndex;

    public MobileWeaponSlotPressedEventData(int weaponIndex)
    {
        this.weaponIndex = Mathf.Max(0, weaponIndex);
    }
}

public readonly struct PlayerWeaponAmmoChangedEventData
{
    public readonly int weaponIndex;
    public readonly int weaponId;
    public readonly string weaponName;
    public readonly int currentAmmo;
    public readonly int reserveAmmo;
    public readonly int magazineSize;
    public readonly int maxReserveAmmo;

    public PlayerWeaponAmmoChangedEventData(
        int weaponIndex,
        int weaponId,
        string weaponName,
        int currentAmmo,
        int reserveAmmo,
        int magazineSize,
        int maxReserveAmmo)
    {
        this.weaponIndex = Mathf.Max(0, weaponIndex);
        this.weaponId = Mathf.Max(0, weaponId);
        this.weaponName = weaponName ?? string.Empty;
        this.currentAmmo = Mathf.Max(0, currentAmmo);
        this.reserveAmmo = Mathf.Max(0, reserveAmmo);
        this.magazineSize = Mathf.Max(1, magazineSize);
        this.maxReserveAmmo = Mathf.Max(0, maxReserveAmmo);
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

public readonly struct PlayerGoldChangedEventData
{
    public readonly int gold;
    public readonly int deltaGold;

    public PlayerGoldChangedEventData(int gold, int deltaGold)
    {
        this.gold = Mathf.Max(0, gold);
        this.deltaGold = deltaGold;
    }
}

public readonly struct PlayerEnergyChangedEventData
{
    public readonly float currentEnergy;
    public readonly float targetEnergy;
    public readonly int level;
    public readonly float deltaEnergy;
    public readonly float maxEnergy;
    public readonly float normalizedEnergy;

    public PlayerEnergyChangedEventData(
        float currentEnergy,
        float targetEnergy,
        int level,
        float deltaEnergy,
        float maxEnergy)
    {
        this.maxEnergy = Mathf.Max(1f, maxEnergy);
        this.currentEnergy = Mathf.Clamp(currentEnergy, 0f, this.maxEnergy);
        this.targetEnergy = Mathf.Clamp(targetEnergy, 0f, this.maxEnergy);
        this.level = Mathf.Max(1, level);
        this.deltaEnergy = deltaEnergy;
        normalizedEnergy = Mathf.Clamp01(this.currentEnergy / this.maxEnergy);
    }
}

public readonly struct PlayerEnergyLevelUpEventData
{
    public readonly int level;
    public readonly float currentEnergy;
    public readonly float maxEnergy;
    public readonly bool autoLevelUp;

    public PlayerEnergyLevelUpEventData(
        int level,
        float currentEnergy,
        float maxEnergy,
        bool autoLevelUp)
    {
        this.level = Mathf.Max(1, level);
        this.maxEnergy = Mathf.Max(1f, maxEnergy);
        this.currentEnergy = Mathf.Clamp(currentEnergy, 0f, this.maxEnergy);
        this.autoLevelUp = autoLevelUp;
    }
}

public readonly struct PlayerEnergyStateChangedEventData
{
    public readonly PlayerEnergyState previousState;
    public readonly PlayerEnergyState currentState;
    public readonly int level;
    public readonly float currentEnergy;
    public readonly float maxEnergy;

    public PlayerEnergyStateChangedEventData(
        PlayerEnergyState previousState,
        PlayerEnergyState currentState,
        int level,
        float currentEnergy,
        float maxEnergy)
    {
        this.previousState = previousState;
        this.currentState = currentState;
        this.level = Mathf.Max(1, level);
        this.maxEnergy = Mathf.Max(1f, maxEnergy);
        this.currentEnergy = Mathf.Clamp(currentEnergy, 0f, this.maxEnergy);
    }
}

public readonly struct PlayerEnergyBlessingSelectedEventData
{
    public readonly int cardIndex;
    public readonly int ownedBuffCount;
    public readonly int blessingId;
    public readonly BlessingTier tier;
    public readonly int stackCount;
    public readonly float value;
    public readonly string blessingName;
    public readonly string description;

    public PlayerEnergyBlessingSelectedEventData(int cardIndex, int ownedBuffCount)
        : this(cardIndex, ownedBuffCount, 0, BlessingTier.Normal, 0, 0f, string.Empty, string.Empty)
    {
    }

    public PlayerEnergyBlessingSelectedEventData(
        int cardIndex,
        int ownedBuffCount,
        int blessingId,
        BlessingTier tier,
        int stackCount,
        float value,
        string blessingName,
        string description)
    {
        this.cardIndex = Mathf.Max(0, cardIndex);
        this.ownedBuffCount = Mathf.Max(0, ownedBuffCount);
        this.blessingId = Mathf.Max(0, blessingId);
        this.tier = tier;
        this.stackCount = Mathf.Max(0, stackCount);
        this.value = value;
        this.blessingName = blessingName ?? string.Empty;
        this.description = description ?? string.Empty;
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

public readonly struct PlayerHealthChangedEventData
{
    public readonly PlayerController player;
    public readonly int currentHp;
    public readonly int maxHp;
    public readonly int hpDelta;
    public readonly int maxHpDelta;

    public PlayerHealthChangedEventData(
        PlayerController player,
        int currentHp,
        int maxHp,
        int hpDelta,
        int maxHpDelta)
    {
        this.player = player;
        this.currentHp = Mathf.Max(0, currentHp);
        this.maxHp = Mathf.Max(1, maxHp);
        this.hpDelta = hpDelta;
        this.maxHpDelta = maxHpDelta;
    }
}

public readonly struct PlayerDiedEventData
{
    public readonly PlayerController player;

    public PlayerDiedEventData(PlayerController player)
    {
        this.player = player;
    }
}

public readonly struct CombatTimeChangedEventData
{
    public readonly float elapsedSeconds;
    public readonly int wholeSeconds;

    public CombatTimeChangedEventData(float elapsedSeconds)
    {
        this.elapsedSeconds = Mathf.Max(0f, elapsedSeconds);
        wholeSeconds = Mathf.Max(0, Mathf.FloorToInt(this.elapsedSeconds));
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

public readonly struct EnemyWaveEventData
{
    public readonly int waveIndex;
    public readonly int difficultyTierIndex;
    public readonly int targetSpawnCount;
    public readonly int spawnedCount;
    public readonly int activeEnemyCount;
    public readonly float delay;
    public readonly EnemyWaveConfig waveConfig;

    public EnemyWaveEventData(
        int waveIndex,
        int difficultyTierIndex,
        int targetSpawnCount,
        int spawnedCount,
        int activeEnemyCount,
        float delay,
        EnemyWaveConfig waveConfig)
    {
        this.waveIndex = Mathf.Max(1, waveIndex);
        this.difficultyTierIndex = Mathf.Max(1, difficultyTierIndex);
        this.targetSpawnCount = Mathf.Max(0, targetSpawnCount);
        this.spawnedCount = Mathf.Max(0, spawnedCount);
        this.activeEnemyCount = Mathf.Max(0, activeEnemyCount);
        this.delay = Mathf.Max(0f, delay);
        this.waveConfig = waveConfig;
    }
}

public readonly struct WeaponHitEventData
{
    public readonly DamageInfo damageInfo;
    public readonly bool hitEnemy;
    public readonly WeaponConfig config;

    public WeaponHitEventData(DamageInfo damageInfo, bool hitEnemy)
    {
        this.damageInfo = damageInfo;
        this.hitEnemy = hitEnemy;
        config = null;
    }

    public WeaponHitEventData(DamageInfo damageInfo, bool hitEnemy, WeaponConfig config)
    {
        this.damageInfo = damageInfo;
        this.hitEnemy = hitEnemy;
        this.config = config;
    }
}

public readonly struct WeaponFiredEventData
{
    public readonly WeaponConfig config;
    public readonly WeaponView weaponView;
    public readonly Transform muzzleTransform;
    public readonly Vector3 muzzlePosition;
    public readonly Quaternion muzzleRotation;
    public readonly int weaponId;
    public readonly string weaponName;

    public WeaponFiredEventData(
        WeaponConfig config,
        WeaponView weaponView,
        Transform muzzleTransform,
        Vector3 muzzlePosition,
        Quaternion muzzleRotation)
    {
        this.config = config;
        this.weaponView = weaponView;
        this.muzzleTransform = muzzleTransform;
        this.muzzlePosition = muzzlePosition;
        this.muzzleRotation = muzzleRotation;
        weaponId = config != null ? config.weaponId : 0;
        weaponName = config != null ? config.weaponName : string.Empty;
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

public readonly struct PlayerActionLockEventData
{
    public readonly bool isLocked;
    public readonly SkillType skillType;
    public readonly string reason;

    public PlayerActionLockEventData(bool isLocked, SkillType skillType, string reason)
    {
        this.isLocked = isLocked;
        this.skillType = skillType;
        this.reason = reason ?? string.Empty;
    }
}

public readonly struct SkillCastEventData
{
    public readonly PlayerController player;
    public readonly PlayerSkillConfig config;
    public readonly int skillId;
    public readonly string skillName;
    public readonly SkillType skillType;
    public readonly Vector3 origin;
    public readonly Vector3 direction;
    public readonly string animationKey;
    public readonly string alternateAnimationKey;
    public readonly string postProcessKey;
    public readonly string fovEffectKey;
    public readonly string cameraShakeKey;

    public SkillCastEventData(PlayerController player, PlayerSkillConfig config, Vector3 origin, Vector3 direction)
    {
        this.player = player;
        this.config = config;
        skillId = config != null ? config.skillId : 0;
        skillName = config != null ? config.skillName : string.Empty;
        skillType = config != null ? config.skillType : SkillType.Dodge;
        this.origin = origin;
        this.direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        animationKey = config != null ? config.animationKey : string.Empty;
        alternateAnimationKey = config != null ? config.alternateAnimationKey : string.Empty;
        postProcessKey = config != null ? config.postProcessKey : string.Empty;
        fovEffectKey = config != null ? config.fovEffectKey : string.Empty;
        cameraShakeKey = config != null ? config.cameraShakeKey : string.Empty;
    }
}

public readonly struct SkillHitEnemyEventData
{
    public readonly int skillId;
    public readonly string skillName;
    public readonly SkillType skillType;
    public readonly EnemyController enemy;
    public readonly int enemyId;
    public readonly float damage;
    public readonly Vector3 hitPoint;
    public readonly Vector3 knockbackDirection;
    public readonly float knockbackForce;

    public SkillHitEnemyEventData(
        PlayerSkillConfig config,
        EnemyController enemy,
        float damage,
        Vector3 hitPoint,
        Vector3 knockbackDirection)
    {
        skillId = config != null ? config.skillId : 0;
        skillName = config != null ? config.skillName : string.Empty;
        skillType = config != null ? config.skillType : SkillType.Dodge;
        this.enemy = enemy;
        enemyId = enemy != null ? enemy.EnemyId : 0;
        this.damage = Mathf.Max(0f, damage);
        this.hitPoint = hitPoint;
        this.knockbackDirection = knockbackDirection.sqrMagnitude > 0.0001f ? knockbackDirection.normalized : Vector3.zero;
        knockbackForce = config != null ? Mathf.Max(0f, config.knockbackForce) : 0f;
    }
}

public readonly struct SkillVisualEventData
{
    public readonly int skillId;
    public readonly string skillName;
    public readonly SkillType skillType;
    public readonly string effectKey;
    public readonly string audioKey;
    public readonly string postProcessKey;
    public readonly string fovEffectKey;
    public readonly string cameraShakeKey;
    public readonly Vector3 position;
    public readonly Vector3 direction;
    public readonly float duration;
    public readonly float intensity;

    public SkillVisualEventData(
        PlayerSkillConfig config,
        string effectKey,
        string audioKey,
        Vector3 position,
        Vector3 direction,
        float duration,
        float intensity)
    {
        skillId = config != null ? config.skillId : 0;
        skillName = config != null ? config.skillName : string.Empty;
        skillType = config != null ? config.skillType : SkillType.Dodge;
        this.effectKey = effectKey ?? string.Empty;
        this.audioKey = audioKey ?? string.Empty;
        postProcessKey = config != null ? config.postProcessKey : string.Empty;
        fovEffectKey = config != null ? config.fovEffectKey : string.Empty;
        cameraShakeKey = config != null ? config.cameraShakeKey : string.Empty;
        this.position = position;
        this.direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        this.duration = Mathf.Max(0f, duration);
        this.intensity = Mathf.Max(0f, intensity);
    }
}

public readonly struct SkillCooldownEventData
{
    public readonly int skillId;
    public readonly SkillType skillType;
    public readonly float remaining;
    public readonly float duration;

    public SkillCooldownEventData(PlayerSkillConfig config, float remaining, float duration)
    {
        skillId = config != null ? config.skillId : 0;
        skillType = config != null ? config.skillType : SkillType.Dodge;
        this.remaining = Mathf.Max(0f, remaining);
        this.duration = Mathf.Max(0f, duration);
    }
}

public readonly struct SkillChargeEventData
{
    public readonly int skillId;
    public readonly SkillType skillType;
    public readonly int currentCount;
    public readonly int maxCount;

    public SkillChargeEventData(PlayerSkillConfig config, int currentCount, int maxCount)
    {
        skillId = config != null ? config.skillId : 0;
        skillType = config != null ? config.skillType : SkillType.Dodge;
        this.currentCount = Mathf.Max(0, currentCount);
        this.maxCount = Mathf.Max(0, maxCount);
    }
}

public readonly struct PickupSpawnedEventData
{
    public readonly PickupItemConfig config;
    public readonly GameObject pickupObject;
    public readonly Vector3 position;

    public PickupSpawnedEventData(PickupItemConfig config, GameObject pickupObject, Vector3 position)
    {
        this.config = config;
        this.pickupObject = pickupObject;
        this.position = position;
    }
}

public readonly struct PickupCollectedEventData
{
    public readonly PickupItemConfig config;
    public readonly GameObject collector;
    public readonly GameObject pickupObject;
    public readonly Vector3 position;

    public PickupCollectedEventData(
        PickupItemConfig config,
        GameObject collector,
        GameObject pickupObject,
        Vector3 position)
    {
        this.config = config;
        this.collector = collector;
        this.pickupObject = pickupObject;
        this.position = position;
    }
}

public readonly struct PickupExpiredEventData
{
    public readonly PickupItemConfig config;
    public readonly GameObject pickupObject;
    public readonly Vector3 position;

    public PickupExpiredEventData(PickupItemConfig config, GameObject pickupObject, Vector3 position)
    {
        this.config = config;
        this.pickupObject = pickupObject;
        this.position = position;
    }
}

public readonly struct PickupTipEventData
{
    public readonly string itemName;
    public readonly string description;
    public readonly string tipColorKey;
    public readonly float duration;

    public PickupTipEventData(string itemName, string description, string tipColorKey, float duration)
    {
        this.itemName = itemName ?? string.Empty;
        this.description = description ?? string.Empty;
        this.tipColorKey = tipColorKey ?? string.Empty;
        this.duration = Mathf.Max(0.1f, duration);
    }
}

public readonly struct PlayerBerserkChangedEventData
{
    public readonly bool active;
    public readonly float remainingTime;
    public readonly float addedDuration;
    public readonly string postProcessKey;

    public PlayerBerserkChangedEventData(
        bool active,
        float remainingTime,
        float addedDuration,
        string postProcessKey)
    {
        this.active = active;
        this.remainingTime = Mathf.Max(0f, remainingTime);
        this.addedDuration = Mathf.Max(0f, addedDuration);
        this.postProcessKey = postProcessKey ?? string.Empty;
    }
}
