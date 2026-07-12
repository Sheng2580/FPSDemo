using Combat;
using Enemy;
using Enemy.Data;
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
    PlayerWeaponChanged,
    PlayerInventoryChanged,
    PlayerBattleGoldChanged,
    PlayerDamaged,
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
    PlayerEnergyLevelUp
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
