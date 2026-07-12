using System;
using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// 单个伤害数字表现
/// 外观由模板对象配置 脚本只负责写数字和播放动画
/// </summary>
[DisallowMultipleComponent]
public class DamageNumberItem : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private TMP_Text damageText;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform rectTransform;

    [Header("出现动画")]
    [SerializeField] private float spawnDuration = 0.16f;
    [SerializeField] private float spawnStartScale = 0.55f;
    [SerializeField] private float spawnEndScale = 1f;
    [SerializeField] private Ease spawnEase = Ease.OutBack;

    [Header("停留和飞行")]
    [SerializeField] private float stayDuration = 0.28f;
    [SerializeField] private float flyDuration = 0.42f;
    [SerializeField] private float flyEndScale = 0.45f;
    [SerializeField] private float flyEndAlpha = 0.22f;
    [SerializeField] private float releaseFadeDuration = 0.08f;
    [SerializeField] private Ease flyEase = Ease.InQuad;

    private DG.Tweening.Sequence _sequence;
    private Action<DamageNumberItem> _onComplete;
    private bool _isCritical;
    private bool _isFollowing;

    public RectTransform RectTransform => rectTransform;
    public bool IsCritical => _isCritical;
    public bool IsFollowing => _isFollowing;

    private void Awake()
    {
        CacheReferences();
    }

    private void Reset()
    {
        CacheReferences();
    }

    private void OnDisable()
    {
        KillTween();
    }

    public void Play(
        Vector2 startPosition,
        Vector2 targetPosition,
        float damage,
        bool isCritical,
        Action<DamageNumberItem> onComplete)
    {
        CacheReferences();
        KillTween();

        _isCritical = isCritical;
        _isFollowing = true;
        _onComplete = onComplete;

        gameObject.SetActive(true);
        rectTransform.anchoredPosition = startPosition;
        rectTransform.localScale = Vector3.one * spawnStartScale;
        SetAlpha(0f);
        SetDamageText(damage);

        // 先放大淡入 停留后飞向能量文字
        _sequence = DOTween.Sequence();
        _sequence.SetUpdate(false);
        _sequence.Append(canvasGroup.DOFade(1f, spawnDuration));
        _sequence.Join(rectTransform.DOScale(spawnEndScale, spawnDuration).SetEase(spawnEase));
        _sequence.AppendInterval(stayDuration);
        _sequence.AppendCallback(() => _isFollowing = false);
        _sequence.Append(rectTransform.DOAnchorPos(targetPosition, flyDuration).SetEase(flyEase));
        _sequence.Join(rectTransform.DOScale(flyEndScale, flyDuration));
        _sequence.Join(canvasGroup.DOFade(flyEndAlpha, flyDuration));
        _sequence.Append(canvasGroup.DOFade(0f, releaseFadeDuration));
        _sequence.OnComplete(Complete);
    }

    public void Release()
    {
        KillTween();
        _onComplete = null;
        _isCritical = false;
        _isFollowing = false;
        SetAlpha(0f);
        gameObject.SetActive(false);
    }

    public void SetFollowPosition(Vector2 anchoredPosition)
    {
        CacheReferences();
        if (rectTransform != null && _isFollowing)
        {
            rectTransform.anchoredPosition = anchoredPosition;
        }
    }

    private void Complete()
    {
        Action<DamageNumberItem> callback = _onComplete;
        _onComplete = null;
        _isFollowing = false;
        callback?.Invoke(this);
    }

    private void SetDamageText(float damage)
    {
        if (damageText == null)
        {
            return;
        }

        float safeDamage = Mathf.Max(0f, damage);
        damageText.text = Mathf.Approximately(safeDamage % 1f, 0f)
            ? safeDamage.ToString("0")
            : safeDamage.ToString("0.#");
    }

    private void SetAlpha(float alpha)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = Mathf.Clamp01(alpha);
        }
    }

    private void KillTween()
    {
        if (_sequence == null)
        {
            return;
        }

        _sequence.Kill();
        _sequence = null;
    }

    private void CacheReferences()
    {
        rectTransform ??= transform as RectTransform;
        canvasGroup ??= GetComponent<CanvasGroup>();
        canvasGroup ??= gameObject.AddComponent<CanvasGroup>();
        damageText ??= GetComponentInChildren<TMP_Text>(true);
    }
}
