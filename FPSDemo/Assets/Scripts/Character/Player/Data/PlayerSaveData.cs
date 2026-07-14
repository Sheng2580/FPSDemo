using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Combat;
using UnityEngine;
using Weapon.Data;

namespace PlayerData
{
    /// <summary>
    /// 玩家永久存档数据
    /// 只保存长期进度
    /// 不保存武器实时战斗属性
    /// </summary>
    [Serializable]
    public class PlayerSaveData
    {
        // 存档版本
        public int saveVersion;
        // 当前金币
        public int gold;
        // 最长生存时间
        public float bestSurvivalTime;
        // 最高单局击杀数
        public int bestKillCount;
        // 最大生命等级
        public int maxHpLevel;
        // 移动速度等级
        public int moveSpeedLevel;
        // 跳跃高度等级
        public int jumpHeightLevel;
        // 技能冷却缩减等级
        public int skillCooldownLevel;
        // 大厅选择的第二把武器 手枪固定携带
        public int selectedSecondWeaponId;
        // 每把武器的永久升级等级
        public List<WeaponUpgradeSaveData> weaponUpgrades = new List<WeaponUpgradeSaveData>();
        // 已解锁武器 id 列表
        public List<int> unlockedWeaponIds = new List<int>();

        public static PlayerSaveData CreateNew()
        {
            return new PlayerSaveData
            {
                saveVersion = PlayerProgressSaveService.CurrentSaveVersion,
                gold = 10000,
                bestSurvivalTime = 0f,
                bestKillCount = 0,
                maxHpLevel = 0,
                moveSpeedLevel = 0,
                jumpHeightLevel = 0,
                skillCooldownLevel = 0,
                selectedSecondWeaponId = 0,
                weaponUpgrades = new List<WeaponUpgradeSaveData>(),
                unlockedWeaponIds = new List<int> { 1, 2, 3 },
            };
        }
    }

    [Serializable]
    public class WeaponUpgradeSaveData
    {
        public int weaponId;
        public int level;

        public WeaponUpgradeSaveData(int weaponId, int level)
        {
            this.weaponId = weaponId;
            this.level = level;
        }
    }

    /// <summary>
    /// 存档列表只读摘要
    /// </summary>
    public sealed class PlayerSaveSlotSummary
    {
        public string FileName { get; }
        public string FullPath { get; }
        public DateTime SavedAt { get; }
        public bool IsLegacy { get; }
        public int Gold { get; }
        public float BestSurvivalTime { get; }
        public int BestKillCount { get; }
        public int PlayerUpgradeLevel { get; }
        public int SelectedSecondWeaponId { get; }

        public PlayerSaveSlotSummary(
            string fileName,
            string fullPath,
            DateTime savedAt,
            bool isLegacy,
            int gold,
            float bestSurvivalTime,
            int bestKillCount,
            int playerUpgradeLevel,
            int selectedSecondWeaponId)
        {
            FileName = fileName ?? string.Empty;
            FullPath = fullPath ?? string.Empty;
            SavedAt = savedAt;
            IsLegacy = isLegacy;
            Gold = Math.Max(0, gold);
            BestSurvivalTime = Math.Max(0f, bestSurvivalTime);
            BestKillCount = Math.Max(0, bestKillCount);
            PlayerUpgradeLevel = Math.Max(0, playerUpgradeLevel);
            SelectedSecondWeaponId = selectedSecondWeaponId;
        }
    }

    /// <summary>
    /// 玩家长期进度存档入口
    /// Hall 和 Combat 都从这里读同一份数据
    /// </summary>
    public static class PlayerProgressSaveService
    {
        public const int CurrentSaveVersion = 2;
        public const int MaxPermanentUpgradeLevel = 10;

        private const string LegacySaveFileName = "PlayerProgress.json";
        private const string SaveDirectoryName = "Saves";
        private const string SaveFilePrefix = "Save_";
        private const string SaveFileExtension = ".json";
        private const string SaveTimeFormat = "yyyyMMdd_HHmmss";

        private static PlayerSaveData cachedSaveData;
        private static string currentSaveFilePath;
        private static bool currentSessionDirty;

