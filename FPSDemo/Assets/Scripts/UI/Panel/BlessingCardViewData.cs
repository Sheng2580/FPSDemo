using System;
using Blessing.Data;
using UnityEngine;

/// <summary>
/// 祝福卡片显示数据
/// </summary>
[Serializable]
public readonly struct BlessingCardViewData
{
    public readonly int BlessingId;
    public readonly string DisplayName;
    public readonly string Description;
    public readonly string ValueText;
    public readonly string CategoryText;
    public readonly BlessingTier Tier;
    public readonly int StackCount;
    public readonly int MaxStack;
    public readonly float Value;
    public readonly string IconKey;

    public bool IsValid => BlessingId > 0;
    public string TierText => ToTierText(Tier);
    public string StackText => MaxStack > 0 ? $"{StackCount}/{MaxStack}" : string.Empty;

    public BlessingCardViewData(
        int blessingId,
        string displayName,
        string description,
        string valueText,
        string categoryText,
        BlessingTier tier,
        int stackCount,
        int maxStack,
        float value,
        string iconKey)
    {
        BlessingId = Mathf.Max(0, blessingId);
        DisplayName = string.IsNullOrEmpty(displayName) ? "未命名祝福" : displayName;
        Description = description ?? string.Empty;
        ValueText = valueText ?? string.Empty;
        CategoryText = categoryText ?? string.Empty;
        Tier = tier;
        StackCount = Mathf.Max(0, stackCount);
        MaxStack = Mathf.Max(0, maxStack);
        Value = value;
        IconKey = iconKey ?? string.Empty;
    }

    public static string ToTierText(BlessingTier tier)
    {
        return tier switch
        {
            BlessingTier.Plus => "+",
            BlessingTier.PlusPlus => "++",
            _ => string.Empty
        };
    }
}
