using System;
using System.Collections.Generic;

namespace PlayerData
{
    /// <summary>
    /// 玩家永久存档数据
    /// 只保存长期进度
    /// 不保存武器实时战斗属性
    /// </summary>
    [Serializable]
    public class PlayerSaveData
    {
        // 当前金币
        public int gold;
        // 最长生存时间
        public float bestSurvivalTime;
        // 最大生命等级
        public int maxHpLevel;
        // 移动速度等级
        public int moveSpeedLevel;
        // 闪避冷却等级
        public int dodgeCooldownLevel;
        // 已解锁武器 id 列表
        public List<int> unlockedWeaponIds = new List<int>();

        public static PlayerSaveData CreateNew()
        {
            return new PlayerSaveData
            {
                gold = 0,
                bestSurvivalTime = 0f,
                maxHpLevel = 0,
                moveSpeedLevel = 0,
                dodgeCooldownLevel = 0,
                unlockedWeaponIds = new List<int> { 1 },
            };
        }
    }
}
