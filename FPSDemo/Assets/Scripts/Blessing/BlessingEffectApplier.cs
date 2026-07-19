using System.Collections.Generic;
using Blessing.Data;
using Combat;
using Pickup;
using PlayerData;
using UnityEngine;
using UnityEngine.SceneManagement;
using Weapon;
using Weapon.Data;

/// <summary>
/// 本局祝福数值应用器
/// </summary>
[DisallowMultipleComponent]
public class BlessingEffectApplier : MonoBehaviour
{
    private const float MinimumFireIntervalMultiplier = 0.2f;
    private const float MinimumReloadTimeMultiplier = 0.4f;
    private const float BlessingTipDuration = 1.5f;
    private const float MaximumKillBerserkChance = 0.3f;
    private const float MaximumKillGrowthChance = 0.3f;
    private const float MaximumKillGrowthMoveSpeedMultiplier = 1.65f;
    private const float MaximumKillGrowthJumpHeightMultiplier = 1.75f;

    private readonly struct WeaponTimingBaseline
    {
        public readonly float fireInterval;
        public readonly float reloadTime;
        public readonly float reloadStartTime;
        public readonly float reloadSingleRoundTime;
        public readonly float reloadEndTime;

        public WeaponTimingBaseline(WeaponConfig config)
        {
            fireInterval = Mathf.Max(0.01f, config.fireInterval);
            reloadTime = Mathf.Max(0f, config.reloadTime);
            reloadStartTime = Mathf.Max(0f, config.reloadStartTime);
            reloadSingleRoundTime = Mathf.Max(0f, config.reloadSingleRoundTime);
            reloadEndTime = Mathf.Max(0f, config.reloadEndTime);
        }
    }

    private readonly Dictionary<int, BlessingConfig> _configMap = new Dictionary<int, BlessingConfig>();
    private readonly Dictionary<WeaponConfig, int> _weaponBonusLevels = new Dictionary<WeaponConfig, int>();
    private readonly Dictionary<WeaponConfig, WeaponTimingBaseline> _weaponTimingBaselines = new Dictionary<WeaponConfig, WeaponTimingBaseline>();
    private readonly BlessingTriggerRuntime _triggerRuntime = new BlessingTriggerRuntime();
    private PlayerController _player;
    private PlayerInventory _inventory;
    private PlayerEnergyRuntime _energyRuntime;
    private PlayerSkillController _skillController;
    private WeaponController _weaponController;
    private PickupEffectResolver _pickupEffectResolver;
    private float _killBerserkChance;
    private string _killBerserkPostProcessKey;
    private float _killGrowthChance;
    private float _killGrowthCooldown;
    private float _nextKillGrowthTriggerTime;
    private bool _configsLoaded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRuntimeInstance()
    {
        if (FindObjectOfType<BlessingEffectApplier>() != null)
        {
            return;
        }

        GameObject runtimeObject = new GameObject("BlessingEffectApplier");
        runtimeObject.AddComponent<BlessingEffectApplier>();
        DontDestroyOnLoad(runtimeObject);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        EventCenter.Instance.AddEventListener<PlayerEnergyBlessingSelectedEventData>(GameEvent.PlayerEnergyBlessingSelected, OnBlessingSelected);
        EventCenter.Instance.AddEventListener<EnemyDamagedEventData>(GameEvent.EnemyDamaged, OnEnemyDamaged);
        EventCenter.Instance.AddEventListener<EnemyDiedEventData>(GameEvent.EnemyDied, OnEnemyDied);
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        EventCenter.Instance.RemoveEventListener<PlayerEnergyBlessingSelectedEventData>(GameEvent.PlayerEnergyBlessingSelected, OnBlessingSelected);
        EventCenter.Instance.RemoveEventListener<EnemyDamagedEventData>(GameEvent.EnemyDamaged, OnEnemyDamaged);
        EventCenter.Instance.RemoveEventListener<EnemyDiedEventData>(GameEvent.EnemyDied, OnEnemyDied);
        _weaponBonusLevels.Clear();
        _weaponTimingBaselines.Clear();
        _killBerserkChance = 0f;
        _killBerserkPostProcessKey = string.Empty;
        ResetKillGrowthTrigger();
        _triggerRuntime.Reset();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _weaponBonusLevels.Clear();
        _weaponTimingBaselines.Clear();
        _killBerserkChance = 0f;
        _killBerserkPostProcessKey = string.Empty;
        ResetKillGrowthTrigger();
        _triggerRuntime.Reset();
        _player = null;
        _inventory = null;
        _energyRuntime = null;
        _skillController = null;
        _weaponController = null;
        _pickupEffectResolver = null;
    }

    private void OnEnemyDamaged(EnemyDamagedEventData eventData)
    {
        _triggerRuntime.OnEnemyDamaged(eventData);
    }

