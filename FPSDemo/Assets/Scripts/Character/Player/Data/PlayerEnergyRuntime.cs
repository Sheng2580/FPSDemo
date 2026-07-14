using Combat;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PlayerData
{
    /// <summary>
    /// 玩家局内能量运行时入口
    /// 监听伤害事件并触发能量事件
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerEnergyRuntime : MonoBehaviour
    {
        private const string CombatSceneName = "Combat";
        private const string DefaultConfigPath = "PlayerEnergyConfigs/DefaultPlayerEnergyConfig";

        private static PlayerEnergyRuntime _instance;

        [SerializeField] private PlayerEnergyConfigAsset configAsset;
        [SerializeField] private bool loadDefaultConfigFromResources = true;

        [Header("调试")]
        [SerializeField] private bool enableDebugLevelUpKey = true;
        [SerializeField] private KeyCode debugLevelUpKey = KeyCode.Y;
        [SerializeField] private bool debugLog;

        private PlayerEnergyConfig _config;
        private PlayerEnergyRuntimeData _runtimeData;

        public PlayerEnergyConfig Config => _config;
        public PlayerEnergyRuntimeData RuntimeData => _runtimeData;
        public PlayerEnergyState CurrentState => _runtimeData != null ? _runtimeData.state : PlayerEnergyState.Charging;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            _instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRuntimeInstance()
        {
            if (_instance != null || FindObjectOfType<PlayerEnergyRuntime>() != null)
            {
                return;
            }

            GameObject runtimeObject = new GameObject("PlayerEnergyRuntime");
            runtimeObject.AddComponent<PlayerEnergyRuntime>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            InitRuntimeData();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            EventCenter.Instance.AddEventListener<EnemyDamagedEventData>(GameEvent.EnemyDamaged, OnEnemyDamaged);
            EventCenter.Instance.AddEventListener(GameEvent.PlayerEnergyBlessingSelectRequested, OnBlessingSelectRequested);
            EventCenter.Instance.AddEventListener(GameEvent.PlayerEnergyBlessingSelectCanceled, OnBlessingSelectCanceled);
            EventCenter.Instance.AddEventListener<PlayerEnergyBlessingSelectedEventData>(GameEvent.PlayerEnergyBlessingSelected, OnBlessingSelected);
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            EventCenter.Instance.RemoveEventListener<EnemyDamagedEventData>(GameEvent.EnemyDamaged, OnEnemyDamaged);
            EventCenter.Instance.RemoveEventListener(GameEvent.PlayerEnergyBlessingSelectRequested, OnBlessingSelectRequested);
            EventCenter.Instance.RemoveEventListener(GameEvent.PlayerEnergyBlessingSelectCanceled, OnBlessingSelectCanceled);
            EventCenter.Instance.RemoveEventListener<PlayerEnergyBlessingSelectedEventData>(GameEvent.PlayerEnergyBlessingSelected, OnBlessingSelected);
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == CombatSceneName)
            {
                InitRuntimeData();
            }
        }

        private void Update()
        {
            if (!enableDebugLevelUpKey || !IsDebugLevelUpPressed())
            {
                return;
            }

            ConfirmLevelUp();
        }

        public void InitRuntimeData()
        {
            _config = ResolveConfig();
            int permanentLevel = PlayerProgressSaveService.GetPlayerSharedUpgradeLevel();
            float permanentBonus = PermanentUpgradeConfigLoader.GetPlayerEnergyGainBonus(permanentLevel, 0f);
            permanentBonus = Mathf.Min(permanentBonus, _config.maxPermanentEnergyGainBonus);
            _runtimeData = new PlayerEnergyRuntimeData();
            _runtimeData.InitForNewRun(_config, 1f + permanentBonus);
            TriggerEnergyChanged(0f);
            TriggerStateChanged(PlayerEnergyState.Charging, true);
        }

        public void SetEnergyGainMultiplier(float multiplier)
        {
            if (_runtimeData == null)
            {
                InitRuntimeData();
            }

            _runtimeData.energyGainMultiplier = Mathf.Max(0f, multiplier);
        }

        public void ConfirmLevelUp()
        {
            if (_runtimeData == null || !_runtimeData.isLevelUpReady)
            {
                if (debugLog)
                {
                    Debug.Log("[PlayerEnergy] 当前未达到升级条件", this);
                }

                return;
            }

            float consumedEnergy = _runtimeData.maxEnergy;
            _runtimeData.LevelUpAndReset(_config);
            TriggerEnergyChanged(-consumedEnergy);
            TriggerLevelUp(GameEvent.PlayerEnergyLevelUp);
            TriggerStateChanged(PlayerEnergyState.Charging);

            if (debugLog)
            {
                Debug.Log($"[PlayerEnergy] 确认升级 Level={_runtimeData.level}", this);
            }
        }

        private bool IsDebugLevelUpPressed()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(debugLevelUpKey))
            {
                return true;
            }
