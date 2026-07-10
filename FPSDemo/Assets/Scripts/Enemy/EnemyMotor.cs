using UnityEngine;
using UnityEngine.AI;

namespace Enemy
{
    /// <summary>
    /// 敌人运动层，NavMeshAgent 只负责给方向，最终位移统一交给 CharacterController
    /// </summary>
    public class EnemyMotor : MonoBehaviour
    {
        [Header("寻路")]
        [SerializeField] private NavMeshAgent agent;
        [SerializeField] private CharacterController characterController;
        [SerializeField] private float nearUpdateInterval = 0.15f;
        [SerializeField] private float middleUpdateInterval = 0.35f;
        [SerializeField] private float farUpdateInterval = 0.75f;
        [SerializeField] private float middleDistance = 8f;
        [SerializeField] private float farDistance = 18f;

        [Header("根运动")]
        [SerializeField] private bool useRootMotion = true;
        [SerializeField] private bool fallbackMoveWhenRootMotionDisabled = true;
        [SerializeField] private float rootMotionSpeedMultiplier = 1f;
        [SerializeField] private float gravity = -20f;

        [Header("引用")]
        [SerializeField] private EnemyView view;

        private Transform _target;
        private EnemyController _controller;
        private float _moveSpeed = 2.2f;
        private float _angularSpeed = 360f;
        private float _stoppingDistance = 1.4f;
        private float _nextDestinationTime;
        private Vector3 _moveDirection;
        private Vector3 _knockbackDirection;
        private float _verticalVelocity;
        private float _knockbackRemainingDistance;
        private float _knockbackSpeed;
        private bool _wantsMove;
        private bool _knockbackActive;

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
            float moveSpeed,
            float angularSpeed,
            float acceleration,
            float stoppingDistance)
        {
            AutoBind();
            _target = target;
            _controller = controller;
            _moveSpeed = Mathf.Max(0.1f, moveSpeed);
            _angularSpeed = Mathf.Max(1f, angularSpeed);
            _stoppingDistance = Mathf.Max(0.1f, stoppingDistance);
            _nextDestinationTime = 0f;

            ConfigureAgent(acceleration);
            view?.SetRootMotionEnabled(useRootMotion);
        }

        public void Tick()
        {
            if (_target == null || _controller == null || _controller.IsDead)
            {
                StopImmediately();
                return;
            }

            Vector3 toTarget = _target.position - transform.position;
            toTarget.y = 0f;
            float distanceSqr = toTarget.sqrMagnitude;
            float stopDistanceSqr = _stoppingDistance * _stoppingDistance;

            if (distanceSqr <= stopDistanceSqr)
            {
                TickIdle(toTarget);
                return;
            }

            TickChase(toTarget.normalized, distanceSqr > middleDistance * middleDistance);
        }

        public void TickIdle(Vector3 lookDirection)
        {
            _wantsMove = false;
            _moveDirection = Vector3.zero;
            StopAgentPath(clearPath: false);
            FaceTarget(lookDirection);
            ApplyGravityOnly();
            SyncAgentPosition();
        }

        public void TickChase(Vector3 desiredDirection, bool run)
        {
            Vector3 direction = ResolveMoveDirection(desiredDirection);
            _moveDirection = direction;
            _wantsMove = direction.sqrMagnitude > 0.0001f;

            if (!_wantsMove)
            {
                TickIdle(Vector3.zero);
                return;
            }

            FaceTarget(direction);

            if (!useRootMotion && fallbackMoveWhenRootMotionDisabled)
            {
                ApplyFallbackMove(direction);
            }
        }

        public void TickAttack(Vector3 lookDirection)
        {
            _wantsMove = false;
            _moveDirection = Vector3.zero;
            StopAgentPath(clearPath: false);
            FaceTarget(lookDirection);
            ApplyGravityOnly();
            SyncAgentPosition();
        }

        public void StopMovement()
        {
            _wantsMove = false;
            _moveDirection = Vector3.zero;
            StopAgentPath(clearPath: false);
        }

        public void StopImmediately()
        {
            _wantsMove = false;
            _moveDirection = Vector3.zero;
            _knockbackActive = false;
            _knockbackRemainingDistance = 0f;
            _verticalVelocity = 0f;
            StopAgentPath(clearPath: true);
        }

        public void SetRootMotionEnabled(bool enabled)
        {
            useRootMotion = enabled;
            view?.SetRootMotionEnabled(enabled);
        }

        public void StartKnockback(Vector3 direction, float distance, float duration)
        {
            Vector3 horizontalDirection = direction;
            horizontalDirection.y = 0f;

            if (horizontalDirection.sqrMagnitude <= 0.0001f || distance <= 0f)
            {
                _knockbackActive = false;
                _knockbackRemainingDistance = 0f;
                return;
            }

            _wantsMove = false;
            _moveDirection = Vector3.zero;
            _knockbackDirection = horizontalDirection.normalized;
            _knockbackRemainingDistance = Mathf.Max(0f, distance);
            _knockbackSpeed = _knockbackRemainingDistance / Mathf.Max(0.01f, duration);
            _knockbackActive = true;
            StopAgentPath(clearPath: false);
        }

