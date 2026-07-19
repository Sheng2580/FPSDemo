using Pickup.Data;
using PlayerData;
using UnityEngine;

namespace Pickup
{
    /// <summary>
    /// 道具效果解析器
    /// 把拾取事件转换成玩家效果和提示事件
    /// </summary>
    [DisallowMultipleComponent]
    public class PickupEffectResolver : MonoBehaviour
    {
        private const string BerserkTimerId = "Pickup_Berserk_Timer";
        private const float TipDuration = 1.8f;

        [Header("调试")]
        [SerializeField] private bool debugPickupTip;

        private string _activeBerserkPostProcessKey;
        private Timer _berserkTimer;
        private float _berserkEndTime = -1f;
        private bool _berserkActive;

        public bool IsBerserkActive => _berserkActive && GetBerserkRemainingTime() > 0f;

        private void Update()
        {
            if (_berserkActive && GetBerserkRemainingTime() <= 0f)
            {
                EndBerserk(true);
            }
        }

        private void OnEnable()
        {
            EventCenter.Instance.AddEventListener<PickupCollectedEventData>(GameEvent.PickupCollected, OnPickupCollected);
        }

        private void OnDisable()
        {
            EventCenter.Instance.RemoveEventListener<PickupCollectedEventData>(GameEvent.PickupCollected, OnPickupCollected);
            EndBerserk(true);
        }

        private void OnPickupCollected(PickupCollectedEventData eventData)
        {
            PickupItemConfig config = eventData.config;
            if (config == null)
            {
                return;
            }

            PlayerController player = ResolvePlayer(eventData.collector);
            float displayValue = 0f;
            switch (config.itemType)
            {
                case PickupItemType.Heal:
                    displayValue = ApplyHeal(player, config);
                    break;
                case PickupItemType.Ammo:
                    displayValue = ApplyAmmo(player, config);
                    break;
                case PickupItemType.Grenade:
                    displayValue = ApplyGrenade(player, config);
                    break;
                case PickupItemType.Berserk:
                    displayValue = ApplyBerserk(player, config);
                    break;
            }

            if (debugPickupTip)
            {
                Debug.Log(
                    $"[PickupEffect] 拾取道具 {config.itemName} Type={config.itemType} DisplayValue={displayValue}",
                    this);
            }

            TriggerTip(config, displayValue);
        }

        private float ApplyHeal(PlayerController player, PickupItemConfig config)
        {
            if (player == null)
            {
                return 0f;
            }

            PlayerRuntimeData runtimeData = ResolveRuntimeData(player);
            float multiplier = runtimeData != null ? Mathf.Max(0f, runtimeData.pickupHealingMultiplier) : 1f;
            return player.Heal(Mathf.RoundToInt(config.healValue * multiplier));
        }

        private float ApplyAmmo(PlayerController player, PickupItemConfig config)
        {
            PlayerInventory inventory = ResolveInventory(player);
            if (inventory == null)
            {
                return 0f;
            }

            PlayerRuntimeData runtimeData = ResolveRuntimeData(player);
            float multiplier = runtimeData != null ? Mathf.Max(0f, runtimeData.pickupAmmoMultiplier) : 1f;
            int ammoAmount = Mathf.Max(0, Mathf.RoundToInt(config.ammoAmount * multiplier));
            return inventory.AddReserveAmmoToAllWeapons(ammoAmount);
        }

        private float ApplyGrenade(PlayerController player, PickupItemConfig config)
        {
            PlayerSkillController skillController = ResolveSkillController(player);
            if (skillController == null)
            {
                return 0f;
            }

            return skillController.AddCurrentCount(SkillType.Grenade, config.grenadeAmount);
        }

        private float ApplyBerserk(PlayerController player, PickupItemConfig config)
        {
            return AddBerserkDuration(player, config.berserkDuration, config.postProcessKey);
        }

        public float AddBerserkDuration(PlayerController player, float baseDuration, string postProcessKey)
        {
            PlayerRuntimeData runtimeData = ResolveRuntimeData(player);
            float multiplier = runtimeData != null ? Mathf.Max(0f, runtimeData.berserkDurationMultiplier) : 1f;
            float addedDuration = Mathf.Max(0f, baseDuration * multiplier);
            if (addedDuration <= 0f || float.IsNaN(addedDuration) || float.IsInfinity(addedDuration))
            {
                return 0f;
            }

            float remainingTime = GetBerserkRemainingTime() + addedDuration;
            float nextEndTime = Time.unscaledTime + remainingTime;
            if (float.IsNaN(remainingTime)
                || float.IsInfinity(remainingTime)
                || float.IsNaN(nextEndTime)
                || float.IsInfinity(nextEndTime))
            {
                return 0f;
            }

            if (!string.IsNullOrEmpty(postProcessKey))
            {
                _activeBerserkPostProcessKey = postProcessKey;
            }

            _berserkActive = true;
            _berserkEndTime = nextEndTime;
            RestartBerserkTimer(remainingTime);
            EventCenter.Instance.EventTrigger(
                GameEvent.PlayerBerserkChanged,
                new PlayerBerserkChangedEventData(true, remainingTime, addedDuration, _activeBerserkPostProcessKey));
            return addedDuration;
        }

