using System;
using PlayerData;
using UnityEngine;

namespace Blessing.Data
{
    /// <summary>
    /// 正式祝福配置
    /// 字段按 Luban 可迁移结构维护
    /// </summary>
    [Serializable]
    public class BlessingConfig
    {
        // 基础祝福编号
        public int blessingId;
        // 祝福名称
        public string blessingName;
        // 描述模板
        public string descriptionTemplate;
        // 分类
        public BlessingCategory category;
        // 作用目标
        public BlessingTargetType targetType;
        // 默认展示等级
        public BlessingTier tier;
        // 解锁能量等级
        public int unlockEnergyLevel;
        // 解锁波次
        public int unlockWave;
        // 抽取权重
        public float weight;
        // 最大层数
        public int maxStack;
        // 是否在第一次祝福抽取时保底出现
        public bool guaranteedFirstRoll;
        // 从指定抽取次数开始持续保底 0表示关闭
        public int guaranteedUntilSelectedFromRoll;
        // 需求武器编号 0 表示不限制
        public int requiredWeaponId;
        // 是否需求技能类型
        public bool requiresSkillType;
        // 需求技能类型
        public SkillType requiredSkillType;
        // 图标 key
        public string iconKey;
        // 效果配置
        public BlessingEffectConfig[] effects;
        // 触发配置
        public BlessingTriggerConfig[] triggers;

        public BlessingConfig Clone()
        {
            BlessingConfig clone = (BlessingConfig)MemberwiseClone();
            if (effects != null)
            {
                clone.effects = new BlessingEffectConfig[effects.Length];
                for (int i = 0; i < effects.Length; i++)
                {
                    clone.effects[i] = effects[i]?.Clone();
                }
            }

            if (triggers != null)
            {
                clone.triggers = new BlessingTriggerConfig[triggers.Length];
                for (int i = 0; i < triggers.Length; i++)
                {
                    clone.triggers[i] = triggers[i]?.Clone();
                }
            }

            return clone;
        }

        public void ApplyMissingDefaults()
        {
            blessingId = Mathf.Max(1, blessingId);
            if (string.IsNullOrEmpty(blessingName))
            {
                blessingName = $"Blessing {blessingId}";
            }

            if (string.IsNullOrEmpty(descriptionTemplate))
            {
                descriptionTemplate = blessingName;
            }

            unlockEnergyLevel = Mathf.Max(1, unlockEnergyLevel);
            unlockWave = Mathf.Max(1, unlockWave);
            weight = Mathf.Max(0f, weight);
            // 0 表示可无限叠加
            maxStack = Mathf.Max(0, maxStack);
            guaranteedUntilSelectedFromRoll = Mathf.Max(0, guaranteedUntilSelectedFromRoll);
            requiredWeaponId = Mathf.Max(0, requiredWeaponId);

            if (string.IsNullOrEmpty(iconKey))
            {
                iconKey = $"Blessing_{blessingId}";
            }

            if (effects != null)
            {
                for (int i = 0; i < effects.Length; i++)
                {
                    effects[i]?.ApplyMissingDefaults(targetType);
                }
            }

            if (triggers != null)
            {
                for (int i = 0; i < triggers.Length; i++)
                {
                    triggers[i]?.ApplyMissingDefaults();
                }
            }
        }

        public float GetFirstEffectValue(BlessingTier selectedTier)
        {
            return effects != null && effects.Length > 0 && effects[0] != null
                ? effects[0].GetValue(selectedTier)
                : 0f;
        }
    }
}
