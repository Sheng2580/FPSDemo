using System.Collections.Generic;
using UnityEngine;

namespace Enemy
{
    /// <summary>
    /// 敌人 AI 调度器，按距离分层降低行为树决策频率
    /// </summary>
    public class EnemyAIScheduler : MonoBehaviour
    {
        [Header("调度预算")]
        [SerializeField] private int maxDecisionTicksPerFrame = 8;

        [Header("距离分层")]
        [SerializeField] private float nearDistance = 10f;
        [SerializeField] private float midDistance = 22f;
        [SerializeField] private float farDistance = 40f;

        [Header("思考间隔")]
        [SerializeField] private float nearThinkInterval = 0.12f;
        [SerializeField] private float midThinkInterval = 0.35f;
        [SerializeField] private float farThinkInterval = 1f;
        [SerializeField] private float sleepThinkInterval = 3f;

        private static EnemyAIScheduler _instance;
        private readonly List<EnemyBrain> _brains = new List<EnemyBrain>();
        private int _cursor;

        public static EnemyAIScheduler Instance
        {
            get
            {
                if (_instance != null)
                {
                    return _instance;
                }

                _instance = FindObjectOfType<EnemyAIScheduler>();
                if (_instance != null)
                {
                    return _instance;
                }

                GameObject schedulerObject = new GameObject("EnemyAIScheduler");
                _instance = schedulerObject.AddComponent<EnemyAIScheduler>();
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
        }

        private void Update()
        {
            TickBrains();
        }

        public void Register(EnemyBrain brain)
        {
            if (brain == null || _brains.Contains(brain))
            {
                return;
            }

            _brains.Add(brain);
            brain.SetSchedule(EnemyPerformanceTier.Near, Time.time);
        }

        public void Unregister(EnemyBrain brain)
        {
            if (brain == null)
            {
                return;
            }

            int index = _brains.IndexOf(brain);
            if (index < 0)
            {
                return;
            }

            _brains.RemoveAt(index);
            if (_cursor > index)
            {
                _cursor--;
            }

            if (_cursor >= _brains.Count)
            {
                _cursor = 0;
            }
        }

        public static void TryUnregister(EnemyBrain brain)
        {
            if (_instance == null)
            {
                return;
            }

            _instance.Unregister(brain);
        }

        private void TickBrains()
        {
            if (_brains.Count == 0)
            {
                return;
            }

            int checkedCount = 0;
            int tickedCount = 0;
            int maxChecks = _brains.Count;
            float now = Time.time;

            // 每帧只处理有限数量的思考，避免大量敌人同时跑行为树
            while (checkedCount < maxChecks && tickedCount < maxDecisionTicksPerFrame && _brains.Count > 0)
            {
                if (_cursor >= _brains.Count)
                {
                    _cursor = 0;
                }

                EnemyBrain brain = _brains[_cursor];
                _cursor++;
                checkedCount++;

                if (brain == null || !brain.IsActive)
                {
                    continue;
                }

                if (now < brain.NextThinkTime)
                {
                    continue;
                }

                EnemyPerformanceTier tier = ResolveTier(brain);
                float interval = ResolveInterval(brain, tier);
                brain.SetSchedule(tier, now + interval);
                brain.TickDecision();
                tickedCount++;
            }
        }

        private EnemyPerformanceTier ResolveTier(EnemyBrain brain)
        {
            float sqrDistance = brain.Blackboard.sqrDistanceToTarget;
            if (brain.AIProfile != null)
            {
                if (sqrDistance <= brain.AIProfile.nearDistance * brain.AIProfile.nearDistance)
                {
                    return EnemyPerformanceTier.Near;
                }

                if (sqrDistance <= brain.AIProfile.midDistance * brain.AIProfile.midDistance)
                {
                    return EnemyPerformanceTier.Mid;
                }

                if (sqrDistance <= brain.AIProfile.farDistance * brain.AIProfile.farDistance)
                {
                    return EnemyPerformanceTier.Far;
                }

                return EnemyPerformanceTier.Sleep;
            }

            if (sqrDistance <= nearDistance * nearDistance)
            {
                return EnemyPerformanceTier.Near;
            }

            if (sqrDistance <= midDistance * midDistance)
            {
                return EnemyPerformanceTier.Mid;
            }

            if (sqrDistance <= farDistance * farDistance)
            {
                return EnemyPerformanceTier.Far;
            }

            return EnemyPerformanceTier.Sleep;
        }

        private float ResolveInterval(EnemyBrain brain, EnemyPerformanceTier tier)
        {
            if (brain.AIProfile != null)
            {
                switch (tier)
                {
                    case EnemyPerformanceTier.Near:
                        return Mathf.Max(0.02f, brain.AIProfile.nearThinkInterval);
                    case EnemyPerformanceTier.Mid:
                        return Mathf.Max(0.05f, brain.AIProfile.midThinkInterval);
                    case EnemyPerformanceTier.Far:
                        return Mathf.Max(0.1f, brain.AIProfile.farThinkInterval);
                    case EnemyPerformanceTier.Sleep:
                    default:
                        return Mathf.Max(0.5f, brain.AIProfile.sleepThinkInterval);
                }
            }

            switch (tier)
            {
                case EnemyPerformanceTier.Near:
                    return Mathf.Max(0.02f, nearThinkInterval);
                case EnemyPerformanceTier.Mid:
                    return Mathf.Max(0.05f, midThinkInterval);
                case EnemyPerformanceTier.Far:
                    return Mathf.Max(0.1f, farThinkInterval);
                case EnemyPerformanceTier.Sleep:
                default:
                    return Mathf.Max(0.5f, sleepThinkInterval);
            }
        }
    }
}
