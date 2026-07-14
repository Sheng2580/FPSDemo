using Combat;
using Enemy.Data;
using UnityEngine;

namespace Enemy
{
    /// <summary>
    /// 敌人生命周期入口，只协调初始化、受伤死亡通知、碰撞开关和回池
    /// </summary>
    public class EnemyController : MonoBehaviour
    {
        [Header("基础身份")]
        [SerializeField] private int enemyId = 1001;
        [SerializeField] private string enemyName = "Zombie Skeleton";
        [SerializeField] private int goldReward = 1;

        [Header("组件")]
        [SerializeField] private EnemyHealth health;
        [SerializeField] private EnemyMotor motor;
        [SerializeField] private EnemyAttack attack;
        [SerializeField] private EnemyView view;
        [SerializeField] private EnemyBrain brain;
        [SerializeField] private EnemyStateMachine stateMachine;
        [SerializeField] private EnemyAudioController audioController;

        private EnemySpawnManager _spawner;
        private EnemyPool _pool;
        private GameObject _sourcePrefab;
        private Collider[] _colliders;
        private bool _active;
        private bool _deathNotified;
        private EnemyRuntimeStats _runtimeStats;

        public int EnemyId => enemyId;
        public string EnemyName => enemyName;
        public int GoldReward => goldReward;
        public bool IsActive => _active;
        public bool IsDead => health != null && health.IsDead;
        public float AttackDistance => attack != null ? attack.AttackDistance : 1.4f;
        public EnemyView View => view;
        public EnemyRuntimeStats RuntimeStats => _runtimeStats;

        private void Awake()
        {
            AutoBind();
            CacheColliders();
        }

        private void Reset()
        {
            AutoBind();
        }

        private void Update()
        {
            if (!_active || IsDead)
            {
                return;
            }

            // 执行 Tick 每帧跑，决策 Tick 由 EnemyAIScheduler 分帧调度
            brain?.TickExecution();
            audioController?.Tick(motor != null && motor.IsMoving);
        }

        public void InitFromSpawner(
            EnemySpawnManager.EnemySpawnDefinition definition,
            Transform target,
            EnemySpawnManager spawner,
            EnemyPool pool,
            GameObject sourcePrefab)
        {
            // 每次从对象池取出都重新注入本局数据，避免复用旧状态
            AutoBind();
            CacheColliders();

            enemyId = definition.enemyId;
            enemyName = definition.enemyName;
            goldReward = Mathf.Max(0, definition.goldReward);
            _spawner = spawner;
            _pool = pool;
            _sourcePrefab = sourcePrefab;
            _active = true;
            _deathNotified = false;

            health?.Init(definition.maxHealth);
            view?.ResetView(enemyName);
            motor?.Init(target, this, definition.moveSpeed, definition.angularSpeed, definition.acceleration, definition.attackDistance);
            attack?.Init(target, this, definition.attackDamage, definition.attackDistance, definition.attackInterval, definition.attackHitDelay);
            brain?.Init(this, target, definition);
            audioController?.Init(enemyId, target);
            SetCollidersEnabled(true);

            EventCenter.Instance.EventTrigger(GameEvent.EnemySpawned, new EnemySpawnedEventData(this));
        }

