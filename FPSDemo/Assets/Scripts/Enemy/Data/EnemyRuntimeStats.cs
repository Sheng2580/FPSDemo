using System;
using UnityEngine;

namespace Enemy.Data
{
    [Serializable]
    public class EnemyRuntimeStats
    {
        public int enemyId;
        public string enemyName;
        public string prefabKey;
        public string prefabResourceKey;
        public string behaviorTreeKey;
        public string aiProfileKey;
        public string bodyPartTemplateKey;
        public string dropPoolKey;

        public float maxHealth;
        public float moveSpeed;
        public float angularSpeed;
        public float acceleration;
        public float attackDamage;
        public float attackDistance;
        public float attackInterval;
        public float attackHitDelay;
        public float detectionRange;
        public int goldReward;
        public int blessingEnergyReward;
        public int experienceReward;

        public float hitStunDuration;
        public float hitReactionCooldown;
        public float hitKnockbackDistance;
        public float hitKnockbackDuration;

        public string idleStateName;
        public string walkStateName;
        public string runStateName;
        public string attackStateName;
        public string damageStateName;
        public string deathStateName;

        public float locomotionTransition;
        public float attackTransition;
        public float hitTransition;
        public float deathTransition;
        public float recoverTransition;

        public float headDamageMultiplier;
        public float bodyDamageMultiplier;
        public float armDamageMultiplier;
        public float legDamageMultiplier;

        public int waveIndex;
        public int entryMaxAliveCount;
        public int spawnCountForBatch;
        public int sceneMaxEnemyCount;
        public int maxNearEnemyCount;
        public int maxActiveAgentCount;
        public int maxAttackersCount;

        public static EnemyRuntimeStats Create(EnemyConfig config, EnemySpawnEntry entry)
        {
            return Create(config, entry, null, 0f);
        }

        public static EnemyRuntimeStats Create(EnemyConfig config, EnemySpawnEntry entry, EnemyWaveConfig wave, float elapsedTime)
        {
            config ??= EnemyConfig.CreateNormalZombie();
            config.ApplyMissingDefaults();

            entry ??= new EnemySpawnEntry();
            entry.ApplyMissingDefaults();

            return new EnemyRuntimeStats
            {
                enemyId = config.enemyId,
                enemyName = config.enemyName,
                prefabKey = config.prefabKey,
                prefabResourceKey = config.prefabResourceKey,
                behaviorTreeKey = config.behaviorTreeKey,
                aiProfileKey = config.aiProfileKey,
                bodyPartTemplateKey = config.bodyPartTemplateKey,
                dropPoolKey = config.dropPoolKey,
                maxHealth = Mathf.Max(1f, config.maxHealth * entry.healthMultiplier),
                moveSpeed = Mathf.Max(0.1f, config.moveSpeed * entry.moveSpeedMultiplier),
                angularSpeed = Mathf.Max(1f, config.angularSpeed),
                acceleration = Mathf.Max(1f, config.acceleration),
                attackDamage = Mathf.Max(0f, config.attackDamage * entry.damageMultiplier),
                attackDistance = Mathf.Max(0.1f, config.attackDistance),
                attackInterval = Mathf.Max(0.1f, config.attackInterval),
                attackHitDelay = Mathf.Max(0f, config.attackHitDelay),
                detectionRange = Mathf.Max(0.1f, config.detectionRange),
                goldReward = Mathf.Max(0, Mathf.RoundToInt(config.goldReward * entry.goldMultiplier)),
                blessingEnergyReward = Mathf.Max(0, config.blessingEnergyReward),
                experienceReward = Mathf.Max(0, config.experienceReward),
                hitStunDuration = Mathf.Max(0.01f, config.hitStunDuration),
                hitReactionCooldown = Mathf.Max(0f, config.hitReactionCooldown),
                hitKnockbackDistance = Mathf.Max(0f, config.hitKnockbackDistance),
                hitKnockbackDuration = Mathf.Max(0.01f, config.hitKnockbackDuration),
                idleStateName = config.idleStateName,
                walkStateName = config.walkStateName,
                runStateName = config.runStateName,
                attackStateName = config.attackStateName,
                damageStateName = config.damageStateName,
                deathStateName = config.deathStateName,
                locomotionTransition = Mathf.Max(0.01f, config.locomotionTransition),
                attackTransition = Mathf.Max(0.01f, config.attackTransition),
                hitTransition = Mathf.Max(0.01f, config.hitTransition),
                deathTransition = Mathf.Max(0.01f, config.deathTransition),
                recoverTransition = Mathf.Max(0.01f, config.recoverTransition),
                headDamageMultiplier = Mathf.Max(0f, config.headDamageMultiplier),
                bodyDamageMultiplier = Mathf.Max(0f, config.bodyDamageMultiplier),
                armDamageMultiplier = Mathf.Max(0f, config.armDamageMultiplier),
                legDamageMultiplier = Mathf.Max(0f, config.legDamageMultiplier),
                waveIndex = wave != null ? wave.waveIndex : 0,
                entryMaxAliveCount = Mathf.Max(1, entry.maxAliveCount),
                spawnCountForBatch = wave != null ? wave.GetSpawnCountForTime(elapsedTime) : 1,
                sceneMaxEnemyCount = wave != null ? Mathf.Max(1, wave.sceneMaxEnemyCount) : Mathf.Max(1, entry.maxAliveCount),
                maxNearEnemyCount = wave != null ? Mathf.Max(1, wave.maxNearEnemyCount) : 1,
                maxActiveAgentCount = wave != null ? Mathf.Max(0, wave.maxActiveAgentCount) : 1,
                maxAttackersCount = wave != null ? Mathf.Max(1, wave.maxAttackersCount) : 1
            };
        }

