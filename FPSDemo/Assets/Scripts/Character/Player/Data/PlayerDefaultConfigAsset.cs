using UnityEngine;

namespace PlayerData
{
    /// <summary>
    /// 玩家默认配置资源
    /// 当前用于 Inspector 配置 后续可替换为 Luban 数据来源
    /// </summary>
    [CreateAssetMenu(menuName = "FPSDemo/Player/Default Config", fileName = "PlayerDefaultConfig")]
    public class PlayerDefaultConfigAsset : ScriptableObject
    {
        public PlayerBaseConfig config = PlayerBaseConfig.CreateDefault();
    }
}
