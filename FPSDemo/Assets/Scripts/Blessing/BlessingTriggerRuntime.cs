using System.Collections.Generic;
using Blessing.Data;
using Combat;
using Enemy;
using Pickup;
using Pickup.Data;
using PlayerData;
using UnityEngine;

/// <summary>
/// 本局祝福触发效果运行器
/// </summary>
public sealed class BlessingTriggerRuntime
{
    private const string AerialBombBundleName = "skill";
    private const string DefaultAerialBombAssetName = "bomb";
    private const string RandomPickupEffectKey = "RandomPickupNoBerserk";
    private const float AerialBombMinimumSpawnHeight = 2.8f;
    private const float AerialBombMaximumSpawnHeight = 5.8f;
    private const float AerialBombMinimumSpawnRadius = 4f;
    private const float AerialBombMaximumSpawnRadius = 7f;
    private const float AerialBombGoldenAngle = 137.5f;
    private const float AerialBombFallSpeed = 20f;
    private const int ExplosiveBulletDamageSourceId = -1401;
    private const string ExplosiveBulletDamageSourceName = "爆炸子弹";
    private const float ExplosionEffectLifeTime = 2.5f;
    private const float ExplosionHapticIntensity = 0.72f;
    private const float ExplosionHapticMaxDistance = 18f;
    private const float TriggerTipDuration = 1.5f;

    private readonly Dictionary<int, BlessingTriggerConfig> _killExplosionTriggers = new Dictionary<int, BlessingTriggerConfig>();
    private readonly Dictionary<int, BlessingTriggerConfig> _killPickupDropTriggers = new Dictionary<int, BlessingTriggerConfig>();
    private readonly Dictionary<int, BlessingTriggerConfig> _hitAerialBombTriggers = new Dictionary<int, BlessingTriggerConfig>();
    private readonly Collider[] _hitBuffer = new Collider[96];
    private readonly HashSet<EnemyController> _hitEnemies = new HashSet<EnemyController>();
    private float _nextAerialBombTriggerTime;
    private float _nextPickupDropTriggerTime;
    private int _activeAerialBombCount;
    private int _aerialBombSpawnSequence;
    private int _generation;

    public void Reset()
    {
        _killExplosionTriggers.Clear();
        _killPickupDropTriggers.Clear();
        _hitAerialBombTriggers.Clear();
        _hitEnemies.Clear();
        _nextAerialBombTriggerTime = 0f;
        _nextPickupDropTriggerTime = 0f;
        _activeAerialBombCount = 0;
        _aerialBombSpawnSequence = 0;
        _generation++;
    }

    public void ApplyConfig(BlessingConfig config)
    {
        if (config?.triggers == null)
        {
            return;
        }

        for (int i = 0; i < config.triggers.Length; i++)
        {
            BlessingTriggerConfig trigger = config.triggers[i];
            if (trigger == null)
            {
                continue;
            }

            if (trigger.triggerType == BlessingTriggerType.OnKillEnemy
                && trigger.damageMultiplier > 0f
                && trigger.radius > 0f)
            {
                _killExplosionTriggers[config.blessingId] = trigger.Clone();
            }
            else if (trigger.triggerType == BlessingTriggerType.OnKillEnemy
                     && string.Equals(trigger.effectKey, RandomPickupEffectKey, System.StringComparison.OrdinalIgnoreCase))
            {
                _killPickupDropTriggers[config.blessingId] = trigger.Clone();
            }
            else if (trigger.triggerType == BlessingTriggerType.OnHitEnemy
                     && !string.IsNullOrEmpty(trigger.effectKey))
            {
                _hitAerialBombTriggers[config.blessingId] = trigger.Clone();
            }
        }
    }

    public void OnEnemyDied(EnemyDiedEventData eventData)
    {
        if ((_killExplosionTriggers.Count == 0 && _killPickupDropTriggers.Count == 0)
            || !IsPlayerKill(eventData.damageInfo))
        {
            return;
        }

        if (IsPlayerBulletHit(eventData.damageInfo))
        {
            foreach (KeyValuePair<int, BlessingTriggerConfig> pair in _killExplosionTriggers)
            {
                BlessingTriggerConfig trigger = pair.Value;
                if (trigger == null || Random.value > trigger.chance)
                {
                    continue;
                }

                TriggerExplosion(eventData, trigger);
            }
        }

        foreach (KeyValuePair<int, BlessingTriggerConfig> pair in _killPickupDropTriggers)
        {
            TryTriggerPickupDrop(eventData, pair.Value);
        }
    }

    private void TryTriggerPickupDrop(EnemyDiedEventData eventData, BlessingTriggerConfig trigger)
    {
        if (trigger == null
            || Time.time < _nextPickupDropTriggerTime
            || Random.value > trigger.chance)
        {
            return;
        }

        _nextPickupDropTriggerTime = Time.time + Mathf.Max(0.1f, trigger.cooldown);
        PickupManager.EnsureForCurrentScene();
        PickupManager manager = PickupManager.ActiveInstance;
        if (manager == null)
        {
            return;
        }

        Vector3 position = eventData.enemy != null
            ? eventData.enemy.transform.position
            : eventData.damageInfo.hitPoint;
        if (manager.TrySpawnRewardPickup(
                position,
                null,
                trigger.maxActiveCount,
                out PickupItemConfig spawnedConfig))
        {
            TriggerTip(
                "战利品猎手",
                $"掉落道具：{spawnedConfig.itemName}",
                spawnedConfig.tipColorKey);
        }
    }

