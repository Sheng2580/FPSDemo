using UnityEngine;

namespace PlayerData
{
    /// <summary>
    /// 玩家技能配置资源
    /// 当前用于 Resources 加载 后续可替换为表格数据
    /// </summary>
    [CreateAssetMenu(menuName = "FPSDemo/Player/Skill Config", fileName = "PlayerSkillConfig")]
    public class PlayerSkillConfigAsset : ScriptableObject
    {
        // 技能配置
        public PlayerSkillConfig config = PlayerSkillConfig.CreateDefaultDodge();

        public PlayerSkillConfig Config => config;

        public PlayerSkillConfig CreateRuntimeConfig()
        {
            PlayerSkillConfig runtimeConfig = config != null ? config.Clone() : PlayerSkillConfig.CreateDefaultDodge();
            runtimeConfig.ApplyMissingDefaults();
            return runtimeConfig;
        }

        private void OnValidate()
        {
            config ??= PlayerSkillConfig.CreateDefaultDodge();
            config.ApplyMissingDefaults();
        }
    }
}
