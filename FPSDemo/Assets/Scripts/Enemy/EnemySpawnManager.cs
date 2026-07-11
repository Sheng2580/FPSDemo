using System;
using System.Collections;
using System.Collections.Generic;
using Enemy.Data;
using UnityEngine;
using UnityEngine.AI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Enemy
{
    /// <summary>
    /// 敌人生成入口，负责读取波次数据、生成运行时数值、控制场景上限和回池
    /// </summary>
    public class EnemySpawnManager : MonoBehaviour
    {
        private const string DefaultEnemyPrefabBundleName = "enemy_prefabs";
        private const int FallbackSceneMaxEnemyCount = 12;
        private const int FallbackSpawnCountPerBatch = 1;
        private const int BurstSpawnCountForTest = 10;
        private const int BurstSpawnMaxAttemptsMultiplier = 4;
        private const int BurstSpawnHardMaxEnemyCount = 60;
        private const float FallbackSpawnInterval = 3f;
        private const float FallbackSpawnDistance = 12f;
        private const float ReturnToPoolDelay = 2.5f;
        private const float SingleSpawnClearRadius = 2.5f;
        private const float SingleSpawnPlayerBlockRadius = 8f;
        private const float BurstSpawnClearRadius = 0.6f;
        private const float BurstSpawnScatterRadius = 6f;
        private const float SpawnPointSampleRadius = 4f;
        private const string SingleSpawnPointNameKeyword = "BirthplaceS";
        private const string BurstSpawnPointNameKeyword = "BirthplaceB";
        private const KeyCode SingleSpawnKey = KeyCode.S;
        private const KeyCode BurstSpawnKey = KeyCode.B;
#if UNITY_EDITOR
        private const string EditorEnemyPrefabFolder = "Assets/Art/ABRes/Enemies/Prefabs";
#endif

        [Serializable]
        public class EnemySpawnDefinition
        {
            public int enemyId = 1001;
            public string enemyName = "Zombie Skeleton";
            public string prefabKey = "ZombieSkeletonOneHanded";
            public string prefabResourceKey = "Enemy_ZombieSkeleton_LOD2";
            public GameObject prefab;
            public float weight = 100f;
            public int goldReward = 1;
            public float maxHealth = 100f;
            public float moveSpeed = 2.2f;
            public float angularSpeed = 360f;
            public float acceleration = 12f;
            public float attackDamage = 10f;
            public float attackDistance = 1.4f;
            public float attackInterval = 1.2f;
            public float attackHitDelay = 0.35f;
        }

        [Serializable]
        public class EnemyPrefabBinding
        {
            public string prefabKey;
            public string prefabResourceKey;
            public GameObject prefab;

            public bool Matches(EnemyRuntimeStats runtimeStats)
            {
                if (runtimeStats == null || prefab == null)
                {
                    return false;
                }

                return (!string.IsNullOrEmpty(prefabKey) && prefabKey == runtimeStats.prefabKey)
                       || (!string.IsNullOrEmpty(prefabResourceKey) && prefabResourceKey == runtimeStats.prefabResourceKey);
            }
        }

        [Header("生成")]
        [SerializeField] private bool autoSpawn = true;
        [SerializeField] private bool useWaveConfigs = true;

        [Header("引用")]
        [SerializeField] private Transform playerTarget;
        [SerializeField] private EnemyPool enemyPool;
        [SerializeField] private List<Transform> spawnPoints = new List<Transform>();
        [SerializeField] private List<EnemyWaveConfigAsset> waveConfigs = new List<EnemyWaveConfigAsset>();
        [SerializeField] private List<EnemyPrefabBinding> prefabBindings = new List<EnemyPrefabBinding>();
        [SerializeField] private List<EnemySpawnDefinition> spawnDefinitions = new List<EnemySpawnDefinition>();

        [Header("AssetBundle")]
        [SerializeField] private bool loadPrefabsFromAssetBundle = true;
        [SerializeField] private string enemyPrefabAssetBundleName = DefaultEnemyPrefabBundleName;
#if UNITY_EDITOR
        [SerializeField] private bool preferEditorDirectPrefab = true;
#endif

        private readonly List<EnemyController> _activeEnemies = new List<EnemyController>();
        private readonly Dictionary<int, int> _aliveCountByEnemyId = new Dictionary<int, int>();
        private readonly Dictionary<string, GameObject> _loadedPrefabByResourceKey = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _loadingPrefabResourceKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _missingPrefabWarnings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private NavMeshPath _spawnPath;
        private float _nextSpawnTime;
        private float _battleStartTime;
        private int _currentWaveIndex;
        private int _currentWaveSpawnedCount;
        private int _currentWaveTargetCount;
        private float _currentWaveStartTime;
        private bool _isWaveRunning;
        private bool _isWaitingNextWave;
        private EnemyWaveConfig _currentWave;

        private void Awake()
        {
            _battleStartTime = Time.time;
            EnsureSpawnPath();
            EnsurePool();
            CachePlayerTarget();
            CacheSpawnPointsIfNeeded();
            LoadWaveConfigsIfNeeded();
            PreloadEnemyPrefabsFromAssetBundle();
        }

        private void Update()
        {
            CachePlayerTarget();
            CacheSpawnPointsIfNeeded();
            RemoveInvalidActiveEnemies();
            TickKeyboardSpawnTest();

            if (!autoSpawn || playerTarget == null)
            {
                return;
            }

            if (useWaveConfigs && HasWaveConfigAssets())
            {
                TickWaveAutoSpawn();
                return;
            }

            if (Time.time < _nextSpawnTime)
            {
                return;
            }

            float elapsedTime = GetElapsedTime();
            EnemyWaveConfig wave = ResolveActiveWave(elapsedTime);
            _nextSpawnTime = Time.time + ResolveSpawnInterval(wave);
            int batchCount = ResolveSpawnCount(wave, elapsedTime);
            int maxCount = ResolveSceneMaxEnemyCount(wave);
            TickAutoSpawn(wave, elapsedTime, batchCount, maxCount);
        }

        public void NotifyEnemyDied(EnemyController enemy)
        {
            if (enemy == null)
            {
                return;
            }

            _activeEnemies.Remove(enemy);
            StartCoroutine(ReturnEnemyAfterDelay(enemy));
        }

        private bool SpawnOne()
        {
            return SpawnOneLegacy();
        }

        private void TickWaveAutoSpawn()
        {
            if (_isWaitingNextWave)
            {
                if (Time.time >= _currentWaveStartTime)
                {
                    StartWave(_currentWaveIndex + 1);
                }

                return;
            }

            if (!_isWaveRunning)
            {
                StartWave(1);
            }

            if (_currentWave == null)
            {
                return;
            }

            int maxCount = ResolveSceneMaxEnemyCount(_currentWave);
            if (_currentWaveSpawnedCount >= _currentWaveTargetCount)
            {
                if (_activeEnemies.Count == 0)
                {
                    CompleteCurrentWave();
                }

                return;
            }

            if (_activeEnemies.Count >= maxCount || Time.time < _nextSpawnTime)
            {
                return;
            }

            float waveElapsedTime = Mathf.Max(0f, Time.time - _currentWaveStartTime);
            int batchCount = ResolveSpawnCountForWaveElapsed(_currentWave, waveElapsedTime);
            int remainWaveCount = Mathf.Max(0, _currentWaveTargetCount - _currentWaveSpawnedCount);
            int remainSceneCapacity = Mathf.Max(0, maxCount - _activeEnemies.Count);
            int targetCount = Mathf.Min(Mathf.Max(1, batchCount), remainWaveCount, remainSceneCapacity);
            if (targetCount <= 0)
            {
                return;
            }

            _nextSpawnTime = Time.time + ResolveSpawnInterval(_currentWave);
            int spawnedCount = TickAutoSpawn(_currentWave, waveElapsedTime, targetCount, maxCount);
            if (spawnedCount <= 0)
            {
                return;
            }

            _currentWaveSpawnedCount += spawnedCount;
            EventCenter.Instance.EventTrigger(
                GameEvent.EnemyWaveProgressChanged,
                CreateWaveEventData(0f));
        }

        private void StartWave(int waveIndex)
        {
            _currentWaveIndex = Mathf.Max(1, waveIndex);
            _currentWave = ResolveWaveByAbsoluteIndex(_currentWaveIndex);
            if (_currentWave == null)
            {
                _isWaveRunning = false;
                return;
            }

            _isWaitingNextWave = false;
            _isWaveRunning = true;
            _currentWaveSpawnedCount = 0;
            _currentWaveStartTime = Time.time;
            _currentWaveTargetCount = ResolveWaveTotalSpawnCount(_currentWave, _currentWaveIndex);
            _nextSpawnTime = Time.time;

            Debug.Log(
                $"[EnemyWave] 第 {_currentWaveIndex} 波开始 Tier={ResolveDifficultyTier(_currentWave, _currentWaveIndex)} Total={_currentWaveTargetCount}",
                this);

            EventCenter.Instance.EventTrigger(
                GameEvent.EnemyWaveStarted,
                CreateWaveEventData(0f));
        }

        private void CompleteCurrentWave()
        {
            float delay = ResolveWaveClearDelay(_currentWave);
            Debug.Log(
                $"[EnemyWave] 第 {_currentWaveIndex} 波清理完成 NextDelay={delay:0.##}",
                this);

            EventCenter.Instance.EventTrigger(
                GameEvent.EnemyWaveCleared,
                CreateWaveEventData(delay));

            _isWaveRunning = false;
            _isWaitingNextWave = true;
            _currentWaveStartTime = Time.time + delay;

            EventCenter.Instance.EventTrigger(
                GameEvent.EnemyWavePreparing,
                CreateWaveEventData(delay, _currentWaveIndex + 1));
        }

        private int TickAutoSpawn(EnemyWaveConfig wave, float elapsedTime, int batchCount, int maxCount)
        {
            if (_activeEnemies.Count >= maxCount)
            {
                return 0;
            }

            int burstSpawnedCount = TryAutoSpawnFromBurstPoint(wave, elapsedTime, batchCount, maxCount);
            if (burstSpawnedCount > 0)
            {
                return burstSpawnedCount;
            }

            return TryAutoSpawnFromSinglePoints(wave, elapsedTime, batchCount, maxCount);
        }

        private int TryAutoSpawnFromBurstPoint(EnemyWaveConfig wave, float elapsedTime, int batchCount, int maxCount)
        {
            Transform spawnPoint = PickSpawnPoint(BurstSpawnPointNameKeyword, BurstSpawnClearRadius, BurstSpawnScatterRadius, false);
            if (spawnPoint == null)
            {
                return 0;
            }

            int remainCapacity = Mathf.Max(0, maxCount - _activeEnemies.Count);
            int targetCount = Mathf.Min(Mathf.Max(1, batchCount), remainCapacity);
            int spawnedCount = SpawnBatchAtPoint(
                wave,
                elapsedTime,
                spawnPoint,
                targetCount,
                BurstSpawnClearRadius,
                BurstSpawnScatterRadius);

            if (spawnedCount > 0)
            {
                Debug.Log(
                    $"[EnemySpawn] 自动 B 点批量生成 Point={spawnPoint.name} Spawned={spawnedCount}/{targetCount}",
                    this);
                return spawnedCount;
            }

            return 0;
        }

        private int TryAutoSpawnFromSinglePoints(EnemyWaveConfig wave, float elapsedTime, int batchCount, int maxCount)
        {
            int spawnedCount = 0;
            int targetCount = Mathf.Max(1, batchCount);
            for (int i = 0; i < targetCount; i++)
            {
                if (_activeEnemies.Count >= maxCount)
                {
                    break;
                }

                Transform spawnPoint = PickSpawnPoint(SingleSpawnPointNameKeyword, SingleSpawnClearRadius, 0f, true);
                if (spawnPoint == null && HasSceneSpawnPoints())
                {
                    break;
                }

                if (TrySpawnOneByCurrentMode(wave, elapsedTime, spawnPoint, SingleSpawnClearRadius, 0f))
                {
                    spawnedCount++;
                    continue;
                }

                break;
            }

            return spawnedCount;
        }

        private int SpawnBatchAtPoint(
            EnemyWaveConfig wave,
            float elapsedTime,
            Transform spawnPoint,
            int targetCount,
            float clearRadius,
            float scatterRadius)
        {
            int spawnedCount = 0;
            int maxAttempts = Mathf.Max(targetCount, targetCount * Mathf.Max(1, BurstSpawnMaxAttemptsMultiplier));
            for (int i = 0; i < maxAttempts && spawnedCount < targetCount; i++)
            {
                if (TrySpawnOneByCurrentMode(wave, elapsedTime, spawnPoint, clearRadius, scatterRadius))
                {
                    spawnedCount++;
                }
            }

            return spawnedCount;
        }

        private bool TrySpawnOneByCurrentMode(
            EnemyWaveConfig wave,
            float elapsedTime,
            Transform spawnPoint,
            float clearRadius,
            float scatterRadius)
        {
            if (useWaveConfigs && wave != null && SpawnOneFromWave(wave, elapsedTime, spawnPoint, clearRadius, scatterRadius))
            {
                return true;
            }

            return SpawnOneLegacy(spawnPoint, clearRadius, scatterRadius);
        }

        private bool SpawnOneFromWave(EnemyWaveConfig wave, float elapsedTime)
        {
            return SpawnOneFromWave(wave, elapsedTime, null, SingleSpawnClearRadius, 0f);
        }

        private bool SpawnOneFromWave(
            EnemyWaveConfig wave,
            float elapsedTime,
            Transform spawnPoint,
            float clearRadius,
            float scatterRadius)
        {
            int absoluteWaveIndex = wave != null ? Mathf.Max(1, wave.waveIndex) : 1;
            float waveElapsedTime = wave != null ? Mathf.Max(0f, elapsedTime - wave.startTime) : 0f;
            if (_isWaveRunning && ReferenceEquals(wave, _currentWave))
            {
                absoluteWaveIndex = _currentWaveIndex;
                waveElapsedTime = Mathf.Max(0f, Time.time - _currentWaveStartTime);
            }

            const int maxRuntimePickAttempts = 8;
            for (int i = 0; i < maxRuntimePickAttempts; i++)
            {
                if (!EnemyRuntimeStats.TryCreateFromWave(wave, absoluteWaveIndex, waveElapsedTime, out EnemyRuntimeStats runtimeStats))
                {
                    return false;
                }

                if (!CanSpawnRuntimeStats(runtimeStats))
                {
                    continue;
                }

                GameObject prefab = ResolvePrefab(runtimeStats);
                if (prefab == null)
                {
                    continue;
                }

                EnemyController enemy = enemyPool.Get(prefab);
                if (enemy == null)
                {
                    continue;
                }

                if (!TryResolveSpawnPositionForSpawn(spawnPoint, clearRadius, scatterRadius, out Vector3 spawnPosition))
                {
                    enemyPool.Return(prefab, enemy);
                    return false;
                }

                enemy.transform.SetPositionAndRotation(spawnPosition, Quaternion.identity);
                enemy.gameObject.SetActive(true);
                enemy.InitFromSpawner(runtimeStats, playerTarget, this, enemyPool, prefab);
                _activeEnemies.Add(enemy);
                IncreaseAliveCount(runtimeStats.enemyId);
                return true;
            }

            return false;
        }

        private bool SpawnOneLegacy()
        {
            return SpawnOneLegacy(null, SingleSpawnClearRadius, 0f);
        }

        private bool SpawnOneLegacy(Transform spawnPoint, float clearRadius, float scatterRadius)
        {
            return SpawnOneLegacy(spawnPoint, clearRadius, scatterRadius, false);
        }

        private bool SpawnOneLegacy(
            Transform spawnPoint,
            float clearRadius,
            float scatterRadius,
            bool allowOccupiedFallback)
        {
            EnemySpawnDefinition definition = PickDefinition();
            if (definition == null)
            {
                return false;
            }

            GameObject prefab = ResolvePrefab(definition);
            if (prefab == null)
            {
                return false;
            }

            EnemyController enemy = enemyPool.Get(prefab);
            if (enemy == null)
            {
                return false;
            }

            if (!TryResolveSpawnPositionForSpawn(
                    spawnPoint,
                    clearRadius,
                    scatterRadius,
                    allowOccupiedFallback,
                    out Vector3 spawnPosition))
            {
                enemyPool.Return(prefab, enemy);
                return false;
            }

            enemy.transform.SetPositionAndRotation(spawnPosition, Quaternion.identity);
            enemy.gameObject.SetActive(true);
            enemy.InitFromSpawner(definition, playerTarget, this, enemyPool, prefab);
            _activeEnemies.Add(enemy);
            IncreaseAliveCount(definition.enemyId);
            return true;
        }

        private EnemySpawnDefinition PickDefinition()
        {
            if (spawnDefinitions == null || spawnDefinitions.Count == 0)
            {
                return CreateDefaultSpawnDefinition();
            }

            float totalWeight = 0f;
            for (int i = 0; i < spawnDefinitions.Count; i++)
            {
                EnemySpawnDefinition definition = spawnDefinitions[i];
                if (definition != null && CanResolveDefinitionPrefab(definition))
                {
                    totalWeight += Mathf.Max(0f, definition.weight);
                }
            }

            if (totalWeight <= 0f)
            {
                return null;
            }

            float randomWeight = UnityEngine.Random.Range(0f, totalWeight);
            for (int i = 0; i < spawnDefinitions.Count; i++)
            {
                EnemySpawnDefinition definition = spawnDefinitions[i];
                if (definition == null || !CanResolveDefinitionPrefab(definition))
                {
                    continue;
                }

                randomWeight -= Mathf.Max(0f, definition.weight);
                if (randomWeight <= 0f)
                {
                    return definition;
                }
            }

            return spawnDefinitions[spawnDefinitions.Count - 1];
        }

        private static EnemySpawnDefinition CreateDefaultSpawnDefinition()
        {
            return new EnemySpawnDefinition();
        }

        private EnemyWaveConfig ResolveActiveWave(float elapsedTime)
        {
            if (!useWaveConfigs || waveConfigs == null || waveConfigs.Count == 0)
            {
                return null;
            }

            EnemyWaveConfig fallback = null;
            for (int i = 0; i < waveConfigs.Count; i++)
            {
                EnemyWaveConfigAsset asset = waveConfigs[i];
                if (asset == null || asset.Config == null)
                {
                    continue;
                }

                EnemyWaveConfig config = asset.Config;
                config.ApplyMissingDefaults();
                if (config.IsInTime(elapsedTime))
                {
                    return config;
                }

                if (fallback == null || config.startTime > fallback.startTime)
                {
                    fallback = config;
                }
            }

            return fallback != null && elapsedTime >= fallback.startTime ? fallback : null;
        }

        private bool HasWaveConfigAssets()
        {
            return waveConfigs != null && waveConfigs.Count > 0;
        }

        private EnemyWaveConfig ResolveWaveByAbsoluteIndex(int absoluteWaveIndex)
        {
            if (!HasWaveConfigAssets())
            {
                return null;
            }

            int safeWaveIndex = Mathf.Max(1, absoluteWaveIndex);
            int targetTier = ResolveDifficultyTierForWave(safeWaveIndex);
            EnemyWaveConfig fallback = null;
            int fallbackTier = 0;

            for (int i = 0; i < waveConfigs.Count; i++)
            {
                EnemyWaveConfigAsset asset = waveConfigs[i];
                EnemyWaveConfig config = asset != null ? asset.Config : null;
                if (config == null)
                {
                    continue;
                }

                config.ApplyMissingDefaults();
                int configTier = Mathf.Max(1, config.difficultyTierIndex);
                if (configTier == targetTier)
                {
                    return config;
                }

                if (configTier < targetTier && configTier > fallbackTier)
                {
                    fallback = config;
                    fallbackTier = configTier;
                }
            }

            return fallback ?? ResolveFirstAvailableWave();
        }

        private EnemyWaveConfig ResolveFirstAvailableWave()
        {
            if (!HasWaveConfigAssets())
            {
                return null;
            }

            EnemyWaveConfig fallback = null;
            int fallbackTier = int.MaxValue;
            for (int i = 0; i < waveConfigs.Count; i++)
            {
                EnemyWaveConfigAsset asset = waveConfigs[i];
                EnemyWaveConfig config = asset != null ? asset.Config : null;
                if (config == null)
                {
                    continue;
                }

                config.ApplyMissingDefaults();
                int configTier = Mathf.Max(1, config.difficultyTierIndex);
                if (fallback == null || configTier < fallbackTier)
                {
                    fallback = config;
                    fallbackTier = configTier;
                }
            }

            return fallback;
        }

        private int ResolveDifficultyTierForWave(int absoluteWaveIndex)
        {
            int safeWaveIndex = Mathf.Max(1, absoluteWaveIndex);
            int wavesPerTier = ResolveWavesPerDifficultyTier();
            return Mathf.Max(1, Mathf.CeilToInt(safeWaveIndex / (float)wavesPerTier));
        }

        private int ResolveWavesPerDifficultyTier()
        {
            if (!HasWaveConfigAssets())
            {
                return 1;
            }

            for (int i = 0; i < waveConfigs.Count; i++)
            {
                EnemyWaveConfig config = waveConfigs[i] != null ? waveConfigs[i].Config : null;
                if (config != null)
                {
                    return Mathf.Max(1, config.wavesPerDifficultyTier);
                }
            }

            return 1;
        }

        private int ResolveWaveTotalSpawnCount(EnemyWaveConfig wave, int absoluteWaveIndex)
        {
            return wave != null ? wave.GetTotalSpawnCountForWave(absoluteWaveIndex) : FallbackSpawnCountPerBatch;
        }

        private int ResolveDifficultyTier(EnemyWaveConfig wave, int absoluteWaveIndex)
        {
            return ResolveDifficultyTierForWave(absoluteWaveIndex);
        }

        private float ResolveWaveClearDelay(EnemyWaveConfig wave)
        {
            return wave != null ? Mathf.Max(0f, wave.waveClearDelay) : 0f;
        }

        private float ResolveSpawnInterval(EnemyWaveConfig wave)
        {
            return wave != null ? Mathf.Max(0.1f, wave.spawnInterval) : FallbackSpawnInterval;
        }

        private int ResolveSpawnCount(EnemyWaveConfig wave, float elapsedTime)
        {
            return wave != null ? wave.GetSpawnCountForTime(elapsedTime) : FallbackSpawnCountPerBatch;
        }

        private int ResolveSpawnCountForWaveElapsed(EnemyWaveConfig wave, float waveElapsedTime)
        {
            return wave != null ? wave.GetSpawnCountForWaveElapsed(waveElapsedTime) : FallbackSpawnCountPerBatch;
        }

        private int ResolveSceneMaxEnemyCount(EnemyWaveConfig wave)
        {
            return wave != null ? Mathf.Max(1, wave.sceneMaxEnemyCount) : FallbackSceneMaxEnemyCount;
        }

        private EnemyWaveEventData CreateWaveEventData(float delay)
        {
            return CreateWaveEventData(delay, _currentWaveIndex);
        }

        private EnemyWaveEventData CreateWaveEventData(float delay, int waveIndex)
        {
            EnemyWaveConfig wave = waveIndex == _currentWaveIndex ? _currentWave : ResolveWaveByAbsoluteIndex(waveIndex);
            int targetCount = waveIndex == _currentWaveIndex
                ? _currentWaveTargetCount
                : ResolveWaveTotalSpawnCount(wave, waveIndex);
            int spawnedCount = waveIndex == _currentWaveIndex ? _currentWaveSpawnedCount : 0;

            return new EnemyWaveEventData(
                waveIndex,
                ResolveDifficultyTier(wave, waveIndex),
                targetCount,
                spawnedCount,
                _activeEnemies.Count,
                delay,
                wave);
        }

        private bool CanSpawnRuntimeStats(EnemyRuntimeStats runtimeStats)
        {
            if (runtimeStats == null)
            {
                return false;
            }

            int currentAlive = _aliveCountByEnemyId.TryGetValue(runtimeStats.enemyId, out int count) ? count : 0;
            return currentAlive < Mathf.Max(1, runtimeStats.entryMaxAliveCount);
        }

        private GameObject ResolvePrefab(EnemyRuntimeStats runtimeStats)
        {
            if (runtimeStats == null)
            {
                return null;
            }

            if (TryGetAssetBundlePrefab(runtimeStats.prefabResourceKey, out GameObject prefab))
            {
                return prefab;
            }

            if (loadPrefabsFromAssetBundle && !string.IsNullOrEmpty(runtimeStats.prefabResourceKey))
            {
                BeginLoadEnemyPrefab(runtimeStats.prefabResourceKey);
                if (TryGetAssetBundlePrefab(runtimeStats.prefabResourceKey, out prefab))
                {
                    return prefab;
                }

                return null;
            }

            return ResolveDirectPrefab(runtimeStats);
        }

        private GameObject ResolvePrefab(EnemySpawnDefinition definition)
        {
            if (definition == null)
            {
                return null;
            }

            if (TryGetAssetBundlePrefab(definition.prefabResourceKey, out GameObject prefab))
            {
                return prefab;
            }

            if (loadPrefabsFromAssetBundle && !string.IsNullOrEmpty(definition.prefabResourceKey))
            {
                BeginLoadEnemyPrefab(definition.prefabResourceKey);
                if (TryGetAssetBundlePrefab(definition.prefabResourceKey, out prefab))
                {
                    return prefab;
                }

                return null;
            }

            return definition.prefab;
        }

        private GameObject ResolveDirectPrefab(EnemyRuntimeStats runtimeStats)
        {
            if (prefabBindings != null)
            {
                for (int i = 0; i < prefabBindings.Count; i++)
                {
                    EnemyPrefabBinding binding = prefabBindings[i];
                    if (binding != null && binding.Matches(runtimeStats))
                    {
                        return binding.prefab;
                    }
                }
            }

            if (spawnDefinitions != null)
            {
                for (int i = 0; i < spawnDefinitions.Count; i++)
                {
                    EnemySpawnDefinition definition = spawnDefinitions[i];
                    if (definition == null || definition.prefab == null)
                    {
                        continue;
                    }

                    if (definition.enemyId == runtimeStats.enemyId
                        || definition.prefabKey == runtimeStats.prefabKey
                        || definition.prefabResourceKey == runtimeStats.prefabResourceKey)
                    {
                        return definition.prefab;
                    }
                }
            }

            if (spawnDefinitions != null && spawnDefinitions.Count > 0 && spawnDefinitions[0] != null)
            {
                Debug.LogWarning(
                    $"[EnemySpawn] 使用第一个敌人 Prefab 作为临时兜底 Key={runtimeStats.prefabKey}",
                    this);
                return spawnDefinitions[0].prefab;
            }

            return null;
        }

        private bool CanResolveDefinitionPrefab(EnemySpawnDefinition definition)
        {
            if (definition == null)
            {
                return false;
            }

            return definition.prefab != null || !string.IsNullOrEmpty(definition.prefabResourceKey);
        }

        private void PreloadEnemyPrefabsFromAssetBundle()
        {
            if (!loadPrefabsFromAssetBundle)
            {
                return;
            }

            HashSet<string> keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CollectPrefabKeys(keys);

            foreach (string key in keys)
            {
                BeginLoadEnemyPrefab(key);
            }
        }

        private void CollectPrefabKeys(HashSet<string> keys)
        {
            if (keys == null)
            {
                return;
            }

            if (prefabBindings != null)
            {
                for (int i = 0; i < prefabBindings.Count; i++)
                {
                    EnemyPrefabBinding binding = prefabBindings[i];
                    AddPrefabKey(keys, binding?.prefabResourceKey);
                }
            }

            if (spawnDefinitions != null)
            {
                for (int i = 0; i < spawnDefinitions.Count; i++)
                {
                    EnemySpawnDefinition definition = spawnDefinitions[i];
                    AddPrefabKey(keys, definition?.prefabResourceKey);
                }
            }

            if (waveConfigs == null)
            {
                return;
            }

            for (int i = 0; i < waveConfigs.Count; i++)
            {
                EnemyWaveConfigAsset waveAsset = waveConfigs[i];
                EnemyWaveConfig wave = waveAsset != null ? waveAsset.Config : null;
                if (wave?.entries == null)
                {
                    continue;
                }

                for (int j = 0; j < wave.entries.Count; j++)
                {
                    EnemySpawnEntry entry = wave.entries[j];
                    EnemyConfig config = entry?.enemyConfig != null ? entry.enemyConfig.CreateRuntimeConfig() : null;
                    AddPrefabKey(keys, config?.prefabResourceKey);
                }
            }

            if (keys.Count == 0)
            {
                AddPrefabKey(keys, CreateDefaultSpawnDefinition().prefabResourceKey);
            }
        }

        private void AddPrefabKey(HashSet<string> keys, string key)
        {
            if (keys == null || string.IsNullOrEmpty(key))
            {
                return;
            }

            keys.Add(key);
        }

        private bool TryGetAssetBundlePrefab(string resourceKey, out GameObject prefab)
        {
            prefab = null;
            if (string.IsNullOrEmpty(resourceKey))
            {
                return false;
            }

            return _loadedPrefabByResourceKey.TryGetValue(resourceKey, out prefab) && prefab != null;
        }

        private void BeginLoadEnemyPrefab(string resourceKey)
        {
            if (!loadPrefabsFromAssetBundle || string.IsNullOrEmpty(resourceKey))
            {
                return;
            }

            if (_loadedPrefabByResourceKey.ContainsKey(resourceKey) || !_loadingPrefabResourceKeys.Add(resourceKey))
            {
                return;
            }

#if UNITY_EDITOR
            if (preferEditorDirectPrefab && TryLoadEditorEnemyPrefab(resourceKey, out GameObject editorPrefab))
            {
                _loadingPrefabResourceKeys.Remove(resourceKey);
                _loadedPrefabByResourceKey[resourceKey] = editorPrefab;
                return;
            }
#endif

            ABManager.Instance.LoadAssetAsync<GameObject>(enemyPrefabAssetBundleName, resourceKey, loadedPrefab =>
            {
                _loadingPrefabResourceKeys.Remove(resourceKey);
                if (loadedPrefab != null)
                {
                    _loadedPrefabByResourceKey[resourceKey] = loadedPrefab;
                    return;
                }

                if (TryLoadEditorEnemyPrefab(resourceKey, out GameObject editorPrefab))
                {
                    _loadedPrefabByResourceKey[resourceKey] = editorPrefab;
                    return;
                }

                WarnMissingEnemyPrefab(resourceKey);
            });
        }

        private bool TryLoadEditorEnemyPrefab(string resourceKey, out GameObject prefab)
        {
#if UNITY_EDITOR
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{EditorEnemyPrefabFolder}/{resourceKey}.prefab");
            return prefab != null;
#else
            prefab = null;
            return false;
#endif
        }

        private void WarnMissingEnemyPrefab(string resourceKey)
        {
            if (string.IsNullOrEmpty(resourceKey) || !_missingPrefabWarnings.Add(resourceKey))
            {
                return;
            }

            Debug.LogWarning($"[EnemySpawn] 找不到敌人 AB Prefab ResourceKey={resourceKey}", this);
        }

        private float GetElapsedTime()
        {
            return Mathf.Max(0f, Time.time - _battleStartTime);
        }

        private void IncreaseAliveCount(int enemyId)
        {
            if (!_aliveCountByEnemyId.ContainsKey(enemyId))
            {
                _aliveCountByEnemyId.Add(enemyId, 0);
            }

            _aliveCountByEnemyId[enemyId]++;
        }

        private void RebuildAliveCounts()
        {
            _aliveCountByEnemyId.Clear();
            for (int i = 0; i < _activeEnemies.Count; i++)
            {
                EnemyController enemy = _activeEnemies[i];
                if (enemy == null || !enemy.IsActive)
                {
                    continue;
                }

                IncreaseAliveCount(enemy.EnemyId);
            }
        }

        private void TickKeyboardSpawnTest()
        {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            return;
#endif

            if (IsSingleSpawnKeyDown())
            {
                TrySpawnSingleForTest();
            }

            if (Input.GetKeyDown(BurstSpawnKey))
            {
                TrySpawnBurstForTest();
            }
        }

        private bool IsSingleSpawnKeyDown()
        {
            if (!Input.GetKeyDown(SingleSpawnKey))
            {
                return false;
            }

            return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        }

        private void TrySpawnSingleForTest()
        {
            if (_activeEnemies.Count >= ResolveCurrentSceneMaxEnemyCount())
            {
                Debug.Log("[EnemySpawn] 场景敌人已到上限，S 测试生成跳过", this);
                return;
            }

            Transform spawnPoint = PickSpawnPoint(SingleSpawnPointNameKeyword, SingleSpawnClearRadius, 0f, true);
            if (spawnPoint == null)
            {
                Debug.LogWarning("[EnemySpawn] 没有找到可用的 BirthplaceS，可能是出生点附近有敌人或玩家太近", this);
                return;
            }

            bool spawned = TrySpawnOneAtPoint(spawnPoint, SingleSpawnClearRadius, 0f);
            Debug.Log(spawned
                ? $"[EnemySpawn] S 测试生成成功 Point={spawnPoint.name}"
                : "[EnemySpawn] S 测试生成失败，资源可能还在异步加载或出生点不可达",
                this);
        }

        private void TrySpawnBurstForTest()
        {
            int maxCount = Mathf.Max(1, BurstSpawnHardMaxEnemyCount);
            int remainCapacity = Mathf.Max(0, maxCount - _activeEnemies.Count);
            int targetCount = Mathf.Min(Mathf.Max(1, BurstSpawnCountForTest), remainCapacity);
            if (targetCount <= 0)
            {
                Debug.Log("[EnemySpawn] 场景敌人已到上限，B 批量生成跳过", this);
                return;
            }

            Transform spawnPoint = PickSpawnPoint(BurstSpawnPointNameKeyword, BurstSpawnClearRadius, BurstSpawnScatterRadius, false);
            if (spawnPoint == null)
            {
                Debug.LogWarning("[EnemySpawn] 没有找到可用的 BirthplaceB，B 批量生成失败", this);
                return;
            }

            Debug.LogWarning(
                $"[EnemySpawn] B 批量生成触发 Point={spawnPoint.name} Target={targetCount} Active={_activeEnemies.Count} Max={maxCount}",
                this);

            int spawnedCount = 0;
            int maxAttempts = Mathf.Max(targetCount, targetCount * Mathf.Max(1, BurstSpawnMaxAttemptsMultiplier));
            for (int i = 0; i < maxAttempts && spawnedCount < targetCount; i++)
            {
                if (_activeEnemies.Count >= maxCount)
                {
                    break;
                }

                bool spawned = TrySpawnOneLegacyForTest(spawnPoint, BurstSpawnClearRadius, BurstSpawnScatterRadius);
                if (spawned)
                {
                    spawnedCount++;
                }
            }

            Debug.LogWarning(
                $"[EnemySpawn] B 批量生成完成 Point={spawnPoint.name} Spawned={spawnedCount}/{targetCount}",
                this);
        }

        private bool TrySpawnOneLegacyForTest(Transform spawnPoint, float clearRadius, float scatterRadius)
        {
            CachePlayerTarget();
            if (playerTarget == null)
            {
                Debug.LogWarning("[EnemySpawn] 找不到玩家目标，跳过本次 B 测试生成", this);
                return false;
            }

            return SpawnOneLegacy(spawnPoint, clearRadius, scatterRadius, true);
        }

        private bool TrySpawnOneAtPoint(Transform spawnPoint, float clearRadius, float scatterRadius)
        {
            CachePlayerTarget();
            if (playerTarget == null)
            {
                Debug.LogWarning("[EnemySpawn] 找不到玩家目标，跳过本次生成", this);
                return false;
            }

            float elapsedTime = GetElapsedTime();
            EnemyWaveConfig wave = ResolveActiveWave(elapsedTime);
            if (useWaveConfigs && wave != null && SpawnOneFromWave(wave, elapsedTime, spawnPoint, clearRadius, scatterRadius))
            {
                return true;
            }

            return SpawnOneLegacy(spawnPoint, clearRadius, scatterRadius);
        }

        private void LoadWaveConfigsIfNeeded()
        {
            if (!useWaveConfigs || (waveConfigs != null && waveConfigs.Count > 0))
            {
                return;
            }

            EnemyWaveConfigAsset[] assets = Resources.LoadAll<EnemyWaveConfigAsset>("EnemyWaves");
            if (assets == null || assets.Length == 0)
            {
                return;
            }

            waveConfigs = new List<EnemyWaveConfigAsset>(assets);
            waveConfigs.Sort((left, right) =>
            {
                float leftTime = left != null && left.Config != null ? left.Config.startTime : 0f;
                float rightTime = right != null && right.Config != null ? right.Config.startTime : 0f;
                return leftTime.CompareTo(rightTime);
            });
        }

        private Vector3 GetSpawnPosition()
        {
            Transform spawnPoint = PickSpawnPoint(null, SingleSpawnClearRadius, 0f, false);
            if (spawnPoint != null && TryResolveSpawnPosition(spawnPoint, SingleSpawnClearRadius, 0f, out Vector3 spawnPosition))
            {
                return spawnPosition;
            }

            Vector2 randomCircle = UnityEngine.Random.insideUnitCircle.normalized * FallbackSpawnDistance;
            Vector3 fallbackPosition = playerTarget.position + new Vector3(randomCircle.x, 0f, randomCircle.y);
            return SampleNavMeshPosition(fallbackPosition);
        }

        private void CacheSpawnPointsIfNeeded()
        {
            spawnPoints ??= new List<Transform>();
            for (int i = spawnPoints.Count - 1; i >= 0; i--)
            {
                if (spawnPoints[i] == null)
                {
                    spawnPoints.RemoveAt(i);
                }
            }

            Transform[] children = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                Transform child = children[i];
                if (child == null || child == transform)
                {
                    continue;
                }

                if (!child.name.StartsWith("Birthplace", StringComparison.OrdinalIgnoreCase)
                    || spawnPoints.Contains(child))
                {
                    continue;
                }

                spawnPoints.Add(child);
            }
        }

        private Transform PickSpawnPoint(string nameKeyword, float clearRadius, float scatterRadius, bool requireClear)
        {
            CacheSpawnPointsIfNeeded();
            if (spawnPoints == null || spawnPoints.Count == 0)
            {
                return null;
            }

            int startIndex = UnityEngine.Random.Range(0, spawnPoints.Count);
            for (int i = 0; i < spawnPoints.Count; i++)
            {
                Transform spawnPoint = spawnPoints[(startIndex + i) % spawnPoints.Count];
                if (spawnPoint == null)
                {
                    continue;
                }

                if (!IsSpawnPointNameMatch(spawnPoint.name, nameKeyword))
                {
                    continue;
                }

                if (ShouldBlockSpawnPointByPlayerDistance(spawnPoint))
                {
                    continue;
                }

                if (!requireClear || TryResolveSpawnPosition(spawnPoint, clearRadius, scatterRadius, out _))
                {
                    return spawnPoint;
                }
            }

            return null;
        }

        private static bool IsSpawnPointNameMatch(string spawnPointName, string nameKeyword)
        {
            if (string.IsNullOrEmpty(nameKeyword))
            {
                return true;
            }

            if (string.IsNullOrEmpty(spawnPointName))
            {
                return false;
            }

            if (spawnPointName.IndexOf(nameKeyword, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            // 兼容旧代码里的 BirthplaceBig 和场景里实际命名的 BirthplaceB
            bool keywordIsBigBirthplace = nameKeyword.Equals("BirthplaceBig", StringComparison.OrdinalIgnoreCase)
                                          || nameKeyword.Equals("BirthplaceB", StringComparison.OrdinalIgnoreCase);
            if (!keywordIsBigBirthplace)
            {
                return false;
            }

            return spawnPointName.Equals("BirthplaceB", StringComparison.OrdinalIgnoreCase)
                   || spawnPointName.Equals("BirthplaceBig", StringComparison.OrdinalIgnoreCase);
        }

        private bool ShouldBlockSpawnPointByPlayerDistance(Transform spawnPoint)
        {
            if (spawnPoint == null
                || playerTarget == null
                || !IsSpawnPointNameMatch(spawnPoint.name, SingleSpawnPointNameKeyword))
            {
                return false;
            }

            Vector3 offset = playerTarget.position - spawnPoint.position;
            offset.y = 0f;
            return offset.sqrMagnitude <= SingleSpawnPlayerBlockRadius * SingleSpawnPlayerBlockRadius;
        }

        private bool TryResolveSpawnPosition(
            Transform spawnPoint,
            float clearRadius,
            float scatterRadius,
            out Vector3 spawnPosition)
        {
            return TryResolveSpawnPosition(spawnPoint, clearRadius, scatterRadius, false, out spawnPosition);
        }

        private bool TryResolveSpawnPosition(
            Transform spawnPoint,
            float clearRadius,
            float scatterRadius,
            bool allowOccupiedFallback,
            out Vector3 spawnPosition)
        {
            Vector3 center = spawnPoint != null
                ? spawnPoint.position
                : playerTarget != null ? playerTarget.position : transform.position;

            int attempts = Mathf.Max(1, scatterRadius > 0f ? 16 : 1);
            Vector3 fallbackPosition = SampleNavMeshPosition(center);
            bool hasFallbackPosition = HasCompletePathToPlayer(fallbackPosition);
            for (int i = 0; i < attempts; i++)
            {
                Vector3 candidate = center;
                if (scatterRadius > 0f)
                {
                    Vector2 offset = UnityEngine.Random.insideUnitCircle * scatterRadius;
                    candidate += new Vector3(offset.x, 0f, offset.y);
                }

                candidate = SampleNavMeshPosition(candidate);
                if (HasCompletePathToPlayer(candidate))
                {
                    fallbackPosition = candidate;
                    hasFallbackPosition = true;
                }

                if (!IsSpawnPositionClear(candidate, clearRadius))
                {
                    continue;
                }

                if (!HasCompletePathToPlayer(candidate))
                {
                    continue;
                }

                spawnPosition = candidate;
                return true;
            }

            if (allowOccupiedFallback && hasFallbackPosition)
            {
                spawnPosition = fallbackPosition;
                return true;
            }

            spawnPosition = center;
            return false;
        }

        private bool TryResolveSpawnPositionForSpawn(
            Transform spawnPoint,
            float clearRadius,
            float scatterRadius,
            out Vector3 spawnPosition)
        {
            return TryResolveSpawnPositionForSpawn(
                spawnPoint,
                clearRadius,
                scatterRadius,
                false,
                out spawnPosition);
        }

        private bool TryResolveSpawnPositionForSpawn(
            Transform spawnPoint,
            float clearRadius,
            float scatterRadius,
            bool allowOccupiedFallback,
            out Vector3 spawnPosition)
        {
            if (spawnPoint != null)
            {
                return TryResolveSpawnPosition(
                    spawnPoint,
                    clearRadius,
                    scatterRadius,
                    allowOccupiedFallback,
                    out spawnPosition);
            }

            if (!HasSceneSpawnPoints())
            {
                spawnPosition = GetSpawnPosition();
                return true;
            }

            spawnPosition = Vector3.zero;
            return false;
        }

        private bool HasSceneSpawnPoints()
        {
            return spawnPoints != null && spawnPoints.Count > 0;
        }

        private bool IsSpawnPositionClear(Vector3 position, float clearRadius)
        {
            float radiusSqr = Mathf.Max(0f, clearRadius) * Mathf.Max(0f, clearRadius);
            if (radiusSqr <= 0f)
            {
                return true;
            }

            for (int i = 0; i < _activeEnemies.Count; i++)
            {
                EnemyController enemy = _activeEnemies[i];
                if (enemy == null || !enemy.IsActive)
                {
                    continue;
                }

                Vector3 offset = enemy.transform.position - position;
                offset.y = 0f;
                if (offset.sqrMagnitude <= radiusSqr)
                {
                    return false;
                }
            }

            return true;
        }

        private bool HasCompletePathToPlayer(Vector3 position)
        {
            if (playerTarget == null)
            {
                return true;
            }

            EnsureSpawnPath();

            if (!TrySampleNavMeshPosition(position, out Vector3 sourcePosition)
                || !TrySampleNavMeshPosition(playerTarget.position, out Vector3 targetPosition))
            {
                return false;
            }

            if (!NavMesh.CalculatePath(sourcePosition, targetPosition, NavMesh.AllAreas, _spawnPath))
            {
                return false;
            }

            return _spawnPath.status == NavMeshPathStatus.PathComplete;
        }

        private void EnsureSpawnPath()
        {
            // NavMeshPath 不能在 MonoBehaviour 字段初始化时创建，Unity 要求放到运行期创建
            _spawnPath ??= new NavMeshPath();
        }

        private Vector3 SampleNavMeshPosition(Vector3 position)
        {
            if (TrySampleNavMeshPosition(position, out Vector3 samplePosition))
            {
                return samplePosition;
            }

            return position;
        }

        private bool TrySampleNavMeshPosition(Vector3 position, out Vector3 samplePosition)
        {
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, SpawnPointSampleRadius, NavMesh.AllAreas))
            {
                samplePosition = hit.position;
                return true;
            }

            samplePosition = position;
            return false;
        }

        private IEnumerator ReturnEnemyAfterDelay(EnemyController enemy)
        {
            if (ReturnToPoolDelay > 0f)
            {
                yield return new WaitForSeconds(ReturnToPoolDelay);
            }

            if (enemy != null)
            {
                enemy.ReturnToPool();
                EventCenter.Instance.EventTrigger(GameEvent.EnemyReturnedToPool, new EnemyReturnedToPoolEventData(enemy));
            }
        }

        private void CachePlayerTarget()
        {
            if (playerTarget != null)
            {
                return;
            }

            PlayerController player = FindObjectOfType<PlayerController>();
            if (player != null)
            {
                playerTarget = player.transform;
            }
        }

        private int ResolveCurrentSceneMaxEnemyCount()
        {
            if (useWaveConfigs && _currentWave != null)
            {
                return ResolveSceneMaxEnemyCount(_currentWave);
            }

            if (useWaveConfigs && HasWaveConfigAssets())
            {
                EnemyWaveConfig waveByIndex = ResolveWaveByAbsoluteIndex(Mathf.Max(1, _currentWaveIndex));
                return ResolveSceneMaxEnemyCount(waveByIndex);
            }

            EnemyWaveConfig wave = ResolveActiveWave(GetElapsedTime());
            return ResolveSceneMaxEnemyCount(wave);
        }

        private void EnsurePool()
        {
            if (enemyPool != null)
            {
                return;
            }

            enemyPool = FindObjectOfType<EnemyPool>();
            if (enemyPool != null)
            {
                return;
            }

            GameObject poolObject = new GameObject("EnemyPool");
            enemyPool = poolObject.AddComponent<EnemyPool>();
        }

        private void RemoveInvalidActiveEnemies()
        {
            for (int i = _activeEnemies.Count - 1; i >= 0; i--)
            {
                if (_activeEnemies[i] == null || !_activeEnemies[i].IsActive)
                {
                    _activeEnemies.RemoveAt(i);
                }
            }

            RebuildAliveCounts();
        }
    }
}
