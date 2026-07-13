namespace Blessing.Data
{
    /// <summary>
    /// 祝福触发类型
    /// </summary>
    public enum BlessingTriggerType
    {
        // 无触发
        None,
        // 开火时
        OnFire,
        // 命中敌人时
        OnHitEnemy,
        // 暴击时
        OnCriticalHit,
        // 击杀敌人时
        OnKillEnemy,
        // 换弹时
        OnReload,
        // 闪避时
        OnDodge,
        // 释放技能时
        OnSkillCast,
        // 玩家受伤时
        OnPlayerDamaged,
        // 波次开始时
        OnWaveStart
    }
}
