using UnityEngine;

namespace Enemy
{
    public class EnemyAnimationEventReceiver : MonoBehaviour
    {
        [Header("攻击组件")]
        [SerializeField] private EnemyAttack attack;

        private void Awake()
        {
            AutoBind();
        }

        private void Reset()
        {
            AutoBind();
        }

        // 攻击判定开始
        public void AtkS()
        {
            AutoBind();
            attack?.AtkS();
        }

        // 攻击判定结束
        public void AtkE()
        {
            AutoBind();
            attack?.AtkE();
        }

        private void AutoBind()
        {
            if (attack != null)
            {
                return;
            }

            attack = GetComponentInParent<EnemyAttack>();
        }
    }
}
