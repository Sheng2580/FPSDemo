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

        private readonly EnemyBlackboard _blackboard = new EnemyBlackboard();
        private static readonly Dictionary<string, EnemyAIProfile> AiProfileCache = new Dictionary<string, EnemyAIProfile>();
        private BehaviorTree _behaviorTree;
        private EnemyAIProfile _aiProfile;
        private bool _active;

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

            // 行为树只写 requestedState，真正移动和动画由状态机执行
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
                TickDecision();
                return;
            }

            stateMachine?.Tick();
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
            if (Time.time < _blackboard.nextFullHitReactionTime)
            {
                // 轻受击只保留 Debug 和扣血，不强制重播受击动画
                Debug.Log(
                    $"[EnemyHitState] {controller.name} 轻受击 Damage={damageInfo.finalDamage:0.##}",
                    controller);
                return;
            }

            // 连发枪每发都有命中反馈，但完整受击动作需要间隔，避免敌人被打成暂停状态
            _blackboard.nextFullHitReactionTime = Time.time + Mathf.Max(0f, fullHitReactionCooldown);
            Vector3 knockbackDirection = ResolveKnockbackDirection(damageInfo);
            _blackboard.EnterHitStun(
                damageInfo,
                hitStunDuration,
                knockbackDirection,
                defaultHitKnockbackDistance,
                hitKnockbackDuration);
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
            stateMachine ??= GetComponent<EnemyStateMachine>();
        }
    }
}
