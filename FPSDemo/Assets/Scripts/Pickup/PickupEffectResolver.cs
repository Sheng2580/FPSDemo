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
        [SerializeField] private bool debugPickupTip = true;

        private string _activeBerserkPostProcessKey;
        private Timer _berserkTimer;

        private void OnEnable()
        {
            EventCenter.Instance.AddEventListener<PickupCollectedEventData>(GameEvent.PickupCollected, OnPickupCollected);
        }

        private void OnDisable()
        {
            EventCenter.Instance.RemoveEventListener<PickupCollectedEventData>(GameEvent.PickupCollected, OnPickupCollected);
            ClearBerserkTimer(true);
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
                    displayValue = ApplyBerserk(config);
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

            return player.Heal(Mathf.RoundToInt(config.healValue));
        }

        private float ApplyAmmo(PlayerController player, PickupItemConfig config)
        {
            PlayerInventory inventory = ResolveInventory(player);
            if (inventory == null)
            {
                return 0f;
            }

            return inventory.AddReserveAmmoToAllWeapons(config.ammoAmount);
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

        private float ApplyBerserk(PickupItemConfig config)
        {
            float addedDuration = Mathf.Max(0f, config.berserkDuration);
            if (addedDuration <= 0f)
            {
                return 0f;
            }

            float remainingTime = GetBerserkRemainingTime() + addedDuration;
            _activeBerserkPostProcessKey = config.postProcessKey;
            RestartBerserkTimer(remainingTime);
            EventCenter.Instance.EventTrigger(
                GameEvent.PlayerBerserkChanged,
                new PlayerBerserkChangedEventData(true, remainingTime, addedDuration, _activeBerserkPostProcessKey));
            return addedDuration;
        }

        private void RestartBerserkTimer(float duration)
        {
            ClearBerserkTimer(false);

            MultiTimerManager timerManager = MultiTimerManager.Instance;
            if (timerManager == null)
            {
                return;
            }

            _berserkTimer = timerManager.CreateTimer(BerserkTimerId, false);
            _berserkTimer.SetTargetTime(Mathf.Max(0.1f, duration));
            _berserkTimer.OnTimeUp += OnBerserkTimeUp;
            _berserkTimer.Start();
        }

        private void ClearBerserkTimer(bool sendEndEvent)
        {
            float remainingTime = GetBerserkRemainingTime();
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

            if (sendEndEvent && remainingTime > 0f)
            {
                EventCenter.Instance.EventTrigger(
                    GameEvent.PlayerBerserkChanged,
                    new PlayerBerserkChangedEventData(false, 0f, 0f, _activeBerserkPostProcessKey));
            }
        }

        private float GetBerserkRemainingTime()
        {
            if (_berserkTimer == null)
            {
                return 0f;
            }

            return Mathf.Max(0f, _berserkTimer.TargetTime - _berserkTimer.CurrentTime);
        }

        private void OnBerserkTimeUp()
        {
            ClearBerserkTimer(false);
            EventCenter.Instance.EventTrigger(
                GameEvent.PlayerBerserkChanged,
                new PlayerBerserkChangedEventData(false, 0f, 0f, _activeBerserkPostProcessKey));
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
