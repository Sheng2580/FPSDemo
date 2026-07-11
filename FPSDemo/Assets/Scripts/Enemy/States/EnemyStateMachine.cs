using UnityEngine;

namespace Enemy
{
    /// <summary>
    /// 敌人执行层，负责状态切换、动画播放、根运动绑定和攻击窗口推进
    /// </summary>
    public class EnemyStateMachine : MonoBehaviour
    {
        [Header("组件")]
        [SerializeField] private EnemyController controller;
        [SerializeField] private EnemyMotor motor;
        [SerializeField] private EnemyAttack attack;
        [SerializeField] private EnemyView view;

        private EnemyBlackboard _blackboard;
        private EnemyState _currentState;
        private EnemyIdleState _idleState;
        private EnemyChaseState _chaseState;
        private EnemyAttackState _attackState;
        private EnemyHitState _hitState;
        private EnemyDeadState _deadState;

        public EnemyStateType CurrentStateType => _currentState != null ? _currentState.StateType : EnemyStateType.None;

        private void Awake()
        {
            AutoBind();
        }

        private void Reset()
        {
            AutoBind();
        }

        public void Init(
            EnemyController owner,
            EnemyBlackboard blackboard,
            EnemyMotor enemyMotor,
            EnemyAttack enemyAttack,
            EnemyView enemyView)
        {
            controller = owner;
            _blackboard = blackboard;
            motor = enemyMotor;
            attack = enemyAttack;
            view = enemyView;

            _idleState = new EnemyIdleState(_blackboard, motor, view);
            _chaseState = new EnemyChaseState(_blackboard, motor, view);
            _attackState = new EnemyAttackState(_blackboard, motor, attack, view);
            _hitState = new EnemyHitState(_blackboard, motor, view);
            _deadState = new EnemyDeadState(_blackboard, motor, attack, view);

            ChangeState(EnemyStateType.Idle, false);
        }

        public void ApplyDecision()
        {
            if (_blackboard == null || !_blackboard.hasRequestedState)
            {
                return;
            }

            ChangeState(_blackboard.requestedState, false);
            _blackboard.ClearRequest();
        }

        public void ForceState(EnemyStateType stateType)
        {
            ChangeState(stateType, true);
        }

        public void Tick()
        {
            if (_blackboard == null)
            {
                return;
            }

            ApplyDecision();
            _currentState?.Tick();
            _blackboard.currentState = CurrentStateType;
        }

        public void ResetStateMachine()
        {
            _currentState?.Exit();
            _currentState = null;
            if (_blackboard != null)
            {
                _blackboard.currentState = EnemyStateType.None;
            }
        }

        private void ChangeState(EnemyStateType stateType, bool force)
        {
            if (stateType == EnemyStateType.None)
            {
                return;
            }

            EnemyState nextState = GetState(stateType);
            if (nextState == null)
            {
                return;
            }

            if (nextState == _currentState && !force)
            {
                return;
            }

            if (_currentState != null && !_currentState.CanExitTo(stateType))
            {
                return;
            }

            _currentState?.Exit();
            _currentState = nextState;
            _blackboard.currentState = stateType;
            _currentState.Enter();
        }

        private EnemyState GetState(EnemyStateType stateType)
        {
            switch (stateType)
            {
                case EnemyStateType.Idle:
                    return _idleState;
                case EnemyStateType.Chase:
                    return _chaseState;
                case EnemyStateType.Attack:
                    return _attackState;
                case EnemyStateType.Hit:
                    return _hitState;
                case EnemyStateType.Dead:
                    return _deadState;
                default:
                    return null;
            }
        }

        private void AutoBind()
        {
            controller ??= GetComponent<EnemyController>();
            motor ??= GetComponent<EnemyMotor>();
            attack ??= GetComponent<EnemyAttack>();
            view ??= GetComponent<EnemyView>();
        }
    }

    public abstract class EnemyState
    {
        private const string DefaultLayerName = "Base Layer";

        protected readonly EnemyBlackboard blackboard;
        protected readonly EnemyMotor motor;
        protected readonly EnemyModel model;

        public abstract EnemyStateType StateType { get; }

        protected EnemyState(EnemyBlackboard blackboard, EnemyMotor motor, EnemyModel model)
        {
            this.blackboard = blackboard;
            this.motor = motor;
            this.model = model;
        }

        public virtual void Enter()
        {
            model?.SetRootMotionAction(OnRootMotionAction);
        }

        public virtual void Tick()
        {
        }

        public virtual void Exit()
        {
            model?.ClearRootMotionAction();
        }

        public virtual bool CanExitTo(EnemyStateType nextState)
        {
            return true;
        }

        protected virtual void OnRootMotionAction(Vector3 dir, Quaternion rot)
        {
            motor?.ReceiveRootMotion(dir, rot);
        }

