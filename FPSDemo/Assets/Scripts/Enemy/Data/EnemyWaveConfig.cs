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

        public int GetSpawnCountForTime(float elapsedTime)
        {
            float timeInWave = Mathf.Max(0f, elapsedTime - startTime);
            int grownCount = spawnCountPerBatch + Mathf.FloorToInt(timeInWave / 60f * spawnCountGrowthPerMinute);
            return Mathf.Clamp(grownCount, 1, Mathf.Max(1, maxSpawnCountPerBatch));
        }
    }
}
