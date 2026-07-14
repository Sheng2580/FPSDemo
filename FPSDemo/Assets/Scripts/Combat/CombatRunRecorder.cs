using System;
using PlayerData;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 当前战斗统计入口
    /// 记录本局结算面板需要的战斗数据
    /// </summary>
    public sealed class CombatRunRecorder : IDisposable
    {
        private const string BattleTimerId = "CombatRunRecorder_BattleTimer";

        public static CombatRunRecorder Active { get; private set; }

        private readonly PlayerController _player;
        private readonly PlayerInventory _inventory;
        private Timer _battleTimer;
        private int _killCount;
        private int _blessingSelectCount;
        private int _pickupCollectedCount;
        private int _weaponFireCount;
        private int _lastPublishedSecond = -1;
        private bool _completed;
        private bool _disposed;

        public float SurvivalSeconds => _battleTimer != null ? _battleTimer.CurrentTime : 0f;
        public int KillCount => _killCount;
        public PlayerController Player => _player;

        public CombatRunRecorder(PlayerController player)
        {
            Active?.Dispose();
            Active = this;
            _player = player;
            _inventory = player != null ? player.Inventory : null;

            EventCenter.Instance.AddEventListener<EnemyDiedEventData>(GameEvent.EnemyDied, OnEnemyDied);
            EventCenter.Instance.AddEventListener<PlayerDiedEventData>(GameEvent.PlayerDied, OnPlayerDied);
            EventCenter.Instance.AddEventListener<PlayerEnergyStateChangedEventData>(GameEvent.PlayerEnergyStateChanged, OnEnergyStateChanged);
            EventCenter.Instance.AddEventListener<PlayerEnergyBlessingSelectedEventData>(GameEvent.PlayerEnergyBlessingSelected, OnBlessingSelected);
            EventCenter.Instance.AddEventListener<PickupCollectedEventData>(GameEvent.PickupCollected, OnPickupCollected);
            EventCenter.Instance.AddEventListener<WeaponFiredEventData>(GameEvent.WeaponFired, OnWeaponFired);
            StartTimer();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            EventCenter.Instance.RemoveEventListener<EnemyDiedEventData>(GameEvent.EnemyDied, OnEnemyDied);
            EventCenter.Instance.RemoveEventListener<PlayerDiedEventData>(GameEvent.PlayerDied, OnPlayerDied);
            EventCenter.Instance.RemoveEventListener<PlayerEnergyStateChangedEventData>(GameEvent.PlayerEnergyStateChanged, OnEnergyStateChanged);
            EventCenter.Instance.RemoveEventListener<PlayerEnergyBlessingSelectedEventData>(GameEvent.PlayerEnergyBlessingSelected, OnBlessingSelected);
            EventCenter.Instance.RemoveEventListener<PickupCollectedEventData>(GameEvent.PickupCollected, OnPickupCollected);
            EventCenter.Instance.RemoveEventListener<WeaponFiredEventData>(GameEvent.WeaponFired, OnWeaponFired);
            StopTimer();

            if (ReferenceEquals(Active, this))
            {
                Active = null;
            }
        }

        private void StartTimer()
        {
            MultiTimerManager timerManager = MultiTimerManager.Instance;
            if (timerManager == null)
            {
                return;
            }

            _battleTimer = timerManager.CreateTimer(BattleTimerId, true);
            _battleTimer.OnUpdate += OnTimerUpdated;
            _battleTimer.Start();
            PublishTime(0f);
        }

        private void StopTimer()
        {
            if (_battleTimer == null)
            {
                return;
            }

            _battleTimer.OnUpdate -= OnTimerUpdated;
            _battleTimer.Stop();
            _battleTimer = null;

            MultiTimerManager timerManager = MultiTimerManager.Instance;
            if (timerManager != null)
            {
                timerManager.RemoveTimer(BattleTimerId);
            }
        }

        private void OnTimerUpdated(float elapsedSeconds)
        {
            int wholeSeconds = Mathf.Max(0, Mathf.FloorToInt(elapsedSeconds));
            if (wholeSeconds != _lastPublishedSecond)
            {
                PublishTime(elapsedSeconds);
            }
        }

        private void PublishTime(float elapsedSeconds)
        {
            _lastPublishedSecond = Mathf.Max(0, Mathf.FloorToInt(elapsedSeconds));
            EventCenter.Instance.EventTrigger(
                GameEvent.CombatTimeChanged,
                new CombatTimeChangedEventData(elapsedSeconds));
        }

        private void OnEnergyStateChanged(PlayerEnergyStateChangedEventData eventData)
        {
            if (_battleTimer == null || _completed)
            {
                return;
            }

            if (eventData.currentState == PlayerEnergyState.BlessingSelecting)
            {
                _battleTimer.Pause();
                return;
            }

            if (_battleTimer.State == TimerState.Paused)
            {
                _battleTimer.Resume();
            }
        }

        private void OnEnemyDied(EnemyDiedEventData eventData)
        {
            if (!_completed && CombatOwnership.IsPlayerOwnedDamage(eventData.damageInfo, _player))
            {
                _killCount++;
            }
        }

        private void OnBlessingSelected(PlayerEnergyBlessingSelectedEventData eventData)
        {
            if (!_completed)
            {
                _blessingSelectCount++;
            }
        }

        private void OnPickupCollected(PickupCollectedEventData eventData)
        {
            if (_completed || _player == null || eventData.collector == null)
            {
                return;
            }

            PlayerController collector = eventData.collector.GetComponentInParent<PlayerController>();
            if (collector == _player)
            {
                _pickupCollectedCount++;
            }
        }

        private void OnWeaponFired(WeaponFiredEventData eventData)
        {
            if (!_completed)
            {
                _weaponFireCount++;
            }
        }

        private void OnPlayerDied(PlayerDiedEventData eventData)
        {
            if (_completed || eventData.player != _player)
            {
                return;
            }

            CompleteRun();
        }

        private void CompleteRun()
        {
            _completed = true;
            float survivalSeconds = SurvivalSeconds;
            _battleTimer?.Stop();

            int goldEarned = _inventory != null ? _inventory.BattleGold : 0;
            CombatRunSettlementResult settlement = PlayerProgressSaveService.SettleCombatRun(
                survivalSeconds,
                _killCount,
                goldEarned);

            Time.timeScale = 0f;
            UIManager.Instance.OpenPanelAsy<global::EndCanvas>(canvas =>
            {
                if (canvas == null)
                {
                    Debug.LogError("[CombatRun] EndCanvas 加载失败");
                    return;
                }

                canvas.ShowResult(new CombatRunResult(
                    survivalSeconds,
                    _killCount,
                    goldEarned,
                    _blessingSelectCount,
                    _pickupCollectedCount,
                    _weaponFireCount,
                    settlement));
            });
        }
    }
}
