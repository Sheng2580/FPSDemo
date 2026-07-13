using System.Collections;
using System.Collections.Generic;
using Combat;
using Enemy;
using PlayerData;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerController))]
public class PlayerSkillController : MonoBehaviour
{
    private const float KnockbackForceToDistance = 0.18f;
    private const float DefaultSkillHitKnockbackDuration = 0.18f;

    [Header("技能配置")]
    [SerializeField] private PlayerSkillConfigAsset dodgeConfigAsset;
    [SerializeField] private PlayerSkillConfigAsset pushConfigAsset;
    [SerializeField] private PlayerSkillConfigAsset grenadeConfigAsset;
    [SerializeField] private bool loadDefaultConfigsFromResources = true;

    [Header("检测")]
    [SerializeField] private LayerMask skillHitLayerMask = ~0;
    [SerializeField] private QueryTriggerInteraction hitTriggerInteraction = QueryTriggerInteraction.Collide;
    [SerializeField] private Transform grenadeSpawnPoint;

    [Header("调试")]
    [SerializeField] private bool debugSkill;

    private readonly Dictionary<SkillType, PlayerSkillConfig> _configs = new Dictionary<SkillType, PlayerSkillConfig>();
    private readonly Dictionary<SkillType, PlayerSkillRuntimeData> _runtimeData = new Dictionary<SkillType, PlayerSkillRuntimeData>();
    private readonly Collider[] _hitBuffer = new Collider[48];
    private readonly HashSet<EnemyController> _hitEnemies = new HashSet<EnemyController>();
    private readonly List<IgnoredCollisionPair> _ignoredCollisionPairs = new List<IgnoredCollisionPair>();

    private PlayerController _player;
    private CharacterController _characterController;
    private Camera _playerCamera;
    private Coroutine _castRoutine;
    private Coroutine _restoreCollisionRoutine;
    private bool _isCastingSkill;
    private bool _isActionLocked;

    public bool IsCastingSkill => _isCastingSkill;

    private void Awake()
    {
        CacheReferences();
        InitSkills();
    }

    private void Update()
    {
        TickRuntimeData();
        TickSkillInput();
    }

    private void OnDisable()
    {
        if (_castRoutine != null)
        {
            StopCoroutine(_castRoutine);
            _castRoutine = null;
        }

        RestoreIgnoredEnemyCollisions();
        SetPlayerInvincible(false);
        SetActionLock(false, null);
        SetSkillMovementLocked(false);
        _isCastingSkill = false;
    }

    public bool TryCastSkill(SkillType skillType)
    {
        if (!_configs.TryGetValue(skillType, out PlayerSkillConfig config)
            || !_runtimeData.TryGetValue(skillType, out PlayerSkillRuntimeData runtimeData))
        {
            return false;
        }

        if (_isCastingSkill || runtimeData.cooldownRemaining > 0f)
        {
            BufferSkillInput(config, runtimeData);
            return false;
        }

        if (config.skillType == SkillType.Grenade && runtimeData.currentCount <= 0)
        {
            if (debugSkill)
            {
                Debug.Log("[Skill] 手雷数量不足", this);
            }

            return false;
        }

        _castRoutine = StartCoroutine(CastSkillRoutine(config, runtimeData));
        return true;
    }

    public bool ApplyCooldownMultiplier(SkillType skillType, float multiplier)
    {
        if (!_runtimeData.TryGetValue(skillType, out PlayerSkillRuntimeData runtimeData)
            || !_configs.TryGetValue(skillType, out PlayerSkillConfig config))
        {
            return false;
        }

        float previousCooldown = GetCooldown(config, runtimeData);
        float previousRemainingRate = previousCooldown > 0f
            ? Mathf.Clamp01(runtimeData.cooldownRemaining / previousCooldown)
            : 0f;

        runtimeData.cooldownMultiplier = Mathf.Max(0.01f, runtimeData.cooldownMultiplier * Mathf.Max(0f, multiplier));
        float nextCooldown = GetCooldown(config, runtimeData);
        runtimeData.cooldownRemaining = Mathf.Min(nextCooldown, nextCooldown * previousRemainingRate);
        EventCenter.Instance.EventTrigger(
            GameEvent.SkillCooldownChanged,
            new SkillCooldownEventData(config, runtimeData.cooldownRemaining, nextCooldown));
        return true;
    }