        public static bool TryCreateFromWave(
            EnemyWaveConfig wave,
            float elapsedTime,
            out EnemyRuntimeStats runtimeStats)
        {
            return TryCreateFromWave(wave, elapsedTime, UnityEngine.Random.value, out runtimeStats);
        }

        public static bool TryCreateFromWave(
            EnemyWaveConfig wave,
            float elapsedTime,
            float weightRoll01,
            out EnemyRuntimeStats runtimeStats)
        {
            runtimeStats = null;
            if (wave == null || !wave.IsInTime(elapsedTime))
            {
                return false;
            }

            wave.ApplyMissingDefaults();
            EnemySpawnEntry entry = SelectEntry(wave, weightRoll01);
            if (entry == null)
            {
                return false;
            }

            runtimeStats = CreateFromEntry(entry, wave, elapsedTime);
            return runtimeStats != null;
        }

        public static EnemyRuntimeStats CreateFromEntry(EnemySpawnEntry entry, EnemyWaveConfig wave, float elapsedTime)
        {
            if (entry == null || entry.enemyConfig == null)
            {
                return null;
            }

            EnemyConfig config = entry.enemyConfig.CreateRuntimeConfig();
            return Create(config, entry, wave, elapsedTime);
        }

        private static EnemySpawnEntry SelectEntry(EnemyWaveConfig wave, float weightRoll01)
        {
            float totalWeight = wave.GetTotalAvailableWeight();
            if (totalWeight <= 0f || wave.entries == null)
            {
                return null;
            }

            float targetWeight = Mathf.Clamp01(weightRoll01) * totalWeight;
            float currentWeight = 0f;
            EnemySpawnEntry fallback = null;

            for (int i = 0; i < wave.entries.Count; i++)
            {
                EnemySpawnEntry entry = wave.entries[i];
                if (entry == null || !entry.IsValid)
                {
                    continue;
                }

                fallback = entry;
                currentWeight += Mathf.Max(0f, entry.weight);
                if (targetWeight <= currentWeight)
                {
                    return entry;
                }
            }

            return fallback;
        }
    }
}
