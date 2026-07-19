using System;
using System.Collections.Generic;
using UnityEngine;

namespace Enemy.Data
{
    [Serializable]
    public class EnemyWaveConfig
    {
        public int waveIndex;
        public float startTime;
        public float endTime = 60f;
        public float spawnInterval = 3f;
        public int spawnCountPerBatch = 1;
        public float spawnCountGrowthPerMinute;
        public int maxSpawnCountPerBatch = 1;
        public int sceneMaxEnemyCount = 12;
        public int maxNearEnemyCount = 12;
        public int maxActiveAgentCount = 12;
        public int maxAttackersCount = 3;
        public int difficultyTierIndex = 1;
        public int wavesPerDifficultyTier = 3;
        public int waveTotalSpawnCount = 12;
        public int waveTotalSpawnGrowth = 3;
        public float waveClearDelay = 2f;
        public bool waitForAvailableSpawnSlot = true;
        public List<EnemySpawnEntry> entries = new List<EnemySpawnEntry>();

        public bool IsInTime(float elapsedTime)
        {
            return elapsedTime >= startTime && (endTime <= 0f || elapsedTime < endTime);
        }

        public void ApplyMissingDefaults()
        {
            spawnInterval = Mathf.Max(0.1f, spawnInterval);
            spawnCountPerBatch = Mathf.Max(1, spawnCountPerBatch);
            spawnCountGrowthPerMinute = Mathf.Max(0f, spawnCountGrowthPerMinute);
            maxSpawnCountPerBatch = Mathf.Max(spawnCountPerBatch, maxSpawnCountPerBatch);
            sceneMaxEnemyCount = Mathf.Max(1, sceneMaxEnemyCount);
            maxNearEnemyCount = Mathf.Clamp(maxNearEnemyCount, 1, sceneMaxEnemyCount);
            maxActiveAgentCount = Mathf.Clamp(maxActiveAgentCount, 0, sceneMaxEnemyCount);
            maxAttackersCount = Mathf.Clamp(maxAttackersCount, 1, sceneMaxEnemyCount);
            difficultyTierIndex = Mathf.Max(1, difficultyTierIndex);
            wavesPerDifficultyTier = Mathf.Max(1, wavesPerDifficultyTier);
            waveTotalSpawnCount = Mathf.Max(1, waveTotalSpawnCount);
            waveTotalSpawnGrowth = Mathf.Max(0, waveTotalSpawnGrowth);
            waveClearDelay = Mathf.Max(0f, waveClearDelay);

            if (entries == null)
            {
                entries = new List<EnemySpawnEntry>();
                return;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                entries[i]?.ApplyMissingDefaults();
            }
        }

        public float GetTotalAvailableWeight()
        {
            if (entries == null)
            {
                return 0f;
            }

            float totalWeight = 0f;
            for (int i = 0; i < entries.Count; i++)
            {
                EnemySpawnEntry entry = entries[i];
                if (entry != null && entry.IsValid)
                {
                    totalWeight += Mathf.Max(0f, entry.weight);
                }
            }

            return totalWeight;
        }

        public float GetTotalAvailableWeight(int absoluteWaveIndex)
        {
            if (entries == null)
            {
                return 0f;
            }

            float totalWeight = 0f;
            int difficultyTier = GetDifficultyTierForWave(absoluteWaveIndex);
            int difficultyTierGrowthStep = GetDifficultyTierGrowthStep(absoluteWaveIndex);
            for (int i = 0; i < entries.Count; i++)
            {
                EnemySpawnEntry entry = entries[i];
                if (entry == null)
                {
                    continue;
                }

                EnemySpawnEntry resolvedEntry = entry.CreateResolvedForWave(absoluteWaveIndex, difficultyTier, difficultyTierGrowthStep);
                if (resolvedEntry.IsValid)
                {
                    totalWeight += Mathf.Max(0f, resolvedEntry.weight);
                }
            }

            return totalWeight;
        }

        public List<EnemySpawnEntry> GetResolvedSpawnEntriesForWave(int absoluteWaveIndex)
        {
            ApplyMissingDefaults();
            List<EnemySpawnEntry> resolvedEntries = new List<EnemySpawnEntry>();
            if (entries == null)
            {
                return resolvedEntries;
            }

            int difficultyTier = GetDifficultyTierForWave(absoluteWaveIndex);
            int difficultyTierGrowthStep = GetDifficultyTierGrowthStep(absoluteWaveIndex);
            for (int i = 0; i < entries.Count; i++)
            {
                EnemySpawnEntry entry = entries[i];
                if (entry == null)
                {
                    continue;
                }

                EnemySpawnEntry resolvedEntry = entry.CreateResolvedForWave(absoluteWaveIndex, difficultyTier, difficultyTierGrowthStep);
                if (resolvedEntry.IsValid)
                {
                    resolvedEntries.Add(resolvedEntry);
                }
            }

            return resolvedEntries;
        }