    private void OnEnemyDied(EnemyDiedEventData eventData)
    {
        _triggerRuntime.OnEnemyDied(eventData);
        ApplyKillRewards(eventData);
    }

    private void OnBlessingSelected(PlayerEnergyBlessingSelectedEventData eventData)
    {
        if (eventData.blessingId <= 0)
        {
            return;
        }

        EnsureConfigsLoaded();
        if (!_configMap.TryGetValue(eventData.blessingId, out BlessingConfig config) || config == null)
        {
            Debug.LogWarning($"[BlessingEffect] 没有找到祝福配置 Id={eventData.blessingId}");
            return;
        }

        CacheRuntimeReferences();
        ApplyBlessingConfig(config, eventData.tier);
    }

    private void EnsureConfigsLoaded()
    {
        if (_configsLoaded)
        {
            return;
        }

        _configMap.Clear();
        if (BlessingJsonConfigLoader.TryLoadBlessingConfigs(out List<BlessingConfig> configs))
        {
            for (int i = 0; i < configs.Count; i++)
            {
                BlessingConfig config = configs[i];
                if (config != null && config.blessingId > 0)
                {
                    _configMap[config.blessingId] = config;
                }
            }
        }

        _configsLoaded = true;
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

        if (_energyRuntime == null)
        {
            _energyRuntime = FindObjectOfType<PlayerEnergyRuntime>();
        }

        if (_skillController == null)
        {
            _skillController = FindObjectOfType<PlayerSkillController>();
        }

        if (_weaponController == null)
        {
            _weaponController = FindObjectOfType<WeaponController>();
        }

        if (_pickupEffectResolver == null)
        {
            _pickupEffectResolver = FindObjectOfType<PickupEffectResolver>();
        }
    }

    private void ApplyBlessingConfig(BlessingConfig config, BlessingTier tier)
    {
        if (config.effects != null)
        {
            for (int i = 0; i < config.effects.Length; i++)
            {
                BlessingEffectConfig effect = config.effects[i];
                if (effect == null)
                {
                    continue;
                }

                ApplyEffect(config, effect, tier);
            }
        }

        ConfigureKillBerserkTrigger(config);
        ConfigureKillGrowthTrigger(config);
        _triggerRuntime.ApplyConfig(config);
    }

    private void ApplyEffect(BlessingConfig config, BlessingEffectConfig effect, BlessingTier tier)
    {
        float value = effect.GetValue(tier);
        switch (effect.statType)
        {
            case BlessingStatType.MaxHp:
                ApplyMaxHp(effect.modifyType, value);
                break;
            case BlessingStatType.MoveSpeed:
                ApplyMoveSpeed(effect.modifyType, value);
                break;
            case BlessingStatType.JumpHeight:
                ApplyJumpHeight(effect.modifyType, value);
                break;
            case BlessingStatType.BerserkDuration:
            case BlessingStatType.ExplosionDamage:
            case BlessingStatType.PickupAmmoGain:
            case BlessingStatType.PickupHealing:
                ApplyPlayerRuntimeMultiplier(effect.statType, effect.modifyType, value);
                break;
            case BlessingStatType.KillAmmoRestore:
            case BlessingStatType.KillHealthRestore:
            case BlessingStatType.KillBerserkDuration:
            case BlessingStatType.KillRandomBaseStat:
                ApplyPlayerKillReward(effect.statType, effect.modifyType, value);
                break;
            case BlessingStatType.EnergyGain:
                ApplyEnergyGain(effect.modifyType, value);
                break;
            case BlessingStatType.WeaponDamage:
            case BlessingStatType.WeaponUpgradeLevel:
            case BlessingStatType.WeaponMagazine:
            case BlessingStatType.WeaponRecoil:
            case BlessingStatType.WeaponFireRate:
            case BlessingStatType.WeaponReloadSpeed:
            case BlessingStatType.WeaponReserveAmmo:
                ApplyWeaponStat(config, effect, value);
                break;
            case BlessingStatType.SkillCooldownReduction:
            case BlessingStatType.SkillMaxCount:
            case BlessingStatType.SkillDamage:
                ApplySkillStat(config, effect.statType, effect.modifyType, value);
                break;
            case BlessingStatType.GoldGain:
                ApplyGoldGain(effect.modifyType, value);
                break;
            case BlessingStatType.GrantMissingPrimaryWeapon:
                Combat.CombatSceneManager.TryGrantMissingPrimaryWeapon(_inventory);
                break;
        }
    }

