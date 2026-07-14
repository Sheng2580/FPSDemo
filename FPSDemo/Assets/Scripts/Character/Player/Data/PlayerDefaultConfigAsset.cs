using UnityEngine;

namespace PlayerData
{
    /// <summary>
    /// 玩家默认配置资源
    /// Luban Json 读取失败时作为兜底
    /// </summary>
    [CreateAssetMenu(menuName = "FPSDemo/Player/Default Config", fileName = "PlayerDefaultConfig")]
    public class PlayerDefaultConfigAsset : ScriptableObject
    {
        private const string DefaultResourcesPath = "PlayerConfigs/PlayerDefaultConfig";

        private static PlayerDefaultConfigAsset _cachedDefaultAsset;
        private static PlayerBaseConfig _cachedRuntimeConfig;

        public PlayerBaseConfig config = PlayerBaseConfig.CreateDefault();

        /// <summary>
        /// 获取玩家基础配置
        /// Hall 和 Combat 共用同一份资源
        /// </summary>
        public static PlayerBaseConfig LoadRuntimeConfig()
        {
            if (_cachedRuntimeConfig != null)
            {
                return _cachedRuntimeConfig;
            }

            if (PlayerBaseConfigJsonLoader.TryLoadConfig(out PlayerBaseConfig jsonConfig))
            {
                _cachedRuntimeConfig = jsonConfig;
                return _cachedRuntimeConfig;
            }

            if (_cachedDefaultAsset == null)
            {
                _cachedDefaultAsset = Resources.Load<PlayerDefaultConfigAsset>(DefaultResourcesPath);
            }

            _cachedRuntimeConfig = _cachedDefaultAsset != null && _cachedDefaultAsset.config != null
                ? _cachedDefaultAsset.config
                : PlayerBaseConfig.CreateDefault();
            return _cachedRuntimeConfig;
        }
    }
}
