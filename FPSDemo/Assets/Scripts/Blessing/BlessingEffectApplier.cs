using System.Collections.Generic;
using Blessing.Data;
using PlayerData;
using UnityEngine;
using Weapon;
using Weapon.Data;

/// <summary>
/// 本局祝福数值应用器
/// </summary>
[DisallowMultipleComponent]
public class BlessingEffectApplier : MonoBehaviour
{
    private readonly Dictionary<int, BlessingConfig> _configMap = new Dictionary<int, BlessingConfig>();
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
        EventCenter.Instance.AddEventListener<PlayerEnergyBlessingSelectedEventData>(GameEvent.PlayerEnergyBlessingSelected, OnBlessingSelected);
    }

    private void OnDisable()
    {
        EventCenter.Instance.RemoveEventListener<PlayerEnergyBlessingSelectedEventData>(GameEvent.PlayerEnergyBlessingSelected, OnBlessingSelected);
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
        if (config.effects == null)
        {
            return;
        }

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
            case BlessingStatType.EnergyGain:
                ApplyEnergyGain(effect.modifyType, value);
                break;
            case BlessingStatType.WeaponDamage:
            case BlessingStatType.WeaponMagazine:
            case BlessingStatType.WeaponRecoil:
                ApplyWeaponStat(config, effect, value);
                break;
            case BlessingStatType.SkillCooldown:
            case BlessingStatType.SkillMaxCount:
                ApplySkillStat(config, effect.statType, effect.modifyType, value);
                break;
            case BlessingStatType.GoldGain:
                ApplyGoldGain(effect.modifyType, value);
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

        if (statType == BlessingStatType.SkillCooldown && (config == null || !config.requiresSkillType))
        {
            float multiplier = ResolveMultiplierModifier(modifyType, value);
            _skillController.ApplyCooldownMultiplier(SkillType.Dodge, multiplier);
            _skillController.ApplyCooldownMultiplier(SkillType.Push, multiplier);
            _skillController.ApplyCooldownMultiplier(SkillType.Grenade, multiplier);
            return;
        }

        SkillType skillType = config != null && config.requiresSkillType ? config.requiredSkillType : SkillType.Grenade;
        switch (statType)
        {
            case BlessingStatType.SkillCooldown:
                _skillController.ApplyCooldownMultiplier(skillType, ResolveMultiplierModifier(modifyType, value));
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
}
