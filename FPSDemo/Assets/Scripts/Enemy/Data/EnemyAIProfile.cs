using System;
using UnityEngine;

namespace Enemy.Data
{
    [Serializable]
    public class EnemyAIProfile
    {
        public string aiProfileKey = "NormalZombieAI";

        public float nearDistance = 8f;
        public float midDistance = 18f;
        public float farDistance = 35f;

        public float nearThinkInterval = 0.12f;
        public float midThinkInterval = 0.35f;
        public float farThinkInterval = 1f;
        public float sleepThinkInterval = 3f;

        public bool useRootMotionNear = true;
        public bool useRootMotionMid;
        public bool enableAgentNear = true;
        public bool enableAgentMid = true;
        public float animatorLodDistance = 24f;

        public int attackPriority = 1;
        public float surroundRadius = 1.8f;

        public static EnemyAIProfile CreateNormalZombie()
        {
            return new EnemyAIProfile
            {
                aiProfileKey = "NormalZombieAI",
                nearDistance = 8f,
                midDistance = 18f,
                farDistance = 35f,
                nearThinkInterval = 0.12f,
                midThinkInterval = 0.35f,
                farThinkInterval = 1f,
                sleepThinkInterval = 3f,
                useRootMotionNear = true,
                useRootMotionMid = false,
                enableAgentNear = true,
                enableAgentMid = true,
                animatorLodDistance = 24f,
                attackPriority = 1,
                surroundRadius = 1.8f
            };
        }

        public static EnemyAIProfile CreateFastZombie()
        {
            EnemyAIProfile profile = CreateNormalZombie();
            profile.aiProfileKey = "FastZombieAI";
            profile.nearDistance = 10f;
            profile.midDistance = 22f;
            profile.farDistance = 40f;
            profile.nearThinkInterval = 0.08f;
            profile.midThinkInterval = 0.25f;
            profile.farThinkInterval = 0.8f;
            profile.attackPriority = 2;
            profile.surroundRadius = 1.5f;
            return profile;
        }

        public static EnemyAIProfile CreateEliteZombie()
        {
            EnemyAIProfile profile = CreateNormalZombie();
            profile.aiProfileKey = "EliteZombieAI";
            profile.nearDistance = 9f;
            profile.midDistance = 20f;
            profile.farDistance = 36f;
            profile.nearThinkInterval = 0.1f;
            profile.midThinkInterval = 0.3f;
            profile.farThinkInterval = 0.9f;
            profile.attackPriority = 4;
            profile.surroundRadius = 2.2f;
            return profile;
        }

        public EnemyAIProfile Clone()
        {
            return (EnemyAIProfile)MemberwiseClone();
        }

        public void ApplyMissingDefaults()
        {
            if (string.IsNullOrEmpty(aiProfileKey))
            {
                aiProfileKey = "NormalZombieAI";
            }

            nearDistance = Mathf.Max(0.1f, nearDistance);
            midDistance = Mathf.Max(nearDistance, midDistance);
            farDistance = Mathf.Max(midDistance, farDistance);
            nearThinkInterval = Mathf.Max(0.02f, nearThinkInterval);
            midThinkInterval = Mathf.Max(nearThinkInterval, midThinkInterval);
            farThinkInterval = Mathf.Max(midThinkInterval, farThinkInterval);
            sleepThinkInterval = Mathf.Max(farThinkInterval, sleepThinkInterval);
            animatorLodDistance = Mathf.Max(nearDistance, animatorLodDistance);
            attackPriority = Mathf.Max(0, attackPriority);
            surroundRadius = Mathf.Max(0.1f, surroundRadius);
        }
    }
}
