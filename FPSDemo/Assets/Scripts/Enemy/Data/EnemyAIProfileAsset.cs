using UnityEngine;

namespace Enemy.Data
{
    [CreateAssetMenu(menuName = "FPSDemo/Enemy/Enemy AI Profile", fileName = "EnemyAIProfile")]
    public class EnemyAIProfileAsset : ScriptableObject
    {
        [SerializeField] private EnemyAIProfile profile = EnemyAIProfile.CreateNormalZombie();

        public EnemyAIProfile Profile => profile;

        public EnemyAIProfile CreateRuntimeProfile()
        {
            EnemyAIProfile runtimeProfile = profile != null ? profile.Clone() : EnemyAIProfile.CreateNormalZombie();
            runtimeProfile.ApplyMissingDefaults();
            return runtimeProfile;
        }

        private void OnValidate()
        {
            profile ??= EnemyAIProfile.CreateNormalZombie();
            profile.ApplyMissingDefaults();
        }
    }
}
