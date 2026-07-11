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
        public int absoluteWaveIndex;
        public float waveElapsedTime;
        public int entryMaxAliveCount;
        public int spawnCountForBatch;
        public int sceneMaxEnemyCount;
        public int maxNearEnemyCount;
        public int maxActiveAgentCount;
        public int maxAttackersCount;
        public int difficultyTierIndex;
        public int difficultyTierGrowthStep;
        public int wavesPerDifficultyTier;
        public int waveTotalSpawnCount;
        public int waveTotalSpawnGrowth;
        public float waveClearDelay;
        public bool waitForAvailableSpawnSlot;
        public int candidateUnlockWaveIndex;
        public int candidateMinWaveIndex;
        public int candidateMaxWaveIndex;
        public float candidateBaseWeight;
        public float candidateFinalWeight;
        public float resolvedHealthMultiplier;
        public float resolvedDamageMultiplier;
        public float resolvedMoveSpeedMultiplier;
        public float resolvedGoldMultiplier;

        public static EnemyRuntimeStats Create(EnemyConfig config, EnemySpawnEntry entry)
        {
            return Create(config, entry, null, 0f);
        }

        public static EnemyRuntimeStats Create(EnemyConfig config, EnemySpawnEntry entry, EnemyWaveConfig wave, float elapsedTime)
        {
            int resolvedAbsoluteWaveIndex = wave != null ? wave.GetFirstWaveIndexInDifficultyTier() : 0;
            float resolvedWaveElapsedTime = wave != null ? Mathf.Max(0f, elapsedTime - wave.startTime) : 0f;
            return Create(config, entry, wave, elapsedTime, resolvedAbsoluteWaveIndex, resolvedWaveElapsedTime);
        }

        public static EnemyRuntimeStats Create(
            EnemyConfig config,
            EnemySpawnEntry entry,
            EnemyWaveConfig wave,
            float elapsedTime,
            int absoluteWaveIndex,
            float waveElapsedTime)
        {
            config ??= EnemyConfig.CreateNormalZombie();
            config.ApplyMissingDefaults();

            entry ??= new EnemySpawnEntry();
            entry.ApplyMissingDefaults();
            int safeAbsoluteWaveIndex = Mathf.Max(0, absoluteWaveIndex);
            float safeWaveElapsedTime = Mathf.Max(0f, waveElapsedTime);

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
                absoluteWaveIndex = safeAbsoluteWaveIndex,
                waveElapsedTime = safeWaveElapsedTime,
                entryMaxAliveCount = Mathf.Max(1, entry.maxAliveCount),
                spawnCountForBatch = wave != null ? wave.GetSpawnCountForWaveElapsed(safeWaveElapsedTime) : 1,
                sceneMaxEnemyCount = wave != null ? Mathf.Max(1, wave.sceneMaxEnemyCount) : Mathf.Max(1, entry.maxAliveCount),
                maxNearEnemyCount = wave != null ? Mathf.Max(1, wave.maxNearEnemyCount) : 1,
                maxActiveAgentCount = wave != null ? Mathf.Max(0, wave.maxActiveAgentCount) : 1,
                maxAttackersCount = wave != null ? Mathf.Max(1, wave.maxAttackersCount) : 1,
                difficultyTierIndex = wave != null ? wave.GetDifficultyTierForWave(safeAbsoluteWaveIndex) : 1,
                difficultyTierGrowthStep = wave != null ? wave.GetDifficultyTierGrowthStep(safeAbsoluteWaveIndex) : 0,
                wavesPerDifficultyTier = wave != null ? Mathf.Max(1, wave.wavesPerDifficultyTier) : 1,
                waveTotalSpawnCount = wave != null ? wave.GetTotalSpawnCountForWave(safeAbsoluteWaveIndex) : 1,
                waveTotalSpawnGrowth = wave != null ? Mathf.Max(0, wave.waveTotalSpawnGrowth) : 0,
                waveClearDelay = wave != null ? Mathf.Max(0f, wave.waveClearDelay) : 5f,
                waitForAvailableSpawnSlot = wave == null || wave.waitForAvailableSpawnSlot,
                candidateUnlockWaveIndex = Mathf.Max(1, entry.unlockWaveIndex),
                candidateMinWaveIndex = Mathf.Max(1, entry.minWaveIndex),
                candidateMaxWaveIndex = Mathf.Max(0, entry.maxWaveIndex),
                candidateBaseWeight = Mathf.Max(0f, entry.baseWeight),
                candidateFinalWeight = Mathf.Max(0f, entry.weight),
                resolvedHealthMultiplier = Mathf.Max(0.01f, entry.healthMultiplier),
                resolvedDamageMultiplier = Mathf.Max(0.01f, entry.damageMultiplier),
                resolvedMoveSpeedMultiplier = Mathf.Max(0.01f, entry.moveSpeedMultiplier),
                resolvedGoldMultiplier = Mathf.Max(0f, entry.goldMultiplier)
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
            int absoluteWaveIndex = wave.GetFirstWaveIndexInDifficultyTier();
            float waveElapsedTime = Mathf.Max(0f, elapsedTime - wave.startTime);
            return TryCreateFromWave(wave, absoluteWaveIndex, waveElapsedTime, weightRoll01, out runtimeStats);
        }

        public static bool TryCreateFromWave(
            EnemyWaveConfig wave,
            int absoluteWaveIndex,
            float waveElapsedTime,
            out EnemyRuntimeStats runtimeStats)
        {
            return TryCreateFromWave(wave, absoluteWaveIndex, waveElapsedTime, UnityEngine.Random.value, out runtimeStats);
        }

        public static bool TryCreateFromWave(
            EnemyWaveConfig wave,
            int absoluteWaveIndex,
            float waveElapsedTime,
            float weightRoll01,
            out EnemyRuntimeStats runtimeStats)
        {
            runtimeStats = null;
            if (wave == null)
            {
                return false;
            }

            wave.ApplyMissingDefaults();
            if (!wave.TryGetSpawnEntryForWave(absoluteWaveIndex, weightRoll01, out EnemySpawnEntry entry))
            {
                return false;
            }

            runtimeStats = CreateFromEntry(entry, wave, absoluteWaveIndex, waveElapsedTime);
            return runtimeStats != null;
        }

        public static EnemyRuntimeStats CreateFromEntry(EnemySpawnEntry entry, EnemyWaveConfig wave, float elapsedTime)
        {
            int absoluteWaveIndex = wave != null ? wave.GetFirstWaveIndexInDifficultyTier() : 0;
            float waveElapsedTime = wave != null ? Mathf.Max(0f, elapsedTime - wave.startTime) : 0f;
            return CreateFromEntry(entry, wave, elapsedTime, absoluteWaveIndex, waveElapsedTime);
        }

        public static EnemyRuntimeStats CreateFromEntry(
            EnemySpawnEntry entry,
            EnemyWaveConfig wave,
            int absoluteWaveIndex,
            float waveElapsedTime)
        {
            float elapsedTime = wave != null ? wave.startTime + Mathf.Max(0f, waveElapsedTime) : 0f;
            return CreateFromEntry(entry, wave, elapsedTime, absoluteWaveIndex, waveElapsedTime);
        }

        private static EnemyRuntimeStats CreateFromEntry(
            EnemySpawnEntry entry,
            EnemyWaveConfig wave,
            float elapsedTime,
            int absoluteWaveIndex,
            float waveElapsedTime)
        {
            if (entry == null || entry.enemyConfig == null)
            {
                return null;
            }

            EnemyConfig config = entry.enemyConfig.CreateRuntimeConfig();
            return Create(config, entry, wave, elapsedTime, absoluteWaveIndex, waveElapsedTime);
        }

    }
}
