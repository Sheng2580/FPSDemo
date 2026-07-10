using UnityEngine;

namespace Enemy.Data
{
    [CreateAssetMenu(menuName = "FPSDemo/Enemy/Enemy Wave Config", fileName = "EnemyWaveConfig")]
    public class EnemyWaveConfigAsset : ScriptableObject
    {
        [SerializeField] private EnemyWaveConfig config = new EnemyWaveConfig();

        public EnemyWaveConfig Config => config;

        private void OnValidate()
        {
            config ??= new EnemyWaveConfig();
            config.ApplyMissingDefaults();
        }
    }
}
