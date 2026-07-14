using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PlayerData
{
    /// <summary>
    /// 玩家永久升级配置
    /// </summary>
    [Serializable]
    public sealed class PlayerPermanentUpgradeConfig
    {
        public int id;
        public string statType;
        public int level;
        public string modifyType;
        public float increaseValue;
        public float value;
        public int costGold;
        public int maxLevel;
        public string displayName;
    }

    /// <summary>
    /// 武器永久升级配置
    /// </summary>
    [Serializable]
    public sealed class WeaponPermanentUpgradeConfig
    {
        public int id;
        public int weaponId;
        public string weaponName;
        public int level;
        public float damageMultiplier;
        public int magazineAdd;
        public int reserveAmmoAdd;
        public float fireIntervalMultiplier;
        public float reloadTimeMultiplier;
        public float recoilMultiplier;
        public float spreadMultiplier;
        public int costGold;
        public int maxLevel;
    }

    /// <summary>
    /// Luban 永久升级 Json 读取入口
    /// Hall 和 Combat 共用同一份升级配置
    /// </summary>
    public static class PermanentUpgradeConfigLoader
    {
        public const string MaxHpStatType = "MaxHp";
        public const string MoveSpeedStatType = "MoveSpeed";
        public const string JumpHeightStatType = "JumpHeight";
        public const string SkillCooldownReductionStatType = "SkillCooldownReduction";
        public const string EnergyGainEfficiencyStatType = "EnergyGainEfficiency";

        private const string PlayerJsonFileName = "tbplayer_permanent_upgrade";
        private const string WeaponJsonFileName = "tbweapon_permanent_upgrade";
        private const string PlayerResourcesJsonFolder = "PlayerJson";
        private const string WeaponResourcesJsonFolder = "UpgradeJson";

        private static readonly List<PlayerPermanentUpgradeConfig> PlayerConfigs =
            new List<PlayerPermanentUpgradeConfig>();
        private static readonly List<WeaponPermanentUpgradeConfig> WeaponConfigs =
            new List<WeaponPermanentUpgradeConfig>();

        private static bool _loaded;

        public static bool TryGetPlayerConfig(int level, out PlayerPermanentUpgradeConfig config)
        {
            return TryGetPlayerConfig(MaxHpStatType, level, out config);
        }

        public static bool TryGetPlayerConfig(
            string statType,
            int level,
            out PlayerPermanentUpgradeConfig config)
        {
            EnsureLoaded();
            config = null;
            for (int i = 0; i < PlayerConfigs.Count; i++)
            {
                PlayerPermanentUpgradeConfig candidate = PlayerConfigs[i];
                if (candidate != null
                    && candidate.level == level
                    && string.Equals(candidate.statType, statType, StringComparison.OrdinalIgnoreCase))
                {
                    config = candidate;
                    return true;
                }
            }

            return false;
        }

        public static bool TryGetWeaponConfig(
            int weaponId,
            int level,
            out WeaponPermanentUpgradeConfig config)
        {
            EnsureLoaded();
            config = null;
            for (int i = 0; i < WeaponConfigs.Count; i++)
            {
                WeaponPermanentUpgradeConfig candidate = WeaponConfigs[i];
                if (candidate != null && candidate.weaponId == weaponId && candidate.level == level)
                {
                    config = candidate;
                    return true;
                }
            }

            return false;
        }

        public static int GetPlayerMaxLevel(int fallback)
        {
            return GetPlayerMaxLevel(MaxHpStatType, fallback);
        }

        public static int GetPlayerMaxLevel(string statType, int fallback)
        {
            EnsureLoaded();
            int maxLevel = 0;
            for (int i = 0; i < PlayerConfigs.Count; i++)
            {
                PlayerPermanentUpgradeConfig config = PlayerConfigs[i];
                if (config != null
                    && string.Equals(config.statType, statType, StringComparison.OrdinalIgnoreCase))
                {
                    maxLevel = Mathf.Max(maxLevel, config.maxLevel, config.level);
                }
            }

            return maxLevel > 0 ? maxLevel : fallback;
        }

        public static int GetWeaponMaxLevel(int weaponId, int fallback)
        {
            EnsureLoaded();
            int maxLevel = 0;
            for (int i = 0; i < WeaponConfigs.Count; i++)
            {
                WeaponPermanentUpgradeConfig config = WeaponConfigs[i];
                if (config != null && config.weaponId == weaponId)
                {
                    maxLevel = Mathf.Max(maxLevel, config.maxLevel, config.level);
                }
            }

            return maxLevel > 0 ? maxLevel : fallback;
        }

        public static int GetPlayerUpgradeCost(int currentLevel, int fallback)
        {
            return GetPlayerUpgradeCost(MaxHpStatType, currentLevel, fallback);
        }

        public static int GetPlayerUpgradeCost(string statType, int currentLevel, int fallback)
        {
            return TryGetPlayerConfig(statType, currentLevel + 1, out PlayerPermanentUpgradeConfig config)
                ? Mathf.Max(0, config.costGold)
                : fallback;
        }

        public static int GetWeaponUpgradeCost(int weaponId, int currentLevel, int fallback)
        {
            return TryGetWeaponConfig(weaponId, currentLevel + 1, out WeaponPermanentUpgradeConfig config)
                ? Mathf.Max(0, config.costGold)
                : fallback;
        }

        public static int GetPlayerMaxHpBonus(int level, int fallback)
        {
            return Mathf.RoundToInt(GetPlayerUpgradeValue(MaxHpStatType, level, fallback));
        }

        public static float GetPlayerUpgradeValue(string statType, int level, float fallback)
        {
            if (level <= 0)
            {
                return 0f;
            }

            return TryGetPlayerConfig(statType, level, out PlayerPermanentUpgradeConfig config)
                ? config.value
                : fallback;
        }

        public static float GetPlayerUpgradeIncreaseValue(string statType, int targetLevel, float fallback)
        {
            return TryGetPlayerConfig(statType, targetLevel, out PlayerPermanentUpgradeConfig config)
                ? config.increaseValue
                : fallback;
        }

        public static float GetPlayerSkillCooldownReduction(int level, float fallback)
        {
            float value = GetPlayerUpgradeValue(SkillCooldownReductionStatType, level, fallback);
            return PlayerSkillConfigJsonLoader.Rules.ClampPermanentCooldownReduction(value);
        }

        public static float GetPlayerEnergyGainBonus(int level, float fallback)
        {
            float value = GetPlayerUpgradeValue(EnergyGainEfficiencyStatType, level, fallback);
            return Mathf.Clamp(value, 0f, 1f);
        }

        private static void EnsureLoaded()
        {
            if (_loaded)
            {
                return;
            }

            _loaded = true;
            LoadPlayerConfigs();
            LoadWeaponConfigs();
        }

        private static void LoadPlayerConfigs()
        {
            if (!TryReadJson(PlayerJsonFileName, PlayerResourcesJsonFolder, out string json))
            {
                Debug.LogWarning("[PermanentUpgrade] 未读取到玩家永久升级表 使用默认规则");
                return;
            }

            try
            {
                PlayerConfigList list = JsonUtility.FromJson<PlayerConfigList>(WrapArrayJson(json));
                if (list?.rows != null)
                {
                    PlayerConfigs.AddRange(list.rows);
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"[PermanentUpgrade] 玩家永久升级表解析失败 {exception.Message}");
            }
        }

        private static void LoadWeaponConfigs()
        {
            if (!TryReadJson(WeaponJsonFileName, WeaponResourcesJsonFolder, out string json))
            {
                Debug.LogWarning("[PermanentUpgrade] 未读取到武器永久升级表 使用默认规则");
                return;
            }

            try
            {
                WeaponConfigList list = JsonUtility.FromJson<WeaponConfigList>(WrapArrayJson(json));
                if (list?.rows != null)
                {
                    WeaponConfigs.AddRange(list.rows);
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"[PermanentUpgrade] 武器永久升级表解析失败 {exception.Message}");
            }
        }

        private static bool TryReadJson(string fileName, string resourcesFolder, out string json)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            string generatedJsonPath = Path.Combine(projectRoot, "MiniTemplate", "GeneratedJson");
            return JsonMgr.Instance.TryLoadJsonText(
                fileName,
                out json,
                string.Empty,
                resourcesFolder,
                generatedJsonPath);
        }

        private static string WrapArrayJson(string json)
        {
            return "{\"rows\":" + json.Trim() + "}";
        }

        [Serializable]
        private sealed class PlayerConfigList
        {
            public PlayerPermanentUpgradeConfig[] rows;
        }

        [Serializable]
        private sealed class WeaponConfigList
        {
            public WeaponPermanentUpgradeConfig[] rows;
        }
    }
}
