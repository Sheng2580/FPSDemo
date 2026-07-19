using System;
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
    private const float ExplosionHapticIntensity = 1f;
    private const float ExplosionHapticMaxDistance = 22f;

    private readonly Collider[] _hitBuffer = new Collider[64];
    private readonly HashSet<EnemyController> _hitEnemies = new HashSet<EnemyController>();

    private PlayerController _owner;
    private PlayerSkillConfig _config;
    private PlayerSkillRuntimeData _runtimeData;
    private Rigidbody _rigidbody;
    private string _poolKey;
    private Coroutine _explodeRoutine;
    private bool _exploded;
    private Action _onReturnedToPool;
    private bool _explodeOnEnvironmentImpact;

    public void Init(
        PlayerController owner,
        PlayerSkillConfig config,
        PlayerSkillRuntimeData runtimeData,
        Vector3 direction,
        string poolKey,
        Action onReturnedToPool = null,
        bool explodeOnEnvironmentImpact = false,
        bool useGravity = true)
    {
        StopAllCoroutines();
        _explodeRoutine = null;
        _owner = owner;
        _config = config != null ? config.Clone() : PlayerSkillConfig.CreateDefaultGrenade();
        _runtimeData = runtimeData;
        _poolKey = string.IsNullOrEmpty(poolKey) ? _config.projectileResourceKey : poolKey;
        _onReturnedToPool = onReturnedToPool;
        _explodeOnEnvironmentImpact = explodeOnEnvironmentImpact;
        _rigidbody = GetComponent<Rigidbody>();
        _exploded = false;

        if (_rigidbody == null)
        {
            _rigidbody = gameObject.AddComponent<Rigidbody>();
        }

        _rigidbody.detectCollisions = false;
        _rigidbody.velocity = Vector3.zero;
        _rigidbody.angularVelocity = Vector3.zero;
        _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        _rigidbody.isKinematic = true;
        _rigidbody.useGravity = useGravity;

        TrailRenderer[] trails = GetComponentsInChildren<TrailRenderer>(true);
        for (int i = 0; i < trails.Length; i++)
        {
            trails[i].Clear();
        }

        Vector3 throwDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
        Vector3 velocity = throwDirection * Mathf.Max(0f, _config.throwForce)
                           + Vector3.up * Mathf.Max(0f, _config.throwUpForce);

        IgnoreOwnerCollisions();
        _rigidbody.position = transform.position;
        _rigidbody.rotation = transform.rotation;
        Physics.SyncTransforms();
        _rigidbody.isKinematic = false;
        _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        _rigidbody.detectCollisions = true;
        _rigidbody.velocity = velocity;

        _explodeRoutine = StartCoroutine(ExplodeAfterDelay());
    }

    private IEnumerator ExplodeAfterDelay()
    {
        float delay = _config != null ? Mathf.Max(0f, _config.explosionDelay) : 1.2f;
        yield return new WaitForSeconds(delay);
        _explodeRoutine = null;
        Explode();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (_explodeOnEnvironmentImpact)
        {
            Explode();
            return;
        }

        TryExplodeOnEnemy(collision != null ? collision.collider : null);
    }

    private void OnTriggerEnter(Collider other)
    {
        TryExplodeOnEnemy(other);
    }

    private void TryExplodeOnEnemy(Collider hitCollider)
    {
        if (_exploded
            || !IsEnemyLayer(hitCollider)
            || !TryResolveEnemy(hitCollider, out EnemyController enemy, out _)
            || enemy.IsDead)
        {
            return;
        }

        Explode();
    }

    private void Explode()
    {
        if (_exploded)
        {
            return;
        }

        _exploded = true;
        if (_explodeRoutine != null)
        {
            StopCoroutine(_explodeRoutine);
            _explodeRoutine = null;
        }

        PlayerSkillConfig config = _config ?? PlayerSkillConfig.CreateDefaultGrenade();
        Vector3 position = transform.position;
        float radiusMultiplier = _runtimeData != null ? Mathf.Max(0f, _runtimeData.radiusMultiplier) : 1f;
        EventCenter.Instance.EventTrigger(
            GameEvent.SkillVisualStarted,
            new SkillVisualEventData(_owner, config, config.explosionEffectKey, config.explosionAudioKey, position, Vector3.up, 0.35f, 1f));
        EventCenter.Instance.EventTrigger(
            GameEvent.ExplosionOccurred,
            new ExplosionOccurredEventData(
                position,
                ExplosionHapticIntensity,
                ExplosionHapticMaxDistance,
                _owner != null ? _owner.gameObject : gameObject));

        _hitEnemies.Clear();
        int enemyLayer = CombatLayerNames.EnemyLayer;
        int hitCount = Physics.OverlapSphereNonAlloc(
            position,
            Mathf.Max(0.1f, config.explosionRadius * radiusMultiplier),
            _hitBuffer,
            enemyLayer >= 0 ? 1 << enemyLayer : Physics.DefaultRaycastLayers,
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

        ReturnToPool();
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

        float skillDamageMultiplier = _runtimeData != null ? Mathf.Max(0f, _runtimeData.damageMultiplier) : 1f;
        float explosionDamageMultiplier = _owner != null && _owner.Stats != null && _owner.Stats.RuntimeData != null
            ? Mathf.Max(0f, _owner.Stats.RuntimeData.explosionDamageMultiplier)
            : 1f;
        float radiusMultiplier = _runtimeData != null ? Mathf.Max(0f, _runtimeData.radiusMultiplier) : 1f;
        float finalDamage = Mathf.Max(0f, config.damage * skillDamageMultiplier * explosionDamageMultiplier);
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
        damageInfo.MarkExplosionDamage();
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

    private void IgnoreOwnerCollisions()
    {
        if (_owner == null)
        {
            return;
        }

        Collider[] grenadeColliders = GetComponentsInChildren<Collider>(true);
        Collider[] ownerColliders = _owner.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < grenadeColliders.Length; i++)
        {
            Collider grenadeCollider = grenadeColliders[i];
            if (grenadeCollider == null)
            {
                continue;
            }

            for (int j = 0; j < ownerColliders.Length; j++)
            {
                Collider ownerCollider = ownerColliders[j];
                if (ownerCollider != null)
                {
                    Physics.IgnoreCollision(grenadeCollider, ownerCollider, true);
                }
            }
        }
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

    private static bool IsEnemyLayer(Collider hitCollider)
    {
        return hitCollider != null
               && CombatLayerNames.IsEnemyLayer(hitCollider.gameObject.layer);
    }

    private void ReturnToPool()
    {
        if (_rigidbody != null)
        {
            _rigidbody.detectCollisions = false;
            _rigidbody.velocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
            _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            _rigidbody.isKinematic = true;
        }

        Action onReturnedToPool = _onReturnedToPool;
        _onReturnedToPool = null;
        string poolKey = string.IsNullOrEmpty(_poolKey) ? gameObject.name : _poolKey;
        PoolMgr.Instance.pushObj(poolKey, gameObject);
        onReturnedToPool?.Invoke();
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        _explodeRoutine = null;
        if (_rigidbody != null)
        {
            _rigidbody.detectCollisions = false;
            _rigidbody.velocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
            _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            _rigidbody.isKinematic = true;
        }

        _owner = null;
        _runtimeData = null;
        _onReturnedToPool = null;
        _explodeOnEnvironmentImpact = false;
        _exploded = false;
    }

}
