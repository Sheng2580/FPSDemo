using System;
using UnityEngine;

namespace Enemy.Data
{
    [Serializable]
    public class EnemySpawnEntry
    {
        public EnemyConfigAsset enemyConfig;
        public float weight = 100f;
        public int maxAliveCount = 8;
        public float healthMultiplier = 1f;
        public float damageMultiplier = 1f;
        public float moveSpeedMultiplier = 1f;
        public float goldMultiplier = 1f;

        public bool IsValid => enemyConfig != null && weight > 0f && maxAliveCount != 0;

        public void ApplyMissingDefaults()
        {
            weight = Mathf.Max(0f, weight);
            if (maxAliveCount <= 0)
            {
                maxAliveCount = 8;
            }

            healthMultiplier = Mathf.Max(0.01f, healthMultiplier);
            damageMultiplier = Mathf.Max(0.01f, damageMultiplier);
            moveSpeedMultiplier = Mathf.Max(0.01f, moveSpeedMultiplier);
            goldMultiplier = Mathf.Max(0f, goldMultiplier);
        }
    }
}