    private void ApplyMaxHp(BlessingModifyType modifyType, float value)
    {
        PlayerRuntimeData runtimeData = _player != null && _player.Stats != null ? _player.Stats.RuntimeData : null;
        if (runtimeData == null)
        {
            return;
        }

        int previousMaxHp = Mathf.Max(1, runtimeData.maxHp);
        int nextMaxHp = Mathf.Max(1, Mathf.RoundToInt(ApplyNumericModifier(previousMaxHp, modifyType, value)));
        int delta = nextMaxHp - previousMaxHp;
        runtimeData.maxHp = nextMaxHp;
        runtimeData.currentHp = delta >= 0
            ? Mathf.Min(runtimeData.maxHp, runtimeData.currentHp + delta)
            : Mathf.Clamp(runtimeData.currentHp, 0, runtimeData.maxHp);

        EventCenter.Instance.EventTrigger(
            GameEvent.PlayerHealthChanged,
            new PlayerHealthChangedEventData(_player, runtimeData.currentHp, runtimeData.maxHp, Mathf.Max(0, delta), delta));
    }

    private void ApplyMoveSpeed(BlessingModifyType modifyType, float value)
    {
        PlayerRuntimeData runtimeData = _player != null && _player.Stats != null ? _player.Stats.RuntimeData : null;
        if (runtimeData == null)
        {
            return;
        }

        runtimeData.moveSpeedMultiplier = Mathf.Clamp(
            ApplyNumericModifier(runtimeData.moveSpeedMultiplier, modifyType, value),
            0.01f,
            MaximumKillGrowthMoveSpeedMultiplier);
    }

    private void ApplyJumpHeight(BlessingModifyType modifyType, float value)
    {
        PlayerRuntimeData runtimeData = _player != null && _player.Stats != null ? _player.Stats.RuntimeData : null;
        if (runtimeData == null)
        {
            return;
        }

        runtimeData.jumpHeightMultiplier = Mathf.Clamp(
            ApplyNumericModifier(runtimeData.jumpHeightMultiplier, modifyType, value),
            0.01f,
            MaximumKillGrowthJumpHeightMultiplier);
    }

    private void ApplyPlayerRuntimeMultiplier(BlessingStatType statType, BlessingModifyType modifyType, float value)
    {
        PlayerRuntimeData runtimeData = _player != null && _player.Stats != null ? _player.Stats.RuntimeData : null;
        if (runtimeData == null)
        {
            return;
        }

        switch (statType)
        {
            case BlessingStatType.BerserkDuration:
                runtimeData.berserkDurationMultiplier = Mathf.Max(
                    0.01f,
                    ApplyNumericModifier(runtimeData.berserkDurationMultiplier, modifyType, value));
                break;
            case BlessingStatType.ExplosionDamage:
                runtimeData.explosionDamageMultiplier = Mathf.Max(
                    0.01f,
                    ApplyNumericModifier(runtimeData.explosionDamageMultiplier, modifyType, value));
                break;
            case BlessingStatType.PickupAmmoGain:
                runtimeData.pickupAmmoMultiplier = Mathf.Max(
                    0.01f,
                    ApplyNumericModifier(runtimeData.pickupAmmoMultiplier, modifyType, value));
                break;
            case BlessingStatType.PickupHealing:
                runtimeData.pickupHealingMultiplier = Mathf.Max(
                    0.01f,
                    ApplyNumericModifier(runtimeData.pickupHealingMultiplier, modifyType, value));
                break;
        }
    }

    private void ApplyEnergyGain(BlessingModifyType modifyType, float value)
    {
        PlayerEnergyRuntimeData runtimeData = _energyRuntime != null ? _energyRuntime.RuntimeData : null;
        if (runtimeData == null)
        {
            return;
        }

        runtimeData.energyGainMultiplier = Mathf.Max(0f, ApplyNumericModifier(runtimeData.energyGainMultiplier, modifyType, value));
    }

    private void ApplyPlayerKillReward(BlessingStatType statType, BlessingModifyType modifyType, float value)
    {
        PlayerRuntimeData runtimeData = _player != null && _player.Stats != null ? _player.Stats.RuntimeData : null;
        if (runtimeData == null)
        {
            return;
        }

        switch (statType)
        {
            case BlessingStatType.KillAmmoRestore:
                runtimeData.killAmmoRestore = Mathf.Max(
                    0f,
                    ApplyNumericModifier(runtimeData.killAmmoRestore, modifyType, value));
                break;
            case BlessingStatType.KillHealthRestore:
                runtimeData.killHealthRestore = Mathf.Max(
                    0f,
                    ApplyNumericModifier(runtimeData.killHealthRestore, modifyType, value));
                break;
            case BlessingStatType.KillBerserkDuration:
                runtimeData.killBerserkDuration = Mathf.Max(
                    0f,
                    ApplyNumericModifier(runtimeData.killBerserkDuration, modifyType, value));
                break;
            case BlessingStatType.KillRandomBaseStat:
                runtimeData.killRandomBaseStatStrength = Mathf.Max(
                    0f,
                    ApplyNumericModifier(runtimeData.killRandomBaseStatStrength, modifyType, value));
                break;
        }
    }

