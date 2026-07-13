using System;

namespace Pickup.Data
{
    /// <summary>
    /// 局内道具运行时状态
    /// 不写回配置
    /// </summary>
    [Serializable]
    public class PickupItemRuntimeData
    {
        // 道具编号
        public int itemId;
        // 生成时间
        public float spawnTime;
        // 剩余存活时间
        public float remainingLifeTime;
        // 是否已拾取
        public bool collected;

        public void Init(PickupItemConfig config, float currentTime)
        {
            itemId = config != null ? config.id : 0;
            spawnTime = currentTime;
            remainingLifeTime = config != null ? config.lifeTime : 0f;
            collected = false;
        }
    }
}
