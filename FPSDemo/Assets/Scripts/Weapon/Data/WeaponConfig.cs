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

    public enum HitSurfaceType
    {
        Default,
        Stone,
        Metal,
        Wood,
        Flesh
    }

    [Serializable]
    public class HitSurfaceFeedbackConfig
    {
        public HitSurfaceType surfaceType;
        public string impactEffectKey;
        public string impactAudioKey;
        public string decalKey;
        public float decalLifeTime;
        public float decalScale;

        public HitSurfaceFeedbackConfig Clone()
        {
            return (HitSurfaceFeedbackConfig)MemberwiseClone();
        }

        public void ApplyMissingDefaults()
        {
            if (string.IsNullOrEmpty(impactEffectKey))
            {
                impactEffectKey = WeaponConfig.GetDefaultImpactEffectKey(surfaceType);
            }

            if (decalLifeTime <= 0f)
            {
                decalLifeTime = WeaponConfig.DefaultDecalLifeTime;
            }

            if (decalScale <= 0f)
            {
                decalScale = WeaponConfig.DefaultDecalScale;
            }
        }
    }

    [Serializable]
    public class WeaponConfig
    {
        public const float DefaultAimInSpeed = 10f;
        public const float DefaultAimOutSpeed = 12f;
        public const float DefaultAimCameraFov = 55f;
        public const float DefaultCrosshairSize = 25f;
        public const float DefaultCrosshairMinSprayAmount = 1f;
        public const float DefaultCrosshairSpreadScale = 0.65f;
        public const float DefaultCrosshairFireKickAmount = 1.15f;
        public const float DefaultCrosshairFireKickDecaySpeed = 7.5f;
        public const float DefaultPistolCrosshairSize = 26f;
        public const float DefaultPistolCrosshairSpreadScale = 0.75f;
        public const float DefaultPistolCrosshairFireKickAmount = 1.2f;
        public const float DefaultPistolCrosshairFireKickDecaySpeed = 8f;
        public const float DefaultAssaultRifleCrosshairSize = 20f;
        public const float DefaultAssaultRifleCrosshairSpreadScale = 0.55f;
        public const float DefaultAssaultRifleCrosshairFireKickAmount = 0.55f;
        public const float DefaultAssaultRifleCrosshairFireKickDecaySpeed = 10.5f;
        public const string DefaultMuzzleFlashEffectKey = "Muzzle Flash";
        public const string DefaultMuzzleSmokeEffectKey = "Muzzle Smoke";
        public const string DefaultImpactEffectKey = "Stone Impact";
        public const string DefaultPistolFireAudioKey = "Pistol_1 Fire";
        public const string DefaultAssaultRifleFireAudioKey = "Assault Rifle_1 Fire";
        public const float DefaultFireVolume = 1f;
        public const float DefaultFirePitchRandom = 0.04f;
        public const float DefaultFireAudioCooldown = 0.03f;
        public const float DefaultFireFeedbackIntensity = 1f;
        public const float DefaultDecalLifeTime = 8f;
        public const float DefaultDecalScale = 1f;
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

        // 战斗表现数据 只提供资源 key 表现层负责播放
        public string muzzleFlashEffectKey;
        public string muzzleSmokeEffectKey;
        public string fireAudioKey;
        public float fireVolume;
        public float firePitchRandom;
        public float fireAudioCooldown;
        public float fireFeedbackIntensity;
        public string defaultImpactEffectKey;
        public HitSurfaceFeedbackConfig[] hitSurfaceFeedbacks;

        // 准星表现数据 每把枪可以单独配置
        public float crosshairSize;
        public float crosshairMinSprayAmount;
        public float crosshairSpreadScale;
        public float crosshairFireKickAmount;
        public float crosshairFireKickDecaySpeed;

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
                impactEffectKey = DefaultImpactEffectKey,
                muzzleFlashEffectKey = DefaultMuzzleFlashEffectKey,
                muzzleSmokeEffectKey = DefaultMuzzleSmokeEffectKey,
                fireAudioKey = DefaultPistolFireAudioKey,
                fireVolume = DefaultFireVolume,
                firePitchRandom = DefaultFirePitchRandom,
                fireAudioCooldown = DefaultFireAudioCooldown,
                fireFeedbackIntensity = DefaultFireFeedbackIntensity,
                defaultImpactEffectKey = DefaultImpactEffectKey,
                hitSurfaceFeedbacks = CreateDefaultHitSurfaceFeedbacks(),
                crosshairSize = DefaultPistolCrosshairSize,
                crosshairMinSprayAmount = DefaultCrosshairMinSprayAmount,
                crosshairSpreadScale = DefaultPistolCrosshairSpreadScale,
                crosshairFireKickAmount = DefaultPistolCrosshairFireKickAmount,
                crosshairFireKickDecaySpeed = DefaultPistolCrosshairFireKickDecaySpeed,
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
                aimLocalPosition = new Vector3(-0.084f, -0.817f, 0.35677f),
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
                impactEffectKey = DefaultImpactEffectKey,
                muzzleFlashEffectKey = DefaultMuzzleFlashEffectKey,
                muzzleSmokeEffectKey = DefaultMuzzleSmokeEffectKey,
                fireAudioKey = DefaultAssaultRifleFireAudioKey,
                fireVolume = DefaultFireVolume,
                firePitchRandom = DefaultFirePitchRandom,
                fireAudioCooldown = DefaultFireAudioCooldown,
                fireFeedbackIntensity = DefaultFireFeedbackIntensity,
                defaultImpactEffectKey = DefaultImpactEffectKey,
                hitSurfaceFeedbacks = CreateDefaultHitSurfaceFeedbacks(),
                crosshairSize = DefaultAssaultRifleCrosshairSize,
                crosshairMinSprayAmount = DefaultCrosshairMinSprayAmount,
                crosshairSpreadScale = DefaultAssaultRifleCrosshairSpreadScale,
                crosshairFireKickAmount = DefaultAssaultRifleCrosshairFireKickAmount,
                crosshairFireKickDecaySpeed = DefaultAssaultRifleCrosshairFireKickDecaySpeed,
                recoilPitch = -0.32f,
                recoilYaw = 0.16f,
                viewRecoilPosition = new Vector3(0f, -0.004f, -0.025f),
                viewRecoilRotation = new Vector3(-1.25f, 0.25f, 0f),
                viewRecoilReturnSpeed = 16f,
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
            WeaponConfig clone = (WeaponConfig)MemberwiseClone();
            if (hitSurfaceFeedbacks != null)
            {
                clone.hitSurfaceFeedbacks = new HitSurfaceFeedbackConfig[hitSurfaceFeedbacks.Length];
                for (int i = 0; i < hitSurfaceFeedbacks.Length; i++)
                {
                    clone.hitSurfaceFeedbacks[i] = hitSurfaceFeedbacks[i]?.Clone();
                }
            }

            return clone;
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

            ApplyMissingCombatFeedbackDefaults();
            ApplyMissingCrosshairDefaults();
        }

        private void ApplyMissingCombatFeedbackDefaults()
        {
            if (string.IsNullOrEmpty(muzzleFlashEffectKey))
            {
                muzzleFlashEffectKey = DefaultMuzzleFlashEffectKey;
            }

            if (string.IsNullOrEmpty(muzzleSmokeEffectKey))
            {
                muzzleSmokeEffectKey = DefaultMuzzleSmokeEffectKey;
            }

            if (string.IsNullOrEmpty(fireAudioKey))
            {
                fireAudioKey = ResolveDefaultFireAudioKey();
            }

            if (fireVolume <= 0f)
            {
                fireVolume = DefaultFireVolume;
            }

            if (firePitchRandom < 0f)
            {
                firePitchRandom = DefaultFirePitchRandom;
            }

            if (fireAudioCooldown < 0f)
            {
                fireAudioCooldown = DefaultFireAudioCooldown;
            }

            if (fireFeedbackIntensity <= 0f)
            {
                fireFeedbackIntensity = DefaultFireFeedbackIntensity;
            }

            if (string.IsNullOrEmpty(defaultImpactEffectKey))
            {
                defaultImpactEffectKey = string.IsNullOrEmpty(impactEffectKey)
                    ? DefaultImpactEffectKey
                    : impactEffectKey;
            }

            if (string.IsNullOrEmpty(impactEffectKey))
            {
                impactEffectKey = defaultImpactEffectKey;
            }

            if (hitSurfaceFeedbacks == null || hitSurfaceFeedbacks.Length == 0)
            {
                hitSurfaceFeedbacks = CreateDefaultHitSurfaceFeedbacks();
            }

            for (int i = 0; i < hitSurfaceFeedbacks.Length; i++)
            {
                hitSurfaceFeedbacks[i] ??= CreateHitSurfaceFeedback(HitSurfaceType.Default);
                hitSurfaceFeedbacks[i].ApplyMissingDefaults();
            }
        }

        private string ResolveDefaultFireAudioKey()
        {
            if (weaponId == 2 || fireMode == WeaponFireMode.FullAuto)
            {
                return DefaultAssaultRifleFireAudioKey;
            }

            return DefaultPistolFireAudioKey;
        }

        private void ApplyMissingCrosshairDefaults()
        {
            ResolveCrosshairDefaults(
                out float defaultSize,
                out float defaultMinSprayAmount,
                out float defaultSpreadScale,
                out float defaultFireKickAmount,
                out float defaultFireKickDecaySpeed);

            if (crosshairSize <= 0f)
            {
                crosshairSize = defaultSize;
            }

            if (crosshairMinSprayAmount <= 0f)
            {
                crosshairMinSprayAmount = defaultMinSprayAmount;
            }

            if (crosshairSpreadScale <= 0f)
            {
                crosshairSpreadScale = defaultSpreadScale;
            }

            if (crosshairFireKickAmount <= 0f)
            {
                crosshairFireKickAmount = defaultFireKickAmount;
            }

            if (crosshairFireKickDecaySpeed <= 0f)
            {
                crosshairFireKickDecaySpeed = defaultFireKickDecaySpeed;
            }
        }

        private void ResolveCrosshairDefaults(
            out float size,
            out float minSprayAmount,
            out float spreadScale,
            out float fireKickAmount,
            out float fireKickDecaySpeed)
        {
            minSprayAmount = DefaultCrosshairMinSprayAmount;

            if (weaponId == 2 || fireMode == WeaponFireMode.FullAuto)
            {
                size = DefaultAssaultRifleCrosshairSize;
                spreadScale = DefaultAssaultRifleCrosshairSpreadScale;
                fireKickAmount = DefaultAssaultRifleCrosshairFireKickAmount;
                fireKickDecaySpeed = DefaultAssaultRifleCrosshairFireKickDecaySpeed;
                return;
            }

            if (weaponId == 1 || fireMode == WeaponFireMode.SemiAuto)
            {
                size = DefaultPistolCrosshairSize;
                spreadScale = DefaultPistolCrosshairSpreadScale;
                fireKickAmount = DefaultPistolCrosshairFireKickAmount;
                fireKickDecaySpeed = DefaultPistolCrosshairFireKickDecaySpeed;
                return;
            }

            size = DefaultCrosshairSize;
            spreadScale = DefaultCrosshairSpreadScale;
            fireKickAmount = DefaultCrosshairFireKickAmount;
            fireKickDecaySpeed = DefaultCrosshairFireKickDecaySpeed;
        }

        public static HitSurfaceFeedbackConfig[] CreateDefaultHitSurfaceFeedbacks()
        {
            return new[]
            {
                CreateHitSurfaceFeedback(HitSurfaceType.Default),
                CreateHitSurfaceFeedback(HitSurfaceType.Stone),
                CreateHitSurfaceFeedback(HitSurfaceType.Metal),
                CreateHitSurfaceFeedback(HitSurfaceType.Wood),
                CreateHitSurfaceFeedback(HitSurfaceType.Flesh)
            };
        }

        public static HitSurfaceFeedbackConfig CreateHitSurfaceFeedback(HitSurfaceType surfaceType)
        {
            return new HitSurfaceFeedbackConfig
            {
                surfaceType = surfaceType,
                impactEffectKey = GetDefaultImpactEffectKey(surfaceType),
                impactAudioKey = string.Empty,
                decalKey = string.Empty,
                decalLifeTime = DefaultDecalLifeTime,
                decalScale = DefaultDecalScale
            };
        }

        public static string GetDefaultImpactEffectKey(HitSurfaceType surfaceType)
        {
            switch (surfaceType)
            {
                case HitSurfaceType.Metal:
                    return "Metal Impact";
                case HitSurfaceType.Wood:
                    return "Wood Impact";
                case HitSurfaceType.Flesh:
                    return "Blood Impact";
                case HitSurfaceType.Stone:
                case HitSurfaceType.Default:
                default:
                    return DefaultImpactEffectKey;
            }
        }
    }
}
