using System;
using UnityEngine;

namespace PlayerData
{
    public enum PlayerEnergyState
    {
        Charging,
        LevelUpReady,
        BlessingSelecting
    }

    /// <summary>
    /// 玩家局内能量运行时数据
    /// 不写回配置资源
    /// </summary>
    [Serializable]
    public class PlayerEnergyRuntimeData
    {
        // 当前能量
        public float currentEnergy;
        // 最大能量
        public float maxEnergy = 100f;
        // 当前能量等级
        public int level = 1;
        // Buff 可叠加能量获取倍率
        public float energyGainMultiplier = 1f;
        // 是否自动升级
        public bool autoLevelUp;
        // 是否已经达到升级准备状态
        public bool isLevelUpReady;
        // 当前能量状态
        public PlayerEnergyState state = PlayerEnergyState.Charging;

        public float NormalizedEnergy => maxEnergy > 0f ? Mathf.Clamp01(currentEnergy / maxEnergy) : 0f;

        public void InitForNewRun(PlayerEnergyConfig config, float baseEnergyGainMultiplier = 1f)
        {
            PlayerEnergyConfig safeConfig = config ?? PlayerEnergyConfig.CreateDefault();
            safeConfig.ApplyMissingDefaults();
            currentEnergy = 0f;
            level = safeConfig.startLevel;
            maxEnergy = safeConfig.CalculateRequiredEnergy(level);
            energyGainMultiplier = Mathf.Max(0f, baseEnergyGainMultiplier);
            autoLevelUp = safeConfig.autoLevelUp;
            isLevelUpReady = false;
            state = PlayerEnergyState.Charging;
        }

        public float CalculateEnergyGain(float finalDamage, PlayerEnergyConfig config)
        {
            PlayerEnergyConfig safeConfig = config ?? PlayerEnergyConfig.CreateDefault();
            safeConfig.ApplyMissingDefaults();
            return Mathf.Max(0f, finalDamage) * safeConfig.damageToEnergyRate * Mathf.Max(0f, energyGainMultiplier);
        }

        public float AddEnergy(float deltaEnergy)
        {
            if (deltaEnergy <= 0f || state != PlayerEnergyState.Charging)
            {
                return 0f;
            }

            float previousEnergy = currentEnergy;
            currentEnergy = Mathf.Clamp(currentEnergy + deltaEnergy, 0f, maxEnergy);
            return currentEnergy - previousEnergy;
        }

        public bool CanLevelUp()
        {
            return currentEnergy >= maxEnergy;
        }

        public void MarkLevelUpReady()
        {
            isLevelUpReady = true;
        }

        public void LevelUpAndReset(PlayerEnergyConfig config)
        {
            level = Mathf.Max(1, level + 1);
            currentEnergy = 0f;
            PlayerEnergyConfig safeConfig = config ?? PlayerEnergyConfig.CreateDefault();
            safeConfig.ApplyMissingDefaults();
            maxEnergy = safeConfig.CalculateRequiredEnergy(level);
            isLevelUpReady = false;
        }
    }
}
