using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 祝福卡片牌组
/// </summary>
public sealed class BlessingCardDeck
{
    private readonly float[] _openRotations = { 8f, 0f, -8f };
    private readonly BlessingCardViewData[] _candidateViews = new BlessingCardViewData[3];
    private BlessingSelectCanvas _owner;
    private RectTransform _cardRoot;
    private RectTransform _cardButton;
    private RectTransform[] _cards;
    private BlessingCardItem[] _cardItems;
    private BlessingCardDeckSettings _settings;
    private BlessingCardItem _selectedCard;

    public bool HasSelectedCard => _selectedCard != null;
    public int SelectedIndex => _selectedCard != null ? _selectedCard.Index : 0;
    public BlessingCardViewData SelectedData => _selectedCard != null ? _selectedCard.ViewData : default;

    public void Configure(
        BlessingSelectCanvas owner,
        Transform canvasRoot,
        RectTransform cardRoot,
        RectTransform cardButton,
        ref RectTransform[] cards,
        ref BlessingCardItem[] cardItems,
        BlessingCardDeckSettings settings)
    {
        _owner = owner;
        _cardRoot = cardRoot;
        _cardButton = cardButton;
        _settings = settings;
        _settings.ApplyMissingDefaults();

        if (cards == null || cards.Length != 3)
        {
            cards = new RectTransform[3];
        }

        if (cardItems == null || cardItems.Length != cards.Length)
        {
            cardItems = new BlessingCardItem[cards.Length];
        }

        _cards = cards;
        _cardItems = cardItems;

        for (int i = 0; i < _cards.Length; i++)
        {
            CacheCardReference(i);
        }
    }

    public void SetCandidates(IReadOnlyList<BlessingCardViewData> candidates)
    {
        for (int i = 0; i < _candidateViews.Length; i++)
        {
            _candidateViews[i] = candidates != null && i < candidates.Count && candidates[i].IsValid
                ? candidates[i]
                : BlessingCandidateProvider.CreateFallbackCandidate(i, 1, 0);

            if (_cardItems != null && i < _cardItems.Length && _cardItems[i] != null)
            {
                _cardItems[i].SetData(_candidateViews[i]);
            }
        }
    }

    public void PrepareForOpen()
    {
        ApplyResponsiveCardSize();
        ClearSelectedCard(true);
        SetCardsRaycast(false);

        Vector2 closedPosition = ResolveClosedPosition();
        for (int i = 0; i < _cardItems.Length; i++)
        {
            BlessingCardItem item = _cardItems[i];
            if (item == null)
            {
                continue;
            }

            item.SetData(_candidateViews[i]);
            item.SetPose(closedPosition, 0f, 0.82f, 1f);
        }
    }

    public DG.Tweening.Sequence CreateOpenSequence()
    {
        Vector2[] openPositions = ResolveOpenPositions();
        DG.Tweening.Sequence sequence = DOTween.Sequence().SetUpdate(true);

        for (int i = 0; i < _cardItems.Length; i++)
        {
            BlessingCardItem item = _cardItems[i];
            if (item == null)
            {
                continue;
            }

            int index = i;
            float delay = index * _settings.cardInterval;
            float openScale = GetOpenScale();
            sequence.Insert(delay, item.Root.DOAnchorPos(openPositions[index], _settings.openDuration).SetEase(Ease.OutCubic));
            sequence.Insert(delay, item.Root.DOLocalRotate(new Vector3(0f, 0f, _openRotations[index]), _settings.openDuration).SetEase(Ease.OutBack));
            sequence.Insert(delay, item.Root.DOScale(openScale, _settings.openDuration).SetEase(Ease.OutBack));
            sequence.InsertCallback(delay, item.ResetVisualColor);
        }

        return sequence;
    }

    public DG.Tweening.Sequence CreateCloseSequence()
    {
        Vector2 closedPosition = ResolveClosedPosition();
        DG.Tweening.Sequence sequence = DOTween.Sequence().SetUpdate(true);

        for (int i = 0; i < _cardItems.Length; i++)
        {
            BlessingCardItem item = _cardItems[i];
            if (item == null)
            {
                continue;
            }

            float delay = i * _settings.cardInterval;
            sequence.Insert(delay, item.Root.DOAnchorPos(closedPosition, _settings.closeDuration).SetEase(Ease.InCubic));
            sequence.Insert(delay, item.Root.DOLocalRotate(Vector3.zero, _settings.closeDuration).SetEase(Ease.InCubic));
            sequence.Insert(delay, item.Root.DOScale(0.82f, _settings.closeDuration).SetEase(Ease.InCubic));
            sequence.Insert(delay, item.FadeTo(1f, _settings.closeDuration));
        }

        return sequence;
    }

    public DG.Tweening.Sequence CreateConfirmSequence()
    {
        Vector2 targetPosition = ResolveCardButtonLocalPosition();
        Vector2 closedPosition = ResolveClosedPosition();
        DG.Tweening.Sequence sequence = DOTween.Sequence().SetUpdate(true);

        for (int i = 0; i < _cardItems.Length; i++)
        {
            BlessingCardItem item = _cardItems[i];
            if (item == null)
            {
                continue;
            }

            if (item == _selectedCard)
            {
                sequence.Join(item.PlayConfirmFly(targetPosition, _settings.confirmDuration));
                continue;
            }

            item.StopSelectedLoop(true, true);
            sequence.Join(item.PlayReturnToClosed(closedPosition, _settings.returnDuration));
        }

        return sequence;
    }

