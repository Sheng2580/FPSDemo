using System;
using System.Collections.Generic;
using Enemy;

namespace Combat
{
    /// <summary>
    /// 本局金币发放入口
    /// 只处理玩家击杀奖励
    /// </summary>
    public sealed class CombatEconomyManager : IDisposable
    {
        private readonly HashSet<int> _rewardedEnemyInstances = new HashSet<int>();
        private readonly PlayerController _player;
        private readonly PlayerInventory _inventory;
        private bool _disposed;

        public CombatEconomyManager(PlayerController player)
        {
            _player = player;
            _inventory = player != null ? player.Inventory : null;
            EventCenter.Instance.AddEventListener<EnemySpawnedEventData>(GameEvent.EnemySpawned, OnEnemySpawned);
            EventCenter.Instance.AddEventListener<EnemyDiedEventData>(GameEvent.EnemyDied, OnEnemyDied);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            EventCenter.Instance.RemoveEventListener<EnemySpawnedEventData>(GameEvent.EnemySpawned, OnEnemySpawned);
            EventCenter.Instance.RemoveEventListener<EnemyDiedEventData>(GameEvent.EnemyDied, OnEnemyDied);
            _rewardedEnemyInstances.Clear();
        }

        private void OnEnemySpawned(EnemySpawnedEventData eventData)
        {
            if (eventData.enemy != null)
            {
                _rewardedEnemyInstances.Remove(eventData.enemy.GetInstanceID());
            }
        }

        private void OnEnemyDied(EnemyDiedEventData eventData)
        {
            if (_inventory == null || eventData.enemy == null || eventData.goldReward <= 0)
            {
                return;
            }

            if (!CombatOwnership.IsPlayerOwnedDamage(eventData.damageInfo, _player))
            {
                return;
            }

            int instanceId = eventData.enemy.GetInstanceID();
            if (!_rewardedEnemyInstances.Add(instanceId))
            {
                return;
            }

            _inventory.AddBattleGold(eventData.goldReward);
        }
    }
}