        public static bool HasCurrentSession => cachedSaveData != null;
        public static bool IsCurrentSessionDirty => currentSessionDirty;
        public static string CurrentSaveFilePath => currentSaveFilePath ?? string.Empty;
        public static string CurrentSaveFileName => string.IsNullOrEmpty(currentSaveFilePath)
            ? string.Empty
            : Path.GetFileName(currentSaveFilePath);

        /// <summary>
        /// 创建只存在内存中的全新存档会话
        /// </summary>
        public static PlayerSaveData BeginNewSession()
        {
            int previousGold = cachedSaveData != null ? cachedSaveData.gold : 0;
            cachedSaveData = Normalize(PlayerSaveData.CreateNew());
            currentSaveFilePath = null;
            currentSessionDirty = true;
            NotifyGoldChanged(previousGold, cachedSaveData.gold);
            return cachedSaveData;
        }

        /// <summary>
        /// 继续最近一次保存的存档
        /// </summary>
        public static bool TryContinueLatestSession(out PlayerSaveData saveData)
        {
            IReadOnlyList<PlayerSaveSlotSummary> summaries = GetSaveSlotSummaries();
            for (int i = 0; i < summaries.Count; i++)
            {
                if (TryLoadSession(summaries[i], out saveData))
                {
                    return true;
                }
            }

            saveData = null;
            return false;
        }

        /// <summary>
        /// 按存档摘要加载指定会话
        /// </summary>
        public static bool TryLoadSession(PlayerSaveSlotSummary summary, out PlayerSaveData saveData)
        {
            if (summary == null)
            {
                saveData = null;
                return false;
            }

            return TryLoadSession(summary.FileName, out saveData);
        }

        /// <summary>
        /// 按文件名加载指定会话
        /// </summary>
        public static bool TryLoadSession(string fileName, out PlayerSaveData saveData)
        {
            saveData = null;
            if (!TryResolveSavePath(fileName, out string path) || !File.Exists(path))
            {
                return false;
            }

            if (!TryReadSaveData(path, out PlayerSaveData loadedData))
            {
                return false;
            }

            int previousGold = cachedSaveData != null ? cachedSaveData.gold : 0;
            cachedSaveData = loadedData;
            currentSaveFilePath = path;
            currentSessionDirty = false;
            saveData = cachedSaveData;
            NotifyGoldChanged(previousGold, cachedSaveData.gold);
            return true;
        }

