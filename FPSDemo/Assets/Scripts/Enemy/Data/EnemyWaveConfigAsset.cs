using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Enemy.Data
{
    [CreateAssetMenu(menuName = "FPSDemo/Enemy/Enemy Wave Config", fileName = "EnemyWaveConfig")]
    public class EnemyWaveConfigAsset : ScriptableObject
    {
        [SerializeField] private EnemyWaveConfig config = new EnemyWaveConfig();

        public EnemyWaveConfig Config => config;

        private void OnValidate()
        {
            config ??= new EnemyWaveConfig();
            config.ApplyMissingDefaults();
        }
    }

    /// <summary>
    /// Luban 敌人和波次 Json 读取入口
    /// Json 成功时返回完整运行时配置 SO 只由调用方作为缺表兜底
    /// </summary>
    public static class EnemyJsonConfigLoader
    {
        private const string EnemyConfigJsonFileName = "tbenemy_config";
        private const string EnemyWaveConfigJsonFileName = "tbenemy_wave_config";
        private const string EnemySpawnPoolJsonFileName = "tbenemy_spawn_pool";
        private const string ResourcesJsonFolder = "EnemyJson";

        public static bool TryLoadEnemyConfigs(out List<EnemyConfig> enemyConfigs)
        {
            enemyConfigs = new List<EnemyConfig>();
            if (!TryReadEnemyConfigRows(out EnemyConfigRow[] rows))
            {
                return false;
            }

            for (int i = 0; i < rows.Length; i++)
            {
                EnemyConfig config = ConvertEnemyConfig(rows[i]);
                if (config != null)
                {
                    enemyConfigs.Add(config);
                }
            }

            return enemyConfigs.Count > 0;
        }

        public static bool TryGetEnemyConfig(int enemyId, out EnemyConfig enemyConfig)
        {
            enemyConfig = null;
            if (!TryLoadEnemyConfigs(out List<EnemyConfig> enemyConfigs))
            {
                return false;
            }

            for (int i = 0; i < enemyConfigs.Count; i++)
            {
                if (enemyConfigs[i].enemyId == enemyId)
                {
                    enemyConfig = enemyConfigs[i].Clone();
                    return true;
                }
            }

            return false;
        }

        public static bool TryLoadWaveConfigs(out List<EnemyWaveConfig> waveConfigs)
        {
            waveConfigs = new List<EnemyWaveConfig>();
            if (!TryReadEnemyConfigRows(out EnemyConfigRow[] enemyRows)
                || !TryReadWaveConfigRows(out EnemyWaveConfigRow[] waveRows)
                || !TryReadSpawnPoolRows(out EnemySpawnPoolRow[] poolRows))
            {
                return false;
            }

            Dictionary<int, EnemyConfig> enemyConfigById = new Dictionary<int, EnemyConfig>();
            for (int i = 0; i < enemyRows.Length; i++)
            {
                EnemyConfig enemyConfig = ConvertEnemyConfig(enemyRows[i]);
                if (enemyConfig != null)
                {
                    enemyConfigById[enemyConfig.enemyId] = enemyConfig;
                }
            }

            Dictionary<int, EnemyWaveConfig> waveConfigById = new Dictionary<int, EnemyWaveConfig>();
            for (int i = 0; i < waveRows.Length; i++)
            {
                EnemyWaveConfig waveConfig = ConvertWaveConfig(waveRows[i]);
                if (waveConfig == null)
                {
                    continue;
                }

                waveConfigById[waveRows[i].id] = waveConfig;
                waveConfigs.Add(waveConfig);
            }

            for (int i = 0; i < poolRows.Length; i++)
            {
                EnemySpawnPoolRow row = poolRows[i];
                if (row == null
                    || !waveConfigById.TryGetValue(row.waveConfigId, out EnemyWaveConfig waveConfig)
                    || !enemyConfigById.TryGetValue(row.enemyId, out EnemyConfig enemyConfig))
                {
                    continue;
                }

                EnemySpawnEntry entry = ConvertSpawnEntry(row, enemyConfig);
                if (entry != null)
                {
                    waveConfig.entries.Add(entry);
                }
            }

            for (int i = waveConfigs.Count - 1; i >= 0; i--)
            {
                EnemyWaveConfig waveConfig = waveConfigs[i];
                waveConfig.ApplyMissingDefaults();
                if (waveConfig.entries == null || waveConfig.entries.Count == 0)
                {
                    waveConfigs.RemoveAt(i);
                }
            }

            waveConfigs.Sort((left, right) => left.difficultyTierIndex.CompareTo(right.difficultyTierIndex));
            return waveConfigs.Count > 0;
        }

        private static EnemyConfig ConvertEnemyConfig(EnemyConfigRow row)
        {
            if (row == null || row.enemyId <= 0)
            {
                return null;
            }

            EnemyConfig config = new EnemyConfig
            {
                enemyId = row.enemyId,
                enemyName = row.enemyName,
                prefabKey = row.prefabKey,
                prefabAssetBundleName = row.prefabAssetBundleName,
                prefabResourceKey = row.prefabResourceKey,
                behaviorTreeKey = row.behaviorTreeKey,
                aiProfileKey = row.aiProfileKey,
                bodyPartTemplateKey = row.bodyPartTemplateKey,
                dropPoolKey = row.dropPoolKey,
                maxHealth = row.maxHealth,
                moveSpeed = row.moveSpeed,
                angularSpeed = row.angularSpeed,
                acceleration = row.acceleration,
                attackDamage = row.attackDamage,
                attackDistance = row.attackDistance,
                attackInterval = row.attackInterval,
                attackHitDelay = row.attackHitDelay,
                detectionRange = row.detectionRange,
                goldReward = row.goldReward,
                blessingEnergyReward = row.blessingEnergyReward,
                experienceReward = row.experienceReward,
                hitStunDuration = row.hitStunDuration,
                hitReactionCooldown = row.hitReactionCooldown,
                hitKnockbackDistance = row.hitKnockbackDistance,
                hitKnockbackDuration = row.hitKnockbackDuration,
                idleStateName = row.idleStateName,
                walkStateName = row.walkStateName,
                runStateName = row.runStateName,
                attackStateName = row.attackStateName,
                damageStateName = row.damageStateName,
                deathStateName = row.deathStateName,
                locomotionTransition = row.locomotionTransition,
                attackTransition = row.attackTransition,
                hitTransition = row.hitTransition,
                deathTransition = row.deathTransition,
                recoverTransition = row.recoverTransition,
                headDamageMultiplier = row.headDamageMultiplier,
                bodyDamageMultiplier = row.bodyDamageMultiplier,
                armDamageMultiplier = row.armDamageMultiplier,
                legDamageMultiplier = row.legDamageMultiplier
            };
            config.ApplyMissingDefaults();
            return config;
        }

        private static EnemyWaveConfig ConvertWaveConfig(EnemyWaveConfigRow row)
        {
            if (row == null || row.id <= 0)
            {
                return null;
            }

            EnemyWaveConfig config = new EnemyWaveConfig
            {
                waveIndex = row.waveIndex,
                startTime = row.startTime,
                endTime = row.endTime,
                spawnInterval = row.spawnInterval,
                spawnCountPerBatch = row.spawnCountPerBatch,
                spawnCountGrowthPerMinute = row.spawnCountGrowthPerMinute,
                maxSpawnCountPerBatch = row.maxSpawnCountPerBatch,
                sceneMaxEnemyCount = row.sceneMaxEnemyCount,
                maxNearEnemyCount = row.maxNearEnemyCount,
                maxActiveAgentCount = row.maxActiveAgentCount,
                maxAttackersCount = row.maxAttackersCount,
                difficultyTierIndex = row.difficultyTierIndex,
                wavesPerDifficultyTier = row.wavesPerDifficultyTier,
                waveTotalSpawnCount = row.waveTotalSpawnCount,
                waveTotalSpawnGrowth = row.waveTotalSpawnGrowth,
                waveClearDelay = row.waveClearDelay,
                waitForAvailableSpawnSlot = row.waitForAvailableSpawnSlot,
                entries = new List<EnemySpawnEntry>()
            };
            config.ApplyMissingDefaults();
            return config;
        }

        private static EnemySpawnEntry ConvertSpawnEntry(EnemySpawnPoolRow row, EnemyConfig enemyConfig)
        {
            if (row == null || enemyConfig == null)
            {
                return null;
            }

            EnemySpawnEntry entry = new EnemySpawnEntry
            {
                unlockWaveIndex = row.unlockWaveIndex,
                minWaveIndex = row.minWaveIndex,
                maxWaveIndex = row.maxWaveIndex,
                baseWeight = row.baseWeight,
                weightGrowthPerWave = row.weightGrowthPerWave,
                weightGrowthPerDifficultyTier = row.weightGrowthPerDifficultyTier,
                maxWeight = row.maxWeight,
                weight = row.weight,
                maxAliveCount = row.maxAliveCount,
                healthMultiplier = row.healthMultiplier,
                healthMultiplierGrowthPerWave = row.healthMultiplierGrowthPerWave,
                healthMultiplierGrowthPerDifficultyTier = row.healthMultiplierGrowthPerDifficultyTier,
                maxHealthMultiplier = row.maxHealthMultiplier,
                damageMultiplier = row.damageMultiplier,
                damageMultiplierGrowthPerWave = row.damageMultiplierGrowthPerWave,
                damageMultiplierGrowthPerDifficultyTier = row.damageMultiplierGrowthPerDifficultyTier,
                maxDamageMultiplier = row.maxDamageMultiplier,
                moveSpeedMultiplier = row.moveSpeedMultiplier,
                moveSpeedMultiplierGrowthPerWave = row.moveSpeedMultiplierGrowthPerWave,
                moveSpeedMultiplierGrowthPerDifficultyTier = row.moveSpeedMultiplierGrowthPerDifficultyTier,
                maxMoveSpeedMultiplier = row.maxMoveSpeedMultiplier,
                goldMultiplier = row.goldMultiplier,
                goldMultiplierGrowthPerWave = row.goldMultiplierGrowthPerWave,
                goldMultiplierGrowthPerDifficultyTier = row.goldMultiplierGrowthPerDifficultyTier,
                maxGoldMultiplier = row.maxGoldMultiplier
            };
            entry.ConfigureRuntimeEnemyConfig(enemyConfig);
            entry.ApplyMissingDefaults();
            return entry;
        }

        private static bool TryReadEnemyConfigRows(out EnemyConfigRow[] rows)
        {
            rows = Array.Empty<EnemyConfigRow>();
            if (!TryReadJson(EnemyConfigJsonFileName, out string json))
            {
                return false;
            }

            try
            {
                EnemyConfigRowList list = JsonUtility.FromJson<EnemyConfigRowList>(WrapArrayJson(json));
                rows = list?.rows ?? Array.Empty<EnemyConfigRow>();
                return rows.Length > 0;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[EnemyJsonConfigLoader] Json解析失败 {EnemyConfigJsonFileName} {exception.Message}");
                rows = Array.Empty<EnemyConfigRow>();
                return false;
            }
        }

        private static bool TryReadWaveConfigRows(out EnemyWaveConfigRow[] rows)
        {
            rows = Array.Empty<EnemyWaveConfigRow>();
            if (!TryReadJson(EnemyWaveConfigJsonFileName, out string json))
            {
                return false;
            }

            try
            {
                EnemyWaveConfigRowList list = JsonUtility.FromJson<EnemyWaveConfigRowList>(WrapArrayJson(json));
                rows = list?.rows ?? Array.Empty<EnemyWaveConfigRow>();
                return rows.Length > 0;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[EnemyJsonConfigLoader] Json解析失败 {EnemyWaveConfigJsonFileName} {exception.Message}");
                rows = Array.Empty<EnemyWaveConfigRow>();
                return false;
            }
        }

        private static bool TryReadSpawnPoolRows(out EnemySpawnPoolRow[] rows)
        {
            rows = Array.Empty<EnemySpawnPoolRow>();
            if (!TryReadJson(EnemySpawnPoolJsonFileName, out string json))
            {
                return false;
            }

            try
            {
                EnemySpawnPoolRowList list = JsonUtility.FromJson<EnemySpawnPoolRowList>(WrapArrayJson(json));
                rows = list?.rows ?? Array.Empty<EnemySpawnPoolRow>();
                return rows.Length > 0;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[EnemyJsonConfigLoader] Json解析失败 {EnemySpawnPoolJsonFileName} {exception.Message}");
                rows = Array.Empty<EnemySpawnPoolRow>();
                return false;
            }
        }

        private static bool TryReadJson(string fileName, out string json)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            string generatedJsonPath = Path.Combine(projectRoot, "MiniTemplate", "GeneratedJson");
            return JsonMgr.Instance.TryLoadJsonText(
                fileName,
                out json,
                string.Empty,
                ResourcesJsonFolder,
                generatedJsonPath);
        }

        private static string WrapArrayJson(string json)
        {
            return "{\"rows\":" + json.Trim() + "}";
        }

        [Serializable]
        private sealed class EnemyConfigRowList
        {
            public EnemyConfigRow[] rows;
        }

        [Serializable]
        private sealed class EnemyWaveConfigRowList
        {
            public EnemyWaveConfigRow[] rows;
        }

        [Serializable]
        private sealed class EnemySpawnPoolRowList
        {
            public EnemySpawnPoolRow[] rows;
        }

        [Serializable]
        private sealed class EnemyConfigRow
        {
            public int enemyId;
            public string enemyName;
            public string prefabKey;
            public string prefabAssetBundleName;
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
        }

        [Serializable]
        private sealed class EnemyWaveConfigRow
        {
            public int id;
            public int waveIndex;
            public float startTime;
            public float endTime;
            public float spawnInterval;
            public int spawnCountPerBatch;
            public float spawnCountGrowthPerMinute;
            public int maxSpawnCountPerBatch;
            public int sceneMaxEnemyCount;
            public int maxNearEnemyCount;
            public int maxActiveAgentCount;
            public int maxAttackersCount;
            public int difficultyTierIndex;
            public int wavesPerDifficultyTier;
            public int waveTotalSpawnCount;
            public int waveTotalSpawnGrowth;
            public float waveClearDelay;
            public bool waitForAvailableSpawnSlot;
        }

        [Serializable]
        private sealed class EnemySpawnPoolRow
        {
            public int id;
            public int waveConfigId;
            public int enemyId;
            public int unlockWaveIndex;
            public int minWaveIndex;
            public int maxWaveIndex;
            public float baseWeight;
            public float weightGrowthPerWave;
            public float weightGrowthPerDifficultyTier;
            public float maxWeight;
            public float weight;
            public int maxAliveCount;
            public float healthMultiplier;
            public float healthMultiplierGrowthPerWave;
            public float healthMultiplierGrowthPerDifficultyTier;
            public float maxHealthMultiplier;
            public float damageMultiplier;
            public float damageMultiplierGrowthPerWave;
            public float damageMultiplierGrowthPerDifficultyTier;
            public float maxDamageMultiplier;
            public float moveSpeedMultiplier;
            public float moveSpeedMultiplierGrowthPerWave;
            public float moveSpeedMultiplierGrowthPerDifficultyTier;
            public float maxMoveSpeedMultiplier;
            public float goldMultiplier;
            public float goldMultiplierGrowthPerWave;
            public float goldMultiplierGrowthPerDifficultyTier;
            public float maxGoldMultiplier;
        }
    }
}
