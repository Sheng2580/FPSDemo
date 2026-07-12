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
        // 最大能量
        public float maxEnergy;
        // 初始等级
        public int startLevel;
        // 每点伤害转能量比例
        public float damageToEnergyRate;
        // 是否自动升级
        public bool autoLevelUp;
        // 是否只统计玩家造成的伤害
        public bool onlyGainFromPlayerDamage;

        public static PlayerEnergyConfig CreateDefault()
        {
            return new PlayerEnergyConfig
            {
                maxEnergy = 100f,
                startLevel = 1,
                damageToEnergyRate = 0.05f,
                autoLevelUp = false,
                onlyGainFromPlayerDamage = true
            };
        }

        public PlayerEnergyConfig Clone()
        {
            return (PlayerEnergyConfig)MemberwiseClone();
        }

        public void ApplyMissingDefaults()
        {
            maxEnergy = Mathf.Max(1f, maxEnergy);
            startLevel = Mathf.Max(1, startLevel);
            damageToEnergyRate = Mathf.Max(0f, damageToEnergyRate);
        }
    }
}
