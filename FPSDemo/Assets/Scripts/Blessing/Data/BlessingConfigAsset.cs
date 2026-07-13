using UnityEngine;

namespace Blessing.Data
{
    /// <summary>
    /// 单条祝福配置资源
    /// 当前用于 Resources 兜底 后续可替换为 Luban 表
    /// </summary>
    [CreateAssetMenu(menuName = "FPSDemo/Blessing/Blessing Config", fileName = "BlessingConfig")]
    public class BlessingConfigAsset : ScriptableObject
    {
        // 祝福配置
        public BlessingConfig config;

        public BlessingConfig Config => config;

        public BlessingConfig CreateRuntimeConfig()
        {
            BlessingConfig runtimeConfig = config != null ? config.Clone() : new BlessingConfig();
            runtimeConfig.ApplyMissingDefaults();
            return runtimeConfig;
        }

        private void OnValidate()
        {
            config ??= new BlessingConfig();
            config.ApplyMissingDefaults();
        }
    }
}
