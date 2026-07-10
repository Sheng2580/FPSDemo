using System;
using UnityEngine;

namespace Weapon.Data
{
    public enum WeaponFireMode
    {
        // 单发武器 每次按下只开一枪
        SemiAuto,
        // 连发武器 按住时按射速连续开火
        FullAuto
    }

    public enum WeaponAttackType
    {
        // 普通枪械即时射线
        Hitscan,
        // 霰弹枪等多条射线
        MultiHitscan,
        // 榴弹 火箭弹 怪物远程弹等实体弹
        Projectile,
        // 爆炸 火焰 电击等范围伤害
        Area
    }

    [Serializable]
    public class WeaponConfig
    {
        public const float DefaultAimInSpeed = 10f;
        public const float DefaultAimOutSpeed = 12f;
        public const float DefaultAimCameraFov = 55f;
        public static readonly Vector3 DefaultAimViewPositionOffset = new Vector3(-0.06f, 0.044f, 0.02f);

        // 基础身份
        public int weaponId;
        public string weaponName;
        public WeaponFireMode fireMode;

        // 伤害和射击
        public float damage;
        public float fireInterval;
        public int magazineSize;
        public int maxReserveAmmo;
        public float reloadTime;
        public float range;

        // 攻击结算数据
        public WeaponAttackType attackType;
        public float spreadAngle;
        public int pelletCount;
        public int maxPenetrationCount;
        public float penetrationDamageMultiplier;
        public string projectilePrefabKey;
        public float projectileSpeed;
        public float projectileLifeTime;
        public float explosionRadius;
        public float explosionFalloff;
        public LayerMask hitLayerMask;
        public string tracerPrefabKey;
        public string impactEffectKey;

        // 后坐力
        public float recoilPitch;
        public float recoilYaw;
        public Vector3 viewRecoilPosition;
        public Vector3 viewRecoilRotation;
        public float viewRecoilReturnSpeed;

        // 开镜数据 每把枪可以单独配置
        public float aimInSpeed;
        public float aimOutSpeed;
        public float aimCameraFov;
        public Vector3 aimViewPositionOffset;
        public Vector3 aimViewRotationOffset;
        public bool useAimLocalPose;
        public Vector3 aimLocalPosition;
        public Vector3 aimLocalEulerAngles;
        // 为 0 时保持模型默认缩放
        public Vector3 aimLocalScale;

        // 动画状态名
        public string idleStateName;
        public string equipStateName;
        public string fireStateName;
        public string reloadStateName;

        // 动画过渡时间
        public float fireTransition;
        public float reloadTransition;
        public float equipTransition;

        /// <summary>
        /// 默认
        /// </summary>
        /// <returns></returns>
        public static WeaponConfig CreateDefaultPistol()
        {
            return new WeaponConfig
            {
                weaponId = 1,
                weaponName = "Default Pistol",
                fireMode = WeaponFireMode.SemiAuto,
                damage = 20f,
                fireInterval = 0.2f,
                magazineSize = 12,
                maxReserveAmmo = 48,
                reloadTime = 1.4f,
                range = 100f,
                attackType = WeaponAttackType.Hitscan,
                spreadAngle = 0.6f,
                pelletCount = 1,
                maxPenetrationCount = 0,
                penetrationDamageMultiplier = 0.5f,
                projectilePrefabKey = string.Empty,
                projectileSpeed = 0f,
                projectileLifeTime = 0f,
                explosionRadius = 0f,
                explosionFalloff = 1f,
                hitLayerMask = Physics.DefaultRaycastLayers,
                tracerPrefabKey = string.Empty,
                impactEffectKey = string.Empty,
                recoilPitch = -1.5f,
                recoilYaw = 0.5f,
                viewRecoilPosition = new Vector3(0f, -0.015f, -0.08f),
                viewRecoilRotation = new Vector3(-6f, 1.5f, 0f),
                viewRecoilReturnSpeed = 18f,
                aimInSpeed = DefaultAimInSpeed,
                aimOutSpeed = DefaultAimOutSpeed,
                aimCameraFov = DefaultAimCameraFov,
                aimViewPositionOffset = DefaultAimViewPositionOffset,
                aimViewRotationOffset = Vector3.zero,
                useAimLocalPose = true,
                aimLocalPosition = new Vector3(-0.084f, -0.799f, 0.411f),
                aimLocalEulerAngles = Vector3.zero,
                aimLocalScale = new Vector3(0.04f, 0.04f, 0.04f),
                idleStateName = "Idle",
                equipStateName = "Take",
                fireStateName = "Fire",
                reloadStateName = "Reload",
                fireTransition = 0.05f,
                reloadTransition = 0.1f,
                equipTransition = 0.1f
            };
        }

