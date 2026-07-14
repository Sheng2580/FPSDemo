using System.Collections.Generic;
using PlayerData;
using UnityEngine;

namespace Blessing.Data
{
    /// <summary>
    /// 祝福候选生成器
    /// 只处理数据筛选和权重抽取
    /// </summary>
    public static class BlessingRoller
    {
        public static BlessingRollResult[] RollCandidates(
            IReadOnlyList<BlessingConfig> configs,
            BlessingTierProbabilityConfigAsset tierProbability,
            BlessingRollContext context,
            int count)
        {
            int targetCount = Mathf.Max(1, count);
            BlessingTier selectedTier = tierProbability != null
                ? tierProbability.RollTier(context.energyLevel)
                : BlessingTier.Normal;

            List<BlessingConfig> candidates = BuildCandidateList(configs, context);
            List<BlessingRollResult> results = new List<BlessingRollResult>(targetCount);
            HashSet<int> selectedIds = new HashSet<int>();

            if (IsFirstRoll(context))
            {
                BlessingConfig guaranteed = PickWeighted(candidates, selectedIds, true);
                if (guaranteed != null)
                {
                    selectedIds.Add(guaranteed.blessingId);
                    results.Add(new BlessingRollResult(guaranteed, selectedTier));
                }
            }

            while (results.Count < targetCount)
            {
                BlessingConfig selected = PickWeighted(candidates, selectedIds, false);
                if (selected == null)
                {
                    break;
                }

                selectedIds.Add(selected.blessingId);
                results.Add(new BlessingRollResult(selected, selectedTier));
            }

            return results.ToArray();
        }

        private static List<BlessingConfig> BuildCandidateList(IReadOnlyList<BlessingConfig> configs, BlessingRollContext context)
        {
            List<BlessingConfig> candidates = new List<BlessingConfig>();
            if (configs == null)
            {
                return candidates;
            }

            for (int i = 0; i < configs.Count; i++)
            {
                BlessingConfig config = configs[i];
                if (config == null)
                {
                    continue;
                }

                config.ApplyMissingDefaults();
                if (!IsUnlocked(config, context)
                    || !MatchesWeapon(config, context)
                    || !MatchesSkill(config, context)
                    || !CanApplyToCurrentLoadout(config, context)
                    || IsMaxStack(config, context))
                {
                    continue;
                }

                candidates.Add(config);
            }

            return candidates;
        }

        private static bool IsUnlocked(BlessingConfig config, BlessingRollContext context)
        {
            return context.energyLevel >= config.unlockEnergyLevel && context.waveIndex >= config.unlockWave;
        }

        private static bool MatchesWeapon(BlessingConfig config, BlessingRollContext context)
        {
            if (config.requiredWeaponId <= 0)
            {
                return true;
            }

            if (context.weaponIds == null)
            {
                return false;
            }

            for (int i = 0; i < context.weaponIds.Length; i++)
            {
                if (context.weaponIds[i] == config.requiredWeaponId)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool MatchesSkill(BlessingConfig config, BlessingRollContext context)
        {
            if (!config.requiresSkillType)
            {
                return true;
            }

            if (context.skillTypes == null)
            {
                return false;
            }

            for (int i = 0; i < context.skillTypes.Length; i++)
            {
                if (context.skillTypes[i] == config.requiredSkillType)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsMaxStack(BlessingConfig config, BlessingRollContext context)
        {
            if (context.stacks == null)
            {
                return false;
            }

            for (int i = 0; i < context.stacks.Length; i++)
            {
                BlessingStackSnapshot stack = context.stacks[i];
                if (stack.blessingId == config.blessingId && stack.stackCount >= config.maxStack)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool CanApplyToCurrentLoadout(BlessingConfig config, BlessingRollContext context)
        {
            if (!HasEffect(config, BlessingStatType.GrantMissingPrimaryWeapon))
            {
                return true;
            }

            return !ContainsWeapon(context.weaponIds, 2) || !ContainsWeapon(context.weaponIds, 3);
        }

        private static bool HasEffect(BlessingConfig config, BlessingStatType statType)
        {
            if (config?.effects == null)
            {
                return false;
            }

            for (int i = 0; i < config.effects.Length; i++)
            {
                if (config.effects[i] != null && config.effects[i].statType == statType)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsWeapon(int[] weaponIds, int weaponId)
        {
            if (weaponIds == null)
            {
                return false;
            }

            for (int i = 0; i < weaponIds.Length; i++)
            {
                if (weaponIds[i] == weaponId)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsFirstRoll(BlessingRollContext context)
        {
            return context.stacks == null || context.stacks.Length == 0;
        }

        private static BlessingConfig PickWeighted(
            List<BlessingConfig> candidates,
            HashSet<int> selectedIds,
            bool guaranteedOnly)
        {
            float totalWeight = 0f;
            for (int i = 0; i < candidates.Count; i++)
            {
                BlessingConfig candidate = candidates[i];
                if (candidate == null
                    || selectedIds.Contains(candidate.blessingId)
                    || (guaranteedOnly && !candidate.guaranteedFirstRoll))
                {
                    continue;
                }

                totalWeight += Mathf.Max(0f, candidate.weight);
            }

            if (totalWeight <= 0f)
            {
                return null;
            }

            float roll = Random.value * totalWeight;
            for (int i = 0; i < candidates.Count; i++)
            {
                BlessingConfig candidate = candidates[i];
                if (candidate == null
                    || selectedIds.Contains(candidate.blessingId)
                    || (guaranteedOnly && !candidate.guaranteedFirstRoll))
                {
                    continue;
                }

                roll -= Mathf.Max(0f, candidate.weight);
                if (roll <= 0f)
                {
                    return candidate;
                }
            }

            return null;
        }
    }

    public readonly struct BlessingRollContext
    {
        public readonly int energyLevel;
        public readonly int waveIndex;
        public readonly int[] weaponIds;
        public readonly SkillType[] skillTypes;
        public readonly BlessingStackSnapshot[] stacks;

        public BlessingRollContext(
            int energyLevel,
            int waveIndex,
            int[] weaponIds,
            SkillType[] skillTypes,
            BlessingStackSnapshot[] stacks)
        {
            this.energyLevel = Mathf.Max(1, energyLevel);
            this.waveIndex = Mathf.Max(1, waveIndex);
            this.weaponIds = weaponIds;
            this.skillTypes = skillTypes;
            this.stacks = stacks;
        }
    }

    public readonly struct BlessingStackSnapshot
    {
        public readonly int blessingId;
        public readonly int stackCount;

        public BlessingStackSnapshot(int blessingId, int stackCount)
        {
            this.blessingId = blessingId;
            this.stackCount = Mathf.Max(0, stackCount);
        }
    }

    public readonly struct BlessingRollResult
    {
        public readonly BlessingConfig config;
        public readonly BlessingTier tier;

        public BlessingRollResult(BlessingConfig config, BlessingTier tier)
        {
            this.config = config;
            this.tier = tier;
        }
    }
}
