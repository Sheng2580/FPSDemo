using Combat;
using Enemy.Data;
using UnityEngine;

namespace Enemy
{
    /// <summary>
    /// 敌人运行时黑板，行为树只读写这里，避免直接操作移动、动画和伤害模块
    /// </summary>
    public class EnemyBlackboard
    {
        public EnemyController controller;
        public EnemyHealth health;
        public EnemyMotor motor;
        public EnemyAttack attack;
        public EnemyView view;
        public EnemyStateMachine stateMachine;
        public Transform target;

        public int enemyId;
        public string enemyName;
        public int goldReward;
        public int maxChaseEnemyCount = 12;
        public int maxAttackersCount = 3;

        public float maxHealth;
        public float moveSpeed;
        public float angularSpeed;
        public float acceleration;
        public float attackDamage;
        public float attackDistance;
        public float attackInterval;
        public float attackHitDelay;
        public float detectionRange = 40f;
        public bool alwaysKnowTarget = true;

        public Vector3 targetPosition;
        public Vector3 toTarget;
        public Vector3 desiredMoveDirection;
        public Vector3 navSteeringTarget;
        public float sqrDistanceToTarget;
        public bool canSeeTarget;
        public bool isTargetInAttackRange;
        public bool hasNavPath;

        public bool isDead;
        public bool isHitStunned;
        public bool isAttacking;
        public bool attackReady;
        public float hitStunEndTime;
        public float nextFullHitReactionTime;
        public DamageInfo lastDamageInfo;
        public Vector3 hitKnockbackDirection;
        public float hitKnockbackDistance;
        public float hitKnockbackDuration;

        public EnemyStateType currentState = EnemyStateType.None;
        public EnemyStateType requestedState = EnemyStateType.None;
        public bool hasRequestedState;

        public EnemyPerformanceTier performanceTier = EnemyPerformanceTier.Near;
        public float nextThinkTime;
        public float lastThinkTime;
        public bool hasChaseSlot = true;
        public int chaseSlotRank = -1;

        public void Init(
            EnemyController newController,
            Transform newTarget,
            EnemySpawnManager.EnemySpawnDefinition definition,
            EnemyHealth newHealth,
            EnemyMotor newMotor,
            EnemyAttack newAttack,
            EnemyView newView,
            EnemyStateMachine newStateMachine)
        {
            controller = newController;
            target = newTarget;
            health = newHealth;
            motor = newMotor;
            attack = newAttack;
            view = newView;
            stateMachine = newStateMachine;

            enemyId = definition.enemyId;
            enemyName = definition.enemyName;
            goldReward = Mathf.Max(0, definition.goldReward);
            maxChaseEnemyCount = 12;
            maxAttackersCount = 3;

            maxHealth = Mathf.Max(1f, definition.maxHealth);
            moveSpeed = Mathf.Max(0.1f, definition.moveSpeed);
            angularSpeed = Mathf.Max(1f, definition.angularSpeed);
            acceleration = Mathf.Max(1f, definition.acceleration);
            attackDamage = Mathf.Max(0f, definition.attackDamage);
            attackDistance = Mathf.Max(0.1f, definition.attackDistance);
            attackInterval = Mathf.Max(0.1f, definition.attackInterval);
            attackHitDelay = Mathf.Max(0f, definition.attackHitDelay);

            currentState = EnemyStateType.None;
            requestedState = EnemyStateType.None;
            hasRequestedState = false;
            isHitStunned = false;
            hitStunEndTime = 0f;
            nextFullHitReactionTime = 0f;
            hitKnockbackDirection = Vector3.zero;
            hitKnockbackDistance = 0f;
            hitKnockbackDuration = 0f;
            performanceTier = EnemyPerformanceTier.Near;
            nextThinkTime = 0f;
            lastThinkTime = 0f;
            hasChaseSlot = true;
            chaseSlotRank = -1;

            RefreshPerception();
        }

