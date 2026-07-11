using Enemy.Data;
using UnityEngine;

namespace Enemy
{
    /// <summary>
    /// 敌人模型数据层
    /// 保存 Animator、动画状态名、过渡时间和根运动开关
    /// </summary>
    public class EnemyModel : CharacterModleBase
    {
        [Header("根运动")]
        [SerializeField] private bool useRootMotion = true;

        [Header("动画状态名")]
        [SerializeField] private string idleStateName = "ZombieSkeleton_OneHanded_Idle";
        [SerializeField] private string walkStateName = "ZombieSkeleton_OneHanded_Walk";
        [SerializeField] private string runStateName = "ZombieSkeleton_OneHanded_Run";
        [SerializeField] private string linkTraverseStateName = "ZombieSkeleton_OneHanded_Dodge";
        [SerializeField] private string attackStateName = "ZombieSkeleton_OneHanded_Attack_1";
        [SerializeField] private string damageStateName = "ZombieSkeleton_OneHanded_Damage";
        [SerializeField] private string deathStateName = "ZombieSkeleton_OneHanded_Death";

        [Header("过渡")]
        [SerializeField] private float locomotionTransition = 0.18f;
        [SerializeField] private float attackTransition = 0.1f;
        [SerializeField] private float hitTransition = 0.14f;
        [SerializeField] private float deathTransition = 0.18f;
        [SerializeField] private float recoverTransition = 0.18f;

        public string IdleStateName => idleStateName;
        public string WalkStateName => walkStateName;
        public string RunStateName => runStateName;
        public string LinkTraverseStateName => linkTraverseStateName;
        public string AttackStateName => attackStateName;
        public string DamageStateName => damageStateName;
        public string DeathStateName => deathStateName;
        public float LocomotionTransition => Mathf.Max(0.01f, locomotionTransition);
        public float AttackTransition => Mathf.Max(0.01f, attackTransition);
        public float HitTransition => Mathf.Max(0.01f, hitTransition);
        public float DeathTransition => Mathf.Max(0.01f, deathTransition);
        public float RecoverTransition => Mathf.Max(0.01f, recoverTransition);

        protected override void Awake()
        {
            base.Awake();
            ApplyRootMotionSetting();
        }

        public void ResetModel()
        {
            ClearRootMotionAction();
            ApplyRootMotionSetting();
        }

        public void SetRootMotionEnabled(bool enabled)
        {
            useRootMotion = enabled;
            ApplyRootMotionSetting();
        }

        public void ApplyRuntimeStats(EnemyRuntimeStats runtimeStats)
        {
            if (runtimeStats == null)
            {
                return;
            }

            // 不同敌人共用同一套执行代码，只替换动画状态名和过渡参数
            idleStateName = string.IsNullOrEmpty(runtimeStats.idleStateName) ? idleStateName : runtimeStats.idleStateName;
            walkStateName = string.IsNullOrEmpty(runtimeStats.walkStateName) ? walkStateName : runtimeStats.walkStateName;
            runStateName = string.IsNullOrEmpty(runtimeStats.runStateName) ? runStateName : runtimeStats.runStateName;
            attackStateName = string.IsNullOrEmpty(runtimeStats.attackStateName) ? attackStateName : runtimeStats.attackStateName;
            damageStateName = string.IsNullOrEmpty(runtimeStats.damageStateName) ? damageStateName : runtimeStats.damageStateName;
            deathStateName = string.IsNullOrEmpty(runtimeStats.deathStateName) ? deathStateName : runtimeStats.deathStateName;
            locomotionTransition = Mathf.Max(0.01f, runtimeStats.locomotionTransition);
            attackTransition = Mathf.Max(0.01f, runtimeStats.attackTransition);
            hitTransition = Mathf.Max(0.01f, runtimeStats.hitTransition);
            deathTransition = Mathf.Max(0.01f, runtimeStats.deathTransition);
            recoverTransition = Mathf.Max(0.01f, runtimeStats.recoverTransition);
        }

        public bool TryGetAnimator(out Animator targetAnimator)
        {
            if (animator == null)
            {
                animator = GetComponent<Animator>();
                animator ??= GetComponentInChildren<Animator>(true);
            }

            targetAnimator = animator;
            return targetAnimator != null;
        }

        private void ApplyRootMotionSetting()
        {
            if (!TryGetAnimator(out Animator targetAnimator))
            {
                return;
            }

            targetAnimator.applyRootMotion = useRootMotion;
        }
    }
}
