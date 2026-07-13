using UnityEngine;

namespace Blessing.Data
{
    /// <summary>
    /// 祝福等级概率配置资源
    /// </summary>
    [CreateAssetMenu(menuName = "FPSDemo/Blessing/Tier Probability Config", fileName = "BlessingTierProbabilityConfig")]
    public class BlessingTierProbabilityConfigAsset : ScriptableObject
    {
        // 概率配置
        public BlessingTierProbabilityConfig[] tiers;

        public BlessingTierProbabilityConfig ResolveForEnergyLevel(int energyLevel)
        {
            BlessingTierProbabilityConfig best = null;
            int safeLevel = Mathf.Max(1, energyLevel);
            if (tiers != null)
            {
                for (int i = 0; i < tiers.Length; i++)
                {
                    BlessingTierProbabilityConfig config = tiers[i];
                    if (config == null)
                    {
                        continue;
                    }

                    config.ApplyMissingDefaults();
                    if (config.minEnergyLevel <= safeLevel && (best == null || config.minEnergyLevel > best.minEnergyLevel))
                    {
                        best = config;
                    }
                }
            }

            return best ?? new BlessingTierProbabilityConfig
            {
                minEnergyLevel = 1,
                normalWeight = 100f,
                plusWeight = 0f,
                plusPlusWeight = 0f
            };
        }

        public BlessingTier RollTier(int energyLevel)
        {
            return ResolveForEnergyLevel(energyLevel).RollTier(Random.value);
        }

        private void OnValidate()
        {
            if (tiers == null)
            {
                return;
            }

            for (int i = 0; i < tiers.Length; i++)
            {
                tiers[i]?.ApplyMissingDefaults();
            }
        }
    }
}
