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
        private float _nextSpawnTime;
        private float _battleStartTime;

        private void Awake()
        {
            _battleStartTime = Time.time;
            EnsurePool();
            CachePlayerTarget();
            LoadWaveConfigsIfNeeded();
            PreloadEnemyPrefabsFromAssetBundle();
        }

        private void Update()
        {
            if (!autoSpawn)
            {
                return;
            }

            CachePlayerTarget();
            RemoveInvalidActiveEnemies();

            if (playerTarget == null || Time.time < _nextSpawnTime)
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

                if (useWaveConfigs && wave != null && SpawnOneFromWave(wave, elapsedTime))
                {
                    continue;
                }

                SpawnOneLegacy();
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

        private void SpawnOne()
        {
            SpawnOneLegacy();
        }

        private bool SpawnOneFromWave(EnemyWaveConfig wave, float elapsedTime)
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

            enemy.transform.SetPositionAndRotation(GetSpawnPosition(), Quaternion.identity);
            enemy.gameObject.SetActive(true);
            enemy.InitFromSpawner(runtimeStats, playerTarget, this, enemyPool, prefab);
            _activeEnemies.Add(enemy);
            IncreaseAliveCount(runtimeStats.enemyId);
            return true;
        }

        private void SpawnOneLegacy()
        {
            EnemySpawnDefinition definition = PickDefinition();
            if (definition == null)
            {
                return;
            }

            GameObject prefab = ResolvePrefab(definition);
            if (prefab == null)
            {
                return;
            }

            EnemyController enemy = enemyPool.Get(prefab);
            if (enemy == null)
            {
                return;
            }

            enemy.transform.SetPositionAndRotation(GetSpawnPosition(), Quaternion.identity);
            enemy.gameObject.SetActive(true);
            enemy.InitFromSpawner(definition, playerTarget, this, enemyPool, prefab);
            _activeEnemies.Add(enemy);
            IncreaseAliveCount(definition.enemyId);
        }

        private EnemySpawnDefinition PickDefinition()
        {
            if (spawnDefinitions == null || spawnDefinitions.Count == 0)
            {
                return null;
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
            if (spawnPoints != null && spawnPoints.Count > 0)
            {
                Transform spawnPoint = spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Count)];
                if (spawnPoint != null)
                {
                    return SampleNavMeshPosition(spawnPoint.position);
                }
            }

            Vector2 randomCircle = UnityEngine.Random.insideUnitCircle.normalized * fallbackSpawnDistance;
            Vector3 fallbackPosition = playerTarget.position + new Vector3(randomCircle.x, 0f, randomCircle.y);
            return SampleNavMeshPosition(fallbackPosition);
        }

        private Vector3 SampleNavMeshPosition(Vector3 position)
        {
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            {
                return hit.position;
            }

            return position;
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