        public void Init(
            EnemyController newController,
            Transform newTarget,
            EnemyRuntimeStats runtimeStats,
            EnemyHealth newHealth,
            EnemyMotor newMotor,
            EnemyAttack newAttack,
            EnemyView newView,
            EnemyStateMachine newStateMachine)
        {
            controller = newController;
            target = newTarget;
            health = newHealth;
            motor = newMotor;
            attack = newAttack;
            view = newView;
            stateMachine = newStateMachine;

            enemyId = runtimeStats.enemyId;
            enemyName = runtimeStats.enemyName;
            goldReward = Mathf.Max(0, runtimeStats.goldReward);
            maxChaseEnemyCount = Mathf.Max(0, runtimeStats.maxActiveAgentCount);
            maxAttackersCount = Mathf.Max(1, runtimeStats.maxAttackersCount);

            maxHealth = Mathf.Max(1f, runtimeStats.maxHealth);
            moveSpeed = Mathf.Max(0.1f, runtimeStats.moveSpeed);
            angularSpeed = Mathf.Max(1f, runtimeStats.angularSpeed);
            acceleration = Mathf.Max(1f, runtimeStats.acceleration);
            attackDamage = Mathf.Max(0f, runtimeStats.attackDamage);
            attackDistance = Mathf.Max(0.1f, runtimeStats.attackDistance);
            attackInterval = Mathf.Max(0.1f, runtimeStats.attackInterval);
            attackHitDelay = Mathf.Max(0f, runtimeStats.attackHitDelay);
            detectionRange = Mathf.Max(0.1f, runtimeStats.detectionRange);

            currentState = EnemyStateType.None;
            requestedState = EnemyStateType.None;
            hasRequestedState = false;
            isHitStunned = false;
            hitStunEndTime = 0f;
            nextFullHitReactionTime = 0f;
            hitKnockbackDirection = Vector3.zero;
            hitKnockbackDistance = 0f;
            hitKnockbackDuration = 0f;
            performanceTier = EnemyPerformanceTier.Near;
            nextThinkTime = 0f;
            lastThinkTime = 0f;
            hasChaseSlot = true;
            chaseSlotRank = -1;

            RefreshPerception();
        }

        public void RefreshPerception()
        {
            // 感知数据每次决策和执行前刷新，状态节点不重复查场景
            isDead = health != null && health.IsDead;
            isHitStunned = Time.time < hitStunEndTime;
            isAttacking = attack != null && attack.IsAttacking;
            attackReady = attack == null || attack.IsAttackReady;

            if (target == null || controller == null)
            {
                targetPosition = Vector3.zero;
                toTarget = Vector3.zero;
                desiredMoveDirection = Vector3.zero;
                sqrDistanceToTarget = float.MaxValue;
                canSeeTarget = false;
                isTargetInAttackRange = false;
                return;
            }

            targetPosition = target.position;
            toTarget = targetPosition - controller.transform.position;
            toTarget.y = 0f;
            sqrDistanceToTarget = toTarget.sqrMagnitude;
            float attackDistanceValue = attackDistance;
            float detectionRangeValue = detectionRange;

            canSeeTarget = alwaysKnowTarget || sqrDistanceToTarget <= detectionRangeValue * detectionRangeValue;
            isTargetInAttackRange = sqrDistanceToTarget <= attackDistanceValue * attackDistanceValue;

            if (toTarget.sqrMagnitude > 0.0001f)
            {
                desiredMoveDirection = toTarget.normalized;
            }
            else
            {
                desiredMoveDirection = Vector3.zero;
            }
        }

        public void BeginDecision()
        {
            requestedState = EnemyStateType.None;
            hasRequestedState = false;
        }

        public void RequestState(EnemyStateType stateType)
        {
            // 行为树只提出状态请求，状态切换合法性由 EnemyStateMachine 判断
            requestedState = stateType;
            hasRequestedState = stateType != EnemyStateType.None;
        }

        public void ClearRequest()
        {
            requestedState = EnemyStateType.None;
            hasRequestedState = false;
        }

        public void EnterHitStun(
            DamageInfo damageInfo,
            float duration,
            Vector3 knockbackDirection,
            float knockbackDistance,
            float knockbackDuration)
        {
            lastDamageInfo = damageInfo;
            hitStunEndTime = Mathf.Max(hitStunEndTime, Time.time + Mathf.Max(0.01f, duration));
            isHitStunned = true;
            hitKnockbackDirection = knockbackDirection;
            hitKnockbackDistance = Mathf.Max(0f, knockbackDistance);
            hitKnockbackDuration = Mathf.Max(0.01f, knockbackDuration);
        }

        public void ClearHitStun()
        {
            hitStunEndTime = 0f;
            isHitStunned = false;
            hitKnockbackDirection = Vector3.zero;
            hitKnockbackDistance = 0f;
            hitKnockbackDuration = 0f;
        }

        public void ClearRuntime()
        {
            controller = null;
            health = null;
            motor = null;
            attack = null;
            view = null;
            stateMachine = null;
            target = null;
            ClearRequest();
            ClearHitStun();
            currentState = EnemyStateType.None;
            performanceTier = EnemyPerformanceTier.Sleep;
            hasChaseSlot = false;
            chaseSlotRank = -1;
        }
    }
}
