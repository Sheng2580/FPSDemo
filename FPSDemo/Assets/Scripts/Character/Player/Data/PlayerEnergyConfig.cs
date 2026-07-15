using System;
using UnityEngine;

namespace PlayerData
{
    /// <summary>
    /// 玩家局内能量配置
    /// 只保存数值和成长规则
    /// </summary>
    [Serializable]
    public class PlayerEnergyConfig
    {
        // 第一级基础能量需求
        public float maxEnergy;
        // 每级线性能量增长
        public float requiredEnergyLinearGrowth;
        // 每级二次能量增长
        public float requiredEnergyQuadraticGrowth;
        // 初始等级
        public int startLevel;
        // 每点伤害转能量比例
        public float damageToEnergyRate;
        // 是否自动升级
        public bool autoLevelUp;
        // 是否只统计玩家造成的伤害
        public bool onlyGainFromPlayerDamage;
        // 局外充能效率加成上限
        public float maxPermanentEnergyGainBonus;

        public static PlayerEnergyConfig CreateDefault()
        {
            return new PlayerEnergyConfig
            {
                maxEnergy = 80f,
                requiredEnergyLinearGrowth = 8f,
                requiredEnergyQuadraticGrowth = 1.2f,
                startLevel = 1,
                damageToEnergyRate = 0.08f,
                autoLevelUp = false,
                onlyGainFromPlayerDamage = true,
                maxPermanentEnergyGainBonus = 1f
            };
        }

        public float CalculateRequiredEnergy(int level)
        {
            int levelOffset = Mathf.Max(0, level - 1);
            float requiredEnergy = maxEnergy
                                   + requiredEnergyLinearGrowth * levelOffset
                                   + requiredEnergyQuadraticGrowth * levelOffset * levelOffset;
            return Mathf.Clamp(Mathf.Round(requiredEnergy), 1f, 100000f);
        }

        public PlayerEnergyConfig Clone()
        {
            return (PlayerEnergyConfig)MemberwiseClone();
        }

        public void ApplyMissingDefaults()
        {
            maxEnergy = Mathf.Max(1f, maxEnergy);
            requiredEnergyLinearGrowth = Mathf.Max(0f, requiredEnergyLinearGrowth);
            requiredEnergyQuadraticGrowth = Mathf.Max(0f, requiredEnergyQuadraticGrowth);
            startLevel = Mathf.Max(1, startLevel);
            damageToEnergyRate = Mathf.Max(0f, damageToEnergyRate);
            maxPermanentEnergyGainBonus = Mathf.Clamp(maxPermanentEnergyGainBonus, 0f, 1f);
        }
    }
}
