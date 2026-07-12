using UnityEngine;

namespace Enemy
{
    /// <summary>
    /// 敌人攻击层
    /// 只负责攻击冷却、动画命中窗口和玩家伤害判定
    /// 动画播放由 EnemyStateMachine 负责
    /// </summary>
    public class EnemyAttack : MonoBehaviour
    {
        private const string PlayerTag = "Player";

        [Header("攻击")]
        [SerializeField] private float attackDamage = 10f;
        [SerializeField] private float attackDistance = 1.4f;
        [SerializeField] private float attackInterval = 1.2f;
        [SerializeField] private float attackHitDelay = 0.35f;
        [SerializeField] private float hitDistancePadding = 0.35f;
        [Tooltip("允许攻击命中的最大垂直高度差 超过后敌人会继续追击或跳上平台")]
        [SerializeField] private float maxAttackVerticalDifference = 1.15f;
        [SerializeField] private bool useAnimationHitWindow = true;

        private Transform _target;
        private PlayerController _targetPlayer;
        private EnemyController _controller;
        private float _nextAttackTime;
        private float _attackStartTime;
        private float _fallbackHitTime;
        private bool _attackPlaying;
        private bool _hitWindowActive;
        private bool _hitAppliedThisWindow;
        private bool _waitingFallbackHit;

        public float AttackDistance => attackDistance;
        public float AttackInterval => attackInterval;
        public float AttackVerticalReach => Mathf.Max(0.1f, maxAttackVerticalDifference);
        public float AttackStartTime => _attackStartTime;
        public float AttackElapsedTime => _attackPlaying ? Time.time - _attackStartTime : 0f;
        public bool IsAttacking => _attackPlaying;
        public bool IsHitWindowActive => _hitWindowActive;
        public bool IsAttackReady => Time.time >= _nextAttackTime && !_attackPlaying;

        public void Init(
            Transform target,
            EnemyController controller,
            float damage,
            float distance,
            float interval,
            float hitDelay)
        {
            _target = target;
            ResolveTargetPlayer();
            _controller = controller;
            attackDamage = Mathf.Max(0f, damage);
            attackDistance = Mathf.Max(0.1f, distance);
            attackInterval = Mathf.Max(0.1f, interval);
            attackHitDelay = Mathf.Max(0f, hitDelay);
            _nextAttackTime = 0f;
            StopAttack();
        }

        public bool TryStartAttack()
        {
            // 状态机已经确认当前处于 Attack 状态，这里只判断冷却和距离
            if (_target == null || _controller == null || _controller.IsDead)
            {
                return false;
            }

            if (Time.time < _nextAttackTime || _attackPlaying)
            {
                return false;
            }

            if (!IsTargetInAttackRange(hitDistancePadding: 0f))
            {
                return false;
            }

            _nextAttackTime = Time.time + attackInterval;
            _attackStartTime = Time.time;
            _attackPlaying = true;
            _hitWindowActive = false;
            _hitAppliedThisWindow = false;
            _waitingFallbackHit = false;

            if (!useAnimationHitWindow)
            {
                // 没有动画事件时走时间兜底，避免敌人完全打不出伤害
                _fallbackHitTime = Time.time + attackHitDelay;
                _waitingFallbackHit = true;
            }

            return true;
        }

        public void StopAttack()
        {
            // 退出 Attack / Hit / Dead 时统一关闭判定窗口
            _attackPlaying = false;
            _hitWindowActive = false;
            _hitAppliedThisWindow = false;
            _waitingFallbackHit = false;
        }

        public void CompleteAttackAnimation()
        {
            StopAttack();
        }

        public void TickHitWindow()
        {
            // 非动画事件模式下，到达延迟时间后自动打开一次判定
            if (_waitingFallbackHit && Time.time >= _fallbackHitTime)
            {
                _waitingFallbackHit = false;
                BeginAttackHitWindow();
                EndAttackHitWindow();
            }

            if (!_hitWindowActive)
            {
                return;
            }

            TryApplyHit();
        }

        public void AtkS()
        {
            // 动画事件 AtkS，允许一次攻击存在多段判定
            BeginAttackHitWindow();
        }

        public void AtkE()
        {
            // 动画事件 AtkE，关闭当前段判定
            EndAttackHitWindow();
        }

        public void BeginAttackHitWindow()
        {
            if (_controller == null || _controller.IsDead)
            {
                return;
            }

            _hitWindowActive = true;
            _hitAppliedThisWindow = false;
            TryApplyHit();
        }

        public void EndAttackHitWindow()
        {
            _hitWindowActive = false;
            _hitAppliedThisWindow = false;
        }

        private void TryApplyHit()
        {
            ResolveTargetPlayer();
            if (_hitAppliedThisWindow || _targetPlayer == null)
            {
                return;
            }

            if (!IsTargetInAttackRange(hitDistancePadding))
            {
                return;
            }

            _targetPlayer.TakeDamage(attackDamage);
            _hitAppliedThisWindow = true;
        }

        private bool IsTargetInAttackRange(float hitDistancePadding)
        {
            ResolveTargetPlayer();
            if (_target == null)
            {
                return false;
            }

            Vector3 toTarget = _target.position - transform.position;
            float verticalDifference = Mathf.Abs(toTarget.y);
            if (verticalDifference > AttackVerticalReach)
            {
                return false;
            }

            toTarget.y = 0f;
            float distance = attackDistance + hitDistancePadding;
            return toTarget.sqrMagnitude <= distance * distance;
        }

        private void ResolveTargetPlayer()
        {
            // 目标来自刷怪器注入，找不到时才按 Player 标签兜底
            if (_targetPlayer != null && _target != null)
            {
                return;
            }

            if (_target != null)
            {
                _targetPlayer = _target.GetComponent<PlayerController>();
                _targetPlayer ??= _target.GetComponentInParent<PlayerController>();
                _targetPlayer ??= _target.GetComponentInChildren<PlayerController>();
            }

            if (_targetPlayer == null)
            {
                GameObject playerObject = GameObject.FindGameObjectWithTag(PlayerTag);
                if (playerObject != null)
                {
                    _target = playerObject.transform;
                    _targetPlayer = playerObject.GetComponent<PlayerController>();
                    _targetPlayer ??= playerObject.GetComponentInParent<PlayerController>();
                    _targetPlayer ??= playerObject.GetComponentInChildren<PlayerController>();
                }
            }

            if (_targetPlayer != null)
            {
                _target = _targetPlayer.transform;
            }
        }
    }
}
