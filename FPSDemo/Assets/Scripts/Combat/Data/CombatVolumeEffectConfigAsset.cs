using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 战斗后处理效果配置资源
    /// 当前用于 Resources 加载 后续可替换为表格数据
    /// </summary>
    [CreateAssetMenu(menuName = "FPSDemo/Combat/Combat Volume Effect Config", fileName = "CombatVolumeEffectConfig")]
    public class CombatVolumeEffectConfigAsset : ScriptableObject
    {
        // 后处理效果配置
        public CombatVolumeEffectConfig config = CombatVolumeEffectConfig.CreateDefaultPlayerDamage();

        public CombatVolumeEffectConfig Config => config;

        public CombatVolumeEffectConfig CreateRuntimeConfig()
        {
            CombatVolumeEffectConfig runtimeConfig = config != null
                ? config.Clone()
                : CombatVolumeEffectConfig.CreateDefaultPlayerDamage();
            runtimeConfig.ApplyMissingDefaults();
            return runtimeConfig;
        }

        private void OnValidate()
        {
            config ??= CombatVolumeEffectConfig.CreateDefaultPlayerDamage();
            config.ApplyMissingDefaults();
        }
    }
}
