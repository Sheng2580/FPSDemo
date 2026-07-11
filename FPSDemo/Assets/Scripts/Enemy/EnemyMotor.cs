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

        [Header("追击散点")]
        [SerializeField] private float chaseTargetScatterRadius = 2.4f;
        [SerializeField] private float nearTargetScatterRadius = 0.85f;
        [SerializeField] private float targetScatterRefreshMinInterval = 1.4f;
        [SerializeField] private float targetScatterRefreshMaxInterval = 3.2f;
        [SerializeField] private float avoidanceDirectionWeight = 0.7f;

        [Header("等待包围")]
        [SerializeField] private float waitAroundRadius = 5.5f;
        [SerializeField] private float waitAroundRadiusJitter = 1.4f;
        [SerializeField] private float waitAroundRefreshMinInterval = 1.6f;
        [SerializeField] private float waitAroundRefreshMaxInterval = 3.8f;
        [SerializeField] private float waitDestinationReachDistance = 0.65f;

        [Header("根运动")]
        [SerializeField] private bool useRootMotion = true;
        [SerializeField] private bool fallbackMoveWhenRootMotionDisabled = true;
        [SerializeField] private bool fallbackMoveWhenRootMotionMissing = true;
        [SerializeField] private float rootMotionFallbackMinDelta = 0.001f;
        [SerializeField] private float rootMotionFallbackSpeedMultiplier = 0.7f;
        [SerializeField] private float rootMotionSpeedMultiplier = 1f;
        [SerializeField] private float gravity = -20f;

        [Header("导航链接")]
        [SerializeField] private float offMeshLinkTraverseDuration = 0.55f;
        [SerializeField] private float offMeshLinkArcHeight = 1.25f;
        [SerializeField] private float offMeshLinkEndSampleRadius = 1.5f;
        [SerializeField] private bool debugOffMeshLink;

        [Header("导航约束")]
        [SerializeField] private bool constrainToNavMesh = true;
        [SerializeField] private float navMeshClampDistance = 0.9f;
        [SerializeField] private float navMeshClampMinInterval = 0.08f;
        [SerializeField] private float navMeshClampHorizontalOffset = 0.28f;

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
        private float _offMeshLinkTimer;
        private float _nextNavMeshClampTime;
        private Vector3 _offMeshLinkStart;
        private Vector3 _offMeshLinkEnd;
        private Vector3 _lastOffMeshLinkPosition;
        private Vector3 _targetScatterOffset;
        private Vector3 _waitAroundOffset;
        private float _nextTargetScatterRefreshTime;
        private float _nextWaitAroundRefreshTime;
        private int _lastRootMotionFrame = -1;
        private bool _wantsMove;
        private bool _knockbackActive;
        private bool _isTraversingOffMeshLink;

        public bool IsTraversingOffMeshLink => _isTraversingOffMeshLink;

        private void Awake()
        {
            AutoBind();
        }

        private void Reset()
        {
            AutoBind();
        }

        private void LateUpdate()
        {
            if (!ShouldUseRootMotionFallback() || _lastRootMotionFrame == Time.frameCount)
            {
                return;
            }

            ApplyFallbackMove(_moveDirection, rootMotionFallbackSpeedMultiplier);
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
            RefreshTargetScatterOffset(true);
            RefreshWaitAroundOffset(true);

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
            TickChase(desiredDirection, run, true);
        }

        public void TickChase(Vector3 desiredDirection, bool run, bool pursueTarget)
        {
            if (TryTickOffMeshLinkTraversal())
            {
                return;
            }

            Vector3 direction = ResolveMoveDirection(desiredDirection, pursueTarget);

            if (TryBeginOffMeshLinkTraversal())
            {
                TickOffMeshLinkTraversal();
                return;
            }

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
            _isTraversingOffMeshLink = false;
            StopAgentPath(clearPath: false);
        }

        public void StopImmediately()
        {
            _wantsMove = false;
            _moveDirection = Vector3.zero;
            _knockbackActive = false;
            _knockbackRemainingDistance = 0f;
            _verticalVelocity = 0f;
            _isTraversingOffMeshLink = false;
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
            _isTraversingOffMeshLink = false;
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
            if (!useRootMotion || _isTraversingOffMeshLink)
            {
                return;
            }

            _lastRootMotionFrame = Time.frameCount;

            // 根运动提供基础步伐，导航只修正朝向和路径方向
            Vector3 motion = _wantsMove ? deltaPosition * Mathf.Max(0f, rootMotionSpeedMultiplier) : Vector3.zero;
            Vector3 horizontalMotion = motion;
            horizontalMotion.y = 0f;
            if (_wantsMove
                && fallbackMoveWhenRootMotionMissing
                && horizontalMotion.sqrMagnitude < rootMotionFallbackMinDelta * rootMotionFallbackMinDelta)
            {
                ApplyFallbackMove(_moveDirection, rootMotionFallbackSpeedMultiplier);
                return;
            }

            motion.y += GetGravityDelta();
            MoveWithController(motion);
            SyncAgentPosition();
        }

        private bool TryTickOffMeshLinkTraversal()
        {
            if (!_isTraversingOffMeshLink)
            {
                return false;
            }

            TickOffMeshLinkTraversal();
            return true;
        }

        private bool TryBeginOffMeshLinkTraversal()
        {
            if (_isTraversingOffMeshLink || !CanUseNavMeshAgent() || !agent.isOnOffMeshLink)
            {
                return false;
            }

            OffMeshLinkData linkData = agent.currentOffMeshLinkData;
            if (!linkData.valid)
            {
                return false;
            }

            _offMeshLinkStart = transform.position;
            _offMeshLinkEnd = ResolveOffMeshLinkEnd(linkData);
            _lastOffMeshLinkPosition = _offMeshLinkStart;
            _offMeshLinkTimer = 0f;
            _verticalVelocity = 0f;
            _knockbackActive = false;
            _isTraversingOffMeshLink = true;

            Vector3 linkDirection = _offMeshLinkEnd - _offMeshLinkStart;
            linkDirection.y = 0f;
            _moveDirection = linkDirection.sqrMagnitude > 0.0001f ? linkDirection.normalized : transform.forward;
            _wantsMove = true;

            agent.isStopped = true;

            if (debugOffMeshLink)
            {
                Debug.Log(
                    $"[EnemyMotor] {name} 开始穿越 NavMeshLink Start={_offMeshLinkStart} End={_offMeshLinkEnd}",
                    this);
            }

            return true;
        }

        private Vector3 ResolveOffMeshLinkEnd(OffMeshLinkData linkData)
        {
            Vector3 endPosition = linkData.endPos;
            if (NavMesh.SamplePosition(endPosition, out NavMeshHit hit, offMeshLinkEndSampleRadius, NavMesh.AllAreas))
            {
                return hit.position;
            }

            return endPosition;
        }

        private void TickOffMeshLinkTraversal()
        {
            if (!_isTraversingOffMeshLink)
            {
                return;
            }

            float duration = Mathf.Max(0.05f, offMeshLinkTraverseDuration);
            _offMeshLinkTimer += Time.deltaTime;
            float t = Mathf.Clamp01(_offMeshLinkTimer / duration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);
            Vector3 nextPosition = Vector3.Lerp(_offMeshLinkStart, _offMeshLinkEnd, smoothT);
            nextPosition.y += Mathf.Sin(smoothT * Mathf.PI) * Mathf.Max(0f, offMeshLinkArcHeight);

            Vector3 motion = nextPosition - _lastOffMeshLinkPosition;
            if (motion.sqrMagnitude > 0.0000001f)
            {
                MoveWithController(motion);
            }

            _lastOffMeshLinkPosition = transform.position;
            FaceTarget(_moveDirection);

            if (t < 1f)
            {
                return;
            }

            CompleteOffMeshLinkTraversal();
        }

        private void CompleteOffMeshLinkTraversal()
        {
            Vector3 finishPosition = _offMeshLinkEnd;
            if (NavMesh.SamplePosition(finishPosition, out NavMeshHit hit, offMeshLinkEndSampleRadius, NavMesh.AllAreas))
            {
                finishPosition = hit.position;
            }

            transform.position = finishPosition;
            _lastOffMeshLinkPosition = finishPosition;
            _verticalVelocity = 0f;
            _isTraversingOffMeshLink = false;
            _wantsMove = false;

            if (CanUseNavMeshAgent())
            {
                agent.Warp(finishPosition);
                agent.CompleteOffMeshLink();
                agent.isStopped = false;
                agent.nextPosition = finishPosition;
            }

            if (debugOffMeshLink)
            {
                Debug.Log($"[EnemyMotor] {name} 完成 NavMeshLink 穿越", this);
            }
        }

        private Vector3 ResolveMoveDirection(Vector3 desiredDirection, bool pursueTarget)
        {
            // Agent 不接管 Transform，避免和 CharacterController 争位置
            Vector3 fallbackDirection = desiredDirection;
            fallbackDirection.y = 0f;

            if (fallbackDirection.sqrMagnitude > 0.0001f)
            {
                fallbackDirection.Normalize();
            }

            if (_target != null && !pursueTarget && !CanUseNavMeshAgent())
            {
                Vector3 waitDirection = ResolveWaitAroundDestination() - transform.position;
                waitDirection.y = 0f;
                return waitDirection.sqrMagnitude > 0.0001f ? waitDirection.normalized : fallbackDirection;
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
            Vector3 destination = pursueTarget
                ? ResolveChaseDestination(distanceSqr)
                : ResolveWaitAroundDestination();

            Vector3 toDestination = destination - transform.position;
            toDestination.y = 0f;
            if (!pursueTarget && toDestination.sqrMagnitude <= waitDestinationReachDistance * waitDestinationReachDistance)
            {
                RefreshWaitAroundOffset(true);
                destination = ResolveWaitAroundDestination();
                toDestination = destination - transform.position;
                toDestination.y = 0f;
            }

            if (Time.time >= _nextDestinationTime)
            {
                _nextDestinationTime = Time.time + GetDestinationInterval(distanceSqr);
                agent.SetDestination(destination);
            }

            if (agent.desiredVelocity.sqrMagnitude > 0.0001f)
            {
                Vector3 desiredVelocity = agent.desiredVelocity;
                desiredVelocity.y = 0f;
                if (desiredVelocity.sqrMagnitude > 0.0001f)
                {
                    Vector3 avoidanceDirection = desiredVelocity.normalized;
                    Vector3 steeringDirection = ResolveSteeringDirection();
                    if (steeringDirection.sqrMagnitude > 0.0001f)
                    {
                        return Vector3.Slerp(
                            steeringDirection,
                            avoidanceDirection,
                            Mathf.Clamp01(avoidanceDirectionWeight)).normalized;
                    }

                    return avoidanceDirection;
                }
            }

            Vector3 steering = ResolveSteeringDirection();
            if (steering.sqrMagnitude > 0.0001f)
            {
                return steering;
            }

            Vector3 fallbackToDestination = destination - transform.position;
            fallbackToDestination.y = 0f;
            if (fallbackToDestination.sqrMagnitude > 0.0001f)
            {
                return fallbackToDestination.normalized;
            }

            return fallbackDirection;
        }

        private Vector3 ResolveSteeringDirection()
        {
            Vector3 steering = agent.steeringTarget - transform.position;
            steering.y = 0f;

            if (steering.sqrMagnitude > 0.0001f)
            {
                return steering.normalized;
            }

            return Vector3.zero;
        }

        private Vector3 ResolveChaseDestination(float distanceSqr)
        {
            RefreshTargetScatterOffset(false);

            float distance = Mathf.Sqrt(Mathf.Max(0f, distanceSqr));
            float farRadius = Mathf.Max(0f, chaseTargetScatterRadius);
            float nearRadius = Mathf.Max(0f, nearTargetScatterRadius);
            float radius = distance <= _stoppingDistance + 1f
                ? nearRadius
                : Mathf.Lerp(nearRadius, farRadius, Mathf.InverseLerp(_stoppingDistance + 1f, middleDistance, distance));

            Vector3 offset = _targetScatterOffset;
            offset.y = 0f;
            if (offset.sqrMagnitude > radius * radius && offset.sqrMagnitude > 0.0001f)
            {
                offset = offset.normalized * radius;
            }

            return _target.position + offset;
        }

        private Vector3 ResolveWaitAroundDestination()
        {
            RefreshWaitAroundOffset(false);

            if (_target == null)
            {
                return transform.position;
            }

            Vector3 destination = _target.position + _waitAroundOffset;
            if (NavMesh.SamplePosition(destination, out NavMeshHit hit, Mathf.Max(1f, waitAroundRadiusJitter + 1f), NavMesh.AllAreas))
            {
                return hit.position;
            }

            return destination;
        }

        private void RefreshTargetScatterOffset(bool force)
        {
            if (!force && Time.time < _nextTargetScatterRefreshTime)
            {
                return;
            }

            float minRadius = Mathf.Min(nearTargetScatterRadius, chaseTargetScatterRadius);
            float maxRadius = Mathf.Max(nearTargetScatterRadius, chaseTargetScatterRadius);
            Vector2 randomOffset = Random.insideUnitCircle.normalized
                                   * Random.Range(Mathf.Max(0f, minRadius), Mathf.Max(0f, maxRadius));
            _targetScatterOffset = new Vector3(randomOffset.x, 0f, randomOffset.y);
            _nextTargetScatterRefreshTime = Time.time + Random.Range(
                Mathf.Max(0.1f, targetScatterRefreshMinInterval),
                Mathf.Max(targetScatterRefreshMinInterval, targetScatterRefreshMaxInterval));
        }

        private void RefreshWaitAroundOffset(bool force)
        {
            if (!force && Time.time < _nextWaitAroundRefreshTime && _waitAroundOffset.sqrMagnitude > 0.0001f)
            {
                return;
            }

            Vector2 direction = Random.insideUnitCircle;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = Vector2.right;
            }

            direction.Normalize();
            float radius = Mathf.Max(0.5f, waitAroundRadius + Random.Range(-waitAroundRadiusJitter, waitAroundRadiusJitter));
            _waitAroundOffset = new Vector3(direction.x * radius, 0f, direction.y * radius);
            _nextWaitAroundRefreshTime = Time.time + Random.Range(
                Mathf.Max(0.1f, waitAroundRefreshMinInterval),
                Mathf.Max(waitAroundRefreshMinInterval, waitAroundRefreshMaxInterval));
        }

        private void ApplyFallbackMove(Vector3 direction)
        {
            ApplyFallbackMove(direction, 1f);
        }

        private void ApplyFallbackMove(Vector3 direction, float speedMultiplier)
        {
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Vector3 motion = direction.normalized * (_moveSpeed * Mathf.Max(0f, speedMultiplier) * Time.deltaTime);
            motion.y += GetGravityDelta();
            MoveWithController(motion);
            SyncAgentPosition();
        }

        private bool ShouldUseRootMotionFallback()
        {
            return useRootMotion
                   && fallbackMoveWhenRootMotionMissing
                   && _wantsMove
                   && !_isTraversingOffMeshLink
                   && !_knockbackActive
                   && _moveDirection.sqrMagnitude > 0.0001f;
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
            if (!_isTraversingOffMeshLink)
            {
                ClampToNavMeshIfNeeded();
            }

            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.nextPosition = transform.position;
            }
        }

        private void ClampToNavMeshIfNeeded()
        {
            if (!constrainToNavMesh || Time.time < _nextNavMeshClampTime)
            {
                return;
            }

            _nextNavMeshClampTime = Time.time + Mathf.Max(0.02f, navMeshClampMinInterval);

            if (!NavMesh.SamplePosition(transform.position, out NavMeshHit hit, navMeshClampDistance, NavMesh.AllAreas))
            {
                return;
            }

            Vector3 horizontalOffset = hit.position - transform.position;
            horizontalOffset.y = 0f;
            if (horizontalOffset.sqrMagnitude < navMeshClampHorizontalOffset * navMeshClampHorizontalOffset)
            {
                return;
            }

            // 根运动可能把身体带到 NavMesh 边缘外，这里只轻微拉回水平位置
            transform.position = new Vector3(hit.position.x, transform.position.y, hit.position.z);
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
            agent.autoTraverseOffMeshLink = false;
            agent.autoRepath = true;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.GoodQualityObstacleAvoidance;
            agent.avoidancePriority = Random.Range(35, 75);

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