    private static void TriggerTip(string title, string description, string colorKey)
    {
        PickupTipEventData eventData = new PickupTipEventData(
            title,
            description,
            colorKey,
            TriggerTipDuration);
        if (UIManager.Instance == null)
        {
            EventCenter.Instance.EventTrigger(GameEvent.PickupTipRequested, eventData);
            return;
        }

        UIManager.Instance.OpenPanelAsy<TipCanvas>(_ =>
            EventCenter.Instance.EventTrigger(GameEvent.PickupTipRequested, eventData));
    }

    public void OnEnemyDamaged(EnemyDamagedEventData eventData)
    {
        if (_hitAerialBombTriggers.Count == 0
            || eventData.enemy == null
            || eventData.enemy.IsDead
            || eventData.currentHealth <= 0f
            || !IsPlayerBulletHit(eventData.damageInfo)
            || Time.time < _nextAerialBombTriggerTime)
        {
            return;
        }

        foreach (KeyValuePair<int, BlessingTriggerConfig> pair in _hitAerialBombTriggers)
        {
            BlessingTriggerConfig trigger = pair.Value;
            int maxActiveCount = trigger != null ? Mathf.Max(1, trigger.maxActiveCount) : 1;
            if (trigger == null
                || _activeAerialBombCount >= maxActiveCount
                || Random.value > trigger.chance)
            {
                continue;
            }

            _nextAerialBombTriggerTime = Time.time + Mathf.Max(0.1f, trigger.cooldown);
            SpawnAerialBomb(eventData, trigger);
            break;
        }
    }

    private void SpawnAerialBomb(EnemyDamagedEventData eventData, BlessingTriggerConfig trigger)
    {
        PlayerController player = eventData.damageInfo.attacker != null
            ? eventData.damageInfo.attacker.GetComponentInParent<PlayerController>()
            : null;
        if (player == null || eventData.enemy == null)
        {
            return;
        }

        string assetName = string.IsNullOrEmpty(trigger.effectKey)
            ? DefaultAerialBombAssetName
            : trigger.effectKey;
        int requestGeneration = _generation;
        int spawnSequence = _aerialBombSpawnSequence++;
        _activeAerialBombCount++;

        PoolMgr.Instance.GetObjForAB(AerialBombBundleName, assetName, bombObject =>
        {
            if (bombObject == null)
            {
                ReleaseAerialBombSlot(requestGeneration);
                return;
            }

            EnemyController target = eventData.enemy;
            if (requestGeneration != _generation || target == null || !target.IsActive || target.IsDead)
            {
                PoolMgr.Instance.pushObj(assetName, bombObject);
                ReleaseAerialBombSlot(requestGeneration);
                return;
            }

            SkillGrenadeProjectile projectile = bombObject.GetComponent<SkillGrenadeProjectile>();
            if (projectile == null)
            {
                Debug.LogWarning("[BlessingTrigger] 空投炸弹 Prefab 缺少 SkillGrenadeProjectile", bombObject);
                Object.Destroy(bombObject);
                ReleaseAerialBombSlot(requestGeneration);
                return;
            }

            PlayerSkillConfig grenadeConfig = ResolveAerialBombConfig(eventData.damageInfo, trigger);
            Vector3 targetPosition = target.transform.position + Vector3.up;
            Vector3 horizontalReference = targetPosition - player.transform.position;
            horizontalReference.y = 0f;
            if (horizontalReference.sqrMagnitude <= 0.0001f)
            {
                horizontalReference = player.transform.forward;
            }

            float angle = Mathf.Repeat(
                spawnSequence * AerialBombGoldenAngle + Random.Range(-18f, 18f),
                360f);
            Vector3 radialDirection = Quaternion.AngleAxis(angle, Vector3.up)
                                      * horizontalReference.normalized;
            Vector3 spawnPosition = targetPosition
                                    + radialDirection * Random.Range(
                                        AerialBombMinimumSpawnRadius,
                                        AerialBombMaximumSpawnRadius)
                                    + Vector3.up * Random.Range(
                                        AerialBombMinimumSpawnHeight,
                                        AerialBombMaximumSpawnHeight);
            Vector3 direction = (targetPosition - spawnPosition).normalized;
            bombObject.transform.SetPositionAndRotation(
                spawnPosition,
                Quaternion.LookRotation(direction, Vector3.up));
            projectile.Init(
                player,
                grenadeConfig,
                null,
                direction,
                assetName,
                () => ReleaseAerialBombSlot(requestGeneration),
                true,
                false);
        });
    }

