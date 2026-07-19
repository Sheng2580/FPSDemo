using System;
using System.Collections.Generic;
using Blessing.Data;
using Enemy;
using PlayerData;
using UnityEngine;

/// <summary>
/// 祝福候选显示数据提供器
/// </summary>
public sealed class BlessingCandidateProvider
{
    private const string BlessingConfigPath = "BlessingConfigs";
    private const string DefaultBlessingDatabasePath = "BlessingConfigs/DefaultBlessingConfigDatabase";
    private const string DefaultTierProbabilityPath = "BlessingConfigs/DefaultBlessingTierProbabilityConfig";
    private readonly BlessingSelectionRuntime _selectionRuntime;

    public BlessingCandidateProvider(BlessingSelectionRuntime selectionRuntime)
    {
        _selectionRuntime = selectionRuntime;
    }

    public BlessingCardViewData[] ResolveCandidates()
    {
        List<BlessingConfig> configs = LoadBlessingConfigs();
        if (configs.Count <= 0)
        {
            return CreateFallbackCandidates();
        }

        int energyLevel = ResolveEnergyLevel();
        BlessingTierProbabilityConfigAsset tierProbability = BlessingJsonConfigLoader.LoadTierProbabilityConfig()
            ?? Resources.Load<BlessingTierProbabilityConfigAsset>(DefaultTierProbabilityPath);
        BlessingRollContext context = new BlessingRollContext(
            energyLevel,
            ResolveWaveIndex(),
            ResolveWeaponIds(),
            ResolveSkillTypes(),
            _selectionRuntime.CreateStackSnapshots());

        BlessingRollResult[] rollResults = BlessingRoller.RollCandidates(configs, tierProbability, context, 3);
        if (rollResults == null || rollResults.Length <= 0)
        {
            return CreateFallbackCandidates();
        }

        BlessingCardViewData[] views = new BlessingCardViewData[3];
        for (int i = 0; i < views.Length; i++)
        {
            views[i] = i < rollResults.Length && rollResults[i].config != null
                ? CreateViewData(rollResults[i])
                : CreateFallbackCandidate(i, energyLevel, 0);
        }

        return views;
    }

    public static BlessingCardViewData CreateFallbackCandidate(int index, int energyLevel, int currentStack)
    {
        BlessingTier tier = energyLevel >= 3 && index == 2
            ? BlessingTier.PlusPlus
            : energyLevel >= 2 && index == 1
                ? BlessingTier.Plus
                : BlessingTier.Normal;

        return index switch
        {
            1 => new BlessingCardViewData(1002, "步枪伤害强化", $"步枪伤害 {FallbackValueText(tier, 10f, 18f, 30f)}", FallbackValueText(tier, 10f, 18f, 30f), "武器", tier, currentStack, 5, FallbackValue(tier, 10f, 18f, 30f), "Blessing_RifleDamage"),
            2 => new BlessingCardViewData(1003, "手雷扩容", tier == BlessingTier.PlusPlus ? "手雷上限 +2" : "手雷上限 +1", tier == BlessingTier.PlusPlus ? "+2" : "+1", "技能", tier, currentStack, 3, tier == BlessingTier.PlusPlus ? 2f : 1f, "Blessing_GrenadeCount"),
            _ => new BlessingCardViewData(1001, "战斗兴奋", $"能量获取 {FallbackValueText(tier, 10f, 18f, 30f)}", FallbackValueText(tier, 10f, 18f, 30f), "玩家", tier, currentStack, 5, FallbackValue(tier, 10f, 18f, 30f), "Blessing_EnergyGain")
        };
    }

    private BlessingCardViewData[] CreateFallbackCandidates()
    {
        int energyLevel = ResolveEnergyLevel();
        return new[]
        {
            CreateFallbackCandidate(0, energyLevel, _selectionRuntime.GetStack(1001)),
            CreateFallbackCandidate(1, energyLevel, _selectionRuntime.GetStack(1002)),
            CreateFallbackCandidate(2, energyLevel, _selectionRuntime.GetStack(1003))
        };
    }

    private List<BlessingConfig> LoadBlessingConfigs()
    {
        if (BlessingJsonConfigLoader.TryLoadBlessingConfigs(out List<BlessingConfig> jsonConfigs))
        {
            return jsonConfigs;
        }

        List<BlessingConfig> configs = new List<BlessingConfig>();

        BlessingConfigDatabaseAsset defaultDatabase = Resources.Load<BlessingConfigDatabaseAsset>(DefaultBlessingDatabasePath);
        AddDatabaseConfigs(configs, defaultDatabase);

        BlessingConfigDatabaseAsset[] databases = Resources.LoadAll<BlessingConfigDatabaseAsset>(BlessingConfigPath);
        for (int i = 0; i < databases.Length; i++)
        {
            if (databases[i] != defaultDatabase)
            {
                AddDatabaseConfigs(configs, databases[i]);
            }
        }

        BlessingConfigAsset[] assets = Resources.LoadAll<BlessingConfigAsset>(BlessingConfigPath);
        for (int i = 0; i < assets.Length; i++)
        {
            BlessingConfig config = assets[i] != null ? assets[i].CreateRuntimeConfig() : null;
            if (config != null)
            {
                configs.Add(config);
            }
        }

        return configs;
    }

