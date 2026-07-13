using System;
using UnityEngine;

namespace Pickup.Data
{
    /// <summary>
    /// 局内道具配置
    /// 只保存数值和资源 key
    /// </summary>
    [Serializable]
    public class PickupItemConfig
    {
        // 道具唯一编号
        public int id;
        // 道具名称
        public string itemName;
        // 描述模板
        public string descriptionTemplate;
        // 道具类型
        public PickupItemType itemType;
        // 资源包名
        public string assetBundleName;
        // ABRes/Prop 下 prefab 名
        public string assetName;
        // 掉落权重
        public float weight;
        // 解锁波次
        public int unlockWave;
        // 场上存活时间
        public float lifeTime;
        // 拾取半径
        public float pickupRadius;
        // 治疗值
        public float healValue;
        // 子弹数量
        public int ammoAmount;
        // 炸弹数量
        public int grenadeAmount;
        // 狂暴持续时间
        public float berserkDuration;
        // 提示颜色 key
        public string tipColorKey;
        // 后处理表现 key
        public string postProcessKey;

        public PickupItemConfig Clone()
        {
            return (PickupItemConfig)MemberwiseClone();
        }

        public void ApplyMissingDefaults()
        {
            id = Mathf.Max(1, id);
            if (string.IsNullOrEmpty(itemName))
            {
                itemName = $"Pickup {id}";
            }

            if (string.IsNullOrEmpty(descriptionTemplate))
            {
                descriptionTemplate = itemName;
            }

            if (string.IsNullOrEmpty(assetBundleName))
            {
                assetBundleName = "prop_runtime";
            }

            if (string.IsNullOrEmpty(assetName))
            {
                assetName = itemName;
            }

            weight = Mathf.Max(0f, weight);
            unlockWave = Mathf.Max(1, unlockWave);
            lifeTime = Mathf.Max(0f, lifeTime);
            pickupRadius = Mathf.Max(0.1f, pickupRadius);
            healValue = Mathf.Max(0f, healValue);
            ammoAmount = Mathf.Max(0, ammoAmount);
            grenadeAmount = Mathf.Max(0, grenadeAmount);
            berserkDuration = Mathf.Max(0f, berserkDuration);

            if (string.IsNullOrEmpty(tipColorKey))
            {
                tipColorKey = itemType.ToString();
            }
        }
    }
}