        protected void PlayAnimation(string stateName, float fixedTransitionTime, bool force = false, int layer = 0)
        {
            if (string.IsNullOrEmpty(stateName) || !TryGetAnimator(out Animator animator))
            {
                return;
            }

            if (!force && CurrAnimationStateName(stateName, layer))
            {
                return;
            }

            // 先尝试完整路径，避免 Animator 子状态机或 Layer 名导致找不到状态
            string fullPath = BuildStatePath(stateName, layer);
            int fullPathHash = Animator.StringToHash(fullPath);

            if (animator.HasState(layer, fullPathHash))
            {
                Debug.Log($"[EnemyAnim] {blackboard.controller.name} Play {fullPath}", blackboard.controller);
                animator.CrossFadeInFixedTime(fullPathHash, fixedTransitionTime, layer);
                return;
            }

            int shortNameHash = Animator.StringToHash(stateName);
            if (animator.HasState(layer, shortNameHash))
            {
                Debug.Log($"[EnemyAnim] {blackboard.controller.name} Play {stateName}", blackboard.controller);
                animator.CrossFadeInFixedTime(shortNameHash, fixedTransitionTime, layer);
                return;
            }

            string controllerName = animator.runtimeAnimatorController != null
                ? animator.runtimeAnimatorController.name
                : "None";
            Debug.LogWarning(
                $"[EnemyAnim] 找不到动画状态 Controller={controllerName} Layer={layer} State={stateName} FullPath={fullPath}",
                animator);
        }

        protected bool HasAnimationState(string stateName, int layer = 0)
        {
            if (string.IsNullOrEmpty(stateName) || !TryGetAnimator(out Animator animator))
            {
                return false;
            }

            string fullPath = BuildStatePath(stateName, layer);
            return animator.HasState(layer, Animator.StringToHash(fullPath))
                   || animator.HasState(layer, Animator.StringToHash(stateName));
        }

        protected virtual bool CurrAnimationStateName(string stateName, out float normalizedTime, int layer = 0)
        {
            normalizedTime = 0f;
            if (!TryGetAnimator(out Animator animator))
            {
                return false;
            }

            string fullPath = BuildStatePath(stateName, layer);
            AnimatorStateInfo nextInfo = animator.GetNextAnimatorStateInfo(layer);
            if (nextInfo.IsName(stateName) || nextInfo.IsName(fullPath))
            {
                normalizedTime = nextInfo.normalizedTime;
                return true;
            }

            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(layer);
            normalizedTime = info.normalizedTime;
            return info.IsName(stateName) || info.IsName(fullPath);
        }

        protected virtual bool CurrAnimationStateName(string stateName, int layer = 0)
        {
            if (!TryGetAnimator(out Animator animator))
            {
                return false;
            }

            string fullPath = BuildStatePath(stateName, layer);
            AnimatorStateInfo nextInfo = animator.GetNextAnimatorStateInfo(layer);
            if (nextInfo.IsName(stateName) || nextInfo.IsName(fullPath))
            {
                return true;
            }

            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(layer);
            return info.IsName(stateName) || info.IsName(fullPath);
        }

        protected virtual bool CurrAnimationStateTag(string tag, out float normalizedTime)
        {
            normalizedTime = 0f;
            if (!TryGetAnimator(out Animator animator))
            {
                return false;
            }

            AnimatorStateInfo nextInfo = animator.GetNextAnimatorStateInfo(0);
            if (nextInfo.IsTag(tag))
            {
                normalizedTime = nextInfo.normalizedTime;
                return true;
            }

            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
            normalizedTime = info.normalizedTime;
            return info.IsTag(tag);
        }

        protected bool TryGetAnimator(out Animator animator)
        {
            animator = null;
            if (model == null)
            {
                return false;
            }

            return model.TryGetAnimator(out animator);
        }

        private static string BuildStatePath(string stateName, int layer)
        {
            if (stateName.Contains("."))
            {
                return stateName;
            }

            return layer == 0 ? $"{DefaultLayerName}.{stateName}" : stateName;
        }
    }

    public class EnemyIdleState : EnemyState
    {
        public override EnemyStateType StateType => EnemyStateType.Idle;

        public EnemyIdleState(EnemyBlackboard blackboard, EnemyMotor motor, EnemyModel model) : base(blackboard, motor, model)
        {
        }

        public override void Enter()
        {
            base.Enter();
            if (model != null)
            {
                PlayAnimation(model.IdleStateName, model.LocomotionTransition, true);
            }
            motor?.StopMovement();
        }

        public override void Tick()
        {
            motor?.TickIdle(blackboard.toTarget);
        }
    }

    public class EnemyChaseState : EnemyState
    {
        public override EnemyStateType StateType => EnemyStateType.Chase;

        public EnemyChaseState(EnemyBlackboard blackboard, EnemyMotor motor, EnemyModel model) : base(blackboard, motor, model)
        {
        }

