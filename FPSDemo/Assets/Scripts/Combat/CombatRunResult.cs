namespace Combat
{
    /// <summary>
    /// 本局结算完成后交给表现层的数据
    /// </summary>
    public sealed class CombatRunResult
    {
        public float SurvivalSeconds { get; }
        public int KillCount { get; }
        public int GoldEarned { get; }
        public int BlessingSelectCount { get; }
        public int PickupCollectedCount { get; }
        public int WeaponFireCount { get; }
        public CombatRunSettlementResult Settlement { get; }

        public CombatRunResult(
            float survivalSeconds,
            int killCount,
            int goldEarned,
            int blessingSelectCount,
            int pickupCollectedCount,
            int weaponFireCount,
            CombatRunSettlementResult settlement)
        {
            SurvivalSeconds = survivalSeconds;
            KillCount = killCount;
            GoldEarned = goldEarned;
            BlessingSelectCount = blessingSelectCount;
            PickupCollectedCount = pickupCollectedCount;
            WeaponFireCount = weaponFireCount;
            Settlement = settlement;
        }
    }
}
