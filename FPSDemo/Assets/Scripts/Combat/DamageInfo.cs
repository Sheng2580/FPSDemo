using Enemy;
using UnityEngine;

namespace Combat
{
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
        }

        public void ApplyBodyPart(EnemyHitBodyPart bodyPart, float multiplier, bool critical)
        {
            hitPart = bodyPart;
            partMultiplier = Mathf.Max(0f, multiplier);
            finalDamage = baseDamage * partMultiplier;
            isCritical = critical;
        }
    }
}
