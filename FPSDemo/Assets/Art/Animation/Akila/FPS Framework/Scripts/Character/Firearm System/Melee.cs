using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Akila.FPSFramework 
{
    public class Melee : InventoryItem
    {
        public GameObject defaultDecal;
        public Vector3Direction decalDirection;
        public LayerMask hittableLayers = -1;
        public float range = 1;
        public float damage = 30;
        public bool multiHit;

        public Vector3 attackCenter
        {
            get
            {
                return (Camera.main.transform.position + Camera.main.transform.forward * (range / 2));
            }
        }

        public Vector3 attackDirection
        {
            get
            {
                return mainCamera.transform.forward;
            }
        }

        private Camera mainCamera;

        protected override void Awake()
        {
            base.Awake();

            mainCamera = Camera.main;
        }

        protected override void Update()
        {
            base.Update();

            if(itemInput.SingleFire)
            {
                Ray ray = new Ray(attackCenter, attackDirection);
                RaycastHit[] hits = Physics.SphereCastAll(ray, range / 2, range, hittableLayers);
                HashSet<IDamageable> damageables = new HashSet<IDamageable>();

                GameObject singleObjHit = null;

                for (int i = 0; i < hits.Length; i++)
                {
                    RaycastHit hit = hits[i];

                    if (hit.transform == playerObj.transform || singleObjHit || hit.point == Vector3.zero)
                        continue;

                    // Try to find a damageable part
                    if (hit.transform.TryGetComponent<IDamageablePart>(out IDamageablePart part) && part.parentDamageable != null)
                    {
                        // Apply "single hit per parent damageable"
                        if (!damageables.Contains(part.parentDamageable))
                        {
                            damageables.Add(part.parentDamageable);

                            PerformHitDamage(hit);
                        }
                    }
                    else if(hit.transform.GetComponent<IDamageable>() != null)
                    {
                        PerformHitDamage(hit);
                    }

                    if (!multiHit && singleObjHit == null)
                        singleObjHit = hit.transform.gameObject;
                }

            }
        }

        protected virtual void PerformHitDamage(RaycastHit hit)
        {
            GameObject obj = hit.transform.gameObject;

            if (hit.distance > range / 2)
                return;

            //Stop if not accepting melee attacks
            if(obj.TryGetComponent<Ignore>(out Ignore _ignore))
            {
                if (_ignore.ignoreMeleeHits)
                    return;
            }

            SpawnHitEffect(hit);

            IDamageable damageable = obj.transform.SearchFor<IDamageable>();
            IDamageablePart damageablePart = obj.GetComponent<IDamageablePart>();

            float damageMultiplier = 1;

            if(damageablePart != null)
            {
                damageMultiplier = damageablePart.damageMultipler;
            }

            //Don't do any damage if there's no damageable obj
            if (damageable == null)
                return;

            damageable.Damage(damage * damageMultiplier, playerObj);
        }

        public void SpawnHitEffect(RaycastHit hit)
        {
            // Handle custom decals
            if (hit.transform.TryGetComponent(out CustomDecal customDecal))
            {
                defaultDecal = customDecal.decalVFX;
            }

            // Apply default or custom decal
            if (defaultDecal != null)
            {
                Vector3 hitPoint = hit.point;
                Quaternion decalRotation = FPSFrameworkCore.GetFromToRotation(hit, decalDirection);
                GameObject decalInstance = Instantiate(defaultDecal, hitPoint, decalRotation);

                if (customDecal && customDecal.parent || customDecal == null)
                {
                    decalInstance.transform.SetParent(hit.transform);
                }

                float decalLifetime = customDecal?.lifeTime ?? 60f;
                Destroy(decalInstance, decalLifetime);
            }
        }

        public override void Drop(bool removeFromList = true)
        {
            // Some melee weapons (e.g., fists) should never be dropped.
            // By setting 'replacement' to null, this prevents the weapon from being dropped 
            // even if the Drop function is called.
            // For other weapons (e.g., knives), assigning a droppable item allows them to drop normally.
            if (replacement != null)
                base.Drop(removeFromList);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;

            Gizmos.DrawWireSphere(attackCenter, range / 2);
        }
    }
}