        public void TickKnockback()
        {
            if (!_knockbackActive)
            {
                ApplyGravityOnly();
                SyncAgentPosition();
                return;
            }

            float step = Mathf.Min(_knockbackRemainingDistance, _knockbackSpeed * Time.deltaTime);
            Vector3 motion = _knockbackDirection * step;
            motion.y += GetGravityDelta();
            MoveWithController(motion);
            _knockbackRemainingDistance = Mathf.Max(0f, _knockbackRemainingDistance - step);

            if (_knockbackRemainingDistance <= 0.001f)
            {
                _knockbackActive = false;
            }

            SyncAgentPosition();
        }

        public void ReceiveRootMotion(Vector3 deltaPosition, Quaternion deltaRotation)
        {
            if (!useRootMotion)
            {
                return;
            }

            // 根运动提供基础步伐，导航只修正朝向和路径方向
            Vector3 motion = _wantsMove ? deltaPosition * Mathf.Max(0f, rootMotionSpeedMultiplier) : Vector3.zero;
            motion.y += GetGravityDelta();
            MoveWithController(motion);
            SyncAgentPosition();
        }

        private Vector3 ResolveMoveDirection(Vector3 desiredDirection)
        {
            // Agent 不接管 Transform，避免和 CharacterController 争位置
            Vector3 fallbackDirection = desiredDirection;
            fallbackDirection.y = 0f;

            if (fallbackDirection.sqrMagnitude > 0.0001f)
            {
                fallbackDirection.Normalize();
            }

            if (!CanUseNavMeshAgent() || _target == null)
            {
                return fallbackDirection;
            }

            agent.isStopped = false;
            agent.nextPosition = transform.position;

            Vector3 toTarget = _target.position - transform.position;
            toTarget.y = 0f;
            float distanceSqr = toTarget.sqrMagnitude;

            if (Time.time >= _nextDestinationTime)
            {
                _nextDestinationTime = Time.time + GetDestinationInterval(distanceSqr);
                agent.SetDestination(_target.position);
            }

            Vector3 steering = agent.steeringTarget - transform.position;
            steering.y = 0f;

            if (steering.sqrMagnitude > 0.0001f)
            {
                return steering.normalized;
            }

            if (agent.desiredVelocity.sqrMagnitude > 0.0001f)
            {
                Vector3 desiredVelocity = agent.desiredVelocity;
                desiredVelocity.y = 0f;
                return desiredVelocity.normalized;
            }

            return fallbackDirection;
        }

        private void ApplyFallbackMove(Vector3 direction)
        {
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Vector3 motion = direction.normalized * (_moveSpeed * Time.deltaTime);
            motion.y += GetGravityDelta();
            MoveWithController(motion);
            SyncAgentPosition();
        }

        private void FaceTarget(Vector3 toTarget)
        {
            if (toTarget.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, _angularSpeed * Time.deltaTime);
        }

        private void ApplyGravityOnly()
        {
            if (characterController == null || !characterController.enabled)
            {
                return;
            }

            MoveWithController(new Vector3(0f, GetGravityDelta(), 0f));
        }

        private float GetGravityDelta()
        {
            if (characterController == null || !characterController.enabled)
            {
                return 0f;
            }

            if (characterController.isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = -1f;
            }

            _verticalVelocity += gravity * Time.deltaTime;
            return _verticalVelocity * Time.deltaTime;
        }

        private void MoveWithController(Vector3 motion)
        {
            if (motion.sqrMagnitude <= 0.0000001f)
            {
                return;
            }

            if (characterController != null && characterController.enabled)
            {
                characterController.Move(motion);
                return;
            }

            transform.position += motion;
        }

        private void StopAgentPath(bool clearPath)
        {
            if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            {
                return;
            }

            agent.isStopped = true;
            if (clearPath)
            {
                agent.ResetPath();
            }
        }

        private void SyncAgentPosition()
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.nextPosition = transform.position;
            }
        }

        private float GetDestinationInterval(float distanceSqr)
        {
            if (distanceSqr >= farDistance * farDistance)
            {
                return farUpdateInterval;
            }

            if (distanceSqr >= middleDistance * middleDistance)
            {
                return middleUpdateInterval;
            }

            return nearUpdateInterval;
        }

        private bool CanUseNavMeshAgent()
        {
            return agent != null
                   && agent.enabled
                   && agent.gameObject.activeInHierarchy
                   && agent.isOnNavMesh;
        }

        private void ConfigureAgent(float acceleration)
        {
            if (agent == null)
            {
                return;
            }

            agent.speed = _moveSpeed;
            agent.angularSpeed = _angularSpeed;
            agent.acceleration = Mathf.Max(1f, acceleration);
            agent.stoppingDistance = _stoppingDistance;
            agent.updatePosition = false;
            agent.updateRotation = false;

            if (!TryPlaceAgentOnNavMesh())
            {
                agent.enabled = false;
            }
        }

        private bool TryPlaceAgentOnNavMesh()
        {
            if (agent == null)
            {
                return false;
            }

            if (!NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
            {
                return false;
            }

            transform.position = hit.position;

            if (!agent.enabled)
            {
                agent.enabled = true;
            }

            if (!agent.isOnNavMesh)
            {
                return false;
            }

            agent.Warp(hit.position);
            agent.isStopped = false;
            return true;
        }

        private void AutoBind()
        {
            agent ??= GetComponent<NavMeshAgent>();
            characterController ??= GetComponent<CharacterController>();
            view ??= GetComponent<EnemyView>();
        }
    }
}
