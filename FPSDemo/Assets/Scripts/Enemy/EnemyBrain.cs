using Combat;
using System.Collections.Generic;
using Enemy.Data;
using UnityEngine;

namespace Enemy
{
    /// <summary>
    /// 敌人思考层，负责刷新黑板、执行行为树决策、把结果交给状态机
    /// </summary>
    public class EnemyBrain : MonoBehaviour
    {
        [Header("行为树")]
        [SerializeField] private string behaviorTreeKey = EnemyBrainFactory.DefaultZombieMeleeTreeKey;

        [Header("组件")]
        [SerializeField] private EnemyController controller;
        [SerializeField] private EnemyHealth health;
        [SerializeField] private EnemyMotor motor;
        [SerializeField] private EnemyAttack attack;
        [SerializeField] private EnemyView view;
        [SerializeField] private EnemyStateMachine stateMachine;

        [Header("受击")]
        [SerializeField] private float hitStunDuration = 0.09f;
        [SerializeField] private float defaultHitKnockbackDistance = 0.08f;
        [SerializeField] private float hitKnockbackDuration = 0.06f;
        [SerializeField] private float fullHitReactionCooldown = 0.2f;

        [Header("调试")]
        [SerializeField] private bool debugBrainState;
        [SerializeField] private float debugBrainStateInterval = 1.5f;

        private readonly EnemyBlackboard _blackboard = new EnemyBlackboard();
        private static readonly Dictionary<string, EnemyAIProfile> AiProfileCache = new Dictionary<string, EnemyAIProfile>();
        private BehaviorTree _behaviorTree;
        private EnemyAIProfile _aiProfile;
        private bool _active;
        private float _nextDebugBrainStateTime;

        public EnemyBlackboard Blackboard => _blackboard;
        public bool IsActive => _active;
        public float NextThinkTime => _blackboard.nextThinkTime;
        public EnemyPerformanceTier PerformanceTier => _blackboard.performanceTier;
        public EnemyAIProfile AIProfile => _aiProfile;

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
            Transform target,
            EnemySpawnManager.EnemySpawnDefinition definition)
        {
            AutoBind();
            controller = owner;
            _active = true;

            _blackboard.Init(controller, target, definition, health, motor, attack, view, stateMachine);
            stateMachine?.Init(controller, _blackboard, motor, attack, view);
            _behaviorTree = EnemyBrainFactory.Build(behaviorTreeKey, _blackboard);

            EnemyAIScheduler.Instance.Register(this);
            TickDecision();
        }

        public void Init(
            EnemyController owner,
            Transform target,
            EnemyRuntimeStats runtimeStats)
        {
            AutoBind();
            controller = owner;
            _active = true;

            string runtimeBehaviorTreeKey = string.IsNullOrEmpty(runtimeStats.behaviorTreeKey)
                ? EnemyBrainFactory.DefaultZombieMeleeTreeKey
                : runtimeStats.behaviorTreeKey;

            behaviorTreeKey = runtimeBehaviorTreeKey;
            hitStunDuration = Mathf.Max(0.01f, runtimeStats.hitStunDuration);
            defaultHitKnockbackDistance = Mathf.Max(0f, runtimeStats.hitKnockbackDistance);
            hitKnockbackDuration = Mathf.Max(0.01f, runtimeStats.hitKnockbackDuration);
            fullHitReactionCooldown = Mathf.Max(0f, runtimeStats.hitReactionCooldown);
            _aiProfile = ResolveAIProfile(runtimeStats.aiProfileKey);

            _blackboard.Init(controller, target, runtimeStats, health, motor, attack, view, stateMachine);
            stateMachine?.Init(controller, _blackboard, motor, attack, view);
            _behaviorTree = EnemyBrainFactory.Build(runtimeBehaviorTreeKey, _blackboard);

            EnemyAIScheduler.Instance.Register(this);
            TickDecision();
        }

        public void TickDecision()
        {
            if (!_active || _behaviorTree == null)
            {
                return;
            }

            // 决策阶段只更新黑板和请求状态，不直接移动或播放动画
            _blackboard.RefreshPerception();
            _blackboard.BeginDecision();
            _behaviorTree.Tick();
            stateMachine?.ApplyDecision();
            _blackboard.lastThinkTime = Time.time;
        }