    private void ApplyWeaponStat(BlessingConfig config, BlessingEffectConfig effect, float value)
    {
        if (effect == null)
        {
            return;
        }

        bool appliedToCurrentWeapon = false;
        bool applied = false;
        BlessingTargetType targetType = ResolveWeaponTargetType(config, effect);

        if (_inventory != null && _inventory.CarriedWeapons != null)
        {
            IReadOnlyList<CarriedWeaponSlot> weapons = _inventory.CarriedWeapons;
            for (int i = 0; i < weapons.Count; i++)
            {
                CarriedWeaponSlot slot = weapons[i];
                if (!ShouldApplyToWeaponSlot(config, targetType, slot))
                {
                    continue;
                }

                slot.EnsureRuntimeReady();
                bool slotApplied = ApplyWeaponStatToRuntime(slot.RuntimeConfig, slot.RuntimeData, effect.statType, effect.modifyType, value);
                applied |= slotApplied;
                appliedToCurrentWeapon |= slotApplied && slot == _inventory.CurrentWeapon;

                if (targetType == BlessingTargetType.CurrentWeapon)
                {
                    break;
                }
            }
        }

        if (!applied)
        {
            WeaponConfig weaponConfig = ResolveCurrentWeaponConfig();
            WeaponRuntimeData runtimeData = ResolveCurrentWeaponRuntimeData();
            appliedToCurrentWeapon = ApplyWeaponStatToRuntime(weaponConfig, runtimeData, effect.statType, effect.modifyType, value);
        }

        if (appliedToCurrentWeapon)
        {
            _weaponController?.RefreshCurrentWeaponRuntimeView();
        }
    }

    private bool ApplyWeaponStatToRuntime(
        WeaponConfig weaponConfig,
        WeaponRuntimeData runtimeData,
        BlessingStatType statType,
        BlessingModifyType modifyType,
        float value)
    {
        if (weaponConfig == null)
        {
            return false;
        }

        EnsureWeaponTimingBaseline(weaponConfig);
        switch (statType)
        {
            case BlessingStatType.WeaponDamage:
                weaponConfig.damage = Mathf.Max(0f, ApplyNumericModifier(weaponConfig.damage, modifyType, value));
                return true;
            case BlessingStatType.WeaponUpgradeLevel:
                return ApplyWeaponUpgradeLevels(weaponConfig, runtimeData, Mathf.RoundToInt(value));
            case BlessingStatType.WeaponMagazine:
                return ApplyWeaponMagazine(weaponConfig, runtimeData, modifyType, value);
            case BlessingStatType.WeaponRecoil:
                float recoilMultiplier = Mathf.Max(0f, ResolveMultiplierModifier(modifyType, value));
                weaponConfig.recoilPitch *= recoilMultiplier;
                weaponConfig.recoilYaw *= recoilMultiplier;
                weaponConfig.viewRecoilRotation *= recoilMultiplier;
                weaponConfig.viewRecoilPosition *= recoilMultiplier;
                return true;
            case BlessingStatType.WeaponFireRate:
                return ApplyWeaponFireRate(weaponConfig, modifyType, value);
            case BlessingStatType.WeaponReloadSpeed:
                return ApplyWeaponReloadSpeed(weaponConfig, modifyType, value);
            case BlessingStatType.WeaponReserveAmmo:
                return ApplyWeaponReserveAmmo(weaponConfig, runtimeData, modifyType, value);
        }

        return false;
    }

    private bool ApplyWeaponUpgradeLevels(WeaponConfig weaponConfig, WeaponRuntimeData runtimeData, int addedLevels)
    {
        int safeAddedLevels = Mathf.Max(0, addedLevels);
        if (weaponConfig == null || safeAddedLevels <= 0)
        {
            return false;
        }

        _weaponBonusLevels.TryGetValue(weaponConfig, out int previousBonusLevel);
        int nextBonusLevel = previousBonusLevel + safeAddedLevels;
        int previousMagazineSize = Mathf.Max(1, weaponConfig.magazineSize);
        int previousReserveAmmo = Mathf.Max(0, weaponConfig.maxReserveAmmo);

        PermanentUpgradeRules.ApplyWeaponBonusLevels(weaponConfig, previousBonusLevel, nextBonusLevel);
        ClampWeaponTimings(weaponConfig);
        _weaponBonusLevels[weaponConfig] = nextBonusLevel;

        if (runtimeData != null)
        {
            int magazineDelta = Mathf.Max(0, weaponConfig.magazineSize - previousMagazineSize);
            int reserveDelta = Mathf.Max(0, weaponConfig.maxReserveAmmo - previousReserveAmmo);
            runtimeData.currentAmmoInMagazine = Mathf.Clamp(
                runtimeData.currentAmmoInMagazine + magazineDelta,
                0,
                weaponConfig.magazineSize);
            runtimeData.currentReserveAmmo = Mathf.Clamp(
                runtimeData.currentReserveAmmo + reserveDelta,
                0,
                weaponConfig.maxReserveAmmo);
        }

        return true;
    }

