namespace Combat
{
    /// <summary>
    /// 单局结算写入结果
    /// </summary>
    public class CombatRunSettlementResult
    {
        // 是否刷新最长存活纪录
        public bool isNewBestSurvivalTime;
        // 是否刷新最高击杀纪录
        public bool isNewBestKillCount;
        // 结算后的永久金币
        public int totalGold;
    }
}
