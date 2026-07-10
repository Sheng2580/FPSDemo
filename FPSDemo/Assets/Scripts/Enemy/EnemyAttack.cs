using UnityEngine;

namespace Enemy
{
    /// <summary>
    /// 敌人攻击层，负责攻击冷却和动画命中窗口内的玩家伤害判定
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
        [SerializeField] private bool useAnimationHitWindow = true;

        [Header("引用")]
        [SerializeField] private EnemyView view;

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
        public float AttackStartTime => _attackStartTime;
        public bool IsAttacking => _attackPlaying;
        public bool IsHitWindowActive => _hitWindowActive;
        public bool IsAttackReady => Time.time >= _nextAttackTime && !_attackPlaying;

        private void Awake()
        {
            AutoBind();
        }

        private void Reset()
        {
            AutoBind();
        }

        public void Init(
            Transform target,
            EnemyController controller,
            float damage,
            float distance,
            float interval,
            float hitDelay)
        {
            AutoBind();
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

        public void Tick()
        {
            TryAttack();
            TickHitWindow();
        }

        public bool TryAttack(bool playAnimation = true)
        {
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

            // 当前主流程由 EnemyAttackState 播动画，旧接口保留给临时测试
            if (playAnimation)
            {
                view?.PlayAttack();
            }

            if (!useAnimationHitWindow)
            {
                _fallbackHitTime = Time.time + attackHitDelay;
                _waitingFallbackHit = true;
            }

            return true;
        }

        public void StopAttack()
        {
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
            BeginAttackHitWindow();
        }

        public void AtkE()
        {
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
            toTarget.y = 0f;
            float distance = attackDistance + hitDistancePadding;
            return toTarget.sqrMagnitude <= distance * distance;
        }

        private void ResolveTargetPlayer()
        {
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

        private void AutoBind()
        {
            view ??= GetComponent<EnemyView>();
        }
    }
}