    public bool AddMaxCount(SkillType skillType, int count)
    {
        if (count == 0
            || !_runtimeData.TryGetValue(skillType, out PlayerSkillRuntimeData runtimeData)
            || !_configs.TryGetValue(skillType, out PlayerSkillConfig config))
        {
            return false;
        }

        runtimeData.maxCount = Mathf.Max(0, runtimeData.maxCount + count);
        if (count > 0)
        {
            runtimeData.currentCount = Mathf.Min(runtimeData.maxCount, runtimeData.currentCount + count);
        }
        else
        {
            runtimeData.currentCount = Mathf.Clamp(runtimeData.currentCount, 0, runtimeData.maxCount);
        }

        EventCenter.Instance.EventTrigger(
            GameEvent.SkillChargeChanged,
            new SkillChargeEventData(config, runtimeData.currentCount, runtimeData.maxCount));
        return true;
    }

    private void CacheReferences()
    {
        _player ??= GetComponent<PlayerController>();
        _characterController ??= GetComponent<CharacterController>();
        _playerCamera ??= GetComponentInChildren<Camera>(true);

        if (_player != null && _player.CameraController != null && _player.CameraController.PlayerCamera != null)
        {
            _playerCamera = _player.CameraController.PlayerCamera;
        }
    }

    private void InitSkills()
    {
        _configs.Clear();
        _runtimeData.Clear();

        AddSkillConfig(ResolveConfig(dodgeConfigAsset, "PlayerSkillConfigs/DefaultDodgeSkillConfig", PlayerSkillConfig.CreateDefaultDodge()));
        AddSkillConfig(ResolveConfig(pushConfigAsset, "PlayerSkillConfigs/DefaultPushSkillConfig", PlayerSkillConfig.CreateDefaultPush()));
        AddSkillConfig(ResolveConfig(grenadeConfigAsset, "PlayerSkillConfigs/DefaultGrenadeSkillConfig", PlayerSkillConfig.CreateDefaultGrenade()));
    }

    private PlayerSkillConfig ResolveConfig(
        PlayerSkillConfigAsset configAsset,
        string resourcePath,
        PlayerSkillConfig fallbackConfig)
    {
        PlayerSkillConfig runtimeConfig = null;
        if (configAsset != null)
        {
            runtimeConfig = configAsset.CreateRuntimeConfig();
        }
        else if (loadDefaultConfigsFromResources)
        {
            PlayerSkillConfigAsset loadedAsset = Resources.Load<PlayerSkillConfigAsset>(resourcePath);
            if (loadedAsset != null)
            {
                runtimeConfig = loadedAsset.CreateRuntimeConfig();
            }
        }

        runtimeConfig ??= fallbackConfig != null ? fallbackConfig.Clone() : PlayerSkillConfig.CreateDefaultDodge();
        runtimeConfig.ApplyMissingDefaults();
        return runtimeConfig;
    }

    private void AddSkillConfig(PlayerSkillConfig config)
    {
        if (config == null)
        {
            return;
        }

        _configs[config.skillType] = config;
        PlayerSkillRuntimeData runtimeData = new PlayerSkillRuntimeData();
        runtimeData.InitForNewRun(config);
        _runtimeData[config.skillType] = runtimeData;

        EventCenter.Instance.EventTrigger(
            GameEvent.SkillChargeChanged,
            new SkillChargeEventData(config, runtimeData.currentCount, runtimeData.maxCount));
    }

    private void TickRuntimeData()
    {
        foreach (KeyValuePair<SkillType, PlayerSkillRuntimeData> pair in _runtimeData)
        {
            PlayerSkillRuntimeData runtimeData = pair.Value;
            if (runtimeData == null)
            {
                continue;
            }

            if (runtimeData.cooldownRemaining > 0f)
            {
                runtimeData.cooldownRemaining = Mathf.Max(0f, runtimeData.cooldownRemaining - Time.deltaTime);
                if (_configs.TryGetValue(pair.Key, out PlayerSkillConfig config))
                {
                    EventCenter.Instance.EventTrigger(
                        GameEvent.SkillCooldownChanged,
                        new SkillCooldownEventData(config, runtimeData.cooldownRemaining, GetCooldown(config, runtimeData)));
                }
            }

            if (!runtimeData.pendingInput)
            {
                continue;
            }

            runtimeData.inputBufferRemaining = Mathf.Max(0f, runtimeData.inputBufferRemaining - Time.deltaTime);
            if (runtimeData.inputBufferRemaining <= 0f)
            {
                runtimeData.pendingInput = false;
                continue;
            }

            if (!_isCastingSkill && runtimeData.cooldownRemaining <= 0f)
            {
                runtimeData.pendingInput = false;
                TryCastSkill(pair.Key);
            }
        }
    }