        /// <summary>
        /// 获取可供读取界面展示的存档列表
        /// </summary>
        public static IReadOnlyList<PlayerSaveSlotSummary> GetSaveSlotSummaries()
        {
            List<PlayerSaveSlotSummary> summaries = new List<PlayerSaveSlotSummary>();
            string saveDirectory = GetSaveDirectoryPath();
            try
            {
                if (Directory.Exists(saveDirectory))
                {
                    string[] paths = Directory.GetFiles(
                        saveDirectory,
                        SaveFilePrefix + "*" + SaveFileExtension,
                        SearchOption.TopDirectoryOnly);
                    for (int i = 0; i < paths.Length; i++)
                    {
                        if (TryCreateSummary(paths[i], false, out PlayerSaveSlotSummary summary))
                        {
                            summaries.Add(summary);
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[PlayerProgress] 存档目录读取失败 {exception.Message}");
            }

            string legacyPath = GetLegacySavePath();
            if (File.Exists(legacyPath)
                && TryCreateSummary(legacyPath, true, out PlayerSaveSlotSummary legacySummary))
            {
                summaries.Add(legacySummary);
            }

            summaries.Sort((left, right) =>
            {
                int timeCompare = right.SavedAt.CompareTo(left.SavedAt);
                return timeCompare != 0
                    ? timeCompare
                    : string.CompareOrdinal(right.FileName, left.FileName);
            });
            return summaries.AsReadOnly();
        }

        public static PlayerSaveData Load()
        {
            if (cachedSaveData != null)
            {
                return cachedSaveData;
            }

            if (TryContinueLatestSession(out PlayerSaveData continuedSaveData))
            {
                return continuedSaveData;
            }

            return BeginNewSession();
        }

        /// <summary>
        /// 兼容旧调用 返回当前内存会话而不重新读盘
        /// </summary>
        public static PlayerSaveData Reload()
        {
            return Load();
        }

        /// <summary>
        /// 更新当前内存会话并标记未保存
        /// </summary>
        public static void Save(PlayerSaveData saveData)
        {
            cachedSaveData = Normalize(saveData);
            currentSessionDirty = true;
        }

        /// <summary>
        /// Hall 手动保存入口
        /// </summary>
        public static bool CommitCurrentSession(out PlayerSaveSlotSummary summary)
        {
            summary = null;
            if (cachedSaveData == null)
            {
                return false;
            }

            cachedSaveData = Normalize(cachedSaveData);
            string saveDirectory = GetSaveDirectoryPath();
            string previousPath = currentSaveFilePath;
            string targetPath = CreateUniqueTimestampSavePath(saveDirectory, previousPath);
            string tempPath = targetPath + ".tmp";

            try
            {
                Directory.CreateDirectory(saveDirectory);
                File.WriteAllText(tempPath, JsonUtility.ToJson(cachedSaveData, true));
                File.Move(tempPath, targetPath);

                currentSaveFilePath = targetPath;
                currentSessionDirty = false;

                if (!string.IsNullOrEmpty(previousPath)
                    && !PathsEqual(previousPath, targetPath)
                    && File.Exists(previousPath))
                {
                    try
                    {
                        File.Delete(previousPath);
                    }
                    catch (Exception deleteException)
                    {
                        Debug.LogWarning($"[PlayerProgress] 新存档已写入 旧文件删除失败 {deleteException.Message}");
                    }
                }

                if (!TryCreateSummary(targetPath, false, out summary))
                {
                    summary = CreateSummary(targetPath, cachedSaveData, false, DateTime.Now);
                }

                Debug.Log($"[PlayerProgress] 手动保存完成 {targetPath}");
                return true;
            }
            catch (Exception exception)
            {
                if (File.Exists(tempPath))
                {
                    try
                    {
                        File.Delete(tempPath);
                    }
                    catch
                    {
                    }
                }

                Debug.LogWarning($"[PlayerProgress] 手动保存失败 {exception.Message}");
                return false;
            }
        }

        public static bool SaveCurrentSession(out PlayerSaveSlotSummary summary)
        {
            return CommitCurrentSession(out summary);
        }

        public static void SaveSelectedSecondWeapon(int weaponId)
        {
            PlayerSaveData saveData = Load();
            saveData.selectedSecondWeaponId = NormalizeSecondWeaponId(weaponId);
            Save(saveData);
        }

        public static CombatRunSettlementResult SettleCombatRun(
            float survivalSeconds,
            int killCount,
            int goldEarned)
        {
            float safeSurvivalSeconds = float.IsNaN(survivalSeconds) || float.IsInfinity(survivalSeconds)
                ? 0f
                : Mathf.Max(0f, survivalSeconds);
            int safeKillCount = Mathf.Max(0, killCount);
            int safeGoldEarned = Mathf.Max(0, goldEarned);

            PlayerSaveData saveData = Load();
            float previousBestSurvivalTime = saveData.bestSurvivalTime;
            int previousBestKillCount = saveData.bestKillCount;
            int previousGold = saveData.gold;

            saveData.gold = (int)Math.Min(
                int.MaxValue,
                (long)Mathf.Max(0, saveData.gold) + safeGoldEarned);
            saveData.bestSurvivalTime = Mathf.Max(saveData.bestSurvivalTime, safeSurvivalSeconds);
            saveData.bestKillCount = Mathf.Max(saveData.bestKillCount, safeKillCount);
            Save(saveData);
            NotifyGoldChanged(previousGold, saveData.gold);

            return new CombatRunSettlementResult
            {
                isNewBestSurvivalTime = safeSurvivalSeconds > previousBestSurvivalTime,
                isNewBestKillCount = safeKillCount > previousBestKillCount,
                totalGold = saveData.gold
            };
        }

        public static int NormalizeSecondWeaponId(int weaponId)
        {
            return weaponId == 2 || weaponId == 3 ? weaponId : 0;
        }

        public static int GetWeaponUpgradeLevel(int weaponId)
        {
            PlayerSaveData saveData = Load();
            int maxLevel = GetWeaponMaxUpgradeLevel(weaponId);
            if (saveData.weaponUpgrades == null)
            {
                return 0;
            }

            for (int i = 0; i < saveData.weaponUpgrades.Count; i++)
            {
                WeaponUpgradeSaveData upgrade = saveData.weaponUpgrades[i];
                if (upgrade != null && upgrade.weaponId == weaponId)
                {
                    return Mathf.Clamp(upgrade.level, 0, maxLevel);
                }
            }

            return 0;
        }

        public static bool TryUpgradeWeapon(int weaponId)
        {
            PlayerSaveData saveData = Load();
            int currentLevel = GetWeaponUpgradeLevel(weaponId);
            int maxLevel = GetWeaponMaxUpgradeLevel(weaponId);
            if (currentLevel >= maxLevel)
            {
                return false;
            }

            int cost = GetWeaponUpgradeCost(weaponId, currentLevel);
            if (saveData.gold < cost)
            {
                return false;
            }

            int previousGold = saveData.gold;
            saveData.gold -= cost;
            SetWeaponUpgradeLevel(saveData, weaponId, currentLevel + 1);
            Save(saveData);
            NotifyGoldChanged(previousGold, saveData.gold);
            return true;
        }

        public static bool TryUpgradePlayerMaxHp()
        {
            return TryUpgradePlayerStat(PermanentUpgradeConfigLoader.MaxHpStatType);
        }

        public static bool TryUpgradeAllPlayerStats()
        {
            PlayerSaveData saveData = Load();
            int currentLevel = GetPlayerSharedUpgradeLevel(saveData);
            int maxLevel = GetPlayerSharedMaxUpgradeLevel();
            if (currentLevel >= maxLevel)
            {
                return false;
            }

            int cost = GetPlayerSharedUpgradeCost(currentLevel);
            if (saveData.gold < cost)
            {
                return false;
            }

            int targetLevel = currentLevel + 1;
            int previousGold = saveData.gold;
            saveData.gold -= cost;
            saveData.maxHpLevel = targetLevel;
            saveData.moveSpeedLevel = targetLevel;
            saveData.jumpHeightLevel = targetLevel;
            saveData.skillCooldownLevel = targetLevel;
            Save(saveData);
            NotifyGoldChanged(previousGold, saveData.gold);
            return true;
        }

        public static bool TryUpgradePlayerMoveSpeed()
        {
            return TryUpgradePlayerStat(PermanentUpgradeConfigLoader.MoveSpeedStatType);
        }

        public static bool TryUpgradePlayerJumpHeight()
        {
            return TryUpgradePlayerStat(PermanentUpgradeConfigLoader.JumpHeightStatType);
        }

        public static bool TryUpgradePlayerSkillCooldown()
        {
            return TryUpgradePlayerStat(PermanentUpgradeConfigLoader.SkillCooldownReductionStatType);
        }

        public static bool TryUpgradePlayerStat(string statType)
        {
            PlayerSaveData saveData = Load();
            int maxLevel = GetPlayerMaxUpgradeLevel(statType);
            int currentLevel = Mathf.Clamp(GetPlayerUpgradeLevel(saveData, statType), 0, maxLevel);
            if (currentLevel >= maxLevel)
            {
                return false;
            }

            int cost = GetPlayerUpgradeCost(statType, currentLevel);
            if (saveData.gold < cost)
            {
                return false;
            }

            int previousGold = saveData.gold;
            saveData.gold -= cost;
            SetPlayerUpgradeLevel(saveData, statType, currentLevel + 1);
            Save(saveData);
            NotifyGoldChanged(previousGold, saveData.gold);
            return true;
        }

        private static void NotifyGoldChanged(int previousGold, int currentGold)
        {
            int safePreviousGold = Mathf.Max(0, previousGold);
            int safeCurrentGold = Mathf.Max(0, currentGold);
            if (safePreviousGold == safeCurrentGold)
            {
                return;
            }

            EventCenter.Instance.EventTrigger(
                GameEvent.PlayerGoldChanged,
                new PlayerGoldChangedEventData(safeCurrentGold, safeCurrentGold - safePreviousGold));
        }

        public static int GetPlayerUpgradeLevel(string statType)
        {
            return GetPlayerUpgradeLevel(Load(), statType);
        }

        public static int GetPlayerSharedUpgradeLevel()
        {
            return GetPlayerSharedUpgradeLevel(Load());
        }

        public static int GetPlayerSharedMaxUpgradeLevel()
        {
            int maxHpLevel = GetPlayerMaxUpgradeLevel(PermanentUpgradeConfigLoader.MaxHpStatType);
            int moveSpeedLevel = GetPlayerMaxUpgradeLevel(PermanentUpgradeConfigLoader.MoveSpeedStatType);
            int jumpHeightLevel = GetPlayerMaxUpgradeLevel(PermanentUpgradeConfigLoader.JumpHeightStatType);
            int skillCooldownLevel = GetPlayerMaxUpgradeLevel(PermanentUpgradeConfigLoader.SkillCooldownReductionStatType);
            int energyGainLevel = GetPlayerMaxUpgradeLevel(PermanentUpgradeConfigLoader.EnergyGainEfficiencyStatType);
            return Mathf.Min(maxHpLevel, moveSpeedLevel, jumpHeightLevel, skillCooldownLevel, energyGainLevel);
        }

        public static int GetPlayerSharedUpgradeCost(int currentLevel)
        {
            return GetPlayerUpgradeCost(PermanentUpgradeConfigLoader.MaxHpStatType, currentLevel);
        }

        public static int GetUpgradeCost(int currentLevel)
        {
            int safeLevel = Mathf.Clamp(currentLevel, 0, MaxPermanentUpgradeLevel);
            return safeLevel >= MaxPermanentUpgradeLevel ? 0 : (safeLevel + 1) * 100;
        }

        public static int GetPlayerMaxUpgradeLevel()
        {
            return GetPlayerMaxUpgradeLevel(PermanentUpgradeConfigLoader.MaxHpStatType);
        }

        public static int GetPlayerMaxUpgradeLevel(string statType)
        {
            return PermanentUpgradeConfigLoader.GetPlayerMaxLevel(statType, MaxPermanentUpgradeLevel);
        }

        public static int GetWeaponMaxUpgradeLevel(int weaponId)
        {
            return PermanentUpgradeConfigLoader.GetWeaponMaxLevel(
                weaponId,
                MaxPermanentUpgradeLevel);
        }

        public static int GetPlayerUpgradeCost(int currentLevel)
        {
            return GetPlayerUpgradeCost(PermanentUpgradeConfigLoader.MaxHpStatType, currentLevel);
        }

        public static int GetPlayerUpgradeCost(string statType, int currentLevel)
        {
            return PermanentUpgradeConfigLoader.GetPlayerUpgradeCost(
                statType,
                currentLevel,
                GetUpgradeCost(currentLevel));
        }

        public static int GetWeaponUpgradeCost(int weaponId, int currentLevel)
        {
            return PermanentUpgradeConfigLoader.GetWeaponUpgradeCost(
                weaponId,
                currentLevel,
                GetUpgradeCost(currentLevel));
        }

        public static void ApplyPermanentWeaponUpgrade(WeaponConfig weaponConfig)
        {
            if (weaponConfig == null)
            {
                return;
            }

            int level = GetWeaponUpgradeLevel(weaponConfig.weaponId);
            PermanentUpgradeRules.ApplyWeaponLevel(weaponConfig, level);
        }

        private static PlayerSaveData Normalize(PlayerSaveData saveData)
        {
            saveData ??= PlayerSaveData.CreateNew();
            saveData.selectedSecondWeaponId = NormalizeSecondWeaponId(saveData.selectedSecondWeaponId);
            saveData.weaponUpgrades ??= new List<WeaponUpgradeSaveData>();
            saveData.unlockedWeaponIds ??= new List<int>();
            saveData.bestSurvivalTime = Mathf.Max(0f, saveData.bestSurvivalTime);
            saveData.bestKillCount = Mathf.Max(0, saveData.bestKillCount);

            if (saveData.saveVersion < CurrentSaveVersion)
            {
                if (saveData.gold <= 0)
                {
                    saveData.gold = 10000;
                }

                int sharedPlayerLevel = Mathf.Max(
                    saveData.maxHpLevel,
                    saveData.moveSpeedLevel,
                    saveData.jumpHeightLevel,
                    saveData.skillCooldownLevel);
                saveData.maxHpLevel = sharedPlayerLevel;
                saveData.moveSpeedLevel = sharedPlayerLevel;
                saveData.jumpHeightLevel = sharedPlayerLevel;
                saveData.skillCooldownLevel = sharedPlayerLevel;

                saveData.saveVersion = CurrentSaveVersion;
            }

            saveData.maxHpLevel = Mathf.Clamp(
                saveData.maxHpLevel,
                0,
                GetPlayerMaxUpgradeLevel(PermanentUpgradeConfigLoader.MaxHpStatType));
            saveData.moveSpeedLevel = Mathf.Clamp(
                saveData.moveSpeedLevel,
                0,
                GetPlayerMaxUpgradeLevel(PermanentUpgradeConfigLoader.MoveSpeedStatType));
            saveData.jumpHeightLevel = Mathf.Clamp(
                saveData.jumpHeightLevel,
                0,
                GetPlayerMaxUpgradeLevel(PermanentUpgradeConfigLoader.JumpHeightStatType));
            saveData.skillCooldownLevel = Mathf.Clamp(
                saveData.skillCooldownLevel,
                0,
                GetPlayerMaxUpgradeLevel(PermanentUpgradeConfigLoader.SkillCooldownReductionStatType));
            for (int i = saveData.weaponUpgrades.Count - 1; i >= 0; i--)
            {
                WeaponUpgradeSaveData upgrade = saveData.weaponUpgrades[i];
                if (upgrade == null || upgrade.weaponId <= 0)
                {
                    saveData.weaponUpgrades.RemoveAt(i);
                    continue;
                }

                upgrade.level = Mathf.Clamp(
                    upgrade.level,
                    0,
                    GetWeaponMaxUpgradeLevel(upgrade.weaponId));
            }

            EnsureWeaponUnlocked(saveData, 1);
            EnsureWeaponUnlocked(saveData, 2);
            EnsureWeaponUnlocked(saveData, 3);
            return saveData;
        }

        private static int GetPlayerUpgradeLevel(PlayerSaveData saveData, string statType)
        {
            if (saveData == null)
            {
                return 0;
            }

            if (string.Equals(statType, PermanentUpgradeConfigLoader.MoveSpeedStatType, StringComparison.OrdinalIgnoreCase))
            {
                return saveData.moveSpeedLevel;
            }

            if (string.Equals(statType, PermanentUpgradeConfigLoader.JumpHeightStatType, StringComparison.OrdinalIgnoreCase))
            {
                return saveData.jumpHeightLevel;
            }

            if (string.Equals(statType, PermanentUpgradeConfigLoader.SkillCooldownReductionStatType, StringComparison.OrdinalIgnoreCase))
            {
                return saveData.skillCooldownLevel;
            }

            return saveData.maxHpLevel;
        }

        private static int GetPlayerSharedUpgradeLevel(PlayerSaveData saveData)
        {
            if (saveData == null)
            {
                return 0;
            }

            return Mathf.Max(
                saveData.maxHpLevel,
                saveData.moveSpeedLevel,
                saveData.jumpHeightLevel,
                saveData.skillCooldownLevel);
        }

        private static void SetPlayerUpgradeLevel(PlayerSaveData saveData, string statType, int level)
        {
            int safeLevel = Mathf.Clamp(level, 0, GetPlayerMaxUpgradeLevel(statType));
            if (string.Equals(statType, PermanentUpgradeConfigLoader.MoveSpeedStatType, StringComparison.OrdinalIgnoreCase))
            {
                saveData.moveSpeedLevel = safeLevel;
                return;
            }

            if (string.Equals(statType, PermanentUpgradeConfigLoader.JumpHeightStatType, StringComparison.OrdinalIgnoreCase))
            {
                saveData.jumpHeightLevel = safeLevel;
                return;
            }

            if (string.Equals(statType, PermanentUpgradeConfigLoader.SkillCooldownReductionStatType, StringComparison.OrdinalIgnoreCase))
            {
                saveData.skillCooldownLevel = safeLevel;
                return;
            }

            saveData.maxHpLevel = safeLevel;
        }

        private static void SetWeaponUpgradeLevel(PlayerSaveData saveData, int weaponId, int level)
        {
            saveData.weaponUpgrades ??= new List<WeaponUpgradeSaveData>();
            for (int i = 0; i < saveData.weaponUpgrades.Count; i++)
            {
                WeaponUpgradeSaveData upgrade = saveData.weaponUpgrades[i];
                if (upgrade != null && upgrade.weaponId == weaponId)
                {
                    upgrade.level = Mathf.Clamp(
                        level,
                        0,
                        GetWeaponMaxUpgradeLevel(weaponId));
                    return;
                }
            }

            saveData.weaponUpgrades.Add(new WeaponUpgradeSaveData(
                weaponId,
                Mathf.Clamp(level, 0, GetWeaponMaxUpgradeLevel(weaponId))));
        }

        private static void EnsureWeaponUnlocked(PlayerSaveData saveData, int weaponId)
        {
            if (!saveData.unlockedWeaponIds.Contains(weaponId))
            {
                saveData.unlockedWeaponIds.Add(weaponId);
            }
        }

        private static bool TryResolveSavePath(string fileName, out string path)
        {
            path = null;
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return false;
            }

            string safeFileName;
            try
            {
                safeFileName = Path.GetFileName(fileName);
            }
            catch
            {
                return false;
            }
            if (!string.Equals(safeFileName, fileName, StringComparison.Ordinal))
            {
                return false;
            }

            if (string.Equals(safeFileName, LegacySaveFileName, StringComparison.OrdinalIgnoreCase))
            {
                path = GetLegacySavePath();
                return true;
            }

            if (!safeFileName.StartsWith(SaveFilePrefix, StringComparison.Ordinal)
                || !safeFileName.EndsWith(SaveFileExtension, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            path = Path.Combine(GetSaveDirectoryPath(), safeFileName);
            return true;
        }

        private static bool TryReadSaveData(string path, out PlayerSaveData saveData)
        {
            saveData = null;
            try
            {
                string json = File.ReadAllText(path);
                PlayerSaveData parsedData = JsonUtility.FromJson<PlayerSaveData>(json);
                if (parsedData == null)
                {
                    Debug.LogWarning($"[PlayerProgress] 存档内容为空 {path}");
                    return false;
                }

                saveData = Normalize(parsedData);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[PlayerProgress] 存档读取失败 {path} {exception.Message}");
                return false;
            }
        }

        private static bool TryCreateSummary(
            string path,
            bool isLegacy,
            out PlayerSaveSlotSummary summary)
        {
            summary = null;
            if (!TryReadSaveData(path, out PlayerSaveData saveData))
            {
                return false;
            }

            DateTime savedAt = ResolveSavedAt(path);
            summary = CreateSummary(path, saveData, isLegacy, savedAt);
            return true;
        }

        private static PlayerSaveSlotSummary CreateSummary(
            string path,
            PlayerSaveData saveData,
            bool isLegacy,
            DateTime savedAt)
        {
            PlayerSaveData safeSaveData = saveData ?? PlayerSaveData.CreateNew();
            return new PlayerSaveSlotSummary(
                Path.GetFileName(path),
                path,
                savedAt,
                isLegacy,
                safeSaveData.gold,
                safeSaveData.bestSurvivalTime,
                safeSaveData.bestKillCount,
                GetPlayerSharedUpgradeLevel(safeSaveData),
                NormalizeSecondWeaponId(safeSaveData.selectedSecondWeaponId));
        }

        private static DateTime ResolveSavedAt(string path)
        {
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(path);
            if (fileNameWithoutExtension.StartsWith(SaveFilePrefix, StringComparison.Ordinal))
            {
                string timestamp = fileNameWithoutExtension.Substring(SaveFilePrefix.Length);
                if (DateTime.TryParseExact(
                        timestamp,
                        SaveTimeFormat,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out DateTime parsedTime))
                {
                    return parsedTime;
                }
            }

            try
            {
                return File.GetLastWriteTime(path);
            }
            catch
            {
                return DateTime.MinValue;
            }
        }

        private static string CreateUniqueTimestampSavePath(string saveDirectory, string previousPath)
        {
            DateTime timestamp = DateTime.Now;
            for (int i = 0; i < 86400; i++)
            {
                string fileName = SaveFilePrefix
                                  + timestamp.AddSeconds(i).ToString(SaveTimeFormat, CultureInfo.InvariantCulture)
                                  + SaveFileExtension;
                string candidatePath = Path.Combine(saveDirectory, fileName);
                if (!File.Exists(candidatePath) && !PathsEqual(candidatePath, previousPath))
                {
                    return candidatePath;
                }
            }

            throw new IOException("无法创建唯一的时间戳存档文件名");
        }

        private static bool PathsEqual(string left, string right)
        {
            if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right))
            {
                return false;
            }

            return string.Equals(
                Path.GetFullPath(left),
                Path.GetFullPath(right),
                StringComparison.OrdinalIgnoreCase);
        }

        private static string GetLegacySavePath()
        {
            return Path.Combine(Application.persistentDataPath, LegacySaveFileName);
        }

        public static string GetSaveDirectoryPath()
        {
            return Path.Combine(Application.persistentDataPath, SaveDirectoryName);
        }
    }

    public static class PermanentUpgradeRules
    {
        public static void ApplyWeaponLevel(WeaponConfig config, int level)
        {
            if (config == null || level <= 0)
            {
                return;
            }

            int safeLevel = Mathf.Clamp(
                level,
                0,
                PlayerProgressSaveService.GetWeaponMaxUpgradeLevel(config.weaponId));
            if (PermanentUpgradeConfigLoader.TryGetWeaponConfig(
                    config.weaponId,
                    safeLevel,
                    out WeaponPermanentUpgradeConfig upgradeConfig))
            {
                ApplyWeaponConfig(config, upgradeConfig);
                return;
            }

            config.damage *= 1f + safeLevel * 0.08f;
            config.magazineSize += GetMagazineIncrease(config.weaponId, safeLevel);
            config.maxReserveAmmo += GetReserveAmmoIncrease(config.weaponId, safeLevel);

            float fireIntervalMultiplier = Mathf.Max(0.78f, 1f - safeLevel * 0.015f);
            float reloadMultiplier = Mathf.Max(0.72f, 1f - safeLevel * 0.025f);
            float controlMultiplier = Mathf.Max(0.72f, 1f - safeLevel * 0.025f);

            config.fireInterval *= fireIntervalMultiplier;
            config.reloadTime *= reloadMultiplier;
            config.reloadStartTime *= reloadMultiplier;
            config.reloadSingleRoundTime *= reloadMultiplier;
            config.reloadEndTime *= reloadMultiplier;
            config.recoilPitch *= controlMultiplier;
            config.recoilYaw *= controlMultiplier;
            config.viewRecoilPosition *= controlMultiplier;
            config.viewRecoilRotation *= controlMultiplier;
            config.spreadAngle *= controlMultiplier;

            config.reloadAmmoPerStep = config.reloadMode == WeaponReloadMode.SingleRound
                ? 1
                : config.magazineSize;
        }

        private static void ApplyWeaponConfig(
            WeaponConfig config,
            WeaponPermanentUpgradeConfig upgradeConfig)
        {
            config.damage *= Mathf.Max(0f, upgradeConfig.damageMultiplier);
            config.magazineSize += Mathf.Max(0, upgradeConfig.magazineAdd);
            config.maxReserveAmmo += Mathf.Max(0, upgradeConfig.reserveAmmoAdd);
            config.fireInterval *= Mathf.Max(0.01f, upgradeConfig.fireIntervalMultiplier);

            float reloadMultiplier = Mathf.Max(0.01f, upgradeConfig.reloadTimeMultiplier);
            config.reloadTime *= reloadMultiplier;
            config.reloadStartTime *= reloadMultiplier;
            config.reloadSingleRoundTime *= reloadMultiplier;
            config.reloadEndTime *= reloadMultiplier;

            float recoilMultiplier = Mathf.Max(0.01f, upgradeConfig.recoilMultiplier);
            config.recoilPitch *= recoilMultiplier;
            config.recoilYaw *= recoilMultiplier;
            config.viewRecoilPosition *= recoilMultiplier;
            config.viewRecoilRotation *= recoilMultiplier;
            config.spreadAngle *= Mathf.Max(0.01f, upgradeConfig.spreadMultiplier);
            config.reloadAmmoPerStep = config.reloadMode == WeaponReloadMode.SingleRound
                ? 1
                : config.magazineSize;
        }

        private static int GetMagazineIncrease(int weaponId, int level)
        {
            switch (weaponId)
            {
                case 2:
                    return level * 2;
                case 3:
                    return Mathf.CeilToInt(level * 0.5f);
                default:
                    return Mathf.CeilToInt(level * 0.75f);
            }
        }

        private static int GetReserveAmmoIncrease(int weaponId, int level)
        {
            switch (weaponId)
            {
                case 2:
                    return level * 10;
                case 3:
                    return level * 3;
                default:
                    return level * 4;
            }
        }
    }
}