    private bool ApplyWeaponFireRate(
        WeaponConfig weaponConfig,
        BlessingModifyType modifyType,
        float value)
    {
        float speedMultiplier = Mathf.Max(0.01f, ResolveMultiplierModifier(modifyType, value));
        weaponConfig.fireInterval /= speedMultiplier;
        ClampWeaponTimings(weaponConfig);
        return true;
    }

    private bool ApplyWeaponReloadSpeed(
        WeaponConfig weaponConfig,
        BlessingModifyType modifyType,
        float value)
    {
        float speedMultiplier = Mathf.Max(0.01f, ResolveMultiplierModifier(modifyType, value));
        weaponConfig.reloadTime /= speedMultiplier;
        weaponConfig.reloadStartTime /= speedMultiplier;
        weaponConfig.reloadSingleRoundTime /= speedMultiplier;
        weaponConfig.reloadEndTime /= speedMultiplier;
        ClampWeaponTimings(weaponConfig);
        return true;
    }

    private bool ApplyWeaponReserveAmmo(
        WeaponConfig weaponConfig,
        WeaponRuntimeData runtimeData,
        BlessingModifyType modifyType,
        float value)
    {
        int previousMaxReserveAmmo = Mathf.Max(0, weaponConfig.maxReserveAmmo);
        int nextMaxReserveAmmo = Mathf.Max(
            0,
            Mathf.RoundToInt(ApplyNumericModifier(previousMaxReserveAmmo, modifyType, value)));
        int delta = nextMaxReserveAmmo - previousMaxReserveAmmo;
        weaponConfig.maxReserveAmmo = nextMaxReserveAmmo;
        if (runtimeData != null)
        {
            runtimeData.currentReserveAmmo = delta >= 0
                ? Mathf.Min(nextMaxReserveAmmo, runtimeData.currentReserveAmmo + delta)
                : Mathf.Clamp(runtimeData.currentReserveAmmo, 0, nextMaxReserveAmmo);
        }

        return true;
    }

    private void EnsureWeaponTimingBaseline(WeaponConfig weaponConfig)
    {
        if (weaponConfig != null && !_weaponTimingBaselines.ContainsKey(weaponConfig))
        {
            _weaponTimingBaselines.Add(weaponConfig, new WeaponTimingBaseline(weaponConfig));
        }
    }

    private void ClampWeaponTimings(WeaponConfig weaponConfig)
    {
        if (weaponConfig == null
            || !_weaponTimingBaselines.TryGetValue(weaponConfig, out WeaponTimingBaseline baseline))
        {
            return;
        }

        weaponConfig.fireInterval = Mathf.Max(
            baseline.fireInterval * MinimumFireIntervalMultiplier,
            weaponConfig.fireInterval);
        weaponConfig.reloadTime = ClampOptionalTiming(
            weaponConfig.reloadTime,
            baseline.reloadTime,
            MinimumReloadTimeMultiplier);
        weaponConfig.reloadStartTime = ClampOptionalTiming(
            weaponConfig.reloadStartTime,
            baseline.reloadStartTime,
            MinimumReloadTimeMultiplier);
        weaponConfig.reloadSingleRoundTime = ClampOptionalTiming(
            weaponConfig.reloadSingleRoundTime,
            baseline.reloadSingleRoundTime,
            MinimumReloadTimeMultiplier);
        weaponConfig.reloadEndTime = ClampOptionalTiming(
            weaponConfig.reloadEndTime,
            baseline.reloadEndTime,
            MinimumReloadTimeMultiplier);
    }

    private static float ClampOptionalTiming(float currentValue, float baselineValue, float minimumMultiplier)
    {
        return baselineValue > 0f
            ? Mathf.Max(baselineValue * minimumMultiplier, currentValue)
            : Mathf.Max(0f, currentValue);
    }

