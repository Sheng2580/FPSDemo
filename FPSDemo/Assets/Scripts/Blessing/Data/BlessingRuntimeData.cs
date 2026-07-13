using System;

namespace Blessing.Data
{
    /// <summary>
    /// 单局祝福运行时数据
    /// </summary>
    [Serializable]
    public class BlessingRuntimeData
    {
        // 祝福编号
        public int blessingId;
        // 当前层数
        public int stackCount;
        // 分类
        public BlessingCategory category;
        // 最近一次选择等级
        public BlessingTier lastTier;
        // 触发冷却计时
        public float cooldownTimer;
    }
}
