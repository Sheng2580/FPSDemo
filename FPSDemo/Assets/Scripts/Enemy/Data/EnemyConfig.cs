using System;

namespace Enemy.Data
{
    [Serializable]
    public class EnemyConfig
    {
        public int enemyId;
        public string enemyName;
        public string prefabKey;
        public string prefabResourceKey;
        public string behaviorTreeKey;
        public string aiProfileKey;
        public string bodyPartTemplateKey;
        public string dropPoolKey;

        public float maxHealth;
        public float moveSpeed;
        public float angularSpeed;
        public float acceleration;
        public float attackDamage;
        public float attackDistance;
        public float attackInterval;
        public float attackHitDelay;
        public float detectionRange;
        public int goldReward;
        public int blessingEnergyReward;
        public int experienceReward;

        public float hitStunDuration;
        public float hitReactionCooldown;
        public float hitKnockbackDistance;
        public float hitKnockbackDuration;

        public string idleStateName;
        public string walkStateName;
        public string runStateName;
        public string attackStateName;
        public string damageStateName;
        public string deathStateName;

        public float locomotionTransition;
        public float attackTransition;
        public float hitTransition;
        public float deathTransition;
        public float recoverTransition;

        public float headDamageMultiplier;
        public float bodyDamageMultiplier;
        public float armDamageMultiplier;
        public float legDamageMultiplier;

        public static EnemyConfig CreateNormalZombie()
        {
            return new EnemyConfig
            {
                enemyId = 1001,
                enemyName = "Zombie Skeleton",
                prefabKey = "ZombieSkeletonOneHanded",
                prefabResourceKey = "Enemy_ZombieSkeleton_LOD2",
                behaviorTreeKey = "ZombieMelee",
                aiProfileKey = "NormalZombieAI",
                bodyPartTemplateKey = "ZombieHumanoidDefault",
                dropPoolKey = "ZombieBasicDrop",
                maxHealth = 100f,
                moveSpeed = 2.2f,
                angularSpeed = 360f,
                acceleration = 12f,
                attackDamage = 10f,
                attackDistance = 1.4f,
                attackInterval = 1.2f,
                attackHitDelay = 0.35f,
                detectionRange = 30f,
                goldReward = 1,
                blessingEnergyReward = 1,
                experienceReward = 1,
                hitStunDuration = 0.09f,
                hitReactionCooldown = 0.2f,
                hitKnockbackDistance = 0.08f,
                hitKnockbackDuration = 0.06f,
                idleStateName = "ZombieSkeleton_OneHanded_Idle",
                walkStateName = "ZombieSkeleton_OneHanded_Walk",
                runStateName = "ZombieSkeleton_OneHanded_Run",
                attackStateName = "ZombieSkeleton_OneHanded_Attack_1",
                damageStateName = "ZombieSkeleton_OneHanded_Damage",
                deathStateName = "ZombieSkeleton_OneHanded_Death",
                locomotionTransition = 0.18f,
                attackTransition = 0.1f,
                hitTransition = 0.14f,
                deathTransition = 0.18f,
                recoverTransition = 0.18f,
                headDamageMultiplier = 2f,
                bodyDamageMultiplier = 1f,
                armDamageMultiplier = 0.75f,
                legDamageMultiplier = 0.6f
            };
        }

        public static EnemyConfig CreateFastZombie()
        {
            EnemyConfig config = CreateNormalZombie();
            config.enemyId = 1002;
            config.enemyName = "Fast Zombie";
            config.prefabKey = "ZombieNerdOneHanded";
            config.prefabResourceKey = "Enemy_ZombieSkeleton_LOD2";
            config.aiProfileKey = "FastZombieAI";
            config.maxHealth = 70f;
            config.moveSpeed = 3.2f;
            config.attackDamage = 8f;
            config.attackInterval = 0.95f;
            config.goldReward = 2;
            config.blessingEnergyReward = 1;
            config.hitStunDuration = 0.07f;
            config.hitReactionCooldown = 0.16f;
            config.hitKnockbackDistance = 0.06f;
            config.hitKnockbackDuration = 0.05f;
            return config;
        }

        public static EnemyConfig CreateEliteZombie()
        {
            EnemyConfig config = CreateNormalZombie();
            config.enemyId = 1003;
            config.enemyName = "Elite Zombie";
            config.prefabKey = "ZombieBruteOneHanded";
            config.prefabResourceKey = "Enemy_ZombieSkeleton_LOD2";
            config.aiProfileKey = "EliteZombieAI";
            config.dropPoolKey = "ZombieEliteDrop";
            config.maxHealth = 280f;
            config.moveSpeed = 1.7f;
            config.attackDamage = 22f;
            config.attackDistance = 1.8f;
            config.attackInterval = 1.6f;
            config.goldReward = 8;
            config.blessingEnergyReward = 5;
            config.experienceReward = 4;
            config.hitStunDuration = 0.12f;
            config.hitReactionCooldown = 0.28f;
            config.hitKnockbackDistance = 0.12f;
            config.hitKnockbackDuration = 0.08f;
            config.attackTransition = 0.12f;
            config.hitTransition = 0.16f;
            config.deathTransition = 0.22f;
            config.headDamageMultiplier = 1.6f;
            return config;
        }

