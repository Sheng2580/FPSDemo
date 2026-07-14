using UnityEngine;

namespace Enemy
{
    /// <summary>
    /// 敌人3D音效入口
    /// 只保存单个敌人的播放节流状态 音频资源和AudioSource统一由MusicMgr管理
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyAudioController : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private EnemyController controller;

        private EnemyAudioProfile _profile;
        private Transform _target;
        private float _nextHitSoundTime;
        private float _nextAmbientSoundTime;
        private float _nextMovementSoundTime;
        private bool _initialized;

        private static float _nextGlobalSpawnSoundTime;
        private static float _nextGlobalMovementSoundTime;

        private void Awake()
        {
            controller ??= GetComponent<EnemyController>();
        }

        private void Reset()
        {
            controller = GetComponent<EnemyController>();
        }

        public void Init(int enemyId, Transform target)
        {
            _profile = EnemyAudioProfileLibrary.Get(enemyId);
            _target = target;
            _nextHitSoundTime = 0f;
            _initialized = _profile != null;
            ScheduleNextAmbientSound();
            ScheduleNextMovementSound();
            PlaySpawn();
        }

        public void Deactivate()
        {
            _target = null;
            _initialized = false;
        }

        public void PlayAttack()
        {
            if (!_initialized)
            {
                return;
            }

            PlayRandom(
                _profile.attackClips,
                _profile.attackVolume,
                _profile.pitchRandom,
                _profile.minDistance,
                _profile.maxDistance,
                EnemyAudioPriority.Attack,
                true);
        }

        public void PlayHit()
        {
            if (!_initialized || Time.time < _nextHitSoundTime)
            {
                return;
            }

            _nextHitSoundTime = Time.time + _profile.hitCooldown;
            PlayRandom(
                _profile.hitClips,
                _profile.hitVolume,
                _profile.pitchRandom,
                _profile.minDistance,
                _profile.maxDistance,
                EnemyAudioPriority.Hit,
                true);
        }

        public void PlayDeath()
        {
            if (!_initialized)
            {
                return;
            }

            PlayRandom(
                _profile.deathClips,
                _profile.deathVolume,
                _profile.pitchRandom * 0.5f,
                _profile.minDistance,
                _profile.maxDistance,
                EnemyAudioPriority.Death,
                true);
        }

        public void Tick(bool isMoving)
        {
            TickMovement(isMoving);
            TickAmbient();
        }

        private void PlaySpawn()
        {
            if (!_initialized || Time.time < _nextGlobalSpawnSoundTime)
            {
                return;
            }

            _nextGlobalSpawnSoundTime = Time.time + 0.4f;
            PlayRandom(
                _profile.spawnClips,
                _profile.spawnVolume,
                _profile.pitchRandom,
                _profile.minDistance,
                _profile.maxDistance,
                EnemyAudioPriority.Spawn,
                true);
        }

        private void TickMovement(bool isMoving)
        {
            if (!_initialized
                || !isMoving
                || _target == null
                || Time.time < _nextMovementSoundTime)
            {
                return;
            }

            ScheduleNextMovementSound();
            Vector3 offset = _target.position - transform.position;
            if (offset.sqrMagnitude > _profile.movementMaxDistance * _profile.movementMaxDistance)
            {
                return;
            }

            if (Time.time < _nextGlobalMovementSoundTime
                || Random.value > _profile.movementChance)
            {
                return;
            }

            _nextGlobalMovementSoundTime = Time.time + 0.9f;
            PlayRandom(
                _profile.movementClips,
                _profile.movementVolume,
                _profile.pitchRandom,
                _profile.minDistance,
                _profile.movementMaxDistance,
                EnemyAudioPriority.Movement,
                false);
        }

        private void TickAmbient()
        {
            if (!_initialized || Time.time < _nextAmbientSoundTime)
            {
                return;
            }

            ScheduleNextAmbientSound();
            if (_target == null || Random.value > _profile.ambientChance)
            {
                return;
            }

            Vector3 offset = _target.position - transform.position;
            if (offset.sqrMagnitude > _profile.maxDistance * _profile.maxDistance)
            {
                return;
            }

            PlayRandom(
                _profile.ambientClips,
                _profile.ambientVolume,
                _profile.pitchRandom,
                _profile.minDistance,
                _profile.maxDistance,
                EnemyAudioPriority.Ambient,
                false);
        }

        private void ScheduleNextAmbientSound()
        {
            if (_profile == null)
            {
                _nextAmbientSoundTime = float.PositiveInfinity;
                return;
            }

            _nextAmbientSoundTime = Time.time + Random.Range(
                _profile.ambientIntervalMin,
                _profile.ambientIntervalMax);
        }

        private void ScheduleNextMovementSound()
        {
            if (_profile == null)
            {
                _nextMovementSoundTime = float.PositiveInfinity;
                return;
            }

            _nextMovementSoundTime = Time.time + Random.Range(
                _profile.movementIntervalMin,
                _profile.movementIntervalMax);
        }

