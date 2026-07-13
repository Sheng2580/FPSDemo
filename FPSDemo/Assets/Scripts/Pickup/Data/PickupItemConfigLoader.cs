using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Pickup.Data
{
    /// <summary>
    /// Luban 局内道具 Json 适配器
    /// </summary>
    public static class PickupItemConfigLoader
    {
        private const string PickupItemJsonFileName = "tbpickup_item";
        private const string ResourcesJsonFolder = "PickupJson";

        public static bool TryLoadConfigs(out List<PickupItemConfig> configs)
        {
            configs = new List<PickupItemConfig>();
            if (!TryReadJson(out string json))
            {
                return false;
            }

            try
            {
                PickupItemRowList list = JsonUtility.FromJson<PickupItemRowList>(WrapArrayJson(json));
                if (list?.rows == null)
                {
                    return false;
                }

                for (int i = 0; i < list.rows.Length; i++)
                {
                    PickupItemConfig config = ConvertRow(list.rows[i]);
                    if (config != null)
                    {
                        configs.Add(config);
                    }
                }

                return configs.Count > 0;
            }
            catch (Exception e)
            {
                Debug.LogError($"[PickupItemConfigLoader] 道具 Json 解析失败 {e.Message}");
                configs.Clear();
                return false;
            }
        }

        public static bool TryGetConfig(int itemId, out PickupItemConfig config)
        {
            config = null;
            if (!TryLoadConfigs(out List<PickupItemConfig> configs))
            {
                return false;
            }

            for (int i = 0; i < configs.Count; i++)
            {
                if (configs[i].id == itemId)
                {
                    config = configs[i];
                    return true;
                }
            }

            return false;
        }

        private static PickupItemConfig ConvertRow(PickupItemRow row)
        {
            if (row == null || row.id <= 0)
            {
                return null;
            }

            PickupItemConfig config = new PickupItemConfig
            {
                id = row.id,
                itemName = row.itemName,
                descriptionTemplate = row.descriptionTemplate,
                itemType = ParseEnum(row.itemType, PickupItemType.Heal),
                assetBundleName = row.assetBundleName,
                assetName = row.assetName,
                weight = row.weight,
                unlockWave = row.unlockWave,
                lifeTime = row.lifeTime,
                pickupRadius = row.pickupRadius,
                healValue = row.healValue,
                ammoAmount = row.ammoAmount,
                grenadeAmount = row.grenadeAmount,
                berserkDuration = row.berserkDuration,
                tipColorKey = row.tipColorKey,
                postProcessKey = row.postProcessKey
            };
            config.ApplyMissingDefaults();
            return config;
        }

        private static bool TryReadJson(out string json)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            string generatedJsonPath = Path.Combine(projectRoot, "MiniTemplate", "GeneratedJson");
            return JsonMgr.Instance.TryLoadJsonText(
                PickupItemJsonFileName,
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
        private sealed class PickupItemRowList
        {
            public PickupItemRow[] rows;
        }

        [Serializable]
        private sealed class PickupItemRow
        {
            public int id;
            public string itemName;
            public string descriptionTemplate;
            public string itemType;
            public string assetBundleName;
            public string assetName;
            public float weight;
            public int unlockWave;
            public float lifeTime;
            public float pickupRadius;
            public float healValue;
            public int ammoAmount;
            public int grenadeAmount;
            public float berserkDuration;
            public string tipColorKey;
            public string postProcessKey;
        }
    }
}