        public override void Enter()
        {
            base.Enter();
        }

        public override void Tick()
        {
            bool hasChaseSlot = blackboard.hasChaseSlot;
            if (model != null)
            {
                if (motor != null && motor.IsTraversingOffMeshLink)
                {
                    string linkStateName = HasAnimationState(model.LinkTraverseStateName)
                        ? model.LinkTraverseStateName
                        : model.RunStateName;
                    PlayAnimation(linkStateName, model.LocomotionTransition);
                }
                else
                {
                    PlayAnimation(hasChaseSlot ? model.RunStateName : model.WalkStateName, model.LocomotionTransition);
                }
            }
            motor?.TickChase(blackboard.desiredMoveDirection, hasChaseSlot, hasChaseSlot);
        }

        public override bool CanExitTo(EnemyStateType nextState)
        {
            if (nextState == EnemyStateType.Dead || nextState == EnemyStateType.Hit)
            {
                return true;
            }

            return motor == null || !motor.IsTraversingOffMeshLink;
        }
    }

    public class EnemyAttackState : EnemyState
    {
        private const string AttackAnimationTag = "Attack";
        private readonly EnemyAttack attack;

        public override EnemyStateType StateType => EnemyStateType.Attack;

        public EnemyAttackState(
            EnemyBlackboard blackboard,
            EnemyMotor motor,
            EnemyAttack attack,
            EnemyModel model) : base(blackboard, motor, model)
        {
            this.attack = attack;
        }

        public override void Enter()
        {
            base.Enter();
            motor?.StopMovement();
        }

        public override void Tick()
        {
            motor?.TickAttack(blackboard.toTarget);
            if (attack == null)
            {
                return;
            }

            if (!attack.IsAttacking && attack.TryAttack(false))
            {
                if (model != null)
                {
                    PlayAnimation(model.AttackStateName, model.AttackTransition, true);
                }
            }

            attack.TickHitWindow();
            CompleteAttackWhenAnimationFinished();

            if (!attack.IsAttacking && model != null)
            {
                PlayAnimation(model.IdleStateName, model.RecoverTransition);
            }
        }

        public override bool CanExitTo(EnemyStateType nextState)
        {
            if (nextState == EnemyStateType.Dead || nextState == EnemyStateType.Hit)
            {
                return true;
            }

            if (attack != null && attack.IsAttacking)
            {
                return false;
            }

            return !CurrAnimationStateTag(AttackAnimationTag, out float normalizedTime) || normalizedTime >= 0.98f;
        }

        private void CompleteAttackWhenAnimationFinished()
        {
            if (attack == null || !attack.IsAttacking)
            {
                return;
            }

            if (!CurrAnimationStateTag(AttackAnimationTag, out float normalizedTime))
            {
                return;
            }

            if (normalizedTime < 0.98f)
            {
                return;
            }

            attack.CompleteAttackAnimation();
        }
    }

    public class EnemyHitState : EnemyState
    {
        public override EnemyStateType StateType => EnemyStateType.Hit;

        public EnemyHitState(EnemyBlackboard blackboard, EnemyMotor motor, EnemyModel model) : base(blackboard, motor, model)
        {
        }

        public override void Enter()
        {
            base.Enter();
            Debug.Log(
                $"[EnemyHitState] {blackboard.controller.name} Enter Hit Damage={blackboard.lastDamageInfo.finalDamage:0.##} Knockback={blackboard.hitKnockbackDistance:0.##}",
                blackboard.controller);
            if (model != null)
            {
                PlayAnimation(model.DamageStateName, model.HitTransition, true);
            }
            blackboard.attack?.StopAttack();
            motor?.StopMovement();
            motor?.StartKnockback(
                blackboard.hitKnockbackDirection,
                blackboard.hitKnockbackDistance,
                blackboard.hitKnockbackDuration);
        }

        public override void Tick()
        {
            motor?.TickKnockback();
        }

        public override bool CanExitTo(EnemyStateType nextState)
        {
            if (nextState == EnemyStateType.Dead || nextState == EnemyStateType.Hit)
            {
                return true;
            }

            return !blackboard.isHitStunned;
        }
    }

    public class EnemyDeadState : EnemyState
    {
        private readonly EnemyAttack attack;

        public override EnemyStateType StateType => EnemyStateType.Dead;

        public EnemyDeadState(
            EnemyBlackboard blackboard,
            EnemyMotor motor,
            EnemyAttack attack,
            EnemyModel model) : base(blackboard, motor, model)
        {
            this.attack = attack;
        }

        public override void Enter()
        {
            base.Enter();
            if (model != null)
            {
                PlayAnimation(model.DeathStateName, model.DeathTransition, true);
            }
            motor?.StopImmediately();
            attack?.StopAttack();
        }
    }
}
