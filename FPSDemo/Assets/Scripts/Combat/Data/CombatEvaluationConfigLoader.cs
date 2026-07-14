using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 战斗评价配置
    /// </summary>
    [Serializable]
    public class CombatEvaluationConfig
    {
        // 评价档位 ID
        public int id;
        // 最低存活秒数
        public float minSurvivalSeconds;
        // 最低击杀数量
        public int minKillCount;
        // 评价文字
        public string evaluationText;
    }

    /// <summary>
    /// Luban 战斗评价配置读取入口
    /// </summary>
    public static class CombatEvaluationConfigLoader
    {
        private const string JsonFileName = "tbcombat_evaluation";
        private const string ResourcesJsonFolder = "CombatJson";

        private static List<CombatEvaluationConfig> cachedConfigs;

        public static IReadOnlyList<CombatEvaluationConfig> Load()
        {
            EnsureLoaded();
            return cachedConfigs;
        }

        public static CombatEvaluationConfig Resolve(float survivalSeconds, int killCount)
        {
            EnsureLoaded();
            float safeSurvivalSeconds = float.IsNaN(survivalSeconds) || float.IsInfinity(survivalSeconds)
                ? 0f
                : Mathf.Max(0f, survivalSeconds);
            int safeKillCount = Mathf.Max(0, killCount);

            for (int i = 0; i < cachedConfigs.Count; i++)
            {
                CombatEvaluationConfig config = cachedConfigs[i];
                if (safeSurvivalSeconds >= config.minSurvivalSeconds &&
                    safeKillCount >= config.minKillCount)
                {
                    return config;
                }
            }

            return cachedConfigs[cachedConfigs.Count - 1];
        }

        public static string ResolveEvaluationText(float survivalSeconds, int killCount)
        {
            return Resolve(survivalSeconds, killCount).evaluationText;
        }

        private static void EnsureLoaded()
        {
            if (cachedConfigs != null)
            {
                return;
            }

            cachedConfigs = TryReadConfigs(out List<CombatEvaluationConfig> configs)
                ? configs
                : CreateFallbackConfigs();
            cachedConfigs.Sort((left, right) => right.id.CompareTo(left.id));
        }

        private static bool TryReadConfigs(out List<CombatEvaluationConfig> configs)
        {
            configs = null;
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            string generatedJsonPath = Path.Combine(projectRoot, "MiniTemplate", "GeneratedJson");
            if (!JsonMgr.Instance.TryLoadJsonText(
                    JsonFileName,
                    out string json,
                    string.Empty,
                    ResourcesJsonFolder,
                    generatedJsonPath))
            {
                Debug.LogWarning("[CombatEvaluation] 未读取到战斗评价表 使用默认数据");
                return false;
            }

            try
            {
                CombatEvaluationRowList list = JsonUtility.FromJson<CombatEvaluationRowList>(WrapArrayJson(json));
                if (list?.rows == null || list.rows.Length == 0)
                {
                    return false;
                }

                configs = new List<CombatEvaluationConfig>(list.rows.Length);
                for (int i = 0; i < list.rows.Length; i++)
                {
                    CombatEvaluationConfig row = list.rows[i];
                    if (row == null || row.id <= 0 || string.IsNullOrWhiteSpace(row.evaluationText))
                    {
                        continue;
                    }

                    row.minSurvivalSeconds = Mathf.Max(0f, row.minSurvivalSeconds);
                    row.minKillCount = Mathf.Max(0, row.minKillCount);
                    row.evaluationText = row.evaluationText.Trim();
                    configs.Add(row);
                }

                return configs.Count > 0;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[CombatEvaluation] 战斗评价表解析失败 {exception.Message}");
                configs = null;
                return false;
            }
        }

        private static List<CombatEvaluationConfig> CreateFallbackConfigs()
        {
            return new List<CombatEvaluationConfig>
            {
                new CombatEvaluationConfig
                {
                    id = 1,
                    minSurvivalSeconds = 0f,
                    minKillCount = 0,
                    evaluationText = "幸存者"
                }
            };
        }

        private static string WrapArrayJson(string json)
        {
            return "{\"rows\":" + json.Trim() + "}";
        }

        [Serializable]
        private sealed class CombatEvaluationRowList
        {
            public CombatEvaluationConfig[] rows;
        }
    }
}
