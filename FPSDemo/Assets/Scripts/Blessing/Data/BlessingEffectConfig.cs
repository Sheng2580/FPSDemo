using System;
using UnityEngine;

namespace Blessing.Data
{
    /// <summary>
    /// 祝福效果配置
    /// </summary>
    [Serializable]
    public class BlessingEffectConfig
    {
        // 作用目标
        public BlessingTargetType targetType;
        // 数值类型
        public BlessingStatType statType;
        // 修正方式
        public BlessingModifyType modifyType;
        // Normal 数值
        public float normalValue;
        // Plus 数值
        public float plusValue;
        // PlusPlus 数值
        public float plusPlusValue;

        public float GetValue(BlessingTier tier)
        {
            return tier switch
            {
                BlessingTier.Plus => plusValue,
                BlessingTier.PlusPlus => plusPlusValue,
                _ => normalValue
            };
        }

        public BlessingEffectConfig Clone()
        {
            return (BlessingEffectConfig)MemberwiseClone();
        }

        public void ApplyMissingDefaults(BlessingTargetType fallbackTargetType)
        {
            if (targetType == BlessingTargetType.GameplayTrigger)
            {
                targetType = fallbackTargetType;
            }
        }
    }
}
