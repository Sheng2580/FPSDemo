using UnityEngine;

namespace PlayerData
{
    /// <summary>
    /// 玩家局内能量配置资源
    /// 当前用于 Resources 加载 后续可替换为表格数据
    /// </summary>
    [CreateAssetMenu(menuName = "FPSDemo/Player/Energy Config", fileName = "PlayerEnergyConfig")]
    public class PlayerEnergyConfigAsset : ScriptableObject
    {
        // 能量配置
        public PlayerEnergyConfig config = PlayerEnergyConfig.CreateDefault();

        public PlayerEnergyConfig Config => config;

        public PlayerEnergyConfig CreateRuntimeConfig()
        {
            PlayerEnergyConfig runtimeConfig = config != null ? config.Clone() : PlayerEnergyConfig.CreateDefault();
            runtimeConfig.ApplyMissingDefaults();
            return runtimeConfig;
        }

        private void OnValidate()
        {
            config ??= PlayerEnergyConfig.CreateDefault();
            config.ApplyMissingDefaults();
        }
    }
}