        public void InitFromSpawner(
            EnemyRuntimeStats runtimeStats,
            Transform target,
            EnemySpawnManager spawner,
            EnemyPool pool,
            GameObject sourcePrefab)
        {
            if (runtimeStats == null)
            {
                return;
            }

            // 每次从对象池取出都重新注入本局最终数值，避免上一只怪的运行时状态残留
            AutoBind();
            CacheColliders();

            _runtimeStats = runtimeStats;
            enemyId = runtimeStats.enemyId;
            enemyName = runtimeStats.enemyName;
            goldReward = Mathf.Max(0, runtimeStats.goldReward);
            _spawner = spawner;
            _pool = pool;
            _sourcePrefab = sourcePrefab;
            _active = true;
            _deathNotified = false;

            view?.ApplyRuntimeStats(runtimeStats);
            health?.Init(runtimeStats.maxHealth);
            view?.ResetView(enemyName);
            ApplyHitBoxRuntimeStats(runtimeStats);
            motor?.Init(target, this, runtimeStats.moveSpeed, runtimeStats.angularSpeed, runtimeStats.acceleration, runtimeStats.attackDistance);
            attack?.Init(target, this, runtimeStats.attackDamage, runtimeStats.attackDistance, runtimeStats.attackInterval, runtimeStats.attackHitDelay);
            brain?.Init(this, target, runtimeStats);
            audioController?.Init(enemyId, target);
            SetCollidersEnabled(true);

            EventCenter.Instance.EventTrigger(GameEvent.EnemySpawned, new EnemySpawnedEventData(this));
        }

        public void NotifyDeath(DamageInfo damageInfo)
        {
            if (_deathNotified)
            {
                return;
            }

            _deathNotified = true;
            _active = false;
            audioController?.PlayDeath();
            // 死亡后先关闭受击和身体碰撞，死亡动画结束后再由刷怪器回池
            SetCollidersEnabled(false);
            brain?.MarkDead();
            motor?.StopImmediately();
            attack?.StopAttack();
            _spawner?.NotifyEnemyDied(this);
        }

        public void NotifyDamaged(DamageInfo damageInfo)
        {
            if (!_active || IsDead)
            {
                return;
            }

            audioController?.PlayHit();
            brain?.MarkHitStunned(damageInfo);
        }

        public void NotifyAttackStarted()
        {
            if (!_active || IsDead)
            {
                return;
            }

            audioController?.PlayAttack();
        }

        public void ReturnToPool()
        {
            _active = false;
            SetCollidersEnabled(false);
            brain?.Deactivate();
            motor?.StopImmediately();
            attack?.StopAttack();
            audioController?.Deactivate();

            if (_pool != null)
            {
                _pool.Return(_sourcePrefab, this);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        private void ApplyHitBoxRuntimeStats(EnemyRuntimeStats runtimeStats)
        {
            EnemyHitBox[] hitBoxes = GetComponentsInChildren<EnemyHitBox>(true);
            for (int i = 0; i < hitBoxes.Length; i++)
            {
                hitBoxes[i]?.ApplyRuntimeStats(runtimeStats);
            }
        }

        private void AutoBind()
        {
            // 生成工具会预挂组件，这里只做兜底，避免对象池复用时缺引用
            health ??= GetComponent<EnemyHealth>();
            motor ??= GetComponent<EnemyMotor>();
            attack ??= GetComponent<EnemyAttack>();
            view ??= GetComponent<EnemyView>();
            view ??= GetComponentInChildren<EnemyView>(true);
            brain ??= GetComponent<EnemyBrain>();
            stateMachine ??= GetComponent<EnemyStateMachine>();
            audioController ??= GetComponent<EnemyAudioController>();

            if (brain == null)
            {
                brain = gameObject.AddComponent<EnemyBrain>();
            }

            if (stateMachine == null)
            {
                stateMachine = gameObject.AddComponent<EnemyStateMachine>();
            }
        }

        private void CacheColliders()
        {
            _colliders = GetComponentsInChildren<Collider>(true);
            ApplyEnemyLayerToColliders();
        }

        private void ApplyEnemyLayerToColliders()
        {
            int enemyLayer = CombatLayerNames.EnemyLayer;
            if (enemyLayer < 0 || _colliders == null)
            {
                return;
            }

            for (int i = 0; i < _colliders.Length; i++)
            {
                if (_colliders[i] != null)
                {
                    _colliders[i].gameObject.layer = enemyLayer;
                }
            }
        }

        private void SetCollidersEnabled(bool enabled)
        {
            if (_colliders == null)
            {
                return;
            }

            for (int i = 0; i < _colliders.Length; i++)
            {
                if (_colliders[i] != null)
                {
                    _colliders[i].enabled = enabled;
                }
            }
        }
    }
}