    public void SelectCard(BlessingCardItem card)
    {
        if (card == null || _selectedCard == card)
        {
            return;
        }

        ClearSelectedCard(true);
        _selectedCard = card;
        _selectedCard.PlaySelectedFocus(GetOpenScale() * _settings.selectedScale);
    }

    public void ClearSelectedCard(bool resetColor)
    {
        if (_selectedCard != null)
        {
            _selectedCard.StopSelectedLoop(resetColor, true);
            _selectedCard = null;
        }

        StopAllCardLoops(resetColor);
    }

    public void StopAllCardLoops(bool resetColor)
    {
        StopAllCardLoops(resetColor, true);
    }

    public void StopAllCardLoops(bool resetColor, bool restoreSibling)
    {
        if (_cardItems == null)
        {
            return;
        }

        for (int i = 0; i < _cardItems.Length; i++)
        {
            _cardItems[i]?.StopSelectedLoop(resetColor, restoreSibling);
        }
    }

    public void SetCardsRaycast(bool enabled)
    {
        if (_cardItems == null)
        {
            return;
        }

        for (int i = 0; i < _cardItems.Length; i++)
        {
            _cardItems[i]?.SetInteractable(enabled);
        }
    }

    public void SetClosedImmediate()
    {
        Vector2 closedPosition = ResolveClosedPosition();
        if (_cardItems == null)
        {
            return;
        }

        for (int i = 0; i < _cardItems.Length; i++)
        {
            BlessingCardItem item = _cardItems[i];
            if (item == null)
            {
                continue;
            }

            item.SetPose(closedPosition, 0f, 0.82f, 1f);
            item.RestoreSiblingIndex();
            item.ResetVisualColor();
        }
    }

    private void CacheCardReference(int index)
    {
        RectTransform namedCard = FindCardByName(index);
        if (namedCard != null)
        {
            _cards[index] = namedCard;
        }

        if (_cards[index] == null && _cardRoot != null)
        {
            Transform card = _cardRoot.Find($"BlessingCard_{index}");
            _cards[index] = card != null ? card as RectTransform : null;
        }

        if (_cards[index] == null)
        {
            return;
        }

        BlessingCardItem item = _cardItems[index];
        if (item == null && !_cards[index].TryGetComponent(out item))
        {
            item = _cards[index].gameObject.AddComponent<BlessingCardItem>();
        }

        _cardItems[index] = item;
        item.Init(_owner, index);
    }

    private RectTransform FindCardByName(int index)
    {
        if (_cardRoot == null)
        {
            return null;
        }

        Transform card = _cardRoot.Find($"BlessingCard_{index}");
        return card != null ? card as RectTransform : null;
    }

    private void ApplyResponsiveCardSize()
    {
        if (_cardRoot == null || _cards == null)
        {
            return;
        }

        Rect rootRect = _cardRoot.rect;
        float width = rootRect.width > 0f ? Mathf.Min(_settings.cardWidth, rootRect.width * 0.24f) : _settings.cardWidth;
        float height = rootRect.height > 0f ? Mathf.Min(_settings.cardHeight, rootRect.height * 0.72f) : _settings.cardHeight;

        for (int i = 0; i < _cards.Length; i++)
        {
            if (_cards[i] == null)
            {
                continue;
            }

            _cards[i].SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            _cards[i].SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        }
    }

    private Vector2 ResolveClosedPosition()
    {
        Rect rootRect = _cardRoot != null ? _cardRoot.rect : Rect.zero;
        float width = rootRect.width > 0f ? rootRect.width : 1920f;
        return new Vector2(-width * 0.68f, 0f);
    }

    private Vector2[] ResolveOpenPositions()
    {
        Rect rootRect = _cardRoot != null ? _cardRoot.rect : Rect.zero;
        float width = rootRect.width > 0f ? rootRect.width : 1920f;
        float height = rootRect.height > 0f ? rootRect.height : 1080f;
        float spacing = width * 0.21f;
        float lowerY = -height * 0.032f;
        float centerY = height * 0.028f;
        float pivotOffsetX = GetPivotVisualCenterOffsetX();

        return new[]
        {
            new Vector2(-spacing - pivotOffsetX, lowerY),
            new Vector2(-pivotOffsetX, centerY),
            new Vector2(spacing - pivotOffsetX, lowerY)
        };
    }

    private Vector2 ResolveCardButtonLocalPosition()
    {
        if (_cardRoot == null || _cardButton == null)
        {
            return Vector2.zero;
        }

        Canvas parentCanvas = _owner != null ? _owner.GetComponentInParent<Canvas>() : null;
        Camera uiCamera = parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? parentCanvas.worldCamera : null;
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera, _cardButton.position);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_cardRoot, screenPoint, uiCamera, out Vector2 localPoint);
        return localPoint;
    }

    private float GetPivotVisualCenterOffsetX()
    {
        RectTransform card = GetFirstValidCard();
        if (card == null)
        {
            return _settings.cardWidth * 0.5f * GetOpenScale();
        }

        float width = card.rect.width > 0f ? card.rect.width : _settings.cardWidth;
        return (0.5f - card.pivot.x) * width * GetOpenScale();
    }

    private RectTransform GetFirstValidCard()
    {
        if (_cards == null)
        {
            return null;
        }

        for (int i = 0; i < _cards.Length; i++)
        {
            if (_cards[i] != null)
            {
                return _cards[i];
            }
        }

        return null;
    }

    private float GetOpenScale()
    {
        Rect rootRect = _cardRoot != null ? _cardRoot.rect : Rect.zero;
        float width = rootRect.width > 0f ? rootRect.width : 1920f;
        return Mathf.Clamp(width / 1920f, _settings.minCardScale, _settings.maxCardScale);
    }
}
