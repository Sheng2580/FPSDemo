using System.Collections.Generic;
using Combat;
using Enemy;
using UnityEngine;

/// <summary>
/// 伤害数字中心
/// 只负责伤害数字对象池和屏幕位置表现
/// </summary>
[DisallowMultipleComponent]
public class DamageCentre : MonoBehaviour
{
    [Header("模板")]
    [SerializeField] private DamageNumberItem normalDamage;
    [SerializeField] private DamageNumberItem knockingDamage;
    [SerializeField] private RectTransform damageLayer;
    [SerializeField] private EnergyCentre energyCentre;

    [Header("对象池")]
    [SerializeField] private int normalPrewarmCount = 24;
    [SerializeField] private int criticalPrewarmCount = 12;
    [SerializeField] private int maxActiveCount = 80;

    [Header("生成位置")]
    [SerializeField] private Vector2 screenOffset = new Vector2(0f, 36f);
    [SerializeField] private float randomOffsetRadius = 22f;
    [SerializeField] private bool hideWhenOffScreen = true;

    [Header("跟随")]
    [SerializeField] private bool followEnemyDuringStay = true;
    [SerializeField] private Vector3 enemyFallbackOffset = new Vector3(0f, 1.6f, 0f);
    [SerializeField] private float maxHitPointDistanceFromEnemy = 3f;

    [Header("调试")]
    [SerializeField] private bool debugLog;

    private readonly Queue<DamageNumberItem> _normalPool = new Queue<DamageNumberItem>(32);
    private readonly Queue<DamageNumberItem> _criticalPool = new Queue<DamageNumberItem>(16);
    private readonly List<DamageNumberItem> _activeItems = new List<DamageNumberItem>(64);
    private readonly Dictionary<DamageNumberItem, FollowTarget> _followTargets = new Dictionary<DamageNumberItem, FollowTarget>(64);

    private RectTransform _rootRect;
    private Canvas _canvas;
    private Camera _worldCamera;
    private Camera _uiCamera;
    private bool _warnedMissingCamera;
    private bool _warnedMissingLayer;
    private bool _warnedMissingTemplate;

    private struct FollowTarget
    {
        public EnemyController enemy;
        public Vector3 localPosition;
        public Vector3 fallbackWorldPosition;
        public Vector2 uiOffset;

        public FollowTarget(EnemyController enemy, Vector3 worldPosition, Vector2 uiOffset)
        {
            this.enemy = enemy;
            fallbackWorldPosition = worldPosition;
            this.uiOffset = uiOffset;
            localPosition = enemy != null ? enemy.transform.InverseTransformPoint(worldPosition) : Vector3.zero;
        }
    }

    private void Awake()
    {
        CacheReferences();
        PrepareTemplates();
        PrewarmPool(normalDamage, normalPrewarmCount, _normalPool);
        PrewarmPool(knockingDamage, criticalPrewarmCount, _criticalPool);
    }

    private void Reset()
    {
        CacheReferences();
    }

    private void OnEnable()
    {
        EventCenter.Instance.AddEventListener<EnemyDamagedEventData>(GameEvent.EnemyDamaged, OnEnemyDamaged);
    }

    private void OnDisable()
    {
        EventCenter.Instance.RemoveEventListener<EnemyDamagedEventData>(GameEvent.EnemyDamaged, OnEnemyDamaged);
        RecycleAll();
    }

    private void LateUpdate()
    {
        if (!followEnemyDuringStay || _followTargets.Count <= 0)
        {
            return;
        }

        UpdateCameras();
        for (int i = _activeItems.Count - 1; i >= 0; i--)
        {
            DamageNumberItem item = _activeItems[i];
            if (item == null)
            {
                continue;
            }

            if (!item.IsFollowing)
            {
                _followTargets.Remove(item);
                continue;
            }

            if (!_followTargets.TryGetValue(item, out FollowTarget followTarget))
            {
                continue;
            }

            if (TryResolveFollowPosition(followTarget, out Vector2 localPosition))
            {
                item.SetFollowPosition(localPosition);
            }
        }
    }

