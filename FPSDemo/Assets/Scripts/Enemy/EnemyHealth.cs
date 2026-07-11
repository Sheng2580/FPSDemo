using Combat;
using UnityEngine;

namespace Enemy
{
    /// <summary>
    /// 敌人生命模块，只负责扣血、死亡判断、伤害事件和 Debug 输出
    /// </summary>
    public class EnemyHealth : MonoBehaviour
    {
        [Header("生命")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float currentHealth;

        [Header("引用")]
        [SerializeField] private EnemyController controller;

        private bool _dead;

        public float MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;
        public bool IsDead => _dead;

        private void Awake()
        {
            AutoBind();
            ResetHealth();
        }

        private void Reset()
        {
            AutoBind();
        }

        public void Init(float newMaxHealth)
        {
            maxHealth = Mathf.Max(1f, newMaxHealth);
            ResetHealth();
        }

        public void ResetHealth()
        {
            currentHealth = maxHealth;
            _dead = false;
        }

        public void TakeDamage(DamageInfo damageInfo)
        {
            if (_dead)
            {
                return;
            }

            float damage = Mathf.Max(0f, damageInfo.finalDamage);
            if (damage <= 0f)
            {
                return;
            }

            currentHealth = Mathf.Max(0f, currentHealth - damage);
            Debug.Log(
                $"[EnemyDamage] {name} Damage={damage:0.##} HP={currentHealth:0.##}/{maxHealth:0.##} Part={damageInfo.hitPart} Critical={damageInfo.isCritical}",
                this);

            EventCenter.Instance.EventTrigger(
                GameEvent.EnemyDamaged,
                new EnemyDamagedEventData(controller, damageInfo, currentHealth, maxHealth));

            if (currentHealth <= 0f)
            {
                Die(damageInfo);
                return;
            }

            controller?.NotifyDamaged(damageInfo);
        }

        private void Die(DamageInfo damageInfo)
        {
            if (_dead)
            {
                return;
            }

            _dead = true;
            EventCenter.Instance.EventTrigger(GameEvent.EnemyDied, new EnemyDiedEventData(controller, damageInfo));
            controller?.NotifyDeath(damageInfo);
        }

        private void AutoBind()
        {
            controller ??= GetComponent<EnemyController>();
        }
    }
}
