using UnityEngine;

namespace Enemy.Data
{
    [CreateAssetMenu(menuName = "FPSDemo/Enemy/Enemy Config", fileName = "EnemyConfig")]
    public class EnemyConfigAsset : ScriptableObject
    {
        [SerializeField] private EnemyConfig config = EnemyConfig.CreateNormalZombie();

        public EnemyConfig Config => config;

        public EnemyConfig CreateRuntimeConfig()
        {
            EnemyConfig runtimeConfig = config != null ? config.Clone() : EnemyConfig.CreateNormalZombie();
            runtimeConfig.ApplyMissingDefaults();
            return runtimeConfig;
        }

        private void OnValidate()
        {
            config ??= EnemyConfig.CreateNormalZombie();
            config.ApplyMissingDefaults();
        }
    }
}
