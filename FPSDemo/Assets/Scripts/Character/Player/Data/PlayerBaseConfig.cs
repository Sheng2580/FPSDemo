using System;

namespace PlayerData
{
    /// <summary>
    /// 玩家基础配置
    /// 只包含身体 生存 移动相关参数
    /// 武器伤害 射速 弹夹不放在这里
    /// </summary>
    [Serializable]
    public class PlayerBaseConfig
    {
        // 最大生命
        public int maxHp;
        // 走路速度
        public float walkSpeed;
        // 跑步速度
        public float runSpeed;
        // 移动输入死区
        public float moveInputDeadZone;
        // 移动加速度
        public float moveAcceleration;
        // 移动减速度
        public float moveDeceleration;
        // 跳跃高度
        public float jumpHeight;
        // 跳跃输入缓存时间
        public float jumpBufferTime;
        // 土狼时间
        public float coyoteTime;
        // 空中移动控制倍率
        public float airMoveControl;
        // 跳跃结束竖直速度阈值
        public float jumpEndVerticalVelocity;
        // 闪避冷却时间
        public float dodgeCooldown;
        // 闪避距离
        public float dodgeDistance;

        public static PlayerBaseConfig CreateDefault()
        {
            return new PlayerBaseConfig
            {
                maxHp = 100,
                walkSpeed = 4.5f,
                runSpeed = 6.5f,
                moveInputDeadZone = 0.05f,
                moveAcceleration = 18f,
                moveDeceleration = 24f,
                jumpHeight = 1.2f,
                jumpBufferTime = 0.15f,
                coyoteTime = 0.12f,
                airMoveControl = 0.65f,
                jumpEndVerticalVelocity = 0f,
                dodgeCooldown = 1f,
                dodgeDistance = 5f,
            };
        }
    }
}
