using UnityEngine;

namespace Blessing.Data
{
    /// <summary>
    /// 祝福配置池资源
    /// 当前用于 Resources 兜底 后续可替换为 Luban 表集合
    /// </summary>
    [CreateAssetMenu(menuName = "FPSDemo/Blessing/Blessing Config Database", fileName = "BlessingConfigDatabase")]
    public class BlessingConfigDatabaseAsset : ScriptableObject
    {
        // 祝福配置列表
        public BlessingConfig[] configs;

        public BlessingConfig[] CreateRuntimeConfigs()
        {
            if (configs == null)
            {
                return new BlessingConfig[0];
            }

            BlessingConfig[] runtimeConfigs = new BlessingConfig[configs.Length];
            for (int i = 0; i < configs.Length; i++)
            {
                runtimeConfigs[i] = configs[i] != null ? configs[i].Clone() : null;
                runtimeConfigs[i]?.ApplyMissingDefaults();
            }

            return runtimeConfigs;
        }

        private void OnValidate()
        {
            if (configs == null)
            {
                return;
            }

            for (int i = 0; i < configs.Length; i++)
            {
                configs[i]?.ApplyMissingDefaults();
            }
        }
    }
}