    private void AddDatabaseConfigs(List<BlessingConfig> configs, BlessingConfigDatabaseAsset database)
    {
        if (database == null)
        {
            return;
        }

        BlessingConfig[] runtimeConfigs = database.CreateRuntimeConfigs();
        for (int i = 0; i < runtimeConfigs.Length; i++)
        {
            if (runtimeConfigs[i] != null)
            {
                configs.Add(runtimeConfigs[i]);
            }
        }
    }

    private BlessingCardViewData CreateViewData(BlessingRollResult rollResult)
    {
        BlessingConfig config = rollResult.config;
        BlessingTier tier = rollResult.tier;
        float value = config.GetFirstEffectValue(tier);
        string valueText = BuildValueText(config, value);
        int currentStack = _selectionRuntime.GetStack(config.blessingId);
        string description = BuildDescription(config, tier, value, valueText, currentStack + 1);

        return new BlessingCardViewData(
            config.blessingId,
            config.blessingName,
            description,
            valueText,
            ToCategoryText(config.category),
            tier,
            currentStack,
            config.maxStack,
            value,
            config.iconKey);
    }

    private string BuildValueText(BlessingConfig config, float value)
    {
        if (config.effects == null || config.effects.Length <= 0 || config.effects[0] == null)
        {
            return string.Empty;
        }

        BlessingEffectConfig effect = config.effects[0];
        if (effect.statType == BlessingStatType.GrantMissingPrimaryWeapon)
        {
            return "新武器";
        }

        float displayValue = effect.modifyType == BlessingModifyType.PercentAdd && Mathf.Abs(value) <= 1f
            ? value * 100f
            : value;

        string sign = displayValue > 0f ? "+" : string.Empty;
        string suffix = effect.modifyType == BlessingModifyType.PercentAdd ? "%" : string.Empty;
        return $"{sign}{displayValue:0.##}{suffix}";
    }

    private string BuildDescription(BlessingConfig config, BlessingTier tier, float value, string valueText, int nextStack)
    {
        string description = string.IsNullOrEmpty(config.descriptionTemplate)
            ? config.blessingName
            : config.descriptionTemplate;

        return description
            .Replace("{0}", FormatTemplateValue(value))
            .Replace("{value:P0}", FormatPercentTemplateValue(value, 0))
            .Replace("{value:P1}", FormatPercentTemplateValue(value, 1))
            .Replace("{value:P2}", FormatPercentTemplateValue(value, 2))
            .Replace("{value}", FormatTemplateValue(value))
            .Replace("{valueText}", valueText)
            .Replace("{tier}", BlessingCardViewData.ToTierText(tier))
            .Replace("{stack}", nextStack.ToString())
            .Replace("{maxStack}", config.maxStack > 0 ? config.maxStack.ToString() : "无限");
    }

    private string FormatTemplateValue(float value)
    {
        return Mathf.Abs(value).ToString("0.##");
    }

    private string FormatPercentTemplateValue(float value, int decimalPlaces)
    {
        string format = decimalPlaces <= 0 ? "0" : "0." + new string('#', decimalPlaces);
        return (Mathf.Abs(value) * 100f).ToString(format) + "%";
    }

    private string ToCategoryText(BlessingCategory category)
    {
        return category switch
        {
            BlessingCategory.PlayerStat => "玩家",
            BlessingCategory.WeaponStat => "武器",
            BlessingCategory.SkillStat => "技能",
            BlessingCategory.GameplayTrigger => "触发",
            BlessingCategory.Economy => "金币",
            _ => "祝福"
        };
    }

    private int ResolveEnergyLevel()
    {
        PlayerEnergyRuntime runtime = UnityEngine.Object.FindObjectOfType<PlayerEnergyRuntime>();
        return runtime != null && runtime.RuntimeData != null
            ? Mathf.Max(1, runtime.RuntimeData.level)
            : 1;
    }

    private int ResolveWaveIndex()
    {
        EnemySpawnManager spawnManager = UnityEngine.Object.FindObjectOfType<EnemySpawnManager>();
        return spawnManager != null ? Mathf.Max(1, spawnManager.CurrentWaveIndex) : 1;
    }

    private int[] ResolveWeaponIds()
    {
        PlayerInventory inventory = UnityEngine.Object.FindObjectOfType<PlayerInventory>();
        if (inventory == null || inventory.CarriedWeapons == null)
        {
            return new[] { 1 };
        }

        List<int> ids = new List<int>();
        IReadOnlyList<CarriedWeaponSlot> weapons = inventory.CarriedWeapons;
        for (int i = 0; i < weapons.Count; i++)
        {
            CarriedWeaponSlot slot = weapons[i];
            slot?.EnsureRuntimeReady();
            int weaponId = slot?.RuntimeConfig?.weaponId ?? 0;
            if (weaponId > 0 && !ids.Contains(weaponId))
            {
                ids.Add(weaponId);
            }
        }

        return ids.Count > 0 ? ids.ToArray() : new[] { 1 };
    }

    private SkillType[] ResolveSkillTypes()
    {
        return (SkillType[])Enum.GetValues(typeof(SkillType));
    }

    private static float FallbackValue(BlessingTier tier, float normal, float plus, float plusPlus)
    {
        return tier switch
        {
            BlessingTier.Plus => plus,
            BlessingTier.PlusPlus => plusPlus,
            _ => normal
        };
    }

    private static string FallbackValueText(BlessingTier tier, float normal, float plus, float plusPlus)
    {
        return $"+{FallbackValue(tier, normal, plus, plusPlus):0.##}%";
    }
}