    private void TickSkillInput()
    {
        GameInputManger input = GameInputManger.Instance;
        if (input == null)
        {
            return;
        }

        if (input.DodgeDown)
        {
            TryCastSkill(SkillType.Dodge);
        }

        if (input.PushDown)
        {
            TryCastSkill(SkillType.Push);
        }

        if (input.GrenadeDown)
        {
            TryCastSkill(SkillType.Grenade);
        }
    }

    private void BufferSkillInput(PlayerSkillConfig config, PlayerSkillRuntimeData runtimeData)
    {
        if (config == null || runtimeData == null || !config.canBufferInput)
        {
            return;
        }

        runtimeData.pendingInput = true;
        runtimeData.inputBufferRemaining = Mathf.Max(0f, config.inputBufferTime);
    }

    private IEnumerator CastSkillRoutine(PlayerSkillConfig config, PlayerSkillRuntimeData runtimeData)
    {
        _isCastingSkill = true;
        runtimeData.isCasting = true;
        runtimeData.castTimeRemaining = Mathf.Max(0f, config.duration);
        runtimeData.cooldownRemaining = GetCooldown(config, runtimeData);

        if (config.skillType == SkillType.Grenade)
        {
            runtimeData.currentCount = Mathf.Max(0, runtimeData.currentCount - 1);
            EventCenter.Instance.EventTrigger(
                GameEvent.SkillChargeChanged,
                new SkillChargeEventData(config, runtimeData.currentCount, runtimeData.maxCount));
        }

        Vector3 origin = GetSkillOrigin();
        Vector3 direction = GetSkillForward();
        SetActionLock(config.lockWeaponDuringCast, config);
        EventCenter.Instance.EventTrigger(GameEvent.MobileSightCanceled);
        EventCenter.Instance.EventTrigger(GameEvent.SkillCastStarted, new SkillCastEventData(_player, config, origin, direction));
        TriggerSkillVisual(config, config.castEffectKey, config.castAudioKey, origin, direction, config.duration, 1f);

        if (debugSkill)
        {
            Debug.Log($"[Skill] 开始释放 {config.skillName}", this);
        }

        switch (config.skillType)
        {
            case SkillType.Push:
                CastPush(config, runtimeData);
                break;
            case SkillType.Grenade:
                CastGrenade(config, runtimeData);
                break;
            case SkillType.Dodge:
            default:
                yield return CastDodgeRoutine(config, runtimeData);
                break;
        }

        if (config.skillType != SkillType.Dodge && config.duration > 0f)
        {
            yield return new WaitForSeconds(config.duration);
        }

        CompleteCast(config, runtimeData);
    }

    private IEnumerator CastDodgeRoutine(PlayerSkillConfig config, PlayerSkillRuntimeData runtimeData)
    {
        Vector3 dodgeDirection = ResolveDodgeDirection();
        float duration = Mathf.Max(0.01f, config.duration);
        float speed = Mathf.Max(0f, config.dodgeDistance) / duration;
        float elapsed = 0f;

        SetSkillMovementLocked(true);
        BeginIgnoreEnemyCollisions(config);
        StartCoroutine(InvincibleRoutine(config.invincibleDuration));

        while (elapsed < duration)
        {
            float deltaTime = Time.deltaTime;
            Vector3 motion = dodgeDirection * (speed * deltaTime);
            if (_characterController != null && _characterController.enabled)
            {
                _characterController.Move(motion);
            }
            else
            {
                transform.position += motion;
            }

            elapsed += deltaTime;
            runtimeData.castTimeRemaining = Mathf.Max(0f, duration - elapsed);
            yield return null;
        }

        SetSkillMovementLocked(false);
    }

