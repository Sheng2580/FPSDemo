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
    private const float DefaultSkillHitKnockbackDuration = 0.32f;
    private const float GrenadePhysicalRadius = 0.22f;
    private const float GrenadeEnemyTriggerRadius = 0.7f;
    private const float GrenadeSpawnForwardOffset = 0.65f;
    private const float GrenadeSpawnDownOffset = 0.12f;

    [Header("技能配置")]
    [SerializeField] private PlayerSkillConfigAsset dodgeConfigAsset;
    [SerializeField] private PlayerSkillConfigAsset pushConfigAsset;
    [SerializeField] private PlayerSkillConfigAsset grenadeConfigAsset;
    [SerializeField] private bool loadDefaultConfigsFromResources = true;

    [Header("检测")]
    [SerializeField] private LayerMask skillHitLayerMask = ~0;
    [SerializeField] private QueryTriggerInteraction hitTriggerInteraction = QueryTriggerInteraction.Collide;

    [Header("调试")]
    [SerializeField] private bool debugSkill;

    private readonly Dictionary<SkillType, PlayerSkillConfig> _configs = new Dictionary<SkillType, PlayerSkillConfig>();
    private readonly Dictionary<SkillType, PlayerSkillRuntimeData> _runtimeData = new Dictionary<SkillType, PlayerSkillRuntimeData>();
    private readonly Collider[] _hitBuffer = new Collider[128];
    private readonly HashSet<EnemyController> _hitEnemies = new HashSet<EnemyController>();
    private readonly List<IgnoredCollisionPair> _ignoredCollisionPairs = new List<IgnoredCollisionPair>();

    private PlayerController _player;
    private CharacterController _characterController;
    private Camera _playerCamera;
    private Coroutine _castRoutine;
    private Coroutine _restoreCollisionRoutine;
    private bool _isCastingSkill;
    private bool _isActionLocked;
    private float _permanentCooldownReduction;

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

    public bool ApplyCooldownReduction(SkillType skillType, float reduction)
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

        runtimeData.blessingCooldownReduction = PlayerSkillConfigJsonLoader.Rules.ClampBlessingCooldownReduction(
            runtimeData.blessingCooldownReduction + Mathf.Max(0f, reduction));
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

    public bool ApplyDamageMultiplier(SkillType skillType, float multiplier)
    {
        if (!_runtimeData.TryGetValue(skillType, out PlayerSkillRuntimeData runtimeData))
        {
            return false;
        }

        runtimeData.damageMultiplier = Mathf.Max(
            0f,
            runtimeData.damageMultiplier * Mathf.Max(0f, multiplier));
        return true;
    }

    public int AddCurrentCount(SkillType skillType, int count)
    {
        if (count <= 0
            || !_runtimeData.TryGetValue(skillType, out PlayerSkillRuntimeData runtimeData)
            || !_configs.TryGetValue(skillType, out PlayerSkillConfig config))
        {
            return 0;
        }

        int previousCount = runtimeData.currentCount;
        runtimeData.currentCount = Mathf.Min(runtimeData.maxCount, runtimeData.currentCount + count);
        int addedCount = runtimeData.currentCount - previousCount;
        if (addedCount > 0)
        {
            EventCenter.Instance.EventTrigger(
                GameEvent.SkillChargeChanged,
                new SkillChargeEventData(config, runtimeData.currentCount, runtimeData.maxCount));
        }

        return addedCount;
    }

    public bool TryGetRuntimeState(
        SkillType skillType,
        out float cooldownRemaining,
        out float cooldownDuration,
        out int currentCount,
        out int maxCount)
    {
        cooldownRemaining = 0f;
        cooldownDuration = 0f;
        currentCount = 0;
        maxCount = 0;
        if (!_configs.TryGetValue(skillType, out PlayerSkillConfig config)
            || !_runtimeData.TryGetValue(skillType, out PlayerSkillRuntimeData runtimeData)
            || config == null
            || runtimeData == null)
        {
            return false;
        }

        cooldownRemaining = Mathf.Max(0f, runtimeData.cooldownRemaining);
        cooldownDuration = GetCooldown(config, runtimeData);
        currentCount = Mathf.Max(0, runtimeData.currentCount);
        maxCount = Mathf.Max(0, runtimeData.maxCount);
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
        _permanentCooldownReduction = ResolvePermanentCooldownReduction();

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
        if (fallbackConfig != null
            && PlayerSkillConfigJsonLoader.TryGetConfig(fallbackConfig.skillType, out PlayerSkillConfig jsonConfig))
        {
            runtimeConfig = jsonConfig;
        }
        else if (configAsset != null)
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
        runtimeData.permanentCooldownReduction = _permanentCooldownReduction;
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
                yield return CastPushRoutine(config, runtimeData);
                break;
            case SkillType.Grenade:
                CastGrenade(config, runtimeData);
                break;
            case SkillType.Dodge:
            default:
                yield return CastDodgeRoutine(config, runtimeData);
                break;
        }

        if (config.skillType == SkillType.Grenade && config.duration > 0f)
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
        Vector3 circleCenter = transform.position;
        float centerHeight = _characterController != null
            ? Mathf.Max(0.5f, _characterController.height * 0.5f)
            : 1f;
        Vector3 checkCenter = circleCenter + Vector3.up * centerHeight;
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

            if (!IsEnemyInsidePushCircle(circleCenter, radius, enemy.transform.position))
            {
                continue;
            }

            ApplySkillDamage(config, runtimeData, enemy, health, hitCollider, checkCenter);
            _hitEnemies.Add(enemy);
            appliedCount++;

            if (config.maxHitCount > 0 && appliedCount >= config.maxHitCount)
            {
                break;
            }
        }

        if (debugSkill)
        {
            Debug.Log($"[Skill] 圆形推敌 半径={radius:0.##} 命中={appliedCount}", this);
        }
    }

    private IEnumerator CastPushRoutine(PlayerSkillConfig config, PlayerSkillRuntimeData runtimeData)
    {
        float duration = Mathf.Max(0f, config.duration);
        float impactTime = Mathf.Min(0.18f, duration * 0.28f);
        if (impactTime > 0f)
        {
            yield return new WaitForSeconds(impactTime);
        }

        CastPush(config, runtimeData);

        float remainingTime = Mathf.Max(0f, duration - impactTime);
        if (remainingTime > 0f)
        {
            yield return new WaitForSeconds(remainingTime);
        }
    }

    private void CastGrenade(PlayerSkillConfig config, PlayerSkillRuntimeData runtimeData)
    {
        if (config == null)
        {
            return;
        }

        PlayerSkillConfig projectileConfig = config.Clone();
        string projectileKey = string.IsNullOrEmpty(projectileConfig.projectileResourceKey)
            ? "bomb"
            : projectileConfig.projectileResourceKey;

        PoolMgr.Instance.GetObjForAB(EffectMgr.SkillEffectBundleName, projectileKey, grenadeObject =>
        {
            if (grenadeObject == null)
            {
                Debug.LogError($"[Skill] 炸弹资源加载失败 {projectileKey}", this);
                return;
            }

            if (this == null || _player == null)
            {
                PoolMgr.Instance.pushObj(projectileKey, grenadeObject);
                return;
            }

            // AB 异步返回后重新读取玩家和摄像机位置 避免移动时使用旧坐标
            Vector3 direction = GetSkillForward();
            Vector3 spawnPosition = GetGrenadeSpawnPosition(direction);
            Quaternion rotation = Quaternion.LookRotation(direction, Vector3.up);

            PrepareGrenadeObject(grenadeObject, projectileKey, spawnPosition, rotation);
            SkillGrenadeProjectile projectile = grenadeObject.GetComponent<SkillGrenadeProjectile>();
            if (projectile == null)
            {
                projectile = grenadeObject.AddComponent<SkillGrenadeProjectile>();
            }

            projectile.Init(_player, projectileConfig, runtimeData, direction, projectileKey);
        });
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

    private static bool IsEnemyInsidePushCircle(Vector3 center, float radius, Vector3 enemyPosition)
    {
        Vector3 toEnemy = enemyPosition - center;
        toEnemy.y = 0f;
        float safeRadius = Mathf.Max(0.1f, radius);
        return toEnemy.sqrMagnitude <= safeRadius * safeRadius;
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

    private static void PrepareGrenadeObject(
        GameObject grenadeObject,
        string projectileKey,
        Vector3 position,
        Quaternion rotation)
    {
        grenadeObject.name = projectileKey;
        grenadeObject.transform.SetParent(null, true);

        Rigidbody rigidbody = grenadeObject.GetComponent<Rigidbody>();
        if (rigidbody == null)
        {
            rigidbody = grenadeObject.AddComponent<Rigidbody>();
        }

        // 定位前先冻结刚体 避免对象池激活时和玩家碰撞体发生解穿
        rigidbody.detectCollisions = false;
        rigidbody.velocity = Vector3.zero;
        rigidbody.angularVelocity = Vector3.zero;
        rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rigidbody.isKinematic = true;
        rigidbody.interpolation = RigidbodyInterpolation.None;
        grenadeObject.transform.SetPositionAndRotation(position, rotation);
        rigidbody.position = position;
        rigidbody.rotation = rotation;
        Physics.SyncTransforms();

        rigidbody.useGravity = true;
        rigidbody.interpolation = RigidbodyInterpolation.Interpolate;

        SphereCollider physicalCollider = null;
        SphereCollider enemyTrigger = null;
        SphereCollider[] sphereColliders = grenadeObject.GetComponents<SphereCollider>();
        for (int i = 0; i < sphereColliders.Length; i++)
        {
            SphereCollider sphereCollider = sphereColliders[i];
            if (sphereCollider.isTrigger)
            {
                enemyTrigger ??= sphereCollider;
            }
            else
            {
                physicalCollider ??= sphereCollider;
            }
        }

        if (physicalCollider == null && grenadeObject.GetComponentInChildren<Collider>(true) == null)
        {
            physicalCollider = grenadeObject.AddComponent<SphereCollider>();
        }

        if (physicalCollider != null)
        {
            physicalCollider.radius = GrenadePhysicalRadius;
        }

        if (enemyTrigger != null)
        {
            enemyTrigger.radius = GrenadeEnemyTriggerRadius;
        }

        ExcludeGrenadeIgnoredLayers(grenadeObject);
    }

    private static void ExcludeGrenadeIgnoredLayers(GameObject grenadeObject)
    {
        int playerLayer = CombatLayerNames.PlayerLayer;
        int wallLayer = CombatLayerNames.PlayerBoundaryLayer;
        int enemyLayer = CombatLayerNames.EnemyLayer;
        if (grenadeObject == null)
        {
            return;
        }

        int commonExcludedMask = 0;
        if (playerLayer >= 0)
        {
            commonExcludedMask |= 1 << playerLayer;
        }

        if (wallLayer >= 0)
        {
            commonExcludedMask |= 1 << wallLayer;
        }

        int physicalExcludedMask = commonExcludedMask;
        if (enemyLayer >= 0)
        {
            physicalExcludedMask |= 1 << enemyLayer;
        }

        if (physicalExcludedMask == 0)
        {
            return;
        }

        Collider[] colliders = grenadeObject.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider targetCollider = colliders[i];
            if (targetCollider != null)
            {
                targetCollider.excludeLayers |= targetCollider.isTrigger
                    ? commonExcludedMask
                    : physicalExcludedMask;
            }
        }
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
        Vector3 forward = _playerCamera != null
            ? _playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f)).direction
            : transform.forward;
        return forward.sqrMagnitude > 0.0001f ? forward.normalized : transform.forward;
    }

    private Vector3 GetGrenadeSpawnPosition(Vector3 direction)
    {
        return GetSkillOrigin()
               + direction * GrenadeSpawnForwardOffset
               + Vector3.down * GrenadeSpawnDownOffset;
    }

    private float GetCooldown(PlayerSkillConfig config, PlayerSkillRuntimeData runtimeData)
    {
        if (config == null)
        {
            return 0f;
        }

        float permanentReduction = runtimeData != null ? runtimeData.permanentCooldownReduction : 0f;
        float blessingReduction = runtimeData != null ? runtimeData.blessingCooldownReduction : 0f;
        return PlayerSkillConfigJsonLoader.Rules.CalculateCooldown(
            config.cooldown,
            permanentReduction,
            blessingReduction);
    }

    private float ResolvePermanentCooldownReduction()
    {
        PlayerSaveData saveData = PlayerProgressSaveService.Load();
        int level = saveData != null ? saveData.skillCooldownLevel : 0;
        return PermanentUpgradeConfigLoader.GetPlayerSkillCooldownReduction(level, level * 0.03f);
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
            new SkillVisualEventData(_player, config, effectKey, audioKey, position, direction, duration, intensity));
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
