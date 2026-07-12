using Enemy;
using UnityEngine;

namespace Combat
{
    ///伤害数据
    public struct DamageInfo
    {
        public float baseDamage;
        public float finalDamage;
        public int weaponId;
        public string weaponName;
        public GameObject attacker;
        public Collider hitCollider;
        public Vector3 hitPoint;
        public Vector3 hitNormal;
        public EnemyHitBodyPart hitPart;
        public float partMultiplier;
        public bool isCritical;
        public bool hasCustomHitReaction;
        public bool forceFullHitReaction;
        public Vector3 customKnockbackDirection;
        public float customKnockbackDistance;
        public float customKnockbackDuration;
        public float customHitStunDuration;

        public DamageInfo(
            float baseDamage,
            int weaponId,
            string weaponName,
            GameObject attacker,
            Collider hitCollider,
            Vector3 hitPoint,
            Vector3 hitNormal)
        {
            this.baseDamage = Mathf.Max(0f, baseDamage);
            finalDamage = this.baseDamage;
            this.weaponId = weaponId;
            this.weaponName = weaponName;
            this.attacker = attacker;
            this.hitCollider = hitCollider;
            this.hitPoint = hitPoint;
            this.hitNormal = hitNormal;
            hitPart = EnemyHitBodyPart.Body;
            partMultiplier = 1f;
            isCritical = false;
            hasCustomHitReaction = false;
            forceFullHitReaction = false;
            customKnockbackDirection = Vector3.zero;
            customKnockbackDistance = 0f;
            customKnockbackDuration = 0f;
            customHitStunDuration = 0f;
        }

        public void ApplyBodyPart(EnemyHitBodyPart bodyPart, float multiplier, bool critical)
        {
            hitPart = bodyPart;
            partMultiplier = Mathf.Max(0f, multiplier);
            finalDamage = baseDamage * partMultiplier;
            isCritical = critical;
        }

        public void ApplyCustomHitReaction(
            Vector3 knockbackDirection,
            float knockbackDistance,
            float knockbackDuration,
            float hitStunDuration,
            bool forceReaction)
        {
            Vector3 horizontalDirection = knockbackDirection;
            horizontalDirection.y = 0f;

            hasCustomHitReaction = true;
            forceFullHitReaction = forceReaction;
            customKnockbackDirection = horizontalDirection.sqrMagnitude > 0.0001f
                ? horizontalDirection.normalized
                : Vector3.zero;
            customKnockbackDistance = Mathf.Max(0f, knockbackDistance);
            customKnockbackDuration = Mathf.Max(0.01f, knockbackDuration);
            customHitStunDuration = Mathf.Max(0.01f, hitStunDuration);
        }
    }
}