    private void CastPush(PlayerSkillConfig config, PlayerSkillRuntimeData runtimeData)
    {
        _hitEnemies.Clear();
        Vector3 origin = GetSkillOrigin();
        Vector3 forward = GetSkillForward();
        Vector3 checkCenter = origin + forward * (Mathf.Max(0.1f, config.detectDistance) * 0.55f);
        float radius = Mathf.Max(0.1f, config.detectRadius);
        int hitCount = Physics.OverlapSphereNonAlloc(
            checkCenter,
            radius,
            _hitBuffer,
            ResolveHitLayerMask(),
            hitTriggerInteraction);

        int appliedCount = 0;
        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = _hitBuffer[i];
            if (!TryResolveEnemy(hitCollider, out EnemyController enemy, out EnemyHealth health))
            {
                continue;
            }

            if (_hitEnemies.Contains(enemy) || enemy.IsDead)
            {
                continue;
            }

            if (!IsEnemyInsidePushCone(config, origin, forward, enemy.transform.position))
            {
                continue;
            }

            ApplySkillDamage(config, runtimeData, enemy, health, hitCollider, origin);
            _hitEnemies.Add(enemy);
            appliedCount++;

            if (config.maxHitCount > 0 && appliedCount >= config.maxHitCount)
            {
                break;
            }
        }

