using System.Collections.Generic;
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
        private const string DefaultLayerName = "Base Layer";
        private static readonly HashSet<string> LoggedAnimatorKeys = new HashSet<string>();

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

        public void ResetModel(string contextName = null)
        {
            ClearRootMotionAction();
            ApplyRootMotionSetting();
            PrepareAnimatorForSpawn(contextName);
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

            // Animator Controller 状态名和过渡时间由每个敌人 Prefab 静态配置
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

        public void PrepareAnimatorForSpawn(string contextName = null)
        {
            if (!TryGetAnimator(out Animator targetAnimator))
            {
                Debug.LogError($"[EnemyAnimRuntime] {ResolveContextName(contextName)} Animator=None", this);
                return;
            }

            targetAnimator.enabled = true;
            targetAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            targetAnimator.applyRootMotion = useRootMotion;
            targetAnimator.updateMode = AnimatorUpdateMode.Normal;
            targetAnimator.Rebind();
            targetAnimator.Update(0f);

            LogAnimatorRuntimeState(targetAnimator, ResolveContextName(contextName));
        }

        private void ApplyRootMotionSetting()
        {
            if (!TryGetAnimator(out Animator targetAnimator))
            {
                return;
            }

            // 手机端可见性裁剪偶发会让敌人停在初始 T 动作，敌人数量由 AI 分层控制
            targetAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            targetAnimator.applyRootMotion = useRootMotion;
        }

        private void LogAnimatorRuntimeState(Animator targetAnimator, string contextName)
        {
            RuntimeAnimatorController controller = targetAnimator.runtimeAnimatorController;
            Avatar avatar = targetAnimator.avatar;
            string controllerName = controller != null ? controller.name : "None";
            string avatarName = avatar != null ? avatar.name : "None";
            int clipCount = controller != null && controller.animationClips != null
                ? controller.animationClips.Length
                : 0;
            bool hasIdle = HasAnimatorState(targetAnimator, idleStateName);
            bool hasRun = HasAnimatorState(targetAnimator, runStateName);
            bool hasAttack = HasAnimatorState(targetAnimator, attackStateName);
            bool avatarValid = avatar != null && avatar.isValid;
            bool avatarHuman = avatar != null && avatar.isHuman;

            string logKey = $"{contextName}|{controllerName}|{avatarName}|{clipCount}|{hasIdle}|{hasRun}|{hasAttack}|{avatarValid}";
            if (!LoggedAnimatorKeys.Add(logKey))
            {
                return;
            }

            string message =
                $"[EnemyAnimRuntime] {contextName} Animator={targetAnimator.name} Enabled={targetAnimator.enabled} " +
                $"Controller={controllerName} Avatar={avatarName} AvatarValid={avatarValid} AvatarHuman={avatarHuman} " +
                $"Clips={clipCount} HasIdle={hasIdle} HasRun={hasRun} HasAttack={hasAttack} " +
                $"Idle={idleStateName} Run={runStateName} Attack={attackStateName}";

            if (controller == null || avatar == null || !avatarValid || clipCount <= 0 || !hasIdle || !hasRun || !hasAttack)
            {
                Debug.LogError(message, targetAnimator);
                return;
            }

            Debug.Log(message, targetAnimator);
        }

        private bool HasAnimatorState(Animator targetAnimator, string stateName)
        {
            if (targetAnimator == null || string.IsNullOrEmpty(stateName))
            {
                return false;
            }

            int layer = 0;
            return targetAnimator.HasState(layer, Animator.StringToHash(stateName))
                   || targetAnimator.HasState(layer, Animator.StringToHash($"{DefaultLayerName}.{stateName}"));
        }

        private string ResolveContextName(string contextName)
        {
            return string.IsNullOrEmpty(contextName) ? name : contextName;
        }
    }
}