        /// <summary>
        /// 默认步枪
        /// </summary>
        /// <returns></returns>
        public static WeaponConfig CreateDefaultAssaultRifle()
        {
            return new WeaponConfig
            {
                weaponId = 2,
                weaponName = "Default Assault Rifle",
                fireMode = WeaponFireMode.FullAuto,
                damage = 16f,
                fireInterval = 0.09f,
                magazineSize = 30,
                maxReserveAmmo = 120,
                reloadTime = 1.65f,
                range = 160f,
                attackType = WeaponAttackType.Hitscan,
                spreadAngle = 0.9f,
                pelletCount = 1,
                maxPenetrationCount = 0,
                penetrationDamageMultiplier = 0.5f,
                projectilePrefabKey = string.Empty,
                projectileSpeed = 0f,
                projectileLifeTime = 0f,
                explosionRadius = 0f,
                explosionFalloff = 1f,
                hitLayerMask = Physics.DefaultRaycastLayers,
                tracerPrefabKey = string.Empty,
                impactEffectKey = string.Empty,
                recoilPitch = -0.55f,
                recoilYaw = 0.35f,
                viewRecoilPosition = new Vector3(0f, -0.006f, -0.04f),
                viewRecoilRotation = new Vector3(-2.2f, 0.7f, 0f),
                viewRecoilReturnSpeed = 24f,
                aimInSpeed = 9f,
                aimOutSpeed = 12f,
                aimCameraFov = 30f,
                aimViewPositionOffset = new Vector3(-0.018f, 0.07f, 0.025f),
                aimViewRotationOffset = Vector3.zero,
                useAimLocalPose = true,
                aimLocalPosition = new Vector3(-0.162f, -1.517f, -0.13f),
                aimLocalEulerAngles = Vector3.zero,
                aimLocalScale = new Vector3(0.06154f, 0.06154f, 0.06154f),
                idleStateName = "Idle",
                equipStateName = "Take",
                fireStateName = "Fire",
                reloadStateName = "Reload",
                fireTransition = 0.03f,
                reloadTransition = 0.08f,
                equipTransition = 0.1f
            };
        }

        public WeaponConfig Clone()
        {
            return (WeaponConfig)MemberwiseClone();
        }

        public void ApplyMissingDefaults()
        {
            // 旧场景或旧资源里新增字段可能是 0 这里补上可用默认值
            if (aimInSpeed <= 0f)
            {
                aimInSpeed = DefaultAimInSpeed;
            }

            if (aimOutSpeed <= 0f)
            {
                aimOutSpeed = DefaultAimOutSpeed;
            }

            if (aimCameraFov <= 1f)
            {
                aimCameraFov = DefaultAimCameraFov;
            }

            if (aimViewPositionOffset == Vector3.zero)
            {
                aimViewPositionOffset = DefaultAimViewPositionOffset;
            }

            if (pelletCount <= 0)
            {
                pelletCount = 1;
            }

            if (penetrationDamageMultiplier <= 0f)
            {
                penetrationDamageMultiplier = 0.5f;
            }

            if (explosionFalloff <= 0f)
            {
                explosionFalloff = 1f;
            }

            if (hitLayerMask.value == 0)
            {
                hitLayerMask = Physics.DefaultRaycastLayers;
            }
        }
    }
}
