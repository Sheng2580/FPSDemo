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
        [SerializeField] private int sceneMaxEnemyCount = 12;
        [SerializeField] private int spawnCountPerBatch = 1;
        [SerializeField] private float spawnInterval = 3f;
        [SerializeField] private float fallbackSpawnDistance = 12f;
        [SerializeField] private float returnToPoolDelay = 2.5f;

        [Header("场景测试生成")]
        [SerializeField] private bool enableKeyboardSpawnTest = true;
        [SerializeField] private bool disableAutoSpawnWhenKeyboardTesting = true;
        [SerializeField] private KeyCode singleSpawnKey = KeyCode.S;
        [SerializeField] private bool singleSpawnRequiresShift = true;
        [SerializeField] private KeyCode burstSpawnKey = KeyCode.B;
        [SerializeField] private int burstSpawnCount = 10;
        [SerializeField] private float singleSpawnClearRadius = 2.5f;
        [SerializeField] private bool blockSingleSpawnPointWhenPlayerNear = true;
        [SerializeField] private float singleSpawnPlayerBlockRadius = 8f;
        [SerializeField] private float burstSpawnClearRadius = 0.6f;
        [SerializeField] private float burstSpawnScatterRadius = 6f;
        [SerializeField] private int burstSpawnMaxAttemptsMultiplier = 4;
        [SerializeField] private bool burstSpawnIgnoreWaveLimit = true;
        [SerializeField] private int burstSpawnHardMaxEnemyCount = 60;
        [SerializeField] private float spawnPointSampleRadius = 4f;
        [SerializeField] private bool requireCompletePathToPlayer = true;
        [SerializeField] private string singleSpawnPointNameKeyword = "BirthplaceS";
        [SerializeField] private string burstSpawnPointNameKeyword = "BirthplaceB";
        [SerializeField] private bool autoCollectSpawnPoints = true;

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

        private void Awake()
        {
            _battleStartTime = Time.time;
            EnsureSpawnPath();
            EnsurePool();
            CachePlayerTarget();
            CacheSpawnPointsIfNeeded();
            ApplyKeyboardTestMode();
            LoadWaveConfigsIfNeeded();
            PreloadEnemyPrefabsFromAssetBundle();
        }

        private void Update()
        {
            CachePlayerTarget();
            CacheSpawnPointsIfNeeded();
            RemoveInvalidActiveEnemies();
            TickKeyboardSpawnTest();

            if (!autoSpawn || playerTarget == null || Time.time < _nextSpawnTime)
            {
                return;
            }

            float elapsedTime = GetElapsedTime();
            EnemyWaveConfig wave = ResolveActiveWave(elapsedTime);
            _nextSpawnTime = Time.time + ResolveSpawnInterval(wave);

            int batchCount = ResolveSpawnCount(wave, elapsedTime);
            int maxCount = ResolveSceneMaxEnemyCount(wave);
            for (int i = 0; i < batchCount; i++)
            {
                if (_activeEnemies.Count >= maxCount)
                {
                    break;
                }

                Transform spawnPoint = PickSpawnPoint(null, singleSpawnClearRadius, 0f, true);
                if (spawnPoint == null && HasSceneSpawnPoints())
                {
                    break;
                }

                if (useWaveConfigs && wave != null && SpawnOneFromWave(wave, elapsedTime, spawnPoint, singleSpawnClearRadius, 0f))
                {
                    continue;
                }

                SpawnOneLegacy(spawnPoint, singleSpawnClearRadius, 0f);
            }
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

        private bool SpawnOneFromWave(EnemyWaveConfig wave, float elapsedTime)
        {
            return SpawnOneFromWave(wave, elapsedTime, null, singleSpawnClearRadius, 0f);
        }

        private bool SpawnOneFromWave(
            EnemyWaveConfig wave,
            float elapsedTime,
            Transform spawnPoint,
            float clearRadius,
            float scatterRadius)
        {
            if (!EnemyRuntimeStats.TryCreateFromWave(wave, elapsedTime, out EnemyRuntimeStats runtimeStats))
            {
                return false;
            }

            if (!CanSpawnRuntimeStats(runtimeStats))
            {
                return false;
            }

            GameObject prefab = ResolvePrefab(runtimeStats);
            if (prefab == null)
            {
                return false;
            }

            EnemyController enemy = enemyPool.Get(prefab);
            if (enemy == null)
            {
                return false;
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

        private bool SpawnOneLegacy()
        {
            return SpawnOneLegacy(null, singleSpawnClearRadius, 0f);
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

        private float ResolveSpawnInterval(EnemyWaveConfig wave)
        {
            return wave != null ? Mathf.Max(0.1f, wave.spawnInterval) : Mathf.Max(0.1f, spawnInterval);
        }

        private int ResolveSpawnCount(EnemyWaveConfig wave, float elapsedTime)
        {
            return wave != null ? wave.GetSpawnCountForTime(elapsedTime) : Mathf.Max(1, spawnCountPerBatch);
        }

        private int ResolveSceneMaxEnemyCount(EnemyWaveConfig wave)
        {
            return wave != null ? Mathf.Max(1, wave.sceneMaxEnemyCount) : Mathf.Max(1, sceneMaxEnemyCount);
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
            if (!enableKeyboardSpawnTest)
            {
                return;
            }

            if (IsSingleSpawnKeyDown())
            {
                TrySpawnSingleForTest();
            }

            if (Input.GetKeyDown(burstSpawnKey))
            {
                TrySpawnBurstForTest();
            }
        }

        private bool IsSingleSpawnKeyDown()
        {
            if (singleSpawnKey == KeyCode.None || !Input.GetKeyDown(singleSpawnKey))
            {
                return false;
            }

            if (!singleSpawnRequiresShift)
            {
                return true;
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

            Transform spawnPoint = PickSpawnPoint(singleSpawnPointNameKeyword, singleSpawnClearRadius, 0f, true);
            if (spawnPoint == null)
            {
                Debug.LogWarning("[EnemySpawn] 没有找到可用的 BirthplaceS，可能是出生点附近有敌人或玩家太近", this);
                return;
            }

            bool spawned = TrySpawnOneAtPoint(spawnPoint, singleSpawnClearRadius, 0f);
            Debug.Log(spawned
                ? $"[EnemySpawn] S 测试生成成功 Point={spawnPoint.name}"
                : "[EnemySpawn] S 测试生成失败，资源可能还在异步加载或出生点不可达",
                this);
        }

        private void TrySpawnBurstForTest()
        {
            int maxCount = burstSpawnIgnoreWaveLimit
                ? Mathf.Max(1, burstSpawnHardMaxEnemyCount)
                : ResolveCurrentSceneMaxEnemyCount();
            int remainCapacity = Mathf.Max(0, maxCount - _activeEnemies.Count);
            int targetCount = Mathf.Min(Mathf.Max(1, burstSpawnCount), remainCapacity);
            if (targetCount <= 0)
            {
                Debug.Log("[EnemySpawn] 场景敌人已到上限，B 批量生成跳过", this);
                return;
            }

            Transform spawnPoint = PickSpawnPoint(burstSpawnPointNameKeyword, burstSpawnClearRadius, burstSpawnScatterRadius, false);
            if (spawnPoint == null)
            {
                Debug.LogWarning("[EnemySpawn] 没有找到可用的 BirthplaceB，B 批量生成失败", this);
                return;
            }

            Debug.LogWarning(
                $"[EnemySpawn] B 批量生成触发 Point={spawnPoint.name} Target={targetCount} Active={_activeEnemies.Count} Max={maxCount}",
                this);

            int spawnedCount = 0;
            int maxAttempts = Mathf.Max(targetCount, targetCount * Mathf.Max(1, burstSpawnMaxAttemptsMultiplier));
            for (int i = 0; i < maxAttempts && spawnedCount < targetCount; i++)
            {
                if (_activeEnemies.Count >= maxCount)
                {
                    break;
                }

                bool spawned = burstSpawnIgnoreWaveLimit
                    ? TrySpawnOneLegacyForTest(spawnPoint, burstSpawnClearRadius, burstSpawnScatterRadius)
                    : TrySpawnOneAtPoint(spawnPoint, burstSpawnClearRadius, burstSpawnScatterRadius);
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
            Transform spawnPoint = PickSpawnPoint(null, singleSpawnClearRadius, 0f, false);
            if (spawnPoint != null && TryResolveSpawnPosition(spawnPoint, singleSpawnClearRadius, 0f, out Vector3 spawnPosition))
            {
                return spawnPosition;
            }

            Vector2 randomCircle = UnityEngine.Random.insideUnitCircle.normalized * fallbackSpawnDistance;
            Vector3 fallbackPosition = playerTarget.position + new Vector3(randomCircle.x, 0f, randomCircle.y);
            return SampleNavMeshPosition(fallbackPosition);
        }

        private void CacheSpawnPointsIfNeeded()
        {
            if (!autoCollectSpawnPoints)
            {
                return;
            }

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

        private void ApplyKeyboardTestMode()
        {
            if (!enableKeyboardSpawnTest || !disableAutoSpawnWhenKeyboardTesting)
            {
                return;
            }

            autoSpawn = false;
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
            if (!blockSingleSpawnPointWhenPlayerNear
                || spawnPoint == null
                || playerTarget == null
                || singleSpawnPlayerBlockRadius <= 0f
                || !IsSpawnPointNameMatch(spawnPoint.name, singleSpawnPointNameKeyword))
            {
                return false;
            }

            Vector3 offset = playerTarget.position - spawnPoint.position;
            offset.y = 0f;
            return offset.sqrMagnitude <= singleSpawnPlayerBlockRadius * singleSpawnPlayerBlockRadius;
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
            if (!requireCompletePathToPlayer || playerTarget == null)
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
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, spawnPointSampleRadius, NavMesh.AllAreas))
            {
                samplePosition = hit.position;
                return true;
            }

            samplePosition = position;
            return false;
        }

        private IEnumerator ReturnEnemyAfterDelay(EnemyController enemy)
        {
            if (returnToPoolDelay > 0f)
            {
                yield return new WaitForSeconds(returnToPoolDelay);
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
