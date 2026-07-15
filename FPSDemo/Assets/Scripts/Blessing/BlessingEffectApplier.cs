using System.Collections.Generic;
using Blessing.Data;
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
    private readonly Dictionary<int, BlessingConfig> _configMap = new Dictionary<int, BlessingConfig>();
    private readonly Dictionary<WeaponConfig, int> _weaponBonusLevels = new Dictionary<WeaponConfig, int>();
    private readonly BlessingTriggerRuntime _triggerRuntime = new BlessingTriggerRuntime();
    private PlayerController _player;
    private PlayerInventory _inventory;
    private PlayerEnergyRuntime _energyRuntime;
    private PlayerSkillController _skillController;
    private WeaponController _weaponController;
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
        EventCenter.Instance.AddEventListener<EnemyDiedEventData>(GameEvent.EnemyDied, OnEnemyDied);
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        EventCenter.Instance.RemoveEventListener<PlayerEnergyBlessingSelectedEventData>(GameEvent.PlayerEnergyBlessingSelected, OnBlessingSelected);
        EventCenter.Instance.RemoveEventListener<EnemyDiedEventData>(GameEvent.EnemyDied, OnEnemyDied);
        _weaponBonusLevels.Clear();
        _triggerRuntime.Reset();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _weaponBonusLevels.Clear();
        _triggerRuntime.Reset();
        _player = null;
        _inventory = null;
        _energyRuntime = null;
        _skillController = null;
        _weaponController = null;
    }

    private void OnEnemyDied(EnemyDiedEventData eventData)
    {
        _triggerRuntime.OnEnemyDied(eventData);
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
            case BlessingStatType.EnergyGain:
                ApplyEnergyGain(effect.modifyType, value);
                break;
            case BlessingStatType.WeaponDamage:
            case BlessingStatType.WeaponUpgradeLevel:
            case BlessingStatType.WeaponMagazine:
            case BlessingStatType.WeaponRecoil:
                ApplyWeaponStat(config, effect, value);
                break;
            case BlessingStatType.SkillCooldownReduction:
            case BlessingStatType.SkillMaxCount:
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

        runtimeData.moveSpeedMultiplier = Mathf.Max(0.01f, ApplyNumericModifier(runtimeData.moveSpeedMultiplier, modifyType, value));
    }

    private void ApplyJumpHeight(BlessingModifyType modifyType, float value)
    {
        PlayerRuntimeData runtimeData = _player != null && _player.Stats != null ? _player.Stats.RuntimeData : null;
        if (runtimeData == null)
        {
            return;
        }

        runtimeData.jumpHeightMultiplier = Mathf.Max(0.01f, ApplyNumericModifier(runtimeData.jumpHeightMultiplier, modifyType, value));
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

        if (statType == BlessingStatType.SkillCooldownReduction && (config == null || !config.requiresSkillType))
        {
            float reduction = ResolveCooldownReduction(modifyType, value);
            _skillController.ApplyCooldownReduction(SkillType.Dodge, reduction);
            _skillController.ApplyCooldownReduction(SkillType.Push, reduction);
            _skillController.ApplyCooldownReduction(SkillType.Grenade, reduction);
            return;
        }

        SkillType skillType = config != null && config.requiresSkillType ? config.requiredSkillType : SkillType.Grenade;
        switch (statType)
        {
            case BlessingStatType.SkillCooldownReduction:
                _skillController.ApplyCooldownReduction(skillType, ResolveCooldownReduction(modifyType, value));
                break;
            case BlessingStatType.SkillMaxCount:
                _skillController.AddMaxCount(skillType, Mathf.RoundToInt(value));
                break;
        }
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
