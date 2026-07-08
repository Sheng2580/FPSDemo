using UnityEngine;

namespace PlayerData
{
    /// <summary>
    /// 玩家最终数值计算器
    /// 统一处理基础配置 存档等级和运行时倍率
    /// </summary>
    public static class PlayerStatsCalculator
    {
        public static int GetMaxHp(PlayerBaseConfig baseConfig, PlayerSaveData saveData)
        {
            PlayerBaseConfig safeBaseConfig = baseConfig ?? PlayerBaseConfig.CreateDefault();
            PlayerSaveData safeSaveData = saveData ?? PlayerSaveData.CreateNew();
            return safeBaseConfig.maxHp + safeSaveData.maxHpLevel * 10;
        }

        public static float GetWalkSpeed(PlayerBaseConfig baseConfig, PlayerSaveData saveData, PlayerRuntimeData runtimeData)
        {
            PlayerBaseConfig safeBaseConfig = baseConfig ?? PlayerBaseConfig.CreateDefault();
            PlayerSaveData safeSaveData = saveData ?? PlayerSaveData.CreateNew();
            PlayerRuntimeData safeRuntimeData = runtimeData ?? CreateDefaultRuntimeData(safeBaseConfig, safeSaveData);
            float baseValue = safeBaseConfig.walkSpeed + safeSaveData.moveSpeedLevel * 0.15f;
            return baseValue * safeRuntimeData.moveSpeedMultiplier;
        }

        public static float GetRunSpeed(PlayerBaseConfig baseConfig, PlayerSaveData saveData, PlayerRuntimeData runtimeData)
        {
            PlayerBaseConfig safeBaseConfig = baseConfig ?? PlayerBaseConfig.CreateDefault();
            PlayerSaveData safeSaveData = saveData ?? PlayerSaveData.CreateNew();
            PlayerRuntimeData safeRuntimeData = runtimeData ?? CreateDefaultRuntimeData(safeBaseConfig, safeSaveData);
            float baseValue = safeBaseConfig.runSpeed + safeSaveData.moveSpeedLevel * 0.15f;
            return baseValue * safeRuntimeData.moveSpeedMultiplier;
        }

        public static float GetJumpHeight(PlayerBaseConfig baseConfig, PlayerRuntimeData runtimeData)
        {
            PlayerBaseConfig safeBaseConfig = baseConfig ?? PlayerBaseConfig.CreateDefault();
            PlayerRuntimeData safeRuntimeData = runtimeData ?? CreateDefaultRuntimeData(safeBaseConfig, PlayerSaveData.CreateNew());
            return safeBaseConfig.jumpHeight * safeRuntimeData.jumpHeightMultiplier;
        }

        public static float GetDodgeCooldown(PlayerBaseConfig baseConfig, PlayerSaveData saveData, PlayerRuntimeData runtimeData)
        {
            PlayerBaseConfig safeBaseConfig = baseConfig ?? PlayerBaseConfig.CreateDefault();
            PlayerSaveData safeSaveData = saveData ?? PlayerSaveData.CreateNew();
            PlayerRuntimeData safeRuntimeData = runtimeData ?? CreateDefaultRuntimeData(safeBaseConfig, safeSaveData);
            float baseValue = safeBaseConfig.dodgeCooldown - safeSaveData.dodgeCooldownLevel * 0.05f;
            baseValue = Mathf.Max(0.2f, baseValue);
            return baseValue * safeRuntimeData.dodgeCooldownMultiplier;
        }

        public static float GetDodgeDistance(PlayerBaseConfig baseConfig)
        {
            PlayerBaseConfig safeBaseConfig = baseConfig ?? PlayerBaseConfig.CreateDefault();
            return safeBaseConfig.dodgeDistance;
        }

        private static PlayerRuntimeData CreateDefaultRuntimeData(PlayerBaseConfig baseConfig, PlayerSaveData saveData)
        {
            PlayerRuntimeData runtimeData = new PlayerRuntimeData();
            runtimeData.InitForNewRun(baseConfig, saveData);
            return runtimeData;
        }
    }
}