        public bool TryGetSpawnEntryForWave(
            int absoluteWaveIndex,
            float weightRoll01,
            out EnemySpawnEntry resolvedEntry)
        {
            resolvedEntry = null;
            List<EnemySpawnEntry> resolvedEntries = GetResolvedSpawnEntriesForWave(absoluteWaveIndex);
            if (resolvedEntries.Count == 0)
            {
                return false;
            }

            float totalWeight = 0f;
            for (int i = 0; i < resolvedEntries.Count; i++)
            {
                totalWeight += Mathf.Max(0f, resolvedEntries[i].weight);
            }

            if (totalWeight <= 0f)
            {
                return false;
            }

            float targetWeight = Mathf.Clamp01(weightRoll01) * totalWeight;
            float currentWeight = 0f;
            EnemySpawnEntry fallback = null;

            for (int i = 0; i < resolvedEntries.Count; i++)
            {
                EnemySpawnEntry entry = resolvedEntries[i];
                fallback = entry;
                currentWeight += Mathf.Max(0f, entry.weight);
                if (targetWeight <= currentWeight)
                {
                    resolvedEntry = entry;
                    return true;
                }
            }

            resolvedEntry = fallback;
            return resolvedEntry != null;
        }

        public int GetSpawnCountForTime(float elapsedTime)
        {
            float timeInWave = Mathf.Max(0f, elapsedTime - startTime);
            int grownCount = spawnCountPerBatch + Mathf.FloorToInt(timeInWave / 60f * spawnCountGrowthPerMinute);
            return Mathf.Clamp(grownCount, 1, Mathf.Max(1, maxSpawnCountPerBatch));
        }

        public int GetSpawnCountForWaveElapsed(float waveElapsedTime)
        {
            int grownCount = spawnCountPerBatch + Mathf.FloorToInt(Mathf.Max(0f, waveElapsedTime) / 60f * spawnCountGrowthPerMinute);
            return Mathf.Clamp(grownCount, 1, Mathf.Max(1, maxSpawnCountPerBatch));
        }

        public int GetTotalSpawnCountForDifficultyWave(int difficultyWaveIndex)
        {
            int safeWaveIndex = Mathf.Max(1, difficultyWaveIndex);
            if (difficultyTierIndex >= 3 && waveTotalSpawnGrowth > 0)
            {
                double growthFactor = 1d + waveTotalSpawnGrowth / (double)Mathf.Max(1, waveTotalSpawnCount);
                double grownCount = waveTotalSpawnCount * Math.Pow(growthFactor, safeWaveIndex - 1);
                return grownCount >= int.MaxValue
                    ? int.MaxValue
                    : Mathf.Max(1, Mathf.RoundToInt((float)grownCount));
            }

            return Mathf.Max(1, waveTotalSpawnCount + (safeWaveIndex - 1) * waveTotalSpawnGrowth);
        }

        public int GetDifficultyTierForWave(int absoluteWaveIndex)
        {
            int safeWaveIndex = Mathf.Max(1, absoluteWaveIndex);
            int safeTierSpan = Mathf.Max(1, wavesPerDifficultyTier);
            return Mathf.Max(1, Mathf.CeilToInt(safeWaveIndex / (float)safeTierSpan));
        }

        public int GetWaveIndexInDifficultyTier(int absoluteWaveIndex)
        {
            int safeWaveIndex = Mathf.Max(1, absoluteWaveIndex);
            int safeTierSpan = Mathf.Max(1, wavesPerDifficultyTier);
            int firstWaveIndex = GetFirstWaveIndexInDifficultyTier();
            return Mathf.Max(1, safeWaveIndex - firstWaveIndex + 1);
        }

        public int GetFirstWaveIndexInDifficultyTier()
        {
            int safeTierSpan = Mathf.Max(1, wavesPerDifficultyTier);
            return (Mathf.Max(1, difficultyTierIndex) - 1) * safeTierSpan + 1;
        }

        public bool IsDifficultyTierMatch(int absoluteWaveIndex)
        {
            return difficultyTierIndex == GetDifficultyTierForWave(absoluteWaveIndex);
        }

        public int GetDifficultyTierGrowthStep(int absoluteWaveIndex)
        {
            return Mathf.Max(0, GetDifficultyTierForWave(absoluteWaveIndex) - Mathf.Max(1, difficultyTierIndex));
        }

        public int GetTotalSpawnCountForWave(int absoluteWaveIndex)
        {
            return GetTotalSpawnCountForDifficultyWave(GetWaveIndexInDifficultyTier(absoluteWaveIndex));
        }
    }
}
