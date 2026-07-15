using System.Collections.Generic;
using Enemy;
using Pickup.Data;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Pickup
{
    /// <summary>
    /// 局内道具管理器
    /// 负责生成 道具池回收和事件分发
    /// </summary>
    [DisallowMultipleComponent]
    public class PickupManager : MonoBehaviour
    {
        private const string CombatSceneName = "Combat";
        private const string SpawnTimerId = "PickupManager_SpawnTimer";
        private static bool RuntimeEnabled => true;

        private static PickupManager activeInstance;
        private static bool sceneLoadedSubscribed;

        [Header("生成")]
        [SerializeField] private bool autoSpawn = true;
        [SerializeField] private bool guaranteeAmmoPickup = true;
        [SerializeField] private float spawnInterval = 5f;
        [SerializeField] private int maxActivePickupCount = 6;
        [SerializeField] private float spawnRadius = 18f;
        [SerializeField] private float minDistanceFromPlayer = 4f;
        [SerializeField] private float spawnHeightOffset = 0.6f;
        [SerializeField] private float navMeshSampleRadius = 4f;
        [SerializeField] private int spawnPositionAttempts = 18;

        [Header("调试")]
        [SerializeField] private bool debugPickup;

        private readonly List<PickupItemConfig> _configs = new List<PickupItemConfig>();
        private readonly List<BasePickupItem> _activePickups = new List<BasePickupItem>();
        private Timer _spawnTimer;
        private Transform _playerTransform;
        private int _currentWaveIndex = 1;
        private bool _configsLoaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            activeInstance = null;
            sceneLoadedSubscribed = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapAfterSceneLoad()
        {
            if (!RuntimeEnabled)
            {
                return;
            }

            SubscribeSceneLoaded();
            EnsureManagerForActiveScene();
        }

        private static void SubscribeSceneLoaded()
        {
            if (sceneLoadedSubscribed)
            {
                return;
            }

            SceneManager.sceneLoaded += OnSceneLoaded;
            sceneLoadedSubscribed = true;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!RuntimeEnabled)
            {
                return;
            }

            if (scene.name == CombatSceneName)
            {
                EnsureManagerForActiveScene();
            }
        }

        private static void EnsureManagerForActiveScene()
        {
            if (!RuntimeEnabled)
            {
                return;
            }

            if (SceneManager.GetActiveScene().name != CombatSceneName)
            {
                return;
            }

            if (activeInstance != null)
            {
                return;
            }

            activeInstance = FindObjectOfType<PickupManager>();
            if (activeInstance != null)
            {
                return;
            }

            GameObject managerObject = new GameObject(nameof(PickupManager));
            activeInstance = managerObject.AddComponent<PickupManager>();
        }

        public static void EnsureForCurrentScene()
        {
            if (!RuntimeEnabled)
            {
                return;
            }

            SubscribeSceneLoaded();
            EnsureManagerForActiveScene();
        }

        private void Awake()
        {
            if (!RuntimeEnabled)
            {
                enabled = false;
                return;
            }

            if (activeInstance != null && activeInstance != this)
            {
                Destroy(gameObject);
                return;
            }

            activeInstance = this;
            // 兼容 Combat 场景中的旧序列化值 同时保留更快的自定义配置
            spawnInterval = Mathf.Min(spawnInterval, 5f);
            maxActivePickupCount = Mathf.Max(maxActivePickupCount, 6);
            EnsureEffectResolver();
            LoadConfigsIfNeeded();
            CachePlayer();
            SyncWaveIndexFromSpawner();
        }

        private void OnEnable()
        {
            EventCenter.Instance.AddEventListener<EnemyWaveEventData>(GameEvent.EnemyWaveStarted, OnEnemyWaveChanged);
            EventCenter.Instance.AddEventListener<EnemyWaveEventData>(GameEvent.EnemyWaveProgressChanged, OnEnemyWaveChanged);
            StartSpawnTimer();
        }

        private void OnDisable()
        {
            EventCenter.Instance.RemoveEventListener<EnemyWaveEventData>(GameEvent.EnemyWaveStarted, OnEnemyWaveChanged);
            EventCenter.Instance.RemoveEventListener<EnemyWaveEventData>(GameEvent.EnemyWaveProgressChanged, OnEnemyWaveChanged);
            StopSpawnTimer();
            RecycleAllActivePickups();
        }

        private void OnDestroy()
        {
            if (activeInstance == this)
            {
                activeInstance = null;
            }
        }

        public void RegisterActivePickup(BasePickupItem pickupItem)
        {
            if (pickupItem != null && !_activePickups.Contains(pickupItem))
            {
                _activePickups.Add(pickupItem);
            }
        }

        public void CollectPickup(BasePickupItem pickupItem, PlayerController player)
        {
            if (pickupItem == null || pickupItem.Config == null || player == null)
            {
                return;
            }

            RemoveActivePickup(pickupItem);
            EventCenter.Instance.EventTrigger(
                GameEvent.PickupCollected,
                new PickupCollectedEventData(
                    pickupItem.Config,
                    player.gameObject,
                    pickupItem.gameObject,
                    pickupItem.transform.position));
            ReturnPickupToPool(pickupItem);
        }

        public void ExpirePickup(BasePickupItem pickupItem)
        {
            if (pickupItem == null || pickupItem.Config == null)
            {
                return;
            }

            RemoveActivePickup(pickupItem);
            EventCenter.Instance.EventTrigger(
                GameEvent.PickupExpired,
                new PickupExpiredEventData(pickupItem.Config, pickupItem.gameObject, pickupItem.transform.position));
            ReturnPickupToPool(pickupItem);
        }

        private void StartSpawnTimer()
        {
            StopSpawnTimer();
            if (!autoSpawn || SceneManager.GetActiveScene().name != CombatSceneName)
            {
                return;
            }

            MultiTimerManager timerManager = MultiTimerManager.Instance;
            if (timerManager == null)
            {
                return;
            }

            _spawnTimer = timerManager.CreateTimer(SpawnTimerId, false);
            _spawnTimer.SetTargetTime(Mathf.Max(0.1f, spawnInterval));
            _spawnTimer.OnTimeUp += OnSpawnTimerUp;
            _spawnTimer.Start();
        }

        private void StopSpawnTimer()
        {
            if (_spawnTimer == null)
            {
                return;
            }

            _spawnTimer.OnTimeUp -= OnSpawnTimerUp;
            _spawnTimer = null;
            MultiTimerManager timerManager = MultiTimerManager.Instance;
            if (timerManager != null)
            {
                timerManager.RemoveTimer(SpawnTimerId);
            }
        }

        private void OnSpawnTimerUp()
        {
            StopSpawnTimer();
            TrySpawnPickup();
            StartSpawnTimer();
        }

        private void TrySpawnPickup()
        {
            RemoveInvalidActivePickups();
            if (_activePickups.Count >= Mathf.Max(0, maxActivePickupCount))
            {
                return;
            }

            LoadConfigsIfNeeded();
            CachePlayer();
            SyncWaveIndexFromSpawner();

            PickupItemConfig config = RollConfig(ShouldGuaranteeAmmoPickup()
                ? PickupItemType.Ammo
                : (PickupItemType?)null);
            if (config == null)
            {
                DebugLog("[Pickup] 没有可用道具配置");
                return;
            }

            if (!TryFindSpawnPosition(out Vector3 spawnPosition))
            {
                DebugLog("[Pickup] 没有找到可用 NavMesh 生成点");
                return;
            }

            SpawnPickup(config, spawnPosition);
        }

        private void SpawnPickup(PickupItemConfig config, Vector3 position)
        {
            PoolMgr.Instance.GetObjForAB(config.assetBundleName, config.assetName, obj =>
            {
                if (obj == null)
                {
                    return;
                }

                if (this == null || SceneManager.GetActiveScene().name != CombatSceneName)
                {
                    PoolMgr.Instance.pushObj(config.assetName, obj);
                    return;
                }

                obj.transform.SetPositionAndRotation(position, Quaternion.identity);
                BasePickupItem pickupItem = obj.GetComponent<BasePickupItem>();
                if (pickupItem == null)
                {
                    Debug.LogError($"[PickupManager] 道具 Prefab 缺少 BasePickupItem: {config.assetBundleName}/{config.assetName}", obj);
                    PoolMgr.Instance.pushObj(config.assetName, obj);
                    return;
                }

                pickupItem.Init(config, this);
                RegisterActivePickup(pickupItem);
                EventCenter.Instance.EventTrigger(
                    GameEvent.PickupSpawned,
                    new PickupSpawnedEventData(config, obj, position));

                DebugLog($"[Pickup] 生成道具 {config.itemName} Pos={position}");
            });
        }

        private PickupItemConfig RollConfig(PickupItemType? requiredType = null)
        {
            float totalWeight = 0f;
            int currentWave = Mathf.Max(1, _currentWaveIndex);
            PickupItemConfig fallbackConfig = null;
            for (int i = 0; i < _configs.Count; i++)
            {
                PickupItemConfig config = _configs[i];
                if (IsAvailableConfig(config, currentWave, requiredType))
                {
                    fallbackConfig = config;
                    totalWeight += Mathf.Max(0f, config.weight);
                }
            }

            if (totalWeight <= 0f)
            {
                return null;
            }

            float randomWeight = Random.Range(0f, totalWeight);
            for (int i = 0; i < _configs.Count; i++)
            {
                PickupItemConfig config = _configs[i];
                if (!IsAvailableConfig(config, currentWave, requiredType))
                {
                    continue;
                }

                randomWeight -= Mathf.Max(0f, config.weight);
                if (randomWeight <= 0f)
                {
                    return config;
                }
            }

            return fallbackConfig;
        }

        private bool ShouldGuaranteeAmmoPickup()
        {
            if (!guaranteeAmmoPickup)
            {
                return false;
            }

            for (int i = 0; i < _activePickups.Count; i++)
            {
                BasePickupItem pickupItem = _activePickups[i];
                if (pickupItem != null &&
                    pickupItem.Config != null &&
                    pickupItem.Config.itemType == PickupItemType.Ammo)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsAvailableConfig(
            PickupItemConfig config,
            int currentWave,
            PickupItemType? requiredType)
        {
            return config != null &&
                   config.unlockWave <= currentWave &&
                   (!requiredType.HasValue || config.itemType == requiredType.Value);
        }

        private bool TryFindSpawnPosition(out Vector3 position)
        {
            Vector3 center = _playerTransform != null ? _playerTransform.position : Vector3.zero;
            float minDistanceSqr = minDistanceFromPlayer * minDistanceFromPlayer;
            for (int i = 0; i < Mathf.Max(1, spawnPositionAttempts); i++)
            {
                Vector2 randomCircle = Random.insideUnitCircle * Mathf.Max(1f, spawnRadius);
                Vector3 candidate = center + new Vector3(randomCircle.x, 0f, randomCircle.y);
                if (_playerTransform != null && (candidate - center).sqrMagnitude < minDistanceSqr)
                {
                    continue;
                }

                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, navMeshSampleRadius, NavMesh.AllAreas))
                {
                    position = hit.position + Vector3.up * spawnHeightOffset;
                    return true;
                }
            }

            position = Vector3.zero;
            return false;
        }

        private void LoadConfigsIfNeeded()
        {
            if (_configsLoaded)
            {
                return;
            }

            _configs.Clear();
            if (PickupItemConfigLoader.TryLoadConfigs(out List<PickupItemConfig> loadedConfigs))
            {
                _configs.AddRange(loadedConfigs);
            }

            _configsLoaded = true;
            DebugLog($"[Pickup] 道具配置数量 {_configs.Count}");
        }

        private void CachePlayer()
        {
            if (_playerTransform != null)
            {
                return;
            }

            PlayerController player = FindObjectOfType<PlayerController>();
            if (player != null)
            {
                _playerTransform = player.transform;
            }
        }

        private void SyncWaveIndexFromSpawner()
        {
            EnemySpawnManager spawnManager = FindObjectOfType<EnemySpawnManager>();
            if (spawnManager != null)
            {
                _currentWaveIndex = Mathf.Max(1, spawnManager.CurrentWaveIndex);
            }
        }

        private void OnEnemyWaveChanged(EnemyWaveEventData eventData)
        {
            _currentWaveIndex = Mathf.Max(1, eventData.waveIndex);
        }

        private void RemoveActivePickup(BasePickupItem pickupItem)
        {
            _activePickups.Remove(pickupItem);
        }

        private void RemoveInvalidActivePickups()
        {
            for (int i = _activePickups.Count - 1; i >= 0; i--)
            {
                BasePickupItem pickupItem = _activePickups[i];
                if (pickupItem == null || !pickupItem.gameObject.activeInHierarchy)
                {
                    _activePickups.RemoveAt(i);
                }
            }
        }

        private void RecycleAllActivePickups()
        {
            for (int i = _activePickups.Count - 1; i >= 0; i--)
            {
                BasePickupItem pickupItem = _activePickups[i];
                if (pickupItem != null)
                {
                    ReturnPickupToPool(pickupItem);
                }
            }

            _activePickups.Clear();
        }

        private void ReturnPickupToPool(BasePickupItem pickupItem)
        {
            if (pickupItem == null)
            {
                return;
            }

            string poolKey = string.IsNullOrEmpty(pickupItem.PoolKey)
                ? pickupItem.gameObject.name
                : pickupItem.PoolKey;
            pickupItem.ClearRuntime();
            PoolMgr.Instance.pushObj(poolKey, pickupItem.gameObject);
        }

        private void EnsureEffectResolver()
        {
            if (GetComponent<PickupEffectResolver>() == null)
            {
                gameObject.AddComponent<PickupEffectResolver>();
            }
        }

        private void DebugLog(string message)
        {
            if (debugPickup)
            {
                Debug.Log(message, this);
            }
        }
    }
}
