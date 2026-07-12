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