    private void OnEnemyDamaged(EnemyDamagedEventData eventData)
    {
        CacheReferences();

        DamageInfo damageInfo = eventData.damageInfo;
        if (damageInfo.finalDamage <= 0f)
        {
            LogDebug("收到伤害事件 但最终伤害为 0");
            return;
        }

        if (!TryResolveSpawnPosition(eventData.enemy, damageInfo, out Vector2 startPosition, out FollowTarget followTarget))
        {
            LogDebug("收到伤害事件 但没有算出屏幕位置");
            return;
        }

        Vector2 targetPosition = ResolveEnergyPosition();
        DamageNumberItem item = TakeItem(damageInfo.isCritical);
        if (item == null)
        {
            WarnMissingTemplate();
            return;
        }

        item.Play(
            startPosition,
            targetPosition,
            damageInfo.finalDamage,
            damageInfo.isCritical,
            RecycleItem);

        if (followEnemyDuringStay && eventData.enemy != null)
        {
            _followTargets[item] = followTarget;
        }

      
    }

    private DamageNumberItem TakeItem(bool isCritical)
    {
        Queue<DamageNumberItem> pool = isCritical ? _criticalPool : _normalPool;
        DamageNumberItem template = isCritical ? knockingDamage : normalDamage;

        if (_activeItems.Count >= maxActiveCount && _activeItems.Count > 0)
        {
            RecycleItem(_activeItems[0]);
        }

        DamageNumberItem item = pool.Count > 0 ? pool.Dequeue() : CreateItem(template);
        if (item == null)
        {
            return null;
        }

        _activeItems.Add(item);
        return item;
    }

    private DamageNumberItem CreateItem(DamageNumberItem template)
    {
        if (template == null || damageLayer == null)
        {
            return null;
        }

        DamageNumberItem item = Instantiate(template, damageLayer);
        item.gameObject.SetActive(false);
        return item;
    }

    private void RecycleItem(DamageNumberItem item)
    {
        if (item == null)
        {
            return;
        }

        bool wasCritical = item.IsCritical;
        _activeItems.Remove(item);
        _followTargets.Remove(item);
        item.Release();

        if (wasCritical)
        {
            _criticalPool.Enqueue(item);
        }
        else
        {
            _normalPool.Enqueue(item);
        }
    }

    private void RecycleAll()
    {
        for (int i = _activeItems.Count - 1; i >= 0; i--)
        {
            RecycleItem(_activeItems[i]);
        }

        _followTargets.Clear();
    }

    private void PrewarmPool(DamageNumberItem template, int count, Queue<DamageNumberItem> pool)
    {
        if (template == null || pool == null)
        {
            return;
        }

        for (int i = 0; i < Mathf.Max(0, count); i++)
        {
            DamageNumberItem item = CreateItem(template);
            if (item == null)
            {
                break;
            }

            pool.Enqueue(item);
        }
    }

    private bool TryResolveSpawnPosition(EnemyController enemy, DamageInfo damageInfo, out Vector2 localPosition, out FollowTarget followTarget)
    {
        localPosition = Vector2.zero;
        followTarget = default;
        UpdateCameras();

        if (_worldCamera == null || damageLayer == null)
        {
            WarnMissingSetup();
            return false;
        }

        Vector3 worldPosition = ResolveWorldPosition(enemy, damageInfo);
        Vector2 uiOffset = screenOffset + Random.insideUnitCircle * randomOffsetRadius;
        followTarget = new FollowTarget(enemy, worldPosition, uiOffset);
        return TryWorldToLocalPosition(worldPosition, uiOffset, out localPosition);
    }

    private bool TryResolveFollowPosition(FollowTarget followTarget, out Vector2 localPosition)
    {
        localPosition = Vector2.zero;
        if (_worldCamera == null || damageLayer == null)
        {
            return false;
        }

        Vector3 worldPosition = followTarget.fallbackWorldPosition;
        if (followTarget.enemy != null && followTarget.enemy.gameObject.activeInHierarchy)
        {
            worldPosition = followTarget.enemy.transform.TransformPoint(followTarget.localPosition);
        }

        return TryWorldToLocalPosition(worldPosition, followTarget.uiOffset, out localPosition);
    }

    private bool TryWorldToLocalPosition(Vector3 worldPosition, Vector2 uiOffset, out Vector2 localPosition)
    {
        localPosition = Vector2.zero;
        Vector3 screenPosition = _worldCamera.WorldToScreenPoint(worldPosition);
        if (screenPosition.z <= 0.01f)
        {
            return false;
        }

        if (hideWhenOffScreen && IsOutsideScreen(screenPosition))
        {
            return false;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            damageLayer,
            screenPosition,
            _uiCamera,
            out localPosition);

        localPosition += uiOffset;
        return true;
    }

