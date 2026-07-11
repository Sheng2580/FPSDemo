using System;
using UnityEngine;

namespace Enemy.Data
{
    [Serializable]
    public class EnemySpawnEntry
    {
        public EnemyConfigAsset enemyConfig;
        public int unlockWaveIndex = 1;
        public int minWaveIndex = 1;
        public int maxWaveIndex;
        public float baseWeight = 100f;
        public float weightGrowthPerWave;
        public float weightGrowthPerDifficultyTier;
        public float maxWeight;
        public float weight = 100f;
        public int maxAliveCount = 8;
        public float healthMultiplier = 1f;
        public float healthMultiplierGrowthPerWave;
        public float healthMultiplierGrowthPerDifficultyTier;
        public float maxHealthMultiplier;
        public float damageMultiplier = 1f;
        public float damageMultiplierGrowthPerWave;
        public float damageMultiplierGrowthPerDifficultyTier;
        public float maxDamageMultiplier;
        public float moveSpeedMultiplier = 1f;
        public float moveSpeedMultiplierGrowthPerWave;
        public float moveSpeedMultiplierGrowthPerDifficultyTier;
        public float maxMoveSpeedMultiplier;
        public float goldMultiplier = 1f;
        public float goldMultiplierGrowthPerWave;
        public float goldMultiplierGrowthPerDifficultyTier;
        public float maxGoldMultiplier;

        public bool IsValid => enemyConfig != null && weight > 0f && maxAliveCount != 0;
        public int EffectiveUnlockWaveIndex => Mathf.Max(1, Mathf.Max(unlockWaveIndex, minWaveIndex));

        public void ApplyMissingDefaults()
        {
            unlockWaveIndex = Mathf.Max(1, unlockWaveIndex);
            minWaveIndex = Mathf.Max(1, minWaveIndex);
            maxWaveIndex = Mathf.Max(0, maxWaveIndex);
            baseWeight = Mathf.Max(0f, baseWeight);
            maxWeight = Mathf.Max(0f, maxWeight);
            weight = Mathf.Max(0f, weight);
            if (maxAliveCount <= 0)
            {
                maxAliveCount = 8;
            }

            healthMultiplier = Mathf.Max(0.01f, healthMultiplier);
            damageMultiplier = Mathf.Max(0.01f, damageMultiplier);
            moveSpeedMultiplier = Mathf.Max(0.01f, moveSpeedMultiplier);
            goldMultiplier = Mathf.Max(0f, goldMultiplier);
            maxHealthMultiplier = Mathf.Max(0f, maxHealthMultiplier);
            maxDamageMultiplier = Mathf.Max(0f, maxDamageMultiplier);
            maxMoveSpeedMultiplier = Mathf.Max(0f, maxMoveSpeedMultiplier);
            maxGoldMultiplier = Mathf.Max(0f, maxGoldMultiplier);
        }

        public bool IsAvailableForWave(int absoluteWaveIndex)
        {
            int safeWaveIndex = Mathf.Max(1, absoluteWaveIndex);
            if (enemyConfig == null || safeWaveIndex < EffectiveUnlockWaveIndex)
            {
                return false;
            }

            return maxWaveIndex <= 0 || safeWaveIndex <= maxWaveIndex;
        }

        public EnemySpawnEntry CreateResolvedForWave(int absoluteWaveIndex, int difficultyTierIndex)
        {
            int difficultyTierGrowthStep = Mathf.Max(0, difficultyTierIndex - 1);
            return CreateResolvedForWave(absoluteWaveIndex, difficultyTierIndex, difficultyTierGrowthStep);
        }

        public EnemySpawnEntry CreateResolvedForWave(int absoluteWaveIndex, int difficultyTierIndex, int difficultyTierGrowthStep)
        {
            EnemySpawnEntry resolved = (EnemySpawnEntry)MemberwiseClone();
            resolved.ApplyMissingDefaults();

            if (!resolved.IsAvailableForWave(absoluteWaveIndex))
            {
                resolved.weight = 0f;
                return resolved;
            }

            int wavesSinceUnlock = Mathf.Max(0, absoluteWaveIndex - resolved.EffectiveUnlockWaveIndex);
            int tiersSinceStart = Mathf.Max(0, difficultyTierGrowthStep);
            resolved.weight = ResolveWeight(resolved, wavesSinceUnlock, tiersSinceStart);
            resolved.healthMultiplier = ResolveMultiplier(
                resolved.healthMultiplier,
                resolved.healthMultiplierGrowthPerWave,
                resolved.healthMultiplierGrowthPerDifficultyTier,
                resolved.maxHealthMultiplier,
                wavesSinceUnlock,
                tiersSinceStart,
                0.01f);
            resolved.damageMultiplier = ResolveMultiplier(
                resolved.damageMultiplier,
                resolved.damageMultiplierGrowthPerWave,
                resolved.damageMultiplierGrowthPerDifficultyTier,
                resolved.maxDamageMultiplier,
                wavesSinceUnlock,
                tiersSinceStart,
                0.01f);
            resolved.moveSpeedMultiplier = ResolveMultiplier(
                resolved.moveSpeedMultiplier,
                resolved.moveSpeedMultiplierGrowthPerWave,
                resolved.moveSpeedMultiplierGrowthPerDifficultyTier,
                resolved.maxMoveSpeedMultiplier,
                wavesSinceUnlock,
                tiersSinceStart,
                0.01f);
            resolved.goldMultiplier = ResolveMultiplier(
                resolved.goldMultiplier,
                resolved.goldMultiplierGrowthPerWave,
                resolved.goldMultiplierGrowthPerDifficultyTier,
                resolved.maxGoldMultiplier,
                wavesSinceUnlock,
                tiersSinceStart,
                0f);

            return resolved;
        }

        private static float ResolveWeight(EnemySpawnEntry entry, int wavesSinceUnlock, int tiersSinceStart)
        {
            float resolvedWeight = entry.baseWeight
                + wavesSinceUnlock * entry.weightGrowthPerWave
                + tiersSinceStart * entry.weightGrowthPerDifficultyTier;

            if (entry.maxWeight > 0f)
            {
                resolvedWeight = Mathf.Min(resolvedWeight, entry.maxWeight);
            }

            return Mathf.Max(0f, resolvedWeight);
        }

        private static float ResolveMultiplier(
            float baseMultiplier,
            float growthPerWave,
            float growthPerDifficultyTier,
            float maxMultiplier,
            int wavesSinceUnlock,
            int tiersSinceStart,
            float minMultiplier)
        {
            float resolvedMultiplier = baseMultiplier
                + wavesSinceUnlock * growthPerWave
                + tiersSinceStart * growthPerDifficultyTier;

            if (maxMultiplier > 0f)
            {
                resolvedMultiplier = Mathf.Min(resolvedMultiplier, maxMultiplier);
            }

            return Mathf.Max(minMultiplier, resolvedMultiplier);
        }
    }
}
