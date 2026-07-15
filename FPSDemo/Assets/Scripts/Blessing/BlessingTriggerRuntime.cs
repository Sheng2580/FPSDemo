using System.Collections.Generic;
using Blessing.Data;
using Combat;
using Enemy;
using PlayerData;
using UnityEngine;

/// <summary>
/// 本局祝福触发效果运行器
/// </summary>
public sealed class BlessingTriggerRuntime
{
    private const int ExplosiveBulletDamageSourceId = -1401;
    private const string ExplosiveBulletDamageSourceName = "爆炸子弹";
    private const float ExplosionEffectLifeTime = 2.5f;

    private readonly Dictionary<int, BlessingTriggerConfig> _killTriggers = new Dictionary<int, BlessingTriggerConfig>();
    private readonly Collider[] _hitBuffer = new Collider[96];
    private readonly HashSet<EnemyController> _hitEnemies = new HashSet<EnemyController>();

    public void Reset()
    {
        _killTriggers.Clear();
        _hitEnemies.Clear();
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
            if (trigger == null || trigger.triggerType != BlessingTriggerType.OnKillEnemy)
            {
                continue;
            }

            _killTriggers[config.blessingId] = trigger.Clone();
        }
    }

    public void OnEnemyDied(EnemyDiedEventData eventData)
    {
        if (_killTriggers.Count == 0 || !IsPlayerBulletKill(eventData.damageInfo))
        {
            return;
        }

        foreach (KeyValuePair<int, BlessingTriggerConfig> pair in _killTriggers)
        {
            BlessingTriggerConfig trigger = pair.Value;
            if (trigger == null || Random.value > trigger.chance)
            {
                continue;
            }

            TriggerExplosion(eventData, trigger);
        }
    }

    private void TriggerExplosion(EnemyDiedEventData eventData, BlessingTriggerConfig trigger)
    {
        Vector3 position = ResolveExplosionPosition(eventData);
        PlayExplosionFeedback(trigger, position);

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

    private static bool IsPlayerBulletKill(DamageInfo damageInfo)
    {
        return damageInfo.weaponId > 0
               && damageInfo.attacker != null
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
