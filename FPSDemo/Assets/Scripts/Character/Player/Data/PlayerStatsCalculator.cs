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
            PlayerBaseConfig safeBaseConfig = baseConfig ?? PlayerDefaultConfigAsset.LoadRuntimeConfig();
            PlayerSaveData safeSaveData = saveData ?? PlayerSaveData.CreateNew();
            int fallbackBonus = safeSaveData.maxHpLevel * 10;
            int upgradeBonus = PermanentUpgradeConfigLoader.GetPlayerMaxHpBonus(
                safeSaveData.maxHpLevel,
                fallbackBonus);
            return safeBaseConfig.maxHp + upgradeBonus;
        }

        public static float GetWalkSpeed(PlayerBaseConfig baseConfig, PlayerSaveData saveData, PlayerRuntimeData runtimeData)
        {
            PlayerBaseConfig safeBaseConfig = baseConfig ?? PlayerDefaultConfigAsset.LoadRuntimeConfig();
            PlayerSaveData safeSaveData = saveData ?? PlayerSaveData.CreateNew();
            PlayerRuntimeData safeRuntimeData = runtimeData ?? CreateDefaultRuntimeData(safeBaseConfig, safeSaveData);
            float fallbackBonus = safeSaveData.moveSpeedLevel * 0.15f;
            float upgradeBonus = PermanentUpgradeConfigLoader.GetPlayerUpgradeValue(
                PermanentUpgradeConfigLoader.MoveSpeedStatType,
                safeSaveData.moveSpeedLevel,
                fallbackBonus);
            float baseValue = safeBaseConfig.walkSpeed + upgradeBonus;
            return baseValue * safeRuntimeData.moveSpeedMultiplier;
        }

        public static float GetRunSpeed(PlayerBaseConfig baseConfig, PlayerSaveData saveData, PlayerRuntimeData runtimeData)
        {
            PlayerBaseConfig safeBaseConfig = baseConfig ?? PlayerDefaultConfigAsset.LoadRuntimeConfig();
            PlayerSaveData safeSaveData = saveData ?? PlayerSaveData.CreateNew();
            PlayerRuntimeData safeRuntimeData = runtimeData ?? CreateDefaultRuntimeData(safeBaseConfig, safeSaveData);
            float fallbackBonus = safeSaveData.moveSpeedLevel * 0.15f;
            float upgradeBonus = PermanentUpgradeConfigLoader.GetPlayerUpgradeValue(
                PermanentUpgradeConfigLoader.MoveSpeedStatType,
                safeSaveData.moveSpeedLevel,
                fallbackBonus);
            float baseValue = safeBaseConfig.runSpeed + upgradeBonus;
            return baseValue * safeRuntimeData.moveSpeedMultiplier;
        }

        public static float GetJumpHeight(PlayerBaseConfig baseConfig, PlayerSaveData saveData, PlayerRuntimeData runtimeData)
        {
            PlayerBaseConfig safeBaseConfig = baseConfig ?? PlayerDefaultConfigAsset.LoadRuntimeConfig();
            PlayerSaveData safeSaveData = saveData ?? PlayerSaveData.CreateNew();
            PlayerRuntimeData safeRuntimeData = runtimeData ?? CreateDefaultRuntimeData(safeBaseConfig, safeSaveData);
            float fallbackBonus = safeSaveData.jumpHeightLevel * 0.05f;
            float upgradeBonus = PermanentUpgradeConfigLoader.GetPlayerUpgradeValue(
                PermanentUpgradeConfigLoader.JumpHeightStatType,
                safeSaveData.jumpHeightLevel,
                fallbackBonus);
            return (safeBaseConfig.jumpHeight + upgradeBonus) * safeRuntimeData.jumpHeightMultiplier;
        }

        private static PlayerRuntimeData CreateDefaultRuntimeData(PlayerBaseConfig baseConfig, PlayerSaveData saveData)
        {
            PlayerRuntimeData runtimeData = new PlayerRuntimeData();
            runtimeData.InitForNewRun(baseConfig, saveData);
            return runtimeData;
        }
    }
}
