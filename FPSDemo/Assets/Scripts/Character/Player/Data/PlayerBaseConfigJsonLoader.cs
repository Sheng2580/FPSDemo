using System;
using System.IO;
using UnityEngine;

namespace PlayerData
{
    /// <summary>
    /// Luban 玩家基础配置读取入口
    /// </summary>
    public static class PlayerBaseConfigJsonLoader
    {
        private const string JsonFileName = "tbplayer_base_config";
        private const string ResourcesJsonFolder = "PlayerJson";

        private static PlayerBaseConfig _cachedConfig;
        private static bool _loaded;

        public static bool TryLoadConfig(out PlayerBaseConfig config)
        {
            EnsureLoaded();
            config = _cachedConfig;
            return config != null;
        }

        private static void EnsureLoaded()
        {
            if (_loaded)
            {
                return;
            }

            _loaded = true;
            if (!TryReadJson(out string json))
            {
                Debug.LogWarning("[PlayerBaseConfig] 未读取到玩家基础配置表 使用 ScriptableObject 兜底");
                return;
            }

            try
            {
                PlayerBaseConfigRowList list = JsonUtility.FromJson<PlayerBaseConfigRowList>(WrapArrayJson(json));
                if (list?.rows == null || list.rows.Length <= 0)
                {
                    Debug.LogWarning("[PlayerBaseConfig] 玩家基础配置表没有数据 使用 ScriptableObject 兜底");
                    return;
                }

                PlayerBaseConfigRow row = list.rows[0];
                if (row == null || row.id <= 0 || row.maxHp <= 0)
                {
                    Debug.LogWarning("[PlayerBaseConfig] 玩家基础配置无效 使用 ScriptableObject 兜底");
                    return;
                }

                _cachedConfig = new PlayerBaseConfig
                {
                    maxHp = row.maxHp,
                    walkSpeed = row.walkSpeed,
                    runSpeed = row.runSpeed,
                    moveInputDeadZone = row.moveInputDeadZone,
                    moveAcceleration = row.moveAcceleration,
                    moveDeceleration = row.moveDeceleration,
                    jumpHeight = row.jumpHeight,
                    jumpBufferTime = row.jumpBufferTime,
                    coyoteTime = row.coyoteTime,
                    airMoveControl = row.airMoveControl,
                    jumpEndVerticalVelocity = row.jumpEndVerticalVelocity
                };
            }
            catch (Exception exception)
            {
                Debug.LogError($"[PlayerBaseConfig] 玩家基础配置表解析失败 {exception.Message}");
            }
        }

        private static bool TryReadJson(out string json)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            string generatedJsonPath = Path.Combine(projectRoot, "MiniTemplate", "GeneratedJson");
            return JsonMgr.Instance.TryLoadJsonText(
                JsonFileName,
                out json,
                string.Empty,
                ResourcesJsonFolder,
                generatedJsonPath);
        }

        private static string WrapArrayJson(string json)
        {
            return "{\"rows\":" + json.Trim() + "}";
        }

        [Serializable]
        private sealed class PlayerBaseConfigRowList
        {
            public PlayerBaseConfigRow[] rows;
        }

        [Serializable]
        private sealed class PlayerBaseConfigRow
        {
            public int id;
            public int maxHp;
            public float walkSpeed;
            public float runSpeed;
            public float moveInputDeadZone;
            public float moveAcceleration;
            public float moveDeceleration;
            public float jumpHeight;
            public float jumpBufferTime;
            public float coyoteTime;
            public float airMoveControl;
            public float jumpEndVerticalVelocity;
        }
    }
}
