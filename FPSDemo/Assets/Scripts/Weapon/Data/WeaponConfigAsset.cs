using UnityEngine;

namespace Weapon.Data
{
    /// <summary>
    /// 单把武器的数据资源
    /// 伤害 射速 弹夹 开镜 FOV 都在这里按武器配置
    /// </summary>
    [CreateAssetMenu(menuName = "FPSDemo/Weapon/Weapon Config", fileName = "WeaponConfig")]
    public class WeaponConfigAsset : ScriptableObject
    {
        [SerializeField] private WeaponConfig config = WeaponConfig.CreateDefaultPistol();

        public WeaponConfig Config => config;

        public WeaponConfig CreateRuntimeConfig()
        {
            // 运行时使用副本 避免游戏中修改到资源本体
            WeaponConfig runtimeConfig = config != null ? config.Clone() : WeaponConfig.CreateDefaultPistol();
            runtimeConfig.ApplyMissingDefaults();
            return runtimeConfig;
        }

        private void OnValidate()
        {
            // Inspector 中保持数据可用
            config ??= WeaponConfig.CreateDefaultPistol();
            config.ApplyMissingDefaults();
        }
    }
}
