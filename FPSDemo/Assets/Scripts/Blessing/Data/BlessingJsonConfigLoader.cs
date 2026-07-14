using System;
using System.Collections.Generic;
using System.IO;
using PlayerData;
using UnityEngine;

namespace Blessing.Data
{
    /// <summary>
    /// Luban 祝福 Json 适配器
    /// </summary>
    public static class BlessingJsonConfigLoader
    {
        private const string BlessingJsonFileName = "tbblessing";
        private const string TierProbabilityJsonFileName = "tbblessing_tier_probability";
        private const string ResourcesJsonFolder = "BlessingJson";

        public static bool TryLoadBlessingConfigs(out List<BlessingConfig> configs)
        {
            configs = new List<BlessingConfig>();
            if (!TryReadJson(BlessingJsonFileName, out string json))
            {
                return false;
            }

            try
            {
                BlessingRowList list = JsonUtility.FromJson<BlessingRowList>(WrapArrayJson(json));
                if (list?.rows == null)
                {
                    return false;
                }

                for (int i = 0; i < list.rows.Length; i++)
                {
                    BlessingConfig config = ConvertBlessingRow(list.rows[i]);
                    if (config != null)
                    {
                        configs.Add(config);
                    }
                }

                return configs.Count > 0;
            }
            catch (Exception e)
            {
                Debug.LogError($"[BlessingJsonConfigLoader] 祝福 Json 解析失败 {e.Message}");
                configs.Clear();
                return false;
            }
        }

        public static BlessingTierProbabilityConfigAsset LoadTierProbabilityConfig()
        {
            if (!TryReadJson(TierProbabilityJsonFileName, out string json))
            {
                return null;
            }

            try
            {
                TierProbabilityRowList list = JsonUtility.FromJson<TierProbabilityRowList>(WrapArrayJson(json));
                if (list?.rows == null || list.rows.Length <= 0)
                {
                    return null;
                }

                BlessingTierProbabilityConfigAsset asset = ScriptableObject.CreateInstance<BlessingTierProbabilityConfigAsset>();
                asset.tiers = new BlessingTierProbabilityConfig[list.rows.Length];
                for (int i = 0; i < list.rows.Length; i++)
                {
                    asset.tiers[i] = ConvertTierProbabilityRow(list.rows[i]);
                }

                return asset;
            }
            catch (Exception e)
            {
                Debug.LogError($"[BlessingJsonConfigLoader] 祝福等级概率 Json 解析失败 {e.Message}");
                return null;
            }
        }

        private static BlessingConfig ConvertBlessingRow(BlessingRow row)
        {
            if (row == null || row.id <= 0)
            {
                return null;
            }

            BlessingTargetType targetType = ParseEnum(row.targetType, BlessingTargetType.Player);
            BlessingTriggerType triggerType = ParseEnum(row.triggerType, BlessingTriggerType.None);

            BlessingConfig config = new BlessingConfig
            {
                blessingId = row.id,
                blessingName = row.blessingName,
                descriptionTemplate = row.descriptionTemplate,
                category = ParseEnum(row.category, BlessingCategory.PlayerStat),
                targetType = targetType,
                tier = ParseEnum(row.tier, BlessingTier.Normal),
                unlockEnergyLevel = row.unlockEnergyLevel,
                unlockWave = row.unlockWave,
                weight = row.weight,
                maxStack = row.maxStack,
                guaranteedFirstRoll = row.guaranteedFirstRoll,
                requiredWeaponId = row.requiredWeaponId,
                requiresSkillType = !string.IsNullOrWhiteSpace(row.requiredSkillType),
                requiredSkillType = ParseEnum(row.requiredSkillType, SkillType.Dodge),
                iconKey = row.iconKey,
                effects = new[]
                {
                    new BlessingEffectConfig
                    {
                        targetType = ParseEnum(row.effectTargetType, targetType),
                        statType = ParseEnum(row.effectStatType, BlessingStatType.MaxHp),
                        modifyType = ParseEnum(row.modifyType, BlessingModifyType.Add),
                        normalValue = row.normalValue,
                        plusValue = row.plusValue,
                        plusPlusValue = row.plusPlusValue
                    }
                },
                triggers = triggerType == BlessingTriggerType.None
                    ? Array.Empty<BlessingTriggerConfig>()
                    : new[]
                    {
                        new BlessingTriggerConfig
                        {
                            triggerType = triggerType,
                            chance = row.triggerChance,
                            cooldown = row.triggerCooldown,
                            effectKey = row.triggerEffectKey,
                            damageMultiplier = row.triggerDamageMultiplier,
                            radius = row.triggerRadius,
                            chainCount = row.triggerChainCount,
                            maxActiveCount = row.triggerMaxActiveCount
                        }
                    }
            };

            config.ApplyMissingDefaults();
            return config;
        }

        private static BlessingTierProbabilityConfig ConvertTierProbabilityRow(TierProbabilityRow row)
        {
            BlessingTierProbabilityConfig config = new BlessingTierProbabilityConfig
            {
                minEnergyLevel = row != null ? row.minEnergyLevel : 1,
                normalWeight = row != null ? row.normalWeight : 100f,
                plusWeight = row != null ? row.plusWeight : 0f,
                plusPlusWeight = row != null ? row.plusPlusWeight : 0f
            };
            config.ApplyMissingDefaults();
            return config;
        }

        private static bool TryReadJson(string fileNameWithoutExtension, out string json)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            string generatedJsonPath = Path.Combine(projectRoot, "MiniTemplate", "GeneratedJson");
            return JsonMgr.Instance.TryLoadJsonText(
                fileNameWithoutExtension,
                out json,
                string.Empty,
                ResourcesJsonFolder,
                generatedJsonPath);
        }

        private static string WrapArrayJson(string json)
        {
            return "{\"rows\":" + json.Trim() + "}";
        }

        private static T ParseEnum<T>(string value, T fallback) where T : struct
        {
            return !string.IsNullOrWhiteSpace(value) && Enum.TryParse(value, true, out T parsed)
                ? parsed
                : fallback;
        }

        [Serializable]
        private sealed class BlessingRowList
        {
            public BlessingRow[] rows;
        }

        [Serializable]
        private sealed class BlessingRow
        {
            public int id;
            public string blessingName;
            public string descriptionTemplate;
            public string category;
            public string targetType;
            public string tier;
            public int unlockEnergyLevel;
            public int unlockWave;
            public float weight;
            public int maxStack;
            public bool guaranteedFirstRoll;
            public int requiredWeaponId;
            public string requiredSkillType;
            public string iconKey;
            public string effectTargetType;
            public string effectStatType;
            public string modifyType;
            public float normalValue;
            public float plusValue;
            public float plusPlusValue;
            public string triggerType;
            public float triggerChance;
            public float triggerCooldown;
            public string triggerEffectKey;
            public float triggerDamageMultiplier;
            public float triggerRadius;
            public int triggerChainCount;
            public int triggerMaxActiveCount;
        }

        [Serializable]
        private sealed class TierProbabilityRowList
        {
            public TierProbabilityRow[] rows;
        }

        [Serializable]
        private sealed class TierProbabilityRow
        {
            public int minEnergyLevel;
            public float normalWeight;
            public float plusWeight;
            public float plusPlusWeight;
        }
    }
}