    private bool ApplyWeaponMagazine(WeaponConfig weaponConfig, WeaponRuntimeData runtimeData, BlessingModifyType modifyType, float value)
    {
        int previousMagazineSize = Mathf.Max(1, weaponConfig.magazineSize);
        int nextMagazineSize = Mathf.Max(1, Mathf.RoundToInt(ApplyNumericModifier(previousMagazineSize, modifyType, value)));
        int delta = nextMagazineSize - previousMagazineSize;
        weaponConfig.magazineSize = nextMagazineSize;
        if (weaponConfig.reloadMode == WeaponReloadMode.SingleRound)
        {
            // 单发装填武器保持一颗一颗上弹 不让弹匣祝福改乱节奏
            weaponConfig.reloadAmmoPerStep = Mathf.Max(1, Mathf.Min(weaponConfig.reloadAmmoPerStep, weaponConfig.magazineSize));
        }
        else
        {
            weaponConfig.reloadAmmoPerStep = Mathf.Max(1, Mathf.Min(weaponConfig.reloadAmmoPerStep + Mathf.Max(0, delta), weaponConfig.magazineSize));
        }

        if (runtimeData == null)
        {
            return true;
        }

        if (delta > 0)
        {
            runtimeData.currentAmmoInMagazine = Mathf.Min(weaponConfig.magazineSize, runtimeData.currentAmmoInMagazine + delta);
        }
        else
        {
            runtimeData.currentAmmoInMagazine = Mathf.Clamp(runtimeData.currentAmmoInMagazine, 0, weaponConfig.magazineSize);
        }

        return true;
    }

    private void ApplySkillStat(BlessingConfig config, BlessingStatType statType, BlessingModifyType modifyType, float value)
    {
        if (_skillController == null)
        {
            return;
        }

        if ((statType == BlessingStatType.SkillCooldownReduction || statType == BlessingStatType.SkillDamage)
            && (config == null || !config.requiresSkillType))
        {
            ApplySkillStatToType(SkillType.Dodge, statType, modifyType, value);
            ApplySkillStatToType(SkillType.Push, statType, modifyType, value);
            ApplySkillStatToType(SkillType.Grenade, statType, modifyType, value);
            return;
        }

        SkillType skillType = config != null && config.requiresSkillType ? config.requiredSkillType : SkillType.Grenade;
        ApplySkillStatToType(skillType, statType, modifyType, value);
    }

    private void ApplySkillStatToType(
        SkillType skillType,
        BlessingStatType statType,
        BlessingModifyType modifyType,
        float value)
    {
        switch (statType)
        {
            case BlessingStatType.SkillCooldownReduction:
                _skillController.ApplyCooldownReduction(skillType, ResolveCooldownReduction(modifyType, value));
                break;
            case BlessingStatType.SkillMaxCount:
                _skillController.AddMaxCount(skillType, Mathf.RoundToInt(value));
                break;
            case BlessingStatType.SkillDamage:
                _skillController.ApplyDamageMultiplier(
                    skillType,
                    ResolveMultiplierModifier(modifyType, value));
                break;
        }
    }

    private void ConfigureKillBerserkTrigger(BlessingConfig config)
    {
        if (!HasEffect(config, BlessingStatType.KillBerserkDuration) || config.triggers == null)
        {
            return;
        }

        for (int i = 0; i < config.triggers.Length; i++)
        {
            BlessingTriggerConfig trigger = config.triggers[i];
            if (trigger == null || trigger.triggerType != BlessingTriggerType.OnKillEnemy)
            {
                continue;
            }

            _killBerserkChance = Mathf.Min(
                MaximumKillBerserkChance,
                _killBerserkChance + Mathf.Clamp01(trigger.chance));
            if (!string.IsNullOrEmpty(trigger.effectKey))
            {
                _killBerserkPostProcessKey = trigger.effectKey;
            }
        }
    }

    private void ConfigureKillGrowthTrigger(BlessingConfig config)
    {
        if (!HasEffect(config, BlessingStatType.KillRandomBaseStat) || config.triggers == null)
        {
            return;
        }

        for (int i = 0; i < config.triggers.Length; i++)
        {
            BlessingTriggerConfig trigger = config.triggers[i];
            if (trigger == null || trigger.triggerType != BlessingTriggerType.OnKillEnemy)
            {
                continue;
            }

            _killGrowthChance = Mathf.Min(
                MaximumKillGrowthChance,
                _killGrowthChance + Mathf.Clamp01(trigger.chance));
            _killGrowthCooldown = Mathf.Max(_killGrowthCooldown, trigger.cooldown);
        }
    }

    private void ResetKillGrowthTrigger()
    {
        _killGrowthChance = 0f;
        _killGrowthCooldown = 0f;
        _nextKillGrowthTriggerTime = 0f;
    }

    private void ApplyKillRewards(EnemyDiedEventData eventData)
    {
        if (!IsPlayerKill(eventData.damageInfo))
        {
            return;
        }

        CacheRuntimeReferences();
        PlayerRuntimeData runtimeData = _player != null && _player.Stats != null ? _player.Stats.RuntimeData : null;
        if (runtimeData == null)
        {
            return;
        }

        int ammoRestore = Mathf.Max(0, Mathf.RoundToInt(runtimeData.killAmmoRestore));
        if (ammoRestore > 0)
        {
            _inventory?.AddReserveAmmoToAllWeapons(ammoRestore);
        }

        int healthRestore = Mathf.Max(0, Mathf.RoundToInt(runtimeData.killHealthRestore));
        if (healthRestore > 0)
        {
            _player.Heal(healthRestore);
        }

        TryApplyKillGrowth(runtimeData);

        _pickupEffectResolver ??= FindObjectOfType<PickupEffectResolver>();
        if (runtimeData.killBerserkDuration <= 0f
            || _killBerserkChance <= 0f
            || _pickupEffectResolver == null
            || _pickupEffectResolver.IsBerserkActive
            || Random.value > _killBerserkChance)
        {
            return;
        }

        float addedDuration = _pickupEffectResolver.AddBerserkDuration(
            _player,
            runtimeData.killBerserkDuration,
            _killBerserkPostProcessKey);
        if (addedDuration > 0f)
        {
            TriggerBlessingTip("祝福触发", $"狂暴 +{addedDuration:0.#} 秒", "Berserk");
        }
    }

