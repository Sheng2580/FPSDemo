using System;

namespace PlayerData
{
    /// <summary>
    /// 玩家技能单局运行时数据
    /// 不写回配置资源
    /// </summary>
    [Serializable]
    public class PlayerSkillRuntimeData
    {
        // 技能编号
        public int skillId;
        // 技能类型
        public SkillType skillType;
        // 剩余冷却
        public float cooldownRemaining;
        // 是否正在释放
        public bool isCasting;
        // 释放剩余时间
        public float castTimeRemaining;
        // 当前携带数量
        public int currentCount;
        // 最大携带数量
        public int maxCount;
        // 是否有输入缓存
        public bool pendingInput;
        // 输入缓存剩余时间
        public float inputBufferRemaining;
        // 本局技能等级
        public int runtimeLevel;
        // 局外永久冷却缩减
        public float permanentCooldownReduction;
        // 局内祝福冷却缩减
        public float blessingCooldownReduction;
        // 伤害倍率
        public float damageMultiplier = 1f;
        // 范围倍率
        public float radiusMultiplier = 1f;

        public void InitForNewRun(PlayerSkillConfig config)
        {
            if (config == null)
            {
                return;
            }

            skillId = config.skillId;
            skillType = config.skillType;
            cooldownRemaining = 0f;
            isCasting = false;
            castTimeRemaining = 0f;
            currentCount = config.skillType == SkillType.Grenade ? config.initialCount : 0;
            maxCount = config.skillType == SkillType.Grenade ? config.maxCount : 0;
            pendingInput = false;
            inputBufferRemaining = 0f;
            runtimeLevel = 1;
            permanentCooldownReduction = 0f;
            blessingCooldownReduction = 0f;
            damageMultiplier = 1f;
            radiusMultiplier = 1f;
        }
    }
}