        public EnemyConfig Clone()
        {
            return (EnemyConfig)MemberwiseClone();
        }

        public void ApplyMissingDefaults()
        {
            if (enemyId <= 0)
            {
                enemyId = 1001;
            }

            if (string.IsNullOrEmpty(enemyName))
            {
                enemyName = "Zombie";
            }

            if (string.IsNullOrEmpty(prefabKey))
            {
                prefabKey = "ZombieSkeletonOneHanded";
            }

            if (string.IsNullOrEmpty(prefabResourceKey))
            {
                prefabResourceKey = "Enemy_ZombieSkeleton_LOD2";
            }

            if (string.IsNullOrEmpty(behaviorTreeKey))
            {
                behaviorTreeKey = "ZombieMelee";
            }

            if (string.IsNullOrEmpty(aiProfileKey))
            {
                aiProfileKey = "NormalZombieAI";
            }

            if (string.IsNullOrEmpty(bodyPartTemplateKey))
            {
                bodyPartTemplateKey = "ZombieHumanoidDefault";
            }

            if (string.IsNullOrEmpty(dropPoolKey))
            {
                dropPoolKey = "ZombieBasicDrop";
            }

            if (maxHealth <= 0f)
            {
                maxHealth = 100f;
            }

            if (moveSpeed <= 0f)
            {
                moveSpeed = 2.2f;
            }

            if (angularSpeed <= 0f)
            {
                angularSpeed = 360f;
            }

            if (acceleration <= 0f)
            {
                acceleration = 12f;
            }

            if (attackDistance <= 0f)
            {
                attackDistance = 1.4f;
            }

            if (attackInterval <= 0f)
            {
                attackInterval = 1.2f;
            }

            if (detectionRange <= 0f)
            {
                detectionRange = 30f;
            }

            if (blessingEnergyReward <= 0)
            {
                blessingEnergyReward = 1;
            }

            if (experienceReward <= 0)
            {
                experienceReward = 1;
            }

            if (hitStunDuration <= 0f)
            {
                hitStunDuration = 0.09f;
            }

            if (hitReactionCooldown <= 0f)
            {
                hitReactionCooldown = 0.2f;
            }

            if (hitKnockbackDistance <= 0f)
            {
                hitKnockbackDistance = 0.08f;
            }

            if (hitKnockbackDuration <= 0f)
            {
                hitKnockbackDuration = 0.06f;
            }

            if (string.IsNullOrEmpty(idleStateName))
            {
                idleStateName = "ZombieSkeleton_OneHanded_Idle";
            }

            if (string.IsNullOrEmpty(walkStateName))
            {
                walkStateName = "ZombieSkeleton_OneHanded_Walk";
            }

            if (string.IsNullOrEmpty(runStateName))
            {
                runStateName = "ZombieSkeleton_OneHanded_Run";
            }

            if (string.IsNullOrEmpty(attackStateName))
            {
                attackStateName = "ZombieSkeleton_OneHanded_Attack_1";
            }

            if (string.IsNullOrEmpty(damageStateName))
            {
                damageStateName = "ZombieSkeleton_OneHanded_Damage";
            }

            if (string.IsNullOrEmpty(deathStateName))
            {
                deathStateName = "ZombieSkeleton_OneHanded_Death";
            }

            if (locomotionTransition <= 0f)
            {
                locomotionTransition = 0.18f;
            }

            if (attackTransition <= 0f)
            {
                attackTransition = 0.1f;
            }

            if (hitTransition <= 0f)
            {
                hitTransition = 0.14f;
            }

            if (deathTransition <= 0f)
            {
                deathTransition = 0.18f;
            }

            if (recoverTransition <= 0f)
            {
                recoverTransition = 0.18f;
            }

            if (headDamageMultiplier <= 0f)
            {
                headDamageMultiplier = 2f;
            }

            if (bodyDamageMultiplier <= 0f)
            {
                bodyDamageMultiplier = 1f;
            }

            if (armDamageMultiplier <= 0f)
            {
                armDamageMultiplier = 0.75f;
            }

            if (legDamageMultiplier <= 0f)
            {
                legDamageMultiplier = 0.6f;
            }
        }
    }
}