    private void TryApplyKillGrowth(PlayerRuntimeData runtimeData)
    {
        if (runtimeData == null
            || runtimeData.killRandomBaseStatStrength <= 0f
            || _killGrowthChance <= 0f
            || Time.time < _nextKillGrowthTriggerTime
            || Random.value > _killGrowthChance)
        {
            return;
        }

        _nextKillGrowthTriggerTime = Time.time + Mathf.Max(0.1f, _killGrowthCooldown);
        float strength = runtimeData.killRandomBaseStatStrength;
        switch (Random.Range(0, 5))
        {
            case 0:
                int hpIncrease = Mathf.Max(1, Mathf.RoundToInt(5f * strength));
                ApplyMaxHp(BlessingModifyType.Add, hpIncrease);
                TriggerBlessingTip("越战越勇", $"最大生命 +{hpIncrease}", "Heal");
                break;
            case 1:
                float moveIncrease = 0.02f * strength;
                runtimeData.moveSpeedMultiplier = Mathf.Min(
                    MaximumKillGrowthMoveSpeedMultiplier,
                    runtimeData.moveSpeedMultiplier * (1f + moveIncrease));
                TriggerBlessingTip("越战越勇", $"移动速度 +{moveIncrease:P0}", "Ammo");
                break;
            case 2:
                float jumpIncrease = 0.03f * strength;
                runtimeData.jumpHeightMultiplier = Mathf.Min(
                    MaximumKillGrowthJumpHeightMultiplier,
                    runtimeData.jumpHeightMultiplier * (1f + jumpIncrease));
                TriggerBlessingTip("越战越勇", $"跳跃高度 +{jumpIncrease:P0}", "Grenade");
                break;
            case 3:
                float damageIncrease = 0.03f * strength;
                ApplyAllWeaponDamageGrowth(damageIncrease);
                TriggerBlessingTip("越战越勇", $"所有武器伤害 +{damageIncrease:P0}", "Berserk");
                break;
            default:
                float cooldownReduction = 0.015f * strength;
                ApplyAllSkillCooldownGrowth(cooldownReduction);
                TriggerBlessingTip("越战越勇", $"技能冷却 -{cooldownReduction:P0}", "Grenade");
                break;
        }
    }

    private void ApplyAllWeaponDamageGrowth(float percentIncrease)
    {
        bool appliedToCurrentWeapon = false;
        bool applied = false;
        if (_inventory != null && _inventory.CarriedWeapons != null)
        {
            IReadOnlyList<CarriedWeaponSlot> weapons = _inventory.CarriedWeapons;
            for (int i = 0; i < weapons.Count; i++)
            {
                CarriedWeaponSlot slot = weapons[i];
                if (slot == null)
                {
                    continue;
                }

                slot.EnsureRuntimeReady();
                bool slotApplied = ApplyWeaponStatToRuntime(
                    slot.RuntimeConfig,
                    slot.RuntimeData,
                    BlessingStatType.WeaponDamage,
                    BlessingModifyType.PercentAdd,
                    percentIncrease);
                applied |= slotApplied;
                appliedToCurrentWeapon |= slotApplied && slot == _inventory.CurrentWeapon;
            }
        }

        if (!applied)
        {
            appliedToCurrentWeapon = ApplyWeaponStatToRuntime(
                ResolveCurrentWeaponConfig(),
                ResolveCurrentWeaponRuntimeData(),
                BlessingStatType.WeaponDamage,
                BlessingModifyType.PercentAdd,
                percentIncrease);
        }

        if (appliedToCurrentWeapon)
        {
            _weaponController?.RefreshCurrentWeaponRuntimeView();
        }
    }

    private void ApplyAllSkillCooldownGrowth(float reduction)
    {
        if (_skillController == null)
        {
            return;
        }

        _skillController.ApplyCooldownReduction(SkillType.Dodge, reduction);
        _skillController.ApplyCooldownReduction(SkillType.Push, reduction);
        _skillController.ApplyCooldownReduction(SkillType.Grenade, reduction);
    }