        public void TickExecution()
        {
            if (!_active)
            {
                return;
            }

            _blackboard.RefreshPerception();
            if (_blackboard.currentState == EnemyStateType.Hit && !_blackboard.isHitStunned)
            {
                // 受击硬直结束后立刻补一次决策，避免站在 Hit 状态里等下一轮调度
                TickDecision();
                return;
            }

            stateMachine?.Tick();
            TickDebugBrainState();
        }

        public void MarkDead()
        {
            _blackboard.isDead = true;
            _blackboard.RequestState(EnemyStateType.Dead);
            stateMachine?.ApplyDecision();
            EnemyAIScheduler.TryUnregister(this);
        }

        public void MarkHitStunned(DamageInfo damageInfo)
        {
            if (!_active || _blackboard.isDead)
            {
                return;
            }

            _blackboard.lastDamageInfo = damageInfo;
            if (!damageInfo.forceFullHitReaction && Time.time < _blackboard.nextFullHitReactionTime)
            {
                if (debugBrainState)
                {
                    // 轻受击只保留调试和扣血 不强制重播受击动画
                    Debug.Log(
                        $"[EnemyHitState] {controller.name} 轻受击 Damage={damageInfo.finalDamage:0.##}",
                        controller);
                }
                return;
            }

            // 连发枪每发都有命中反馈，但完整受击动作需要间隔，避免敌人被打成暂停状态
            _blackboard.nextFullHitReactionTime = Time.time + Mathf.Max(0f, fullHitReactionCooldown);
            Vector3 knockbackDirection = ResolveKnockbackDirection(damageInfo);
            if (damageInfo.hasCustomHitReaction && damageInfo.customKnockbackDirection.sqrMagnitude > 0.0001f)
            {
                knockbackDirection = damageInfo.customKnockbackDirection;
            }

            float resolvedHitStunDuration = damageInfo.hasCustomHitReaction
                ? damageInfo.customHitStunDuration
                : hitStunDuration;
            float resolvedKnockbackDistance = damageInfo.hasCustomHitReaction
                ? damageInfo.customKnockbackDistance
                : defaultHitKnockbackDistance;
            float resolvedKnockbackDuration = damageInfo.hasCustomHitReaction
                ? damageInfo.customKnockbackDuration
                : hitKnockbackDuration;

            _blackboard.EnterHitStun(
                damageInfo,
                resolvedHitStunDuration,
                knockbackDirection,
                resolvedKnockbackDistance,
                resolvedKnockbackDuration);
            stateMachine?.ForceState(EnemyStateType.Hit);
        }

        private Vector3 ResolveKnockbackDirection(DamageInfo damageInfo)
        {
            Vector3 direction = Vector3.zero;

            if (controller != null && damageInfo.attacker != null)
            {
                direction = controller.transform.position - damageInfo.attacker.transform.position;
            }

            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
            {
                return direction.normalized;
            }

            direction = -damageInfo.hitNormal;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
            {
                return direction.normalized;
            }

            return controller != null ? -controller.transform.forward : Vector3.back;
        }

        public void Deactivate()
        {
            _active = false;
            EnemyAIScheduler.TryUnregister(this);
            stateMachine?.ResetStateMachine();
            _blackboard.ClearRuntime();
            _behaviorTree = null;
        }

        public void SetSchedule(EnemyPerformanceTier tier, float nextThinkTime)
        {
            _blackboard.performanceTier = tier;
            _blackboard.nextThinkTime = nextThinkTime;
            ApplyPerformanceTier(tier);
        }

        public void SetChaseSlot(bool hasSlot, int slotRank)
        {
            SetCombatSlots(hasSlot, slotRank, hasSlot, slotRank);
        }

