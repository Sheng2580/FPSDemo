namespace Blessing.Data
{
    /// <summary>
    /// 祝福修正数值类型
    /// </summary>
    public enum BlessingStatType
    {
        // 最大生命
        MaxHp,
        // 移动速度
        MoveSpeed,
        // 跳跃高度
        JumpHeight,
        // 狂暴持续时间
        BerserkDuration,
        // 爆炸伤害
        ExplosionDamage,
        // 弹药道具获取
        PickupAmmoGain,
        // 医疗道具恢复
        PickupHealing,
        // 能量获取
        EnergyGain,
        // 武器伤害
        WeaponDamage,
        // 武器额外等级
        WeaponUpgradeLevel,
        // 武器弹匣
        WeaponMagazine,
        // 武器后坐力
        WeaponRecoil,
        // 技能冷却缩减
        SkillCooldownReduction,
        // 技能最大数量
        SkillMaxCount,
        // 金币获取
        GoldGain,
        // 获得当前未携带的另一把主武器
        GrantMissingPrimaryWeapon,
        // 武器射击速度
        WeaponFireRate,
        // 武器换弹速度
        WeaponReloadSpeed,
        // 武器备弹上限
        WeaponReserveAmmo,
        // 技能伤害
        SkillDamage,
        // 击杀恢复备弹
        KillAmmoRestore,
        // 击杀恢复生命
        KillHealthRestore,
        // 击杀触发狂暴的持续时间
        KillBerserkDuration,
        // 击杀随机基础属性成长强度
        KillRandomBaseStat
    }
}
