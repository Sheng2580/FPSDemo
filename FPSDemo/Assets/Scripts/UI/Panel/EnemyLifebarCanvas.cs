using System.Collections.Generic;
using Enemy;
using UnityEngine;
using UnityEngine.UI;

[UICanvas(UILoadType.AssetBundle, UILayer.HUD)]
[DisallowMultipleComponent]
public class EnemyLifebarCanvas : BaseCanvas
{
    // 血条的样式 尺寸 层级 填充方式 全部在 prefab 中配置
    // 脚本只处理事件分配 世界坐标跟随 血量刷新和回收
    [Header("模板")]
    [SerializeField] private RectTransform lifebarTemplate;
    [SerializeField] private RectTransform lifebarLayer;

    [Header("对象池")]
    [SerializeField] private int prewarmCount = 24;
    [SerializeField] private int maxActiveCount = 80;

    [Header("显示时间")]
    [SerializeField] private float hideDelay = 3f;
    [SerializeField] private bool useUnscaledTimer;

    [Header("位置")]
    [SerializeField] private Vector3 fallbackWorldOffset = new Vector3(0f, 2.1f, 0f);
    [SerializeField] private float rendererBoundsPadding = 0.18f;
    [SerializeField] private Vector2 screenOffset = new Vector2(0f, 20f);
    [SerializeField] private bool hideWhenOffScreen = true;

    [Header("白条延迟")]
    [SerializeField] private float whiteDelay = 0.18f;
    [SerializeField] private float whiteCatchUpSpeed = 1.8f;

    [Header("调试")]
    [SerializeField] private bool debugLog;

    private const string TemplateName = "Slider";
    private const string FillRedName = "FillRed";
    private const string FillWhiteName = "Fillwhite";
    private const string FillWhiteAltName = "FillWhite";
    private const string TimerPrefix = "EnemyLifebar_";

    private readonly Dictionary<EnemyController, LifebarView> _activeViews = new Dictionary<EnemyController, LifebarView>(64);
    private readonly Queue<LifebarView> _freeViews = new Queue<LifebarView>(32);
    private readonly List<EnemyController> _removeBuffer = new List<EnemyController>(16);
    private readonly HashSet<string> _activeTimerIds = new HashSet<string>();

    private Canvas _canvas;
    private RectTransform _canvasRect;
    private Camera _worldCamera;
    private Camera _uiCamera;
    private int _nextViewId;

    public override bool NeedRaycaster => false;

    protected override void Reset()
    {
        base.Reset();
        CacheReferences();
    }

    public override void Awake()
    {
        base.Awake();
        CacheReferences();
        PrepareTemplate();
        PrewarmPool();
    }

    private void OnEnable()
    {
        EventCenter.Instance.AddEventListener<EnemyDamagedEventData>(GameEvent.EnemyDamaged, OnEnemyDamaged);
        EventCenter.Instance.AddEventListener<EnemyDiedEventData>(GameEvent.EnemyDied, OnEnemyDied);
        EventCenter.Instance.AddEventListener<EnemyReturnedToPoolEventData>(GameEvent.EnemyReturnedToPool, OnEnemyReturnedToPool);
    }

    private void OnDisable()
    {
        EventCenter.Instance.RemoveEventListener<EnemyDamagedEventData>(GameEvent.EnemyDamaged, OnEnemyDamaged);
        EventCenter.Instance.RemoveEventListener<EnemyDiedEventData>(GameEvent.EnemyDied, OnEnemyDied);
        EventCenter.Instance.RemoveEventListener<EnemyReturnedToPoolEventData>(GameEvent.EnemyReturnedToPool, OnEnemyReturnedToPool);
        RecycleAll();
    }

    private void LateUpdate()
    {
        UpdateCameras();
        float deltaTime = Time.unscaledDeltaTime;

        // 遍历期间不直接修改字典 失效对象先放入缓存
        foreach (KeyValuePair<EnemyController, LifebarView> pair in _activeViews)
        {
            EnemyController enemy = pair.Key;
            LifebarView view = pair.Value;
            if (enemy == null || !enemy.IsActive || enemy.IsDead)
            {
                _removeBuffer.Add(enemy);
                continue;
            }

            UpdateViewPosition(enemy, view);
            view.Tick(deltaTime, whiteCatchUpSpeed);
        }

        for (int i = 0; i < _removeBuffer.Count; i++)
        {
            RecycleByEnemy(_removeBuffer[i]);
        }

        _removeBuffer.Clear();
    }

