using System;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 战斗后处理效果配置
    /// 只保存数值和效果 key
    /// </summary>
    [Serializable]
    public class CombatVolumeEffectConfig
    {
        // 效果类型
        public CombatVolumeEffectType effectType;
        // 效果 key
        public string effectKey;
        // 淡入时间
        public float fadeInTime;
        // 保持时间
        public float holdTime;
        // 淡出时间
        public float fadeOutTime;
        // 最小强度
        public float minIntensity;
        // 伤害转强度倍率
        public float damageToIntensityScale;
        // 已损生命转强度倍率
        public float missingHpIntensityScale;
        // 颜色滤镜
        public Color colorFilter;
        // 暗角颜色
        public Color vignetteColor;
        // 暗角强度增量
        public float vignetteIntensityBoost;
        // 暗角平滑度增量
        public float vignetteSmoothnessBoost;
        // 曝光偏移
        public float postExposureOffset;
        // 饱和度偏移
        public float saturationOffset;
        // Bloom 强度增量
        public float bloomIntensityBoost;
        // Bloom 染色混合
        public float bloomTintBlend;
        // 是否启用 Bloom 脉冲
        public bool enableBloomPulse;

        public static CombatVolumeEffectConfig CreateDefaultPlayerDamage()
        {
            return new CombatVolumeEffectConfig
            {
                effectType = CombatVolumeEffectType.PlayerDamage,
                effectKey = "CombatVolume_PlayerDamage",
                fadeInTime = 0.08f,
                holdTime = 0.08f,
                fadeOutTime = 0.32f,
                minIntensity = 0.35f,
                damageToIntensityScale = 0.08f,
                missingHpIntensityScale = 0.35f,
                colorFilter = new Color(1f, 0.52f, 0.48f, 1f),
                vignetteColor = new Color(0.42f, 0.02f, 0.01f, 1f),
                vignetteIntensityBoost = 0.32f,
                vignetteSmoothnessBoost = 0.12f,
                postExposureOffset = -0.35f,
                saturationOffset = -24f,
                bloomIntensityBoost = 0.25f,
                bloomTintBlend = 0.35f,
                enableBloomPulse = true
            };
        }

        public static CombatVolumeEffectConfig CreateDefaultDodge()
        {
            return new CombatVolumeEffectConfig
            {
                effectType = CombatVolumeEffectType.Dodge,
                effectKey = "Skill_Dodge_SprintPulse",
                fadeInTime = 0.045f,
                holdTime = 0.04f,
                fadeOutTime = 0.18f,
                minIntensity = 0.65f,
                damageToIntensityScale = 0f,
                missingHpIntensityScale = 0f,
                colorFilter = new Color(0.86f, 0.93f, 1f, 1f),
                vignetteColor = new Color(0.08f, 0.16f, 0.24f, 1f),
                vignetteIntensityBoost = 0.16f,
                vignetteSmoothnessBoost = 0.08f,
                postExposureOffset = 0.08f,
                saturationOffset = -8f,
                bloomIntensityBoost = 0.08f,
                bloomTintBlend = 0.2f,
                enableBloomPulse = false
            };
        }

        public static CombatVolumeEffectConfig CreateDefaultPush()
        {
            return new CombatVolumeEffectConfig
            {
                effectType = CombatVolumeEffectType.Push,
                effectKey = "Skill_Push_ImpactPulse",
                fadeInTime = 0.035f,
                holdTime = 0.05f,
                fadeOutTime = 0.16f,
                minIntensity = 0.55f,
                damageToIntensityScale = 0f,
                missingHpIntensityScale = 0f,
                colorFilter = new Color(1f, 0.88f, 0.72f, 1f),
                vignetteColor = new Color(0.32f, 0.18f, 0.06f, 1f),
                vignetteIntensityBoost = 0.12f,
                vignetteSmoothnessBoost = 0.06f,
                postExposureOffset = 0.12f,
                saturationOffset = -4f,
                bloomIntensityBoost = 0.12f,
                bloomTintBlend = 0.25f,
                enableBloomPulse = true
            };
        }

        public static CombatVolumeEffectConfig CreateDefaultGrenade()
        {
            return new CombatVolumeEffectConfig
            {
                effectType = CombatVolumeEffectType.Grenade,
                effectKey = "Skill_Grenade_ExplosionPulse",
                fadeInTime = 0.04f,
                holdTime = 0.06f,
                fadeOutTime = 0.22f,
                minIntensity = 0.7f,
                damageToIntensityScale = 0f,
                missingHpIntensityScale = 0f,
                colorFilter = new Color(1f, 0.76f, 0.52f, 1f),
                vignetteColor = new Color(0.42f, 0.16f, 0.04f, 1f),
                vignetteIntensityBoost = 0.18f,
                vignetteSmoothnessBoost = 0.08f,
                postExposureOffset = 0.18f,
                saturationOffset = -6f,
                bloomIntensityBoost = 0.18f,
                bloomTintBlend = 0.3f,
                enableBloomPulse = true
            };
        }

        public static CombatVolumeEffectConfig CreateDefaultBerserk()
        {
            return new CombatVolumeEffectConfig
            {
                effectType = CombatVolumeEffectType.Berserk,
                effectKey = "Pickup_Berserk_SpeedLines",
                fadeInTime = 0.18f,
                holdTime = 0f,
                fadeOutTime = 0.35f,
                minIntensity = 0.8f,
                damageToIntensityScale = 0f,
                missingHpIntensityScale = 0f,
                colorFilter = new Color(1f, 0.42f, 0.28f, 1f),
                vignetteColor = new Color(0.55f, 0.04f, 0.02f, 1f),
                vignetteIntensityBoost = 0.18f,
                vignetteSmoothnessBoost = 0.08f,
                postExposureOffset = 0.08f,
                saturationOffset = 10f,
                bloomIntensityBoost = 0.12f,
                bloomTintBlend = 0.25f,
                enableBloomPulse = true
            };
        }

        public CombatVolumeEffectConfig Clone()
        {
            return (CombatVolumeEffectConfig)MemberwiseClone();
        }

        public void ApplyMissingDefaults()
        {
            if (string.IsNullOrEmpty(effectKey))
            {
                effectKey = effectType.ToString();
            }

            fadeInTime = Mathf.Max(0f, fadeInTime);
            holdTime = Mathf.Max(0f, holdTime);
            fadeOutTime = Mathf.Max(0f, fadeOutTime);
            minIntensity = Mathf.Clamp01(minIntensity);
            damageToIntensityScale = Mathf.Max(0f, damageToIntensityScale);
            missingHpIntensityScale = Mathf.Max(0f, missingHpIntensityScale);
            vignetteIntensityBoost = Mathf.Max(0f, vignetteIntensityBoost);
            vignetteSmoothnessBoost = Mathf.Max(0f, vignetteSmoothnessBoost);
            bloomIntensityBoost = Mathf.Max(0f, bloomIntensityBoost);
            bloomTintBlend = Mathf.Clamp01(bloomTintBlend);
        }
    }
}
