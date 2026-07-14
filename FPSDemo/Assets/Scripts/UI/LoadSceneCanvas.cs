using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>
/// 场景异步加载遮罩
/// 在持久化 UI 根节点上覆盖场景切换过程
/// </summary>
[UICanvas(UILoadType.AssetBundle, UILayer.System, UseSafeArea = false)]
[DisallowMultipleComponent]
public sealed class LoadSceneCanvas : BaseCanvas
{
    [Header("淡入淡出")]
    [SerializeField] private float fadeInDuration = 0.22f;
    [SerializeField] private float fadeOutDuration = 0.28f;
    [SerializeField] private float minimumVisibleDuration = 0.65f;

    private Coroutine _loadRoutine;
    private Tween _fadeTween;

    public bool IsLoading => _loadRoutine != null;
    public override bool UseSafeArea => false;
    public override bool NeedRaycaster => true;

    public static void LoadSceneWithTransition(string sceneName, UnityAction onSceneLoaded = null)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return;
        }

        UIManager uiManager = UIManager.Instance;
        if (uiManager == null)
        {
            LoadSceneDirect(sceneName, onSceneLoaded);
            return;
        }

        uiManager.OpenPanelAsy<LoadSceneCanvas>(loadingCanvas =>
        {
            if (loadingCanvas != null)
            {
                loadingCanvas.LoadSceneAsync(sceneName, onSceneLoaded);
                return;
            }

            Debug.LogError($"[LoadSceneCanvas] 加载面板失败 使用无遮罩异步切换 {sceneName}");
            LoadSceneDirect(sceneName, onSceneLoaded);
        });
    }

    public override void Show()
    {
        base.Show();
        KillFade();
        if (canvasGroup == null)
        {
            return;
        }

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = true;
    }

    private void OnDisable()
    {
        KillFade();
        if (_loadRoutine != null)
        {
            StopCoroutine(_loadRoutine);
            _loadRoutine = null;
        }
    }

    public void LoadSceneAsync(string sceneName, UnityAction onSceneLoaded = null)
    {
        if (_loadRoutine != null || string.IsNullOrWhiteSpace(sceneName))
        {
            return;
        }

        _loadRoutine = StartCoroutine(LoadSceneRoutine(sceneName, onSceneLoaded));
    }

    private IEnumerator LoadSceneRoutine(string sceneName, UnityAction onSceneLoaded)
    {
        float showStartedAt = Time.unscaledTime;
        yield return FadeTo(1f, fadeInDuration, Ease.OutQuad);

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        if (operation == null)
        {
            Debug.LogError($"[LoadSceneCanvas] 无法异步加载场景 {sceneName}", this);
            yield return FadeTo(0f, fadeOutDuration, Ease.InQuad);
            FinishLoading();
            yield break;
        }

        operation.allowSceneActivation = false;
        while (operation.progress < 0.9f)
        {
            yield return null;
        }

        float remainingVisibleTime = minimumVisibleDuration - (Time.unscaledTime - showStartedAt);
        if (remainingVisibleTime > 0f)
        {
            yield return new WaitForSecondsRealtime(remainingVisibleTime);
        }

        operation.allowSceneActivation = true;
        while (!operation.isDone)
        {
            yield return null;
        }

        // 等待新场景完成首帧初始化后再移除遮罩
        yield return null;
        onSceneLoaded?.Invoke();
        yield return FadeTo(0f, fadeOutDuration, Ease.InQuad);
        FinishLoading();
    }

    private IEnumerator FadeTo(float alpha, float duration, Ease ease)
    {
        KillFade();
        if (canvasGroup == null || duration <= 0f)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = alpha;
            }

            yield break;
        }

        _fadeTween = canvasGroup
            .DOFade(alpha, duration)
            .SetEase(ease)
            .SetUpdate(true);
        yield return _fadeTween.WaitForCompletion();
        _fadeTween = null;
    }

    private void FinishLoading()
    {
        _loadRoutine = null;
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ClosePanel<LoadSceneCanvas>();
            return;
        }

        Hide();
    }

    private void KillFade()
    {
        _fadeTween?.Kill();
        _fadeTween = null;
    }

    private static void LoadSceneDirect(string sceneName, UnityAction onSceneLoaded)
    {
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        if (operation == null)
        {
            Debug.LogError($"[LoadSceneCanvas] 无法异步加载场景 {sceneName}");
            return;
        }

        operation.completed += _ => onSceneLoaded?.Invoke();
    }
}