    private void OnEnemyDamaged(EnemyDamagedEventData eventData)
    {
        EnemyController enemy = eventData.enemy;
        if (enemy == null || eventData.maxHealth <= 0f)
        {
            return;
        }

        LifebarView view = GetOrCreateView(enemy);
        if (view == null)
        {
            return;
        }

        float healthRate = Mathf.Clamp01(eventData.currentHealth / eventData.maxHealth);
        view.SetHealth(healthRate, whiteDelay);
        RestartHideTimer(enemy);

        if (debugLog)
        {
            Debug.Log($"[EnemyLifebar] 显示血条 {enemy.EnemyName} {eventData.currentHealth:0.#}/{eventData.maxHealth:0.#}", this);
        }
    }

    private void OnEnemyDied(EnemyDiedEventData eventData)
    {
        RecycleByEnemy(eventData.enemy);
    }

    private void OnEnemyReturnedToPool(EnemyReturnedToPoolEventData eventData)
    {
        RecycleByEnemy(eventData.enemy);
    }

    private LifebarView GetOrCreateView(EnemyController enemy)
    {
        if (_activeViews.TryGetValue(enemy, out LifebarView activeView))
        {
            return activeView;
        }

        if (_activeViews.Count >= maxActiveCount)
        {
            return null;
        }

        LifebarView view = TakeFromPool();
        if (view == null)
        {
            return null;
        }

        Transform anchor = ResolveAnchor(enemy);
        Vector3 worldOffset = anchor != null
            ? new Vector3(fallbackWorldOffset.x, rendererBoundsPadding, fallbackWorldOffset.z)
            : ResolveWorldOffset(enemy);

        view.Bind(enemy, anchor, worldOffset);
        _activeViews.Add(enemy, view);
        return view;
    }

    private LifebarView TakeFromPool()
    {
        LifebarView view = _freeViews.Count > 0 ? _freeViews.Dequeue() : CreateView();
        view.SetActive(true);
        return view;
    }

    private LifebarView CreateView()
    {
        if (lifebarTemplate == null)
        {
            return null;
        }

        RectTransform instance = Instantiate(lifebarTemplate, lifebarLayer);
        instance.name = $"EnemyLifebar_{_nextViewId++:00}";
        LifebarView view = new LifebarView(instance);
        return view;
    }

    private void PrewarmPool()
    {
        if (lifebarTemplate == null)
        {
            return;
        }

        int count = Mathf.Max(0, prewarmCount);
        for (int i = 0; i < count; i++)
        {
            LifebarView view = CreateView();
            if (view == null)
            {
                break;
            }

            view.SetActive(false);
            _freeViews.Enqueue(view);
        }
    }

    private void RecycleByEnemy(EnemyController enemy)
    {
        if (enemy == null || !_activeViews.TryGetValue(enemy, out LifebarView view))
        {
            return;
        }

        _activeViews.Remove(enemy);
        ClearHideTimer(enemy);
        view.Release();
        _freeViews.Enqueue(view);
    }

    private void RecycleAll()
    {
        foreach (KeyValuePair<EnemyController, LifebarView> pair in _activeViews)
        {
            ClearHideTimer(pair.Key);
            pair.Value.Release();
            _freeViews.Enqueue(pair.Value);
        }

        _activeViews.Clear();
        _removeBuffer.Clear();
    }

    private void RestartHideTimer(EnemyController enemy)
    {
        if (enemy == null || hideDelay <= 0f)
        {
            return;
        }

        string timerId = GetTimerId(enemy);
        MultiTimerManager timerManager = MultiTimerManager.Instance;
        if (timerManager == null)
        {
            return;
        }

        Timer timer = timerManager.CreateTimer(timerId, useUnscaledTimer);
        _activeTimerIds.Add(timerId);
        timer.SetTargetTime(hideDelay);
        timer.OnTimeUp += () => RecycleByEnemy(enemy);
        timer.Start();
    }