        private void RestartBerserkTimer(float duration)
        {
            RemoveBerserkTimer();

            MultiTimerManager timerManager = MultiTimerManager.Instance;
            if (timerManager == null)
            {
                return;
            }

            _berserkTimer = timerManager.CreateTimer(BerserkTimerId, true);
            _berserkTimer.SetTargetTime(Mathf.Max(0.1f, duration));
            _berserkTimer.OnTimeUp += OnBerserkTimeUp;
            _berserkTimer.Start();
        }

        private void RemoveBerserkTimer()
        {
            if (_berserkTimer != null)
            {
                _berserkTimer.OnTimeUp -= OnBerserkTimeUp;
                _berserkTimer = null;
                MultiTimerManager timerManager = MultiTimerManager.Instance;
                if (timerManager != null)
                {
                    timerManager.RemoveTimer(BerserkTimerId);
                }
            }
        }

        private void EndBerserk(bool sendEndEvent)
        {
            bool wasActive = _berserkActive;
            RemoveBerserkTimer();
            _berserkActive = false;
            _berserkEndTime = -1f;
            if (sendEndEvent && wasActive)
            {
                EventCenter.Instance.EventTrigger(
                    GameEvent.PlayerBerserkChanged,
                    new PlayerBerserkChangedEventData(false, 0f, 0f, _activeBerserkPostProcessKey));
            }
        }

        private float GetBerserkRemainingTime()
        {
            if (!_berserkActive)
            {
                return 0f;
            }

            return Mathf.Max(0f, _berserkEndTime - Time.unscaledTime);
        }

        private void OnBerserkTimeUp()
        {
            EndBerserk(true);
        }

        private void TriggerTip(PickupItemConfig config, float displayValue)
        {
            string description = BuildDescription(config, displayValue);
            PickupTipEventData tipEventData = new PickupTipEventData(
                config.itemName,
                description,
                config.tipColorKey,
                TipDuration);

            if (UIManager.Instance == null)
            {
                DebugLog("UIManager 为空 直接触发 PickupTipRequested");
                EventCenter.Instance.EventTrigger(GameEvent.PickupTipRequested, tipEventData);
                return;
            }

            UIManager.Instance.OpenPanelAsy<TipCanvas>(_ =>
            {
                DebugLog($"打开 TipCanvas 回调 Canvas={(_ != null)} Item={config.itemName}");
                EventCenter.Instance.EventTrigger(GameEvent.PickupTipRequested, tipEventData);
            });
        }

        private string BuildDescription(PickupItemConfig config, float displayValue)
        {
            string valueText = Mathf.Approximately(displayValue, Mathf.Round(displayValue))
                ? Mathf.RoundToInt(displayValue).ToString()
                : displayValue.ToString("0.#");
            string template = string.IsNullOrEmpty(config.descriptionTemplate)
                ? config.itemName
                : config.descriptionTemplate;

            try
            {
                return string.Format(template, valueText);
            }
            catch
            {
                return $"{template} {valueText}";
            }
        }

        private PlayerController ResolvePlayer(GameObject collector)
        {
            if (collector != null)
            {
                PlayerController player = collector.GetComponent<PlayerController>();
                player ??= collector.GetComponentInParent<PlayerController>();
                player ??= collector.GetComponentInChildren<PlayerController>();
                if (player != null)
                {
                    return player;
                }
            }

            return FindObjectOfType<PlayerController>();
        }

        private PlayerInventory ResolveInventory(PlayerController player)
        {
            if (player != null)
            {
                PlayerInventory inventory = player.GetComponent<PlayerInventory>();
                inventory ??= player.GetComponentInChildren<PlayerInventory>(true);
                if (inventory != null)
                {
                    return inventory;
                }
            }

            return FindObjectOfType<PlayerInventory>();
        }

        private static PlayerRuntimeData ResolveRuntimeData(PlayerController player)
        {
            return player != null && player.Stats != null ? player.Stats.RuntimeData : null;
        }

        private PlayerSkillController ResolveSkillController(PlayerController player)
        {
            if (player != null)
            {
                PlayerSkillController skillController = player.GetComponent<PlayerSkillController>();
                skillController ??= player.GetComponentInChildren<PlayerSkillController>(true);
                if (skillController != null)
                {
                    return skillController;
                }
            }

            return FindObjectOfType<PlayerSkillController>();
        }

        private void DebugLog(string message)
        {
            if (debugPickupTip)
            {
                Debug.Log($"[PickupEffect] {message}", this);
            }
        }
    }
}
