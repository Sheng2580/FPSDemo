using Pickup.Data;
using UnityEngine;

namespace Pickup
{
    /// <summary>
    /// 局内道具基类
    /// 负责触发拾取和生命周期回收
    /// </summary>
    [DisallowMultipleComponent]
    public class BasePickupItem : MonoBehaviour
    {
        private const string TimerPrefix = "PickupLife_";

        [Header("触发器")]
        [SerializeField] private Collider pickupTrigger;

        [Header("调试")]
        [SerializeField] private bool debugPickup;

        private readonly PickupItemRuntimeData _runtimeData = new PickupItemRuntimeData();
        private PickupItemConfig _config;
        private PickupManager _manager;
        private string _lifeTimerId;
        private string _poolKey;
        private bool _initialized;
        private bool _collected;

        public PickupItemConfig Config => _config;
        public string PoolKey => _poolKey;

        public void Init(PickupItemConfig config, PickupManager manager)
        {
            if (config == null || manager == null)
            {
                return;
            }

            ClearLifeTimer();
            _config = config.Clone();
            _config.ApplyMissingDefaults();
            _manager = manager;
            _poolKey = _config.assetName;
            _collected = false;
            _initialized = true;
            _runtimeData.Init(_config, Time.time);

            EnsureTriggerCollider();
            StartLifeTimer();
        }

        public void ClearRuntime()
        {
            ClearLifeTimer();
            _config = null;
            _manager = null;
            _poolKey = string.Empty;
            _initialized = false;
            _collected = false;
        }

        private void OnDisable()
        {
            ClearLifeTimer();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_initialized || _collected || other == null)
            {
                return;
            }

            PlayerController player = ResolvePlayer(other);
            if (player == null)
            {
                DebugLog($"触发器进入但不是玩家 Other={other.name}");
                return;
            }

            DebugLog($"玩家拾取道具 {_config?.itemName} Other={other.name}");
            _collected = true;
            ClearLifeTimer();
            _manager?.CollectPickup(this, player);
        }

        private void EnsureTriggerCollider()
        {
            if (pickupTrigger != null)
            {
                pickupTrigger.isTrigger = true;
                ApplyPickupRadius(pickupTrigger);
                return;
            }

            Collider[] colliders = GetComponents<Collider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider != null && collider.isTrigger)
                {
                    pickupTrigger = collider;
                    ApplyPickupRadius(pickupTrigger);
                    return;
                }
            }

            if (colliders.Length > 0 && colliders[0] != null)
            {
                pickupTrigger = colliders[0];
                pickupTrigger.isTrigger = true;
                ApplyPickupRadius(pickupTrigger);
                return;
            }

            Debug.LogError($"[PickupItem] 道具缺少触发 Collider: {name}", this);
        }

        private void ApplyPickupRadius(Collider targetCollider)
        {
            if (targetCollider == null || _config == null)
            {
                return;
            }

            float radius = Mathf.Max(0.1f, _config.pickupRadius);
            switch (targetCollider)
            {
                case SphereCollider sphereCollider:
                    sphereCollider.radius = radius;
                    break;
                case CapsuleCollider capsuleCollider:
                    capsuleCollider.radius = radius;
                    capsuleCollider.height = Mathf.Max(capsuleCollider.height, radius * 2f);
                    break;
                case BoxCollider boxCollider:
                    boxCollider.size = new Vector3(radius * 2f, Mathf.Max(1f, radius * 2f), radius * 2f);
                    break;
            }
        }

        private void StartLifeTimer()
        {
            if (_config == null || _config.lifeTime <= 0f)
            {
                return;
            }

            MultiTimerManager timerManager = MultiTimerManager.Instance;
            if (timerManager == null)
            {
                return;
            }

            _lifeTimerId = $"{TimerPrefix}{GetInstanceID()}";
            Timer timer = timerManager.CreateTimer(_lifeTimerId, false);
            timer.SetTargetTime(_config.lifeTime);
            timer.OnTimeUp += OnLifeTimeUp;
            timer.Start();
        }

        private void ClearLifeTimer()
        {
            if (string.IsNullOrEmpty(_lifeTimerId))
            {
                return;
            }

            MultiTimerManager timerManager = MultiTimerManager.Instance;
            if (timerManager != null)
            {
                timerManager.RemoveTimer(_lifeTimerId);
            }

            _lifeTimerId = string.Empty;
        }

        private void OnLifeTimeUp()
        {
            if (!_initialized || _collected)
            {
                return;
            }

            ClearLifeTimer();
            _manager?.ExpirePickup(this);
        }

        private PlayerController ResolvePlayer(Collider other)
        {
            PlayerController player = other.GetComponent<PlayerController>();
            player ??= other.GetComponentInParent<PlayerController>();
            player ??= other.GetComponentInChildren<PlayerController>();
            return player;
        }

        private void DebugLog(string message)
        {
            if (debugPickup)
            {
                Debug.Log($"[PickupItem] {message}", this);
            }
        }
    }
}