    private static bool IsPlayerKill(DamageInfo damageInfo)
    {
        return damageInfo.attacker != null
               && damageInfo.attacker.GetComponentInParent<PlayerController>() != null;
    }

    private static bool HasEffect(BlessingConfig config, BlessingStatType statType)
    {
        if (config?.effects == null)
        {
            return false;
        }

        for (int i = 0; i < config.effects.Length; i++)
        {
            if (config.effects[i] != null && config.effects[i].statType == statType)
            {
                return true;
            }
        }

        return false;
    }

    private static void TriggerBlessingTip(string title, string description, string colorKey)
    {
        PickupTipEventData eventData = new PickupTipEventData(
            title,
            description,
            colorKey,
            BlessingTipDuration);
        if (UIManager.Instance == null)
        {
            EventCenter.Instance.EventTrigger(GameEvent.PickupTipRequested, eventData);
            return;
        }

        UIManager.Instance.OpenPanelAsy<TipCanvas>(_ =>
            EventCenter.Instance.EventTrigger(GameEvent.PickupTipRequested, eventData));
    }

    private void ApplyGoldGain(BlessingModifyType modifyType, float value)
    {
        if (_inventory == null)
        {
            return;
        }

        _inventory.ApplyBattleGoldGainMultiplier(ResolveMultiplierModifier(modifyType, value));
    }

    private WeaponConfig ResolveCurrentWeaponConfig()
    {
        if (_inventory != null && _inventory.CurrentWeapon != null)
        {
            _inventory.CurrentWeapon.EnsureRuntimeReady();
            return _inventory.CurrentWeapon.RuntimeConfig;
        }

        return _weaponController != null ? _weaponController.Config : null;
    }

    private WeaponRuntimeData ResolveCurrentWeaponRuntimeData()
    {
        if (_inventory != null && _inventory.CurrentWeapon != null)
        {
            _inventory.CurrentWeapon.EnsureRuntimeReady();
            return _inventory.CurrentWeapon.RuntimeData;
        }

        return _weaponController != null ? _weaponController.RuntimeData : null;
    }

    private BlessingTargetType ResolveWeaponTargetType(BlessingConfig config, BlessingEffectConfig effect)
    {
        BlessingTargetType targetType = effect != null ? effect.targetType : BlessingTargetType.CurrentWeapon;
        if (targetType == BlessingTargetType.CurrentWeapon
            || targetType == BlessingTargetType.SpecificWeapon
            || targetType == BlessingTargetType.AllWeapons)
        {
            return targetType;
        }

        targetType = config != null ? config.targetType : BlessingTargetType.CurrentWeapon;
        return targetType == BlessingTargetType.SpecificWeapon || targetType == BlessingTargetType.AllWeapons
            ? targetType
            : BlessingTargetType.CurrentWeapon;
    }

    private bool ShouldApplyToWeaponSlot(BlessingConfig config, BlessingTargetType targetType, CarriedWeaponSlot slot)
    {
        if (slot == null || !slot.HasWeaponView)
        {
            return false;
        }

        switch (targetType)
        {
            case BlessingTargetType.AllWeapons:
                return true;
            case BlessingTargetType.SpecificWeapon:
                slot.EnsureRuntimeReady();
                int requiredWeaponId = config != null ? config.requiredWeaponId : 0;
                return requiredWeaponId > 0 && slot.RuntimeConfig != null && slot.RuntimeConfig.weaponId == requiredWeaponId;
            case BlessingTargetType.CurrentWeapon:
            default:
                return _inventory == null || slot == _inventory.CurrentWeapon;
        }
    }

    private float ApplyNumericModifier(float currentValue, BlessingModifyType modifyType, float value)
    {
        switch (modifyType)
        {
            case BlessingModifyType.Add:
                return currentValue + value;
            case BlessingModifyType.PercentAdd:
                return currentValue * (1f + value);
            case BlessingModifyType.Multiply:
                return currentValue * value;
            case BlessingModifyType.Override:
                return value;
            default:
                return currentValue;
        }
    }

    private float ResolveMultiplierModifier(BlessingModifyType modifyType, float value)
    {
        switch (modifyType)
        {
            case BlessingModifyType.Add:
                return 1f + value;
            case BlessingModifyType.PercentAdd:
                return 1f + value;
            case BlessingModifyType.Multiply:
                return value;
            case BlessingModifyType.Override:
                return value;
            default:
                return 1f;
        }
    }

    private float ResolveCooldownReduction(BlessingModifyType modifyType, float value)
    {
        switch (modifyType)
        {
            case BlessingModifyType.Multiply:
                return Mathf.Max(0f, 1f - value);
            case BlessingModifyType.Add:
            case BlessingModifyType.PercentAdd:
            case BlessingModifyType.Override:
                return Mathf.Max(0f, value);
            default:
                return 0f;
        }
    }
}
