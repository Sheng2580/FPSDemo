using System.Collections;
using System.Collections.Generic;
using Combat;
using Enemy;
using PlayerData;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class SkillGrenadeProjectile : MonoBehaviour
{
    private const float KnockbackForceToDistance = 0.18f;
    private const float DefaultExplosionKnockbackDuration = 0.22f;

    private readonly Collider[] _hitBuffer = new Collider[64];
    private readonly HashSet<EnemyController> _hitEnemies = new HashSet<EnemyController>();

    private PlayerController _owner;
    private PlayerSkillConfig _config;
    private PlayerSkillRuntimeData _runtimeData;
    private Rigidbody _rigidbody;
    private bool _exploded;

    public void Init(
        PlayerController owner,
        PlayerSkillConfig config,
        PlayerSkillRuntimeData runtimeData,
        Vector3 direction)
    {
        _owner = owner;
        _config = config != null ? config.Clone() : PlayerSkillConfig.CreateDefaultGrenade();
        _runtimeData = runtimeData;
        _rigidbody = GetComponent<Rigidbody>();

        Vector3 throwDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
        Vector3 velocity = throwDirection * Mathf.Max(0f, _config.throwForce) + Vector3.up * Mathf.Max(0f, _config.throwUpForce);
        _rigidbody.velocity = velocity;

        StartCoroutine(ExplodeAfterDelay());
    }

    private IEnumerator ExplodeAfterDelay()
    {
        float delay = _config != null ? Mathf.Max(0f, _config.explosionDelay) : 1.2f;
        yield return new WaitForSeconds(delay);
        Explode();
    }

    private void Explode()
    {
        if (_exploded)
        {
            return;
        }

        _exploded = true;
        PlayerSkillConfig config = _config ?? PlayerSkillConfig.CreateDefaultGrenade();
        Vector3 position = transform.position;
        float radiusMultiplier = _runtimeData != null ? Mathf.Max(0f, _runtimeData.radiusMultiplier) : 1f;
        EventCenter.Instance.EventTrigger(
            GameEvent.SkillVisualStarted,
            new SkillVisualEventData(config, config.explosionEffectKey, config.explosionAudioKey, position, Vector3.up, 0.35f, 1f));

        _hitEnemies.Clear();
        int hitCount = Physics.OverlapSphereNonAlloc(
            position,
            Mathf.Max(0.1f, config.explosionRadius * radiusMultiplier),
            _hitBuffer,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Collide);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = _hitBuffer[i];
            if (!TryResolveEnemy(hitCollider, out EnemyController enemy, out EnemyHealth health)
                || enemy.IsDead
                || _hitEnemies.Contains(enemy))
            {
                continue;
            }

            ApplyExplosionDamage(config, enemy, health, hitCollider, position);
            _hitEnemies.Add(enemy);
        }

        Destroy(gameObject);
    }

    private void ApplyExplosionDamage(
        PlayerSkillConfig config,
        EnemyController enemy,
        EnemyHealth health,
        Collider hitCollider,
        Vector3 explosionPosition)
    {
        Vector3 hitPoint = hitCollider != null ? hitCollider.ClosestPoint(explosionPosition) : enemy.transform.position;
        Vector3 knockbackDirection = enemy.transform.position - explosionPosition;
        knockbackDirection.y = 0f;
        if (knockbackDirection.sqrMagnitude <= 0.0001f)
        {
            knockbackDirection = enemy.transform.position - (_owner != null ? _owner.transform.position : explosionPosition);
        }

        float damageMultiplier = _runtimeData != null ? Mathf.Max(0f, _runtimeData.damageMultiplier) : 1f;
        float radiusMultiplier = _runtimeData != null ? Mathf.Max(0f, _runtimeData.radiusMultiplier) : 1f;
        float finalDamage = Mathf.Max(0f, config.damage * damageMultiplier);
        float knockbackDistance = config.knockbackForce * radiusMultiplier * KnockbackForceToDistance;

        DamageInfo damageInfo = new DamageInfo(
            finalDamage,
            -config.skillId,
            config.skillName,
            _owner != null ? _owner.gameObject : gameObject,
            hitCollider,
            hitPoint,
            -knockbackDirection.normalized);
        damageInfo.ApplyBodyPart(EnemyHitBodyPart.Body, 1f, false);
        damageInfo.ApplyCustomHitReaction(
            knockbackDirection,
            knockbackDistance,
            DefaultExplosionKnockbackDuration,
            Mathf.Max(0.01f, config.stunDuration),
            true);

        health.TakeDamage(damageInfo);
        EventCenter.Instance.EventTrigger(
            GameEvent.SkillHitEnemy,
            new SkillHitEnemyEventData(config, enemy, damageInfo.finalDamage, hitPoint, knockbackDirection));
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
}