        public void SetCombatSlots(bool hasChaseSlot, int chaseSlotRank, bool hasAttackSlot, int attackSlotRank)
        {
            bool changed = _blackboard.hasChaseSlot != hasChaseSlot
                           || _blackboard.chaseSlotRank != chaseSlotRank
                           || _blackboard.hasAttackSlot != hasAttackSlot
                           || _blackboard.attackSlotRank != attackSlotRank;

            _blackboard.hasChaseSlot = hasChaseSlot;
            _blackboard.chaseSlotRank = hasChaseSlot ? chaseSlotRank : -1;
            _blackboard.hasAttackSlot = hasAttackSlot;
            _blackboard.attackSlotRank = hasAttackSlot ? attackSlotRank : -1;

            if (changed)
            {
                _blackboard.nextThinkTime = Mathf.Min(_blackboard.nextThinkTime, Time.time);
            }
        }

        public void RefreshPerceptionForSchedule()
        {
            if (!_active)
            {
                return;
            }

            _blackboard.RefreshPerception();
        }

        private void ApplyPerformanceTier(EnemyPerformanceTier tier)
        {
            if (_aiProfile == null || motor == null)
            {
                return;
            }

            bool useRootMotion = tier == EnemyPerformanceTier.Near
                ? _aiProfile.useRootMotionNear
                : tier == EnemyPerformanceTier.Mid && _aiProfile.useRootMotionMid;
            motor.SetRootMotionEnabled(useRootMotion);
        }

        private void TickDebugBrainState()
        {
            if (!debugBrainState || Time.time < _nextDebugBrainStateTime)
            {
                return;
            }

            _nextDebugBrainStateTime = Time.time + Mathf.Max(0.25f, debugBrainStateInterval);
            float distance = Mathf.Sqrt(Mathf.Max(0f, _blackboard.sqrDistanceToTarget));
            EnemyStateType stateType = stateMachine != null ? stateMachine.CurrentStateType : _blackboard.currentState;
            Debug.Log(
                $"[EnemyBrainState] {name} State={stateType} Target={(_blackboard.target != null ? _blackboard.target.name : "None")} Distance={distance:0.00} See={_blackboard.canSeeTarget} AttackRange={_blackboard.isTargetInAttackRange} ChaseSlot={_blackboard.hasChaseSlot}:{_blackboard.chaseSlotRank} AttackSlot={_blackboard.hasAttackSlot}:{_blackboard.attackSlotRank} Tier={_blackboard.performanceTier} NextThink={Mathf.Max(0f, _blackboard.nextThinkTime - Time.time):0.00}s",
                this);
        }

        private static EnemyAIProfile ResolveAIProfile(string aiProfileKey)
        {
            string safeKey = string.IsNullOrEmpty(aiProfileKey) ? "NormalZombieAI" : aiProfileKey;
            if (AiProfileCache.TryGetValue(safeKey, out EnemyAIProfile cachedProfile))
            {
                return cachedProfile.Clone();
            }

            EnemyAIProfileAsset[] assets = Resources.LoadAll<EnemyAIProfileAsset>("EnemyAIProfiles");
            if (assets != null)
            {
                for (int i = 0; i < assets.Length; i++)
                {
                    EnemyAIProfileAsset asset = assets[i];
                    if (asset == null || asset.Profile == null)
                    {
                        continue;
                    }

                    EnemyAIProfile runtimeProfile = asset.CreateRuntimeProfile();
                    if (!AiProfileCache.ContainsKey(runtimeProfile.aiProfileKey))
                    {
                        AiProfileCache.Add(runtimeProfile.aiProfileKey, runtimeProfile);
                    }
                }
            }

            if (AiProfileCache.TryGetValue(safeKey, out cachedProfile))
            {
                return cachedProfile.Clone();
            }

            return EnemyAIProfile.CreateNormalZombie();
        }

        private void AutoBind()
        {
            controller ??= GetComponent<EnemyController>();
            health ??= GetComponent<EnemyHealth>();
            motor ??= GetComponent<EnemyMotor>();
            attack ??= GetComponent<EnemyAttack>();
            view ??= GetComponent<EnemyView>();
            view ??= GetComponentInChildren<EnemyView>(true);
            stateMachine ??= GetComponent<EnemyStateMachine>();
        }
    }
}
