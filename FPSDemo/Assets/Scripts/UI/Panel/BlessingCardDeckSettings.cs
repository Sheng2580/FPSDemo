using System;
using UnityEngine;

/// <summary>
/// 祝福卡片牌组布局参数
/// </summary>
[Serializable]
public struct BlessingCardDeckSettings
{
    public float openDuration;
    public float closeDuration;
    public float cardInterval;
    public float cardWidth;
    public float cardHeight;
    public float minCardScale;
    public float maxCardScale;
    public float selectedScale;
    public float confirmDuration;
    public float returnDuration;

    public static BlessingCardDeckSettings CreateDefault()
    {
        return new BlessingCardDeckSettings
        {
            openDuration = 0.42f,
            closeDuration = 0.28f,
            cardInterval = 0.08f,
            cardWidth = 420f,
            cardHeight = 620f,
            minCardScale = 0.72f,
            maxCardScale = 1f,
            selectedScale = 1.05f,
            confirmDuration = 0.45f,
            returnDuration = 0.32f
        };
    }

    public void ApplyMissingDefaults()
    {
        BlessingCardDeckSettings defaults = CreateDefault();
        openDuration = openDuration > 0f ? openDuration : defaults.openDuration;
        closeDuration = closeDuration > 0f ? closeDuration : defaults.closeDuration;
        cardInterval = cardInterval >= 0f ? cardInterval : defaults.cardInterval;
        cardWidth = cardWidth > 0f ? cardWidth : defaults.cardWidth;
        cardHeight = cardHeight > 0f ? cardHeight : defaults.cardHeight;
        minCardScale = minCardScale > 0f ? minCardScale : defaults.minCardScale;
        maxCardScale = maxCardScale > 0f ? maxCardScale : defaults.maxCardScale;
        selectedScale = selectedScale > 0f ? selectedScale : defaults.selectedScale;
        confirmDuration = confirmDuration > 0f ? confirmDuration : defaults.confirmDuration;
        returnDuration = returnDuration > 0f ? returnDuration : defaults.returnDuration;
    }
}
