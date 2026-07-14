using System;
using UnityEngine;

namespace PlayerData
{
    /// <summary>
    /// 玩家技能配置
    /// 只保存数值和资源 key
    /// </summary>
    [Serializable]
    public class PlayerSkillConfig
    {
        // 技能唯一编号
        public int skillId;
        // 技能名称
        public string skillName;
        // 技能类型
        public SkillType skillType;
        // 冷却时间
        public float cooldown;
        // 技能持续时间
        public float duration;
        // 释放期间是否锁定武器
        public bool lockWeaponDuringCast;
        // 是否允许输入缓存
        public bool canBufferInput;
        // 输入缓存时间
        public float inputBufferTime;

        // 闪避距离
        public float dodgeDistance;
        // 无敌持续时间
        public float invincibleDuration;
        // 碰撞关闭时间
        public float collisionDisableDuration;

        // 检测距离
        public float detectDistance;
        // 检测半径
        public float detectRadius;
        // 检测角度
        public float detectAngle;
        // 最大命中数量
        public int maxHitCount;
        // 伤害
        public float damage;
        // 击退力度
        public float knockbackForce;
        // 硬直时间
        public float stunDuration;

        // 初始数量
        public int initialCount;
        // 最大携带数量
        public int maxCount;
        // 爆炸半径
        public float explosionRadius;
        // 爆炸延迟
        public float explosionDelay;
        // 投掷力度
        public float throwForce;
        // 向上投掷力度
        public float throwUpForce;
        // 投掷物存活时间
        public float projectileLifeTime;

        // 图标资源 key
        public string iconKey;
        // 主动画 key
        public string animationKey;
        // 备用动画 key
        public string alternateAnimationKey;
        // 投掷动画 key
        public string throwAnimationKey;
        // 投掷物资源 key
        public string projectileResourceKey;
        // 释放特效 key
        public string castEffectKey;
        // 命中特效 key
        public string hitEffectKey;
        // 爆炸特效 key
        public string explosionEffectKey;
        // 释放音效 key
        public string castAudioKey;
        // 命中音效 key
        public string hitAudioKey;
        // 爆炸音效 key
        public string explosionAudioKey;
        // FOV 表现 key
        public string fovEffectKey;
        // 后处理表现 key
        public string postProcessKey;
        // 镜头震动 key
        public string cameraShakeKey;

        public static PlayerSkillConfig CreateDefaultDodge()
        {
            return new PlayerSkillConfig
            {
                skillId = 1,
                skillName = "Dodge",
                skillType = SkillType.Dodge,
                cooldown = 3.5f,
                duration = 0.22f,
                lockWeaponDuringCast = true,
                canBufferInput = true,
                inputBufferTime = 0.12f,
                dodgeDistance = 4.2f,
                invincibleDuration = 0.18f,
                collisionDisableDuration = 0.12f,
                postProcessKey = "Skill_Dodge_SprintPulse",
                fovEffectKey = "Skill_Dodge_FOV",
                cameraShakeKey = "Skill_Dodge_Shake"
            };
        }

        public static PlayerSkillConfig CreateDefaultPush()
        {
            return new PlayerSkillConfig
            {
                skillId = 2,
                skillName = "Push",
                skillType = SkillType.Push,
                cooldown = 6f,
                duration = 0.6f,
                lockWeaponDuringCast = true,
                canBufferInput = true,
                inputBufferTime = 0.12f,
                detectDistance = 0f,
                detectRadius = 4.5f,
                detectAngle = 360f,
                maxHitCount = 0,
                damage = 60f,
                knockbackForce = 30f,
                stunDuration = 0.65f,
                animationKey = "Melee Weapon_1|Attack 1",
                alternateAnimationKey = "Melee Weapon_1|Attack 2",
                castEffectKey = "Skill_Push_Cast",
                hitEffectKey = "Skill_Push_Hit",
                castAudioKey = "Skill_Push_Swing",
                hitAudioKey = "Skill_Push_Hit",
                postProcessKey = "Skill_Push_ImpactPulse",
                cameraShakeKey = "Skill_Push_Shake"
            };
        }

        public static PlayerSkillConfig CreateDefaultGrenade()
        {
            return new PlayerSkillConfig
            {
                skillId = 3,
                skillName = "Grenade",
                skillType = SkillType.Grenade,
                cooldown = 8f,
                duration = 0.45f,
                lockWeaponDuringCast = true,
                canBufferInput = true,
                inputBufferTime = 0.12f,
                initialCount = 2,
                maxCount = 3,
                damage = 80f,
                explosionRadius = 4.5f,
                explosionDelay = 1.2f,
                knockbackForce = 8f,
                stunDuration = 0.35f,
                throwForce = 13f,
                throwUpForce = 2.5f,
                projectileLifeTime = 5f,
                projectileResourceKey = "Gernade",
                throwAnimationKey = "Grenade_1 |Throw",
                explosionEffectKey = "Skill_Grenade_Explosion",
                explosionAudioKey = "Skill_Grenade_Explosion",
                postProcessKey = "Skill_Grenade_ExplosionPulse",
                cameraShakeKey = "Skill_Grenade_Shake"
            };
        }

        public PlayerSkillConfig Clone()
        {
            return (PlayerSkillConfig)MemberwiseClone();
        }

        public void ApplyMissingDefaults()
        {
            skillId = Mathf.Max(1, skillId);
            cooldown = Mathf.Max(0f, cooldown);
            duration = Mathf.Max(0f, duration);
            inputBufferTime = Mathf.Max(0f, inputBufferTime);
            dodgeDistance = Mathf.Max(0f, dodgeDistance);
            invincibleDuration = Mathf.Max(0f, invincibleDuration);
            collisionDisableDuration = Mathf.Max(0f, collisionDisableDuration);
            detectDistance = Mathf.Max(0f, detectDistance);
            detectRadius = Mathf.Max(0f, detectRadius);
            detectAngle = Mathf.Clamp(detectAngle, 0f, 360f);
            maxHitCount = Mathf.Max(0, maxHitCount);
            damage = Mathf.Max(0f, damage);
            knockbackForce = Mathf.Max(0f, knockbackForce);
            stunDuration = Mathf.Max(0f, stunDuration);
            initialCount = Mathf.Max(0, initialCount);
            maxCount = Mathf.Max(initialCount, maxCount);
            explosionRadius = Mathf.Max(0f, explosionRadius);
            explosionDelay = Mathf.Max(0f, explosionDelay);
            throwForce = Mathf.Max(0f, throwForce);
            throwUpForce = Mathf.Max(0f, throwUpForce);
            projectileLifeTime = Mathf.Max(0f, projectileLifeTime);
        }
    }
}