    private static PlayerSkillConfig ResolveAerialBombConfig(
        DamageInfo damageInfo,
        BlessingTriggerConfig trigger)
    {
        PlayerSkillConfig config = PlayerSkillConfigJsonLoader.TryGetConfig(
            SkillType.Grenade,
            out PlayerSkillConfig loadedConfig)
            ? loadedConfig.Clone()
            : PlayerSkillConfig.CreateDefaultGrenade();
        config.skillName = "空投炸弹";
        config.damage = Mathf.Max(1f, damageInfo.finalDamage * Mathf.Max(0f, trigger.damageMultiplier));
        config.explosionRadius = Mathf.Max(0.1f, trigger.radius);
        config.explosionDelay = 1.5f;
        config.throwForce = AerialBombFallSpeed;
        config.throwUpForce = 0f;
        return config;
    }

    private void ReleaseAerialBombSlot(int requestGeneration)
    {
        if (requestGeneration == _generation)
        {
            _activeAerialBombCount = Mathf.Max(0, _activeAerialBombCount - 1);
        }
    }

    private void TriggerExplosion(EnemyDiedEventData eventData, BlessingTriggerConfig trigger)
    {
        Vector3 position = ResolveExplosionPosition(eventData);
        PlayExplosionFeedback(trigger, position);
        EventCenter.Instance.EventTrigger(
            GameEvent.ExplosionOccurred,
            new ExplosionOccurredEventData(
                position,
                ExplosionHapticIntensity,
                ExplosionHapticMaxDistance,
                eventData.damageInfo.attacker));

        float radius = Mathf.Max(0.1f, trigger.radius);
        float explosionDamageMultiplier = ResolveExplosionDamageMultiplier(eventData.damageInfo.attacker);
        float damage = Mathf.Max(
            1f,
            eventData.damageInfo.finalDamage
            * Mathf.Max(0f, trigger.damageMultiplier)
            * explosionDamageMultiplier);
        int enemyLayer = CombatLayerNames.EnemyLayer;
        int hitCount = Physics.OverlapSphereNonAlloc(
            position,
            radius,
            _hitBuffer,
            enemyLayer >= 0 ? 1 << enemyLayer : Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Collide);

        _hitEnemies.Clear();
        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = _hitBuffer[i];
            if (!TryResolveEnemy(hitCollider, out EnemyController enemy, out EnemyHealth health)
                || health.IsDead
                || !_hitEnemies.Add(enemy))
            {
                continue;
            }

            Vector3 hitPoint = hitCollider.ClosestPoint(position);
            Vector3 hitNormal = position - hitPoint;
            DamageInfo explosionDamage = new DamageInfo(
                damage,
                ExplosiveBulletDamageSourceId,
                ExplosiveBulletDamageSourceName,
                eventData.damageInfo.attacker,
                hitCollider,
                hitPoint,
                hitNormal.sqrMagnitude > 0.0001f ? hitNormal.normalized : Vector3.up);
            explosionDamage.ApplyBodyPart(EnemyHitBodyPart.Body, 1f, false);
            explosionDamage.MarkExplosionDamage();
            health.TakeDamage(explosionDamage);
        }
    }

    private static bool IsPlayerBulletHit(DamageInfo damageInfo)
    {
        return damageInfo.weaponId > 0
               && !damageInfo.isExplosionDamage
               && damageInfo.attacker != null
               && damageInfo.attacker.GetComponentInParent<PlayerController>() != null;
    }

    private static bool IsPlayerKill(DamageInfo damageInfo)
    {
        return damageInfo.attacker != null
               && damageInfo.attacker.GetComponentInParent<PlayerController>() != null;
    }

    private static float ResolveExplosionDamageMultiplier(GameObject attacker)
    {
        PlayerController player = attacker != null ? attacker.GetComponentInParent<PlayerController>() : null;
        PlayerRuntimeData runtimeData = player != null && player.Stats != null ? player.Stats.RuntimeData : null;
        return runtimeData != null ? Mathf.Max(0f, runtimeData.explosionDamageMultiplier) : 1f;
    }

    private static Vector3 ResolveExplosionPosition(EnemyDiedEventData eventData)
    {
        if (eventData.damageInfo.hitCollider != null)
        {
            return eventData.damageInfo.hitCollider.bounds.center;
        }

        return eventData.enemy != null
            ? eventData.enemy.transform.position + Vector3.up
            : eventData.damageInfo.hitPoint;
    }

    private static void PlayExplosionFeedback(BlessingTriggerConfig trigger, Vector3 position)
    {
        if (!string.IsNullOrEmpty(trigger.effectKey))
        {
            EffectMgr.Instance?.PlayEffectForAB(
                trigger.effectKey,
                position,
                Quaternion.identity,
                EffectMgr.SkillEffectBundleName,
                ExplosionEffectLifeTime);
        }

        if (!string.IsNullOrEmpty(trigger.audioKey))
        {
            MusicMgr.Instance?.PlayWorldSoundForAB(
                trigger.audioKey,
                position,
                EffectMgr.SkillEffectBundleName,
                1f,
                0.04f,
                2f,
                34f,
                96,
                true,
                1f,
                2.1f);
        }
    }

    private static bool TryResolveEnemy(Collider hitCollider, out EnemyController enemy, out EnemyHealth health)
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
}