        private void PlayRandom(
            string[] clipNames,
            float volume,
            float pitchRandom,
            float minDistance,
            float maxDistance,
            int priority,
            bool allowSteal)
        {
            if (clipNames == null || clipNames.Length == 0)
            {
                return;
            }

            string clipName = clipNames[Random.Range(0, clipNames.Length)];
            MusicMgr.Instance?.PlayWorldSoundForAB(
                clipName,
                transform.position,
                MusicMgr.EnemyAudioBundleName,
                volume,
                pitchRandom,
                minDistance,
                maxDistance,
                priority,
                allowSteal);
        }
    }

    internal static class EnemyAudioPriority
    {
        public const int Death = 48;
        public const int Attack = 80;
        public const int Spawn = 100;
        public const int Hit = 120;
        public const int Ambient = 190;
        public const int Movement = 220;
    }

    internal sealed class EnemyAudioProfile
    {
        public readonly string[] spawnClips;
        public readonly string[] attackClips;
        public readonly string[] hitClips;
        public readonly string[] deathClips;
        public readonly string[] ambientClips;
        public readonly string[] movementClips;
        public readonly float spawnVolume;
        public readonly float attackVolume;
        public readonly float hitVolume;
        public readonly float deathVolume;
        public readonly float ambientVolume;
        public readonly float movementVolume;
        public readonly float pitchRandom;
        public readonly float minDistance;
        public readonly float maxDistance;
        public readonly float movementMaxDistance;
        public readonly float hitCooldown;
        public readonly float ambientIntervalMin;
        public readonly float ambientIntervalMax;
        public readonly float ambientChance;
        public readonly float movementIntervalMin;
        public readonly float movementIntervalMax;
        public readonly float movementChance;

        public EnemyAudioProfile(
            string[] spawnClips,
            string[] attackClips,
            string[] hitClips,
            string[] deathClips,
            string[] ambientClips,
            string[] movementClips,
            float pitchRandom = 0.06f,
            float maxDistance = 22f)
        {
            this.spawnClips = spawnClips;
            this.attackClips = attackClips;
            this.hitClips = hitClips;
            this.deathClips = deathClips;
            this.ambientClips = ambientClips;
            this.movementClips = movementClips;
            spawnVolume = 0.65f;
            attackVolume = 0.85f;
            hitVolume = 0.65f;
            deathVolume = 0.9f;
            ambientVolume = 0.55f;
            movementVolume = 0.38f;
            this.pitchRandom = pitchRandom;
            minDistance = 1.5f;
            this.maxDistance = maxDistance;
            movementMaxDistance = Mathf.Min(14f, maxDistance);
            hitCooldown = 0.16f;
            ambientIntervalMin = 7f;
            ambientIntervalMax = 13f;
            ambientChance = 0.35f;
            movementIntervalMin = 4.2f;
            movementIntervalMax = 6.5f;
            movementChance = 0.45f;
        }
    }

    internal static class EnemyAudioProfileLibrary
    {
        private static readonly EnemyAudioProfile SkeletonProfile = new EnemyAudioProfile(
            new[] { "Zombie Hello_02" },
            new[] { "Zombie Attack_01", "Zombie Attack_02" },
            new[] { "Zombie Hit_01", "Zombie Hit_03" },
            new[] { "Zombie Death_01", "Zombie Death_02" },
            new[] { "Zombie Growl_01", "Zombie Growl_02" },
            new[] { "Zombie Running_01", "Zombie Running_02" });

        private static readonly EnemyAudioProfile NerdProfile = new EnemyAudioProfile(
            new[] { "Zombie Hello_02" },
            new[] { "Zombie Attack_02", "Zombie Attack_03" },
            new[] { "Zombie Hit_02", "Zombie Hit_04" },
            new[] { "Zombie Death_03", "Zombie Death_04" },
            new[] { "Zombie Growl_03", "Zombie Growl_04" },
            new[] { "Zombie Running_01", "Zombie Running_02" },
            0.08f,
            21f);

        private static readonly EnemyAudioProfile OldCroneProfile = new EnemyAudioProfile(
            new[] { "Zombie Sudden Growl_01" },
            new[] { "Zombie Attack_01", "Zombie Attack_03" },
            new[] { "Zombie Hit_02", "Zombie Hit_05" },
            new[] { "Zombie Death_05", "Zombie Death_14" },
            new[] { "Zombie Deep_01", "Zombie Growl_06" },
            new[] { "Zombie Running_01", "Zombie Running_02" },
            0.04f,
            26f);

        public static EnemyAudioProfile Get(int enemyId)
        {
            switch (enemyId)
            {
                case 1002:
                    return NerdProfile;
                case 1003:
                    return OldCroneProfile;
                default:
                    return SkeletonProfile;
            }
        }
    }
}