    private void ClearHideTimer(EnemyController enemy)
    {
        if (enemy == null)
        {
            return;
        }

        string timerId = GetTimerId(enemy);
        if (!_activeTimerIds.Remove(timerId))
        {
            return;
        }

        MultiTimerManager timerManager = MultiTimerManager.Instance;
        if (timerManager != null)
        {
            timerManager.RemoveTimer(timerId);
        }
    }

    private string GetTimerId(EnemyController enemy)
    {
        return $"{TimerPrefix}{enemy.GetInstanceID()}";
    }

    private void UpdateViewPosition(EnemyController enemy, LifebarView view)
    {
        if (_canvasRect == null || _worldCamera == null)
        {
            view.SetVisible(false);
            return;
        }

        Vector3 worldPosition = view.GetWorldPosition(enemy.transform);
        Vector3 screenPosition = _worldCamera.WorldToScreenPoint(worldPosition);
        if (screenPosition.z <= 0.01f)
        {
            view.SetVisible(false);
            return;
        }

        if (hideWhenOffScreen && IsOutsideScreen(screenPosition))
        {
            view.SetVisible(false);
            return;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvasRect,
            screenPosition,
            _uiCamera,
            out Vector2 localPosition);

        view.RectTransform.anchoredPosition = localPosition + screenOffset;
        view.SetVisible(true);
    }

    private bool IsOutsideScreen(Vector3 screenPosition)
    {
        return screenPosition.x < 0f ||
               screenPosition.x > Screen.width ||
               screenPosition.y < 0f ||
               screenPosition.y > Screen.height;
    }

    private Transform ResolveAnchor(EnemyController enemy)
    {
        Transform root = enemy.transform;
        Transform anchor = FindChildByName(root, "LifebarAnchor");
        anchor ??= FindChildByName(root, "LifeBarAnchor");
        anchor ??= FindChildByName(root, "HpAnchor");
        anchor ??= FindChildByName(root, "HealthBarAnchor");
        anchor ??= FindChildByName(root, "Head");
        anchor ??= FindChildByName(root, "head");
        return anchor;
    }

