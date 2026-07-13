namespace Blessing.Data
{
    /// <summary>
    /// 祝福作用目标
    /// </summary>
    public enum BlessingTargetType
    {
        // 玩家
        Player,
        // 当前武器
        CurrentWeapon,
        // 指定武器
        SpecificWeapon,
        // 全部武器
        AllWeapons,
        // 技能
        Skill,
        // 玩法触发器
        GameplayTrigger,
        // 经济收益
        Economy
    }
}
