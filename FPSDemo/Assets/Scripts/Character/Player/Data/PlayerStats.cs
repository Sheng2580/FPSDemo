namespace PlayerData
{
    /// <summary>
    /// 玩家数值访问层
    /// 聚合基础配置 永久存档和当前局运行时数据
    /// 统一提供最终属性
    /// 不包含武器数据
    /// </summary>
    public class PlayerStats
    {
        // 基础配置数据
        private PlayerBaseConfig _baseConfig;
        // 永久存档数据
        private PlayerSaveData _saveData;
        // 当前局运行时数据
        private PlayerRuntimeData _runtimeData;

        // 基础配置
        public PlayerBaseConfig BaseConfig => _baseConfig;
        // 永久存档
        public PlayerSaveData SaveData => _saveData;
        // 当前局运行时数据
        public PlayerRuntimeData RuntimeData => _runtimeData;

        // 最终走路速度
        public float WalkSpeed => PlayerStatsCalculator.GetWalkSpeed(_baseConfig, _saveData, _runtimeData);
        // 最终跑步速度
        public float RunSpeed => PlayerStatsCalculator.GetRunSpeed(_baseConfig, _saveData, _runtimeData);
        // 最终输入死区
        public float MoveInputDeadZone => _baseConfig.moveInputDeadZone;
        // 最终移动加速度
        public float MoveAcceleration => _baseConfig.moveAcceleration;
        // 最终移动减速度
        public float MoveDeceleration => _baseConfig.moveDeceleration;
        // 最终跳跃高度
        public float JumpHeight => PlayerStatsCalculator.GetJumpHeight(_baseConfig, _runtimeData);
        // 最终跳跃缓存时间
        public float JumpBufferTime => _baseConfig.jumpBufferTime;
        // 最终土狼时间
        public float CoyoteTime => _baseConfig.coyoteTime;
        // 最终空中移动控制倍率
        public float AirMoveControl => _baseConfig.airMoveControl;
        // 最终跳跃结束竖直速度阈值
        public float JumpEndVerticalVelocity => _baseConfig.jumpEndVerticalVelocity;
        // 最终闪避冷却
        public float DodgeCooldown => PlayerStatsCalculator.GetDodgeCooldown(_baseConfig, _saveData, _runtimeData);
        // 最终闪避距离
        public float DodgeDistance => PlayerStatsCalculator.GetDodgeDistance(_baseConfig);
        // 最终最大生命
        public int MaxHp => _runtimeData.maxHp;
        // 当前生命
        public int CurrentHp => _runtimeData.currentHp;

        public void Init(PlayerBaseConfig baseConfig, PlayerSaveData saveData)
        {
            _baseConfig = baseConfig ?? PlayerBaseConfig.CreateDefault();
            _saveData = saveData ?? PlayerSaveData.CreateNew();
            _runtimeData = new PlayerRuntimeData();
            _runtimeData.InitForNewRun(_baseConfig, _saveData);
        }
    }
}
