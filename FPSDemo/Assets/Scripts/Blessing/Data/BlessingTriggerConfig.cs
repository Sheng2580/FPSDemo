using System;
using UnityEngine;

namespace Blessing.Data
{
    /// <summary>
    /// 祝福触发配置
    /// </summary>
    [Serializable]
    public class BlessingTriggerConfig
    {
        // 触发类型
        public BlessingTriggerType triggerType;
        // 触发概率
        public float chance;
        // 触发冷却
        public float cooldown;
        // 效果 key
        public string effectKey;
        // 音效 key
        public string audioKey;
        // 伤害倍率
        public float damageMultiplier;
        // 半径
        public float radius;
        // 链接数量
        public int chainCount;
        // 最大激活数量
        public int maxActiveCount;

        public BlessingTriggerConfig Clone()
        {
            return (BlessingTriggerConfig)MemberwiseClone();
        }

        public void ApplyMissingDefaults()
        {
            chance = Mathf.Clamp01(chance);
            cooldown = Mathf.Max(0f, cooldown);
            damageMultiplier = Mathf.Max(0f, damageMultiplier);
            radius = Mathf.Max(0f, radius);
            chainCount = Mathf.Max(0, chainCount);
            maxActiveCount = Mathf.Max(0, maxActiveCount);
        }
    }
}