    private Transform FindChildByName(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null && children[i].name == childName)
            {
                return children[i];
            }
        }

        return null;
    }

    private Vector3 ResolveWorldOffset(EnemyController enemy)
    {
        float height = fallbackWorldOffset.y;
        Renderer[] renderers = enemy.GetComponentsInChildren<Renderer>(false);
        bool hasBounds = false;
        Bounds bounds = default;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer targetRenderer = renderers[i];
            if (targetRenderer == null || !targetRenderer.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = targetRenderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(targetRenderer.bounds);
            }
        }

        if (hasBounds)
        {
            // 没有锚点时使用渲染包围盒估算头顶位置
            height = Mathf.Max(0.2f, bounds.max.y - enemy.transform.position.y + rendererBoundsPadding);
        }

        return new Vector3(fallbackWorldOffset.x, height, fallbackWorldOffset.z);
    }

    private void CacheReferences()
    {
        _canvas = GetComponentInParent<Canvas>();
        if (_canvas == null)
        {
            _canvas = GetComponent<Canvas>();
        }

        _canvasRect = _canvas != null ? _canvas.transform as RectTransform : transform as RectTransform;
        lifebarLayer ??= transform as RectTransform;

        if (lifebarTemplate == null)
        {
            Transform template = transform.Find(TemplateName);
            if (template != null)
            {
                lifebarTemplate = template as RectTransform;
            }
        }

        if (lifebarTemplate != null && lifebarLayer == null)
        {
            lifebarLayer = lifebarTemplate.parent as RectTransform;
        }

        UpdateCameras();
    }

    private void UpdateCameras()
    {
        if (_worldCamera == null)
        {
            _worldCamera = Camera.main;
        }

        if (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            _uiCamera = _canvas.worldCamera;
        }
        else
        {
            _uiCamera = null;
        }
    }

    private void PrepareTemplate()
    {
        if (lifebarTemplate == null)
        {
            Debug.LogWarning("[EnemyLifebar] 没有找到 Slider 模板", this);
            return;
        }

        lifebarTemplate.gameObject.SetActive(false);
    }

    private static void SetHorizontalFill(Image image, float amount)
    {
        if (image == null)
        {
            return;
        }

        amount = Mathf.Clamp01(amount);
        image.fillAmount = amount;
    }

    private sealed class LifebarView
    {
        private readonly RectTransform _rectTransform;
        private readonly Image _fillRed;
        private readonly Image _fillWhite;
        private readonly CanvasGroup _canvasGroup;

        private Transform _anchor;
        private Vector3 _worldOffset;
        private float _redAmount = 1f;
        private float _whiteAmount = 1f;
        private float _whiteDelayTimer;

        public RectTransform RectTransform => _rectTransform;

        public LifebarView(RectTransform rectTransform)
        {
            _rectTransform = rectTransform;
            _fillRed = FindImageInView(rectTransform, FillRedName);
            _fillWhite = FindImageInView(rectTransform, FillWhiteName) ?? FindImageInView(rectTransform, FillWhiteAltName);
            _canvasGroup = rectTransform.GetComponent<CanvasGroup>();
        }

        public void Bind(EnemyController enemy, Transform anchor, Vector3 worldOffset)
        {
            _anchor = anchor;
            _worldOffset = worldOffset;
            _redAmount = 1f;
            _whiteAmount = 1f;
            _whiteDelayTimer = 0f;
            ApplyFill();
            SetVisible(enemy != null);
        }

        public void SetHealth(float healthRate, float whiteDelay)
        {
            healthRate = Mathf.Clamp01(healthRate);
            if (healthRate >= _redAmount)
            {
                // 回血或重置时红白条同步 避免白条反向追赶
                _redAmount = healthRate;
                _whiteAmount = healthRate;
                _whiteDelayTimer = 0f;
                ApplyFill();
                return;
            }

            // 受伤时红条立刻变化 白条延迟追上
            _redAmount = healthRate;
            _whiteDelayTimer = Mathf.Max(0f, whiteDelay);
            ApplyFill();
        }

        public void Tick(float deltaTime, float catchUpSpeed)
        {
            if (_whiteDelayTimer > 0f)
            {
                _whiteDelayTimer = Mathf.Max(0f, _whiteDelayTimer - deltaTime);
                return;
            }

            if (Mathf.Approximately(_whiteAmount, _redAmount))
            {
                return;
            }

            _whiteAmount = Mathf.MoveTowards(_whiteAmount, _redAmount, Mathf.Max(0.01f, catchUpSpeed) * deltaTime);
            ApplyFill();
        }

        public Vector3 GetWorldPosition(Transform fallback)
        {
            if (_anchor != null)
            {
                return _anchor.position + _worldOffset;
            }

            return fallback != null ? fallback.position + _worldOffset : _worldOffset;
        }

        public void SetVisible(bool isVisible)
        {
            if (_canvasGroup == null)
            {
                return;
            }

            _canvasGroup.alpha = isVisible ? 1f : 0f;
        }

        public void SetActive(bool isActive)
        {
            if (_rectTransform != null)
            {
                _rectTransform.gameObject.SetActive(isActive);
            }
        }

        public void Release()
        {
            _anchor = null;
            _worldOffset = Vector3.zero;
            _redAmount = 1f;
            _whiteAmount = 1f;
            _whiteDelayTimer = 0f;
            ApplyFill();
            SetVisible(false);
            SetActive(false);
        }

        private void ApplyFill()
        {
            if (_fillRed != null)
            {
                SetHorizontalFill(_fillRed, _redAmount);
            }

            if (_fillWhite != null)
            {
                SetHorizontalFill(_fillWhite, _whiteAmount);
            }
        }

        private static Image FindImageInView(RectTransform root, string imageName)
        {
            if (root == null)
            {
                return null;
            }

            Transform[] children = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                Transform child = children[i];
                if (child != null && child.name == imageName)
                {
                    return child.GetComponent<Image>();
                }
            }

            return null;
        }
    }
}
