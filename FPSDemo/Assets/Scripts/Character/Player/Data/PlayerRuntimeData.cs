using System;

namespace PlayerData
{
    /// <summary>
    /// 玩家单局运行时数据
    /// 只保存当前一局的状态和临时倍率
    /// 武器运行时数据后续单独拆分
    /// </summary>
    [Serializable]
    public class PlayerRuntimeData
    {
        // 当前生命
        public int currentHp;
        // 当前局最大生命
        public int maxHp;
        // 移动速度倍率
        public float moveSpeedMultiplier = 1f;
        // 跳跃高度倍率
        public float jumpHeightMultiplier = 1f;
        // 狂暴持续时间倍率
        public float berserkDurationMultiplier = 1f;
        // 爆炸伤害倍率
        public float explosionDamageMultiplier = 1f;
        // 弹药道具获取倍率
        public float pickupAmmoMultiplier = 1f;
        // 医疗道具恢复倍率
        public float pickupHealingMultiplier = 1f;
        // 是否无敌
        public bool isInvincible;
        // 是否霸体
        public bool isSuperArmor;

        public void InitForNewRun(PlayerBaseConfig baseConfig, PlayerSaveData saveData)
        {
            maxHp = PlayerStatsCalculator.GetMaxHp(baseConfig, saveData);
            currentHp = maxHp;
            moveSpeedMultiplier = 1f;
            jumpHeightMultiplier = 1f;
            berserkDurationMultiplier = 1f;
            explosionDamageMultiplier = 1f;
            pickupAmmoMultiplier = 1f;
            pickupHealingMultiplier = 1f;
            isInvincible = false;
            isSuperArmor = false;
        }
    }
}