        if (debugSkill)
        {
            Debug.Log($"[Skill] 推敌命中数量 {appliedCount}", this);
        }
    }

    private void CastGrenade(PlayerSkillConfig config, PlayerSkillRuntimeData runtimeData)
    {
        Vector3 spawnPosition = GetGrenadeSpawnPosition();
        Vector3 direction = GetSkillForward();
        GameObject grenadeObject = CreateGrenadeObject(config, spawnPosition, Quaternion.LookRotation(direction, Vector3.up));
        SkillGrenadeProjectile projectile = grenadeObject.GetComponent<SkillGrenadeProjectile>();
        if (projectile == null)
        {
            projectile = grenadeObject.AddComponent<SkillGrenadeProjectile>();
        }

        projectile.Init(_player, config, runtimeData, direction);
    }

    private void ApplySkillDamage(
        PlayerSkillConfig config,
        PlayerSkillRuntimeData runtimeData,
        EnemyController enemy,
        EnemyHealth health,
        Collider hitCollider,
        Vector3 origin)
    {
        if (config == null || runtimeData == null || enemy == null || health == null)
        {
            return;
        }

        Vector3 hitPoint = hitCollider != null ? hitCollider.ClosestPoint(origin) : enemy.transform.position;
        Vector3 knockbackDirection = enemy.transform.position - transform.position;
        knockbackDirection.y = 0f;
        if (knockbackDirection.sqrMagnitude <= 0.0001f)
        {
            knockbackDirection = GetSkillForward();
        }

        float finalDamage = Mathf.Max(0f, config.damage * Mathf.Max(0f, runtimeData.damageMultiplier));
        DamageInfo damageInfo = new DamageInfo(
            finalDamage,
            -config.skillId,
            config.skillName,
            gameObject,
            hitCollider,
            hitPoint,
            -knockbackDirection.normalized);
        damageInfo.ApplyBodyPart(EnemyHitBodyPart.Body, 1f, false);
        damageInfo.ApplyCustomHitReaction(
            knockbackDirection,
            config.knockbackForce * KnockbackForceToDistance,
            DefaultSkillHitKnockbackDuration,
            Mathf.Max(0.01f, config.stunDuration),
            true);

        health.TakeDamage(damageInfo);
        EventCenter.Instance.EventTrigger(
            GameEvent.SkillHitEnemy,
            new SkillHitEnemyEventData(config, enemy, damageInfo.finalDamage, hitPoint, knockbackDirection));
        TriggerSkillVisual(config, config.hitEffectKey, config.hitAudioKey, hitPoint, knockbackDirection, config.stunDuration, 1f);
    }

    private void CompleteCast(PlayerSkillConfig config, PlayerSkillRuntimeData runtimeData)
    {
        runtimeData.isCasting = false;
        runtimeData.castTimeRemaining = 0f;
        _isCastingSkill = false;
        _castRoutine = null;
        SetActionLock(false, config);
        SetSkillMovementLocked(false);
        EventCenter.Instance.EventTrigger(GameEvent.SkillCastCompleted, new SkillCastEventData(_player, config, GetSkillOrigin(), GetSkillForward()));

        if (debugSkill)
        {
            Debug.Log($"[Skill] 释放完成 {config.skillName}", this);
        }
    }

    private Vector3 ResolveDodgeDirection()
    {
        Vector2 moveInput = GameInputManger.Instance != null ? GameInputManger.Instance.Movement : Vector2.zero;
        Vector3 forward = transform.forward;
        Vector3 right = transform.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 direction = forward * moveInput.y + right * moveInput.x;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f)
        {
            return direction.normalized;
        }

        return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
    }

    private bool IsEnemyInsidePushCone(PlayerSkillConfig config, Vector3 origin, Vector3 forward, Vector3 enemyPosition)
    {
        Vector3 toEnemy = enemyPosition - origin;
        toEnemy.y = 0f;
        float distance = toEnemy.magnitude;
        if (distance > Mathf.Max(0.1f, config.detectDistance + config.detectRadius))
        {
            return false;
        }

        if (toEnemy.sqrMagnitude <= 0.0001f)
        {
            return true;
        }

        float angle = Vector3.Angle(forward, toEnemy.normalized);
        return angle <= Mathf.Max(1f, config.detectAngle) * 0.5f;
    }

    private bool TryResolveEnemy(Collider hitCollider, out EnemyController enemy, out EnemyHealth health)
    {
        enemy = null;
        health = null;
        if (hitCollider == null)
        {
            return false;
        }

        enemy = hitCollider.GetComponentInParent<EnemyController>();
        health = hitCollider.GetComponentInParent<EnemyHealth>();
        if (enemy == null)
        {
            enemy = hitCollider.GetComponentInChildren<EnemyController>();
        }

        if (health == null)
        {
            health = hitCollider.GetComponentInChildren<EnemyHealth>();
        }

        return enemy != null && health != null;
    }

    private GameObject CreateGrenadeObject(PlayerSkillConfig config, Vector3 position, Quaternion rotation)
    {
        GameObject prefab = !string.IsNullOrEmpty(config.projectileResourceKey)
            ? Resources.Load<GameObject>(config.projectileResourceKey)
            : null;
        GameObject grenadeObject = prefab != null
            ? Instantiate(prefab, position, rotation)
            : GameObject.CreatePrimitive(PrimitiveType.Sphere);

        grenadeObject.name = string.IsNullOrEmpty(config.projectileResourceKey)
            ? "SkillGrenade"
            : config.projectileResourceKey;
        grenadeObject.transform.SetPositionAndRotation(position, rotation);
        grenadeObject.transform.localScale = prefab != null ? grenadeObject.transform.localScale : Vector3.one * 0.22f;

        Rigidbody rigidbody = grenadeObject.GetComponent<Rigidbody>();
        if (rigidbody == null)
        {
            rigidbody = grenadeObject.AddComponent<Rigidbody>();
        }

        rigidbody.useGravity = true;
        rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        return grenadeObject;
    }

    private Vector3 GetSkillOrigin()
    {
        if (_playerCamera != null)
        {
            return _playerCamera.transform.position;
        }

        return transform.position + Vector3.up * 1.5f;
    }

    private Vector3 GetSkillForward()
    {
        Transform source = _playerCamera != null ? _playerCamera.transform : transform;
        Vector3 forward = source.forward;
        return forward.sqrMagnitude > 0.0001f ? forward.normalized : transform.forward;
    }

    private Vector3 GetGrenadeSpawnPosition()
    {
        if (grenadeSpawnPoint != null)
        {
            return grenadeSpawnPoint.position;
        }

        return GetSkillOrigin() + GetSkillForward() * 0.45f + Vector3.down * 0.12f;
    }

    private float GetCooldown(PlayerSkillConfig config, PlayerSkillRuntimeData runtimeData)
    {
        if (config == null)
        {
            return 0f;
        }

        float multiplier = runtimeData != null ? Mathf.Max(0f, runtimeData.cooldownMultiplier) : 1f;
        return Mathf.Max(0f, config.cooldown * multiplier);
    }

    private int ResolveHitLayerMask()
    {
        return skillHitLayerMask.value == 0 ? Physics.DefaultRaycastLayers : skillHitLayerMask.value;
    }

    private void SetActionLock(bool isLocked, PlayerSkillConfig config)
    {
        if (_isActionLocked == isLocked)
        {
            return;
        }

        _isActionLocked = isLocked;
        EventCenter.Instance.EventTrigger(
            GameEvent.PlayerActionLockChanged,
            new PlayerActionLockEventData(isLocked, config != null ? config.skillType : SkillType.Dodge, config != null ? config.skillName : string.Empty));
    }

    private void SetSkillMovementLocked(bool isLocked)
    {
        _player?.SetSkillMovementLocked(isLocked);
    }

    private void TriggerSkillVisual(
        PlayerSkillConfig config,
        string effectKey,
        string audioKey,
        Vector3 position,
        Vector3 direction,
        float duration,
        float intensity)
    {
        EventCenter.Instance.EventTrigger(
            GameEvent.SkillVisualStarted,
            new SkillVisualEventData(config, effectKey, audioKey, position, direction, duration, intensity));
    }

    private IEnumerator InvincibleRoutine(float duration)
    {
        if (duration <= 0f)
        {
            yield break;
        }

        SetPlayerInvincible(true);
        yield return new WaitForSeconds(duration);
        SetPlayerInvincible(false);
    }

    private void SetPlayerInvincible(bool isInvincible)
    {
        if (_player == null || _player.Stats == null || _player.Stats.RuntimeData == null)
        {
            return;
        }

        _player.Stats.RuntimeData.isInvincible = isInvincible;
    }

    private void BeginIgnoreEnemyCollisions(PlayerSkillConfig config)
    {
        RestoreIgnoredEnemyCollisions();
        float duration = Mathf.Max(0f, config.collisionDisableDuration);
        if (duration <= 0f)
        {
            return;
        }

        Collider[] playerColliders = GetComponentsInChildren<Collider>(true);
        if (playerColliders == null || playerColliders.Length == 0)
        {
            return;
        }

        float radius = Mathf.Max(2f, config.dodgeDistance + 2f);
        int hitCount = Physics.OverlapSphereNonAlloc(
            transform.position,
            radius,
            _hitBuffer,
            ResolveHitLayerMask(),
            QueryTriggerInteraction.Collide);

        for (int i = 0; i < hitCount; i++)
        {
            Collider enemyCollider = _hitBuffer[i];
            if (enemyCollider == null || enemyCollider.GetComponentInParent<EnemyController>() == null)
            {
                continue;
            }

            for (int j = 0; j < playerColliders.Length; j++)
            {
                Collider playerCollider = playerColliders[j];
                if (playerCollider == null || playerCollider == enemyCollider)
                {
                    continue;
                }

                Physics.IgnoreCollision(playerCollider, enemyCollider, true);
                _ignoredCollisionPairs.Add(new IgnoredCollisionPair(playerCollider, enemyCollider));
            }
        }

        _restoreCollisionRoutine = StartCoroutine(RestoreIgnoredCollisionsRoutine(duration));
    }

    private IEnumerator RestoreIgnoredCollisionsRoutine(float duration)
    {
        yield return new WaitForSeconds(duration);
        RestoreIgnoredEnemyCollisions();
    }

    private void RestoreIgnoredEnemyCollisions()
    {
        if (_restoreCollisionRoutine != null)
        {
            StopCoroutine(_restoreCollisionRoutine);
            _restoreCollisionRoutine = null;
        }

        for (int i = 0; i < _ignoredCollisionPairs.Count; i++)
        {
            IgnoredCollisionPair pair = _ignoredCollisionPairs[i];
            if (pair.playerCollider != null && pair.enemyCollider != null)
            {
                Physics.IgnoreCollision(pair.playerCollider, pair.enemyCollider, false);
            }
        }

        _ignoredCollisionPairs.Clear();
    }

    private readonly struct IgnoredCollisionPair
    {
        public readonly Collider playerCollider;
        public readonly Collider enemyCollider;

        public IgnoredCollisionPair(Collider playerCollider, Collider enemyCollider)
        {
            this.playerCollider = playerCollider;
            this.enemyCollider = enemyCollider;
        }
    }
}