    private Vector3 ResolveWorldPosition(EnemyController enemy, DamageInfo damageInfo)
    {
        if (enemy != null)
        {
            if (IsValidEnemyHitPoint(enemy, damageInfo.hitPoint))
            {
                return damageInfo.hitPoint;
            }

            Transform anchor = ResolveEnemyAnchor(enemy);
            if (anchor != null)
            {
                return anchor.position;
            }

            return enemy.transform.position + enemyFallbackOffset;
        }

        if (damageInfo.hitPoint.sqrMagnitude > 0.0001f)
        {
            return damageInfo.hitPoint;
        }

        return Vector3.zero;
    }

    private bool IsValidEnemyHitPoint(EnemyController enemy, Vector3 hitPoint)
    {
        if (enemy == null || hitPoint.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        float maxDistance = Mathf.Max(0.1f, maxHitPointDistanceFromEnemy);
        return Vector3.SqrMagnitude(hitPoint - enemy.transform.position) <= maxDistance * maxDistance;
    }

    private Transform ResolveEnemyAnchor(EnemyController enemy)
    {
        Transform root = enemy.transform;
        Transform anchor = FindChildByName(root, "DamageNumberAnchor");
        anchor ??= FindChildByName(root, "HitNumberAnchor");
        anchor ??= FindChildByName(root, "LifebarAnchor");
        anchor ??= FindChildByName(root, "LifeBarAnchor");
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
            Transform child = children[i];
            if (child != null && child.name == childName)
            {
                return child;
            }
        }

        return null;
    }

    private Vector2 ResolveEnergyPosition()
    {
        if (energyCentre != null && energyCentre.TryGetLocalPosition(damageLayer, _uiCamera, out Vector2 energyPosition))
        {
            return energyPosition;
        }

        return Vector2.zero;
    }

    private bool IsOutsideScreen(Vector3 screenPosition)
    {
        return screenPosition.x < 0f ||
               screenPosition.x > Screen.width ||
               screenPosition.y < 0f ||
               screenPosition.y > Screen.height;
    }

    private void PrepareTemplates()
    {
        if (normalDamage != null)
        {
            normalDamage.gameObject.SetActive(false);
        }

        if (knockingDamage != null)
        {
            knockingDamage.gameObject.SetActive(false);
        }
    }

    private void CacheReferences()
    {
        _canvas = GetComponentInParent<Canvas>(true);
        _rootRect = transform as RectTransform;
        damageLayer ??= _rootRect;
        energyCentre ??= GetComponentInParent<HUDCanvas>()?.EnergyCentre;

        if (normalDamage == null)
        {
            normalDamage = FindTemplate("NormalDamage");
        }

        if (knockingDamage == null)
        {
            knockingDamage = FindTemplate("KnockingDamage");
        }

        UpdateCameras();
    }

    private DamageNumberItem FindTemplate(string templateName)
    {
        Transform target = transform.Find(templateName);
        if (target == null)
        {
            return null;
        }

        return target.GetComponent<DamageNumberItem>();
    }

    private void UpdateCameras()
    {
        if (_worldCamera == null || !_worldCamera.isActiveAndEnabled)
        {
            _worldCamera = ResolveWorldCamera();
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

    private Camera ResolveWorldCamera()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera != null && mainCamera.isActiveAndEnabled)
        {
            return mainCamera;
        }

        Camera[] cameras = Camera.allCameras;
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera != null && camera.isActiveAndEnabled && camera.targetTexture == null)
            {
                return camera;
            }
        }

        return null;
    }

    private void WarnMissingSetup()
    {
        if (!debugLog || _warnedMissingCamera && _warnedMissingLayer)
        {
            return;
        }

        if (_worldCamera == null && !_warnedMissingCamera)
        {
            _warnedMissingCamera = true;
            Debug.LogWarning("[HUDDamage] 没有找到可用相机 伤害数字无法转换屏幕坐标", this);
        }

        if (damageLayer == null && !_warnedMissingLayer)
        {
            _warnedMissingLayer = true;
            Debug.LogWarning("[HUDDamage] 缺少 DamageLayer 伤害数字无法挂载", this);
        }
    }

    private void WarnMissingTemplate()
    {
        if (!debugLog || _warnedMissingTemplate)
        {
            return;
        }

        _warnedMissingTemplate = true;
        Debug.LogWarning("[HUDDamage] 缺少 NormalDamage 或 KnockingDamage 模板 伤害数字无法生成", this);
    }

    private void LogDebug(string message)
    {
        if (debugLog)
        {
            Debug.Log($"[HUDDamage] {message}", this);
        }
    }
}
