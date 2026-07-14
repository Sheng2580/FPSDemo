using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PlayerData
{
    /// <summary>
    /// 玩家技能冷却规则
    /// 永久升级和祝福分别限制后再合并
    /// </summary>
    [Serializable]
    public sealed class PlayerSkillRulesConfig
    {
        public const float DefaultMaxPermanentCooldownReduction = 0.3f;
        public const float DefaultMaxBlessingCooldownReduction = 0.3f;

        // 局外永久冷却缩减上限
        public float maxPermanentCooldownReduction = DefaultMaxPermanentCooldownReduction;
        // 局内祝福冷却缩减上限
        public float maxBlessingCooldownReduction = DefaultMaxBlessingCooldownReduction;

        // 最终冷却缩减上限
        public float MaxTotalCooldownReduction => Mathf.Clamp01(
            maxPermanentCooldownReduction + maxBlessingCooldownReduction);

        public float ClampPermanentCooldownReduction(float value)
        {
            return Mathf.Clamp(value, 0f, Mathf.Clamp01(maxPermanentCooldownReduction));
        }

        public float ClampBlessingCooldownReduction(float value)
        {
            return Mathf.Clamp(value, 0f, Mathf.Clamp01(maxBlessingCooldownReduction));
        }

        public float CalculateCooldown(float baseCooldown, float permanentReduction, float blessingReduction)
        {
            float finalReduction = Mathf.Clamp(
                ClampPermanentCooldownReduction(permanentReduction)
                + ClampBlessingCooldownReduction(blessingReduction),
                0f,
                MaxTotalCooldownReduction);
            return Mathf.Max(0f, baseCooldown) * (1f - finalReduction);
        }

        public PlayerSkillRulesConfig Clone()
        {
            return new PlayerSkillRulesConfig
            {
                maxPermanentCooldownReduction = maxPermanentCooldownReduction,
                maxBlessingCooldownReduction = maxBlessingCooldownReduction
            };
        }
    }

    /// <summary>
    /// Luban 玩家技能配置读取入口
    /// 正式数据来自 Json ScriptableObject 只作为兜底
    /// </summary>
    public static class PlayerSkillConfigJsonLoader
    {
        private const string SkillJsonFileName = "tbplayer_skill_config";
        private const string RulesJsonFileName = "tbplayer_skill_rules";
        private const string ResourcesJsonFolder = "PlayerJson";

        private static readonly Dictionary<SkillType, PlayerSkillConfig> Configs =
            new Dictionary<SkillType, PlayerSkillConfig>();

        private static PlayerSkillRulesConfig _rules = new PlayerSkillRulesConfig();
        private static bool _loaded;

        public static PlayerSkillRulesConfig Rules
        {
            get
            {
                EnsureLoaded();
                return _rules;
            }
        }

        public static bool TryGetConfig(SkillType skillType, out PlayerSkillConfig config)
        {
            EnsureLoaded();
            if (Configs.TryGetValue(skillType, out PlayerSkillConfig cachedConfig) && cachedConfig != null)
            {
                config = cachedConfig.Clone();
                return true;
            }

            config = null;
            return false;
        }

        private static void EnsureLoaded()
        {
            if (_loaded)
            {
                return;
            }

            _loaded = true;
            LoadRules();
            LoadSkillConfigs();
        }

        private static void LoadRules()
        {
            if (!TryReadJson(RulesJsonFileName, out string json))
            {
                Debug.LogWarning("[PlayerSkillConfig] 未读取到技能冷却规则表 使用默认上限");
                return;
            }

            try
            {
                PlayerSkillRulesRowList list = JsonUtility.FromJson<PlayerSkillRulesRowList>(WrapArrayJson(json));
                if (list?.rows == null || list.rows.Length <= 0 || list.rows[0] == null)
                {
                    Debug.LogWarning("[PlayerSkillConfig] 技能冷却规则表没有数据 使用默认上限");
                    return;
                }

                PlayerSkillRulesRow row = list.rows[0];
                _rules = new PlayerSkillRulesConfig
                {
                    maxPermanentCooldownReduction = Mathf.Clamp01(row.maxPermanentCooldownReduction),
                    maxBlessingCooldownReduction = Mathf.Clamp01(row.maxBlessingCooldownReduction)
                };
            }
            catch (Exception exception)
            {
                Debug.LogError($"[PlayerSkillConfig] 技能冷却规则表解析失败 {exception.Message}");
            }
        }

        private static void LoadSkillConfigs()
        {
            if (!TryReadJson(SkillJsonFileName, out string json))
            {
                Debug.LogWarning("[PlayerSkillConfig] 未读取到玩家技能配置表 使用 ScriptableObject 兜底");
                return;
            }

            try
            {
                PlayerSkillConfigRowList list = JsonUtility.FromJson<PlayerSkillConfigRowList>(WrapArrayJson(json));
                if (list?.rows == null)
                {
                    return;
                }

                for (int i = 0; i < list.rows.Length; i++)
                {
                    PlayerSkillConfigRow row = list.rows[i];
                    if (row == null
                        || row.skillId <= 0
                        || !Enum.TryParse(row.skillType, true, out SkillType skillType))
                    {
                        continue;
                    }

                    PlayerSkillConfig config = CreateConfig(row, skillType);
                    config.ApplyMissingDefaults();
                    Configs[skillType] = config;
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"[PlayerSkillConfig] 玩家技能配置表解析失败 {exception.Message}");
            }
        }

        private static PlayerSkillConfig CreateConfig(PlayerSkillConfigRow row, SkillType skillType)
        {
            return new PlayerSkillConfig
            {
                skillId = row.skillId,
                skillName = row.skillName,
                skillType = skillType,
                cooldown = row.cooldown,
                duration = row.duration,
                lockWeaponDuringCast = row.lockWeaponDuringCast,
                canBufferInput = row.canBufferInput,
                inputBufferTime = row.inputBufferTime,
                dodgeDistance = row.dodgeDistance,
                invincibleDuration = row.invincibleDuration,
                collisionDisableDuration = row.collisionDisableDuration,
                detectDistance = row.detectDistance,
                detectRadius = row.detectRadius,
                detectAngle = row.detectAngle,
                maxHitCount = row.maxHitCount,
                damage = row.damage,
                knockbackForce = row.knockbackForce,
                stunDuration = row.stunDuration,
                initialCount = row.initialCount,
                maxCount = row.maxCount,
                explosionRadius = row.explosionRadius,
                explosionDelay = row.explosionDelay,
                throwForce = row.throwForce,
                throwUpForce = row.throwUpForce,
                projectileLifeTime = row.projectileLifeTime,
                iconKey = row.iconKey,
                animationKey = row.animationKey,
                alternateAnimationKey = row.alternateAnimationKey,
                throwAnimationKey = row.throwAnimationKey,
                projectileResourceKey = row.projectileResourceKey,
                castEffectKey = row.castEffectKey,
                hitEffectKey = row.hitEffectKey,
                explosionEffectKey = row.explosionEffectKey,
                castAudioKey = row.castAudioKey,
                hitAudioKey = row.hitAudioKey,
                explosionAudioKey = row.explosionAudioKey,
                fovEffectKey = row.fovEffectKey,
                postProcessKey = row.postProcessKey,
                cameraShakeKey = row.cameraShakeKey
            };
        }

        private static bool TryReadJson(string fileName, out string json)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            string generatedJsonPath = Path.Combine(projectRoot, "MiniTemplate", "GeneratedJson");
            return JsonMgr.Instance.TryLoadJsonText(
                fileName,
                out json,
                string.Empty,
                ResourcesJsonFolder,
                generatedJsonPath);
        }

        private static string WrapArrayJson(string json)
        {
            return "{\"rows\":" + json.Trim() + "}";
        }

        [Serializable]
        private sealed class PlayerSkillConfigRowList
        {
            public PlayerSkillConfigRow[] rows;
        }

        [Serializable]
        private sealed class PlayerSkillRulesRowList
        {
            public PlayerSkillRulesRow[] rows;
        }

        [Serializable]
        private sealed class PlayerSkillRulesRow
        {
            public int id;
            public float maxPermanentCooldownReduction;
            public float maxBlessingCooldownReduction;
        }

        [Serializable]
        private sealed class PlayerSkillConfigRow
        {
            public int skillId;
            public string skillName;
            public string skillType;
            public float cooldown;
            public float duration;
            public bool lockWeaponDuringCast;
            public bool canBufferInput;
            public float inputBufferTime;
            public float dodgeDistance;
            public float invincibleDuration;
            public float collisionDisableDuration;
            public float detectDistance;
            public float detectRadius;
            public float detectAngle;
            public int maxHitCount;
            public float damage;
            public float knockbackForce;
            public float stunDuration;
            public int initialCount;
            public int maxCount;
            public float explosionRadius;
            public float explosionDelay;
            public float throwForce;
            public float throwUpForce;
            public float projectileLifeTime;
            public string iconKey;
            public string animationKey;
            public string alternateAnimationKey;
            public string throwAnimationKey;
            public string projectileResourceKey;
            public string castEffectKey;
            public string hitEffectKey;
            public string explosionEffectKey;
            public string castAudioKey;
            public string hitAudioKey;
            public string explosionAudioKey;
            public string fovEffectKey;
            public string postProcessKey;
            public string cameraShakeKey;
        }
    }
}
