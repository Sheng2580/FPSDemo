using System;
using UnityEngine;

namespace Blessing.Data
{
    /// <summary>
    /// 能量等级到祝福等级概率配置
    /// </summary>
    [Serializable]
    public class BlessingTierProbabilityConfig
    {
        // 最小能量等级
        public int minEnergyLevel;
        // Normal 权重
        public float normalWeight;
        // Plus 权重
        public float plusWeight;
        // PlusPlus 权重
        public float plusPlusWeight;

        public BlessingTier RollTier(float random01)
        {
            float total = Mathf.Max(0f, normalWeight) + Mathf.Max(0f, plusWeight) + Mathf.Max(0f, plusPlusWeight);
            if (total <= 0f)
            {
                return BlessingTier.Normal;
            }

            float roll = Mathf.Clamp01(random01) * total;
            if (roll < normalWeight)
            {
                return BlessingTier.Normal;
            }

            roll -= normalWeight;
            return roll < plusWeight ? BlessingTier.Plus : BlessingTier.PlusPlus;
        }

        public void ApplyMissingDefaults()
        {
            minEnergyLevel = Mathf.Max(1, minEnergyLevel);
            normalWeight = Mathf.Max(0f, normalWeight);
            plusWeight = Mathf.Max(0f, plusWeight);
            plusPlusWeight = Mathf.Max(0f, plusPlusWeight);
        }
    }
}