#endif

#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.yKey.wasPressedThisFrame;
#else
            return false;
#endif
        }

        private PlayerEnergyConfig ResolveConfig()
        {
            PlayerEnergyConfig runtimeConfig = null;
            if (configAsset != null)
            {
                runtimeConfig = configAsset.CreateRuntimeConfig();
            }
            else if (loadDefaultConfigFromResources)
            {
                PlayerEnergyConfigAsset loadedAsset = Resources.Load<PlayerEnergyConfigAsset>(DefaultConfigPath);
                if (loadedAsset != null)
                {
                    runtimeConfig = loadedAsset.CreateRuntimeConfig();
                }
            }

            runtimeConfig ??= PlayerEnergyConfig.CreateDefault();
            runtimeConfig.ApplyMissingDefaults();
            return runtimeConfig;
        }

        private void OnEnemyDamaged(EnemyDamagedEventData eventData)
        {
            if (_config == null || _runtimeData == null)
            {
                InitRuntimeData();
            }

            if (_runtimeData.state != PlayerEnergyState.Charging)
            {
                return;
            }

            if (_config.onlyGainFromPlayerDamage && !IsPlayerDamage(eventData.damageInfo))
            {
                return;
            }

            float energyGain = _runtimeData.CalculateEnergyGain(eventData.damageInfo.finalDamage, _config);
            float deltaEnergy = _runtimeData.AddEnergy(energyGain);
            if (deltaEnergy <= 0f)
            {
                return;
            }

            TriggerEnergyChanged(deltaEnergy);

            if (!_runtimeData.CanLevelUp())
            {
                return;
            }

            if (_runtimeData.autoLevelUp)
            {
                float consumedEnergy = _runtimeData.maxEnergy;
                _runtimeData.LevelUpAndReset(_config);
                TriggerEnergyChanged(-consumedEnergy);
                TriggerLevelUp(GameEvent.PlayerEnergyLevelUp);
                TriggerStateChanged(PlayerEnergyState.Charging, true);
                return;
            }

            _runtimeData.MarkLevelUpReady();
            TriggerStateChanged(PlayerEnergyState.LevelUpReady);
            TriggerLevelUp(GameEvent.PlayerEnergyLevelUpReady);
        }

        private void OnBlessingSelectRequested()
        {
            if (_runtimeData == null)
            {
                InitRuntimeData();
            }

            if (_runtimeData.state != PlayerEnergyState.LevelUpReady)
            {
                if (debugLog)
                {
                    Debug.Log($"[PlayerEnergy] 当前状态不能选择祝福 State={_runtimeData.state}", this);
                }

                return;
            }

            TriggerStateChanged(PlayerEnergyState.BlessingSelecting);
        }

        private void OnBlessingSelectCanceled()
        {
            if (_runtimeData == null || _runtimeData.state != PlayerEnergyState.BlessingSelecting)
            {
                return;
            }

            TriggerStateChanged(PlayerEnergyState.LevelUpReady);
        }

        private void OnBlessingSelected(PlayerEnergyBlessingSelectedEventData eventData)
        {
            if (_runtimeData == null || _runtimeData.state != PlayerEnergyState.BlessingSelecting)
            {
                return;
            }

            ConfirmLevelUp();
        }

        private bool IsPlayerDamage(DamageInfo damageInfo)
        {
            if (damageInfo.attacker == null)
            {
                return false;
            }

            return damageInfo.attacker.GetComponentInParent<PlayerController>() != null;
        }

        private void TriggerEnergyChanged(float deltaEnergy)
        {
            EventCenter.Instance.EventTrigger(
                GameEvent.PlayerEnergyChanged,
                new PlayerEnergyChangedEventData(
                    _runtimeData.currentEnergy,
                    _runtimeData.currentEnergy,
                    _runtimeData.level,
                    deltaEnergy,
                    _runtimeData.maxEnergy));
        }

        private void TriggerLevelUp(GameEvent gameEvent)
        {
            EventCenter.Instance.EventTrigger(
                gameEvent,
                new PlayerEnergyLevelUpEventData(
                    _runtimeData.level,
                    _runtimeData.currentEnergy,
                    _runtimeData.maxEnergy,
                    _runtimeData.autoLevelUp));
        }

        private void TriggerStateChanged(PlayerEnergyState targetState, bool force = false)
        {
            if (_runtimeData == null)
            {
                return;
            }

            PlayerEnergyState previousState = _runtimeData.state;
            if (!force && previousState == targetState)
            {
                return;
            }

            _runtimeData.state = targetState;
            EventCenter.Instance.EventTrigger(
                GameEvent.PlayerEnergyStateChanged,
                new PlayerEnergyStateChangedEventData(
                    previousState,
                    targetState,
                    _runtimeData.level,
                    _runtimeData.currentEnergy,
                    _runtimeData.maxEnergy));
        }
    }
}
