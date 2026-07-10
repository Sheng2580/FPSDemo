using Combat;
using Enemy.Data;
using UnityEngine;

namespace Enemy
{
    public class EnemyHitBox : MonoBehaviour
    {
        [Header("命中部位")]
        [SerializeField] private EnemyHitBodyPart bodyPart = EnemyHitBodyPart.Body;
        [SerializeField] private float damageMultiplier = 1f;
        [SerializeField] private bool criticalPart;

        [Header("引用")]
        [SerializeField] private EnemyHealth health;

        public EnemyHitBodyPart BodyPart => bodyPart;
        public float DamageMultiplier => damageMultiplier;
        public bool CriticalPart => criticalPart;

        private void Awake()
        {
            AutoBindHealth();
        }

        private void Reset()
        {
            AutoBindHealth();
        }

        public bool TryApplyDamage(ref DamageInfo damageInfo)
        {
            AutoBindHealth();
            if (health == null)
            {
                return false;
            }

            damageInfo.ApplyBodyPart(bodyPart, damageMultiplier, criticalPart);
            health.TakeDamage(damageInfo);
            Debug.Log(
                $"[EnemyHitBox] {health.name} Part={bodyPart} Multiplier={damageMultiplier:0.##} Damage={damageInfo.finalDamage:0.##}",
                health);
            return true;
        }

        public void ApplyRuntimeStats(EnemyRuntimeStats runtimeStats)
        {
            if (runtimeStats == null)
            {
                return;
            }

            switch (bodyPart)
            {
                case EnemyHitBodyPart.Head:
                    damageMultiplier = Mathf.Max(0f, runtimeStats.headDamageMultiplier);
                    break;
                case EnemyHitBodyPart.Arm:
                    damageMultiplier = Mathf.Max(0f, runtimeStats.armDamageMultiplier);
                    break;
                case EnemyHitBodyPart.Leg:
                    damageMultiplier = Mathf.Max(0f, runtimeStats.legDamageMultiplier);
                    break;
                case EnemyHitBodyPart.Body:
                default:
                    damageMultiplier = Mathf.Max(0f, runtimeStats.bodyDamageMultiplier);
                    break;
            }
        }

        private void AutoBindHealth()
        {
            if (health != null)
            {
                return;
            }

            health = GetComponentInParent<EnemyHealth>();
        }
    }
}
