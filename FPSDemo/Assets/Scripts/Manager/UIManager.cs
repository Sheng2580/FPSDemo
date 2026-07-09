using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// UI 面板层级
/// </summary>
public enum UILayer
{
    HUD,
    Touch,
    Normal,
    Popup,
    Tip,
    System
}

/// <summary>
/// 项目统一 UI 管理器
/// </summary>
public class UIManager : UnitySingleTonMono<UIManager>
{
    private const string RootPrefabPath = "UI/UI_Root";

    private readonly Dictionary<UILayer, int> layerBaseOrders = new Dictionary<UILayer, int>
    {
        { UILayer.HUD, 100 },
        { UILayer.Touch, 200 },
        { UILayer.Normal, 300 },
        { UILayer.Popup, 500 },
        { UILayer.Tip, 700 },
        { UILayer.System, 900 }
    };

    private readonly Dictionary<string, Queue<BaseCanvas>> uiPool = new Dictionary<string, Queue<BaseCanvas>>();
    private readonly Dictionary<string, BaseCanvas> openedCanvases = new Dictionary<string, BaseCanvas>();
    private readonly Dictionary<string, List<UnityAction<BaseCanvas>>> loadingCallbacks = new Dictionary<string, List<UnityAction<BaseCanvas>>>();
    private readonly Dictionary<string, int> loadingVersions = new Dictionary<string, int>();
    private readonly Dictionary<Type, CanvasConfig> canvasConfigs = new Dictionary<Type, CanvasConfig>();

    private GameObject rootObj;
    private Canvas rootCanvas;
    private RectTransform safeAreaRoot;
    private RectTransform fullScreenRoot;
    private readonly Dictionary<UILayer, RectTransform> layerRoots = new Dictionary<UILayer, RectTransform>();

    private readonly Vector2 designResolution = new Vector2(1920, 1080);
    private bool isInitialized;

    private struct CanvasConfig
    {
        public UILayer Layer;
        public UILoadType LoadType;
        public string AssetBundleName;
        public string AssetName;
        public string ResourcesPath;
        public bool UseSafeArea;
    }

    public override void Awake()
    {
        base.Awake();
        if (Instance != this)
        {
            return;
        }

        EnsureUIRoot();
    }

    public void EnsureUIRoot()
    {
        if (isInitialized && rootObj != null)
        {
            return;
        }

        rootObj = GameObject.Find("UI_Root");
        if (rootObj == null)
        {
            CreateOrLoadRoot();
        }
        else
        {
            SetupRoot(rootObj);
        }

        EnsureEventSystem();
        isInitialized = rootObj != null;
    }

    private void CreateOrLoadRoot()
    {
        GameObject rootPrefab = Resources.Load<GameObject>(RootPrefabPath);
        rootObj = rootPrefab != null
            ? Instantiate(rootPrefab)
            : new GameObject("UI_Root", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));

        rootObj.name = "UI_Root";
        SetupRoot(rootObj);
    }

    private void SetupRoot(GameObject uiRoot)
    {
        rootObj = uiRoot;

        RectTransform rootRect = rootObj.transform as RectTransform;
        if (rootRect == null)
        {
            rootRect = rootObj.AddComponent<RectTransform>();
        }

        StretchRect(rootRect);

        rootCanvas = rootObj.GetComponent<Canvas>();
        if (rootCanvas == null)
        {
            rootCanvas = rootObj.AddComponent<Canvas>();
        }

        rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        rootCanvas.pixelPerfect = true;

        CanvasScaler scaler = rootObj.GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = rootObj.AddComponent<CanvasScaler>();
        }

        SetupCanvasScaler(scaler);
        CacheLayerRoots(rootRect);
        DontDestroyOnLoad(rootObj);
    }

    private void CacheLayerRoots(RectTransform rootRect)
    {
        layerRoots.Clear();

        safeAreaRoot = FindOrCreateContainer("SafeAreaRoot", rootRect, true);
        fullScreenRoot = FindOrCreateContainer("FullScreenRoot", rootRect, false);

        layerRoots[UILayer.HUD] = FindOrCreateContainer("HUDLayer", safeAreaRoot, false);
        layerRoots[UILayer.Touch] = FindOrCreateContainer("TouchLayer", safeAreaRoot, false);
        layerRoots[UILayer.Normal] = FindOrCreateContainer("NormalLayer", safeAreaRoot, false);
        layerRoots[UILayer.Popup] = FindOrCreateContainer("PopupLayer", safeAreaRoot, false);
        layerRoots[UILayer.Tip] = FindOrCreateContainer("TipLayer", safeAreaRoot, false);
        layerRoots[UILayer.System] = FindOrCreateContainer("SystemLayer", fullScreenRoot, false);
    }

    private RectTransform FindOrCreateContainer(string objectName, RectTransform parent, bool addSafeAreaAdapter)
    {
        Transform child = parent.Find(objectName);
        RectTransform rect = child as RectTransform;
        if (rect == null)
        {
            GameObject obj = new GameObject(objectName, typeof(RectTransform));
            rect = obj.transform as RectTransform;
            rect.SetParent(parent, false);
        }

        StretchRect(rect);

        if (addSafeAreaAdapter && rect.GetComponent<SafeAreaAdapter>() == null)
        {
            rect.gameObject.AddComponent<SafeAreaAdapter>();
        }

        return rect;
    }

    private void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystemObj = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        DontDestroyOnLoad(eventSystemObj);
    }

    private void SetupCanvasScaler(CanvasScaler scaler)
    {
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = designResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.8f;
        scaler.referencePixelsPerUnit = 100;
    }

    private static void StretchRect(RectTransform rect)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
        rect.localPosition = Vector3.zero;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    public T OpenPanel<T>() where T : BaseCanvas
    {
        EnsureUIRoot();

        CanvasConfig config = GetCanvasConfig<T>();
        string canvasName = typeof(T).Name;

        BaseCanvas canvas = GetOrOpenCachedCanvas<T>(canvasName, config);
        if (canvas != null)
        {
            return canvas as T;
        }

        GameObject canvasObj = LoadCanvasObject(config);
        if (canvasObj == null)
        {
            Debug.LogError($"[UIManager] Load canvas failed: {GetLoadLog(config)}");
            return null;
        }

        T loadedCanvas = canvasObj.GetComponent<T>();
        if (loadedCanvas == null)
        {
            Debug.LogError($"[UIManager] Canvas prefab missing component {typeof(T).Name}: {GetLoadLog(config)}");
            Destroy(canvasObj);
            return null;
        }

        loadedCanvas.name = canvasName;
        OpenLoadedCanvas(canvasName, loadedCanvas, config);
        return loadedCanvas;
    }

    public void OpenPanelAsy<T>(UnityAction<T> callback = null) where T : BaseCanvas
    {
        EnsureUIRoot();

        CanvasConfig config = GetCanvasConfig<T>();
        string canvasName = typeof(T).Name;

        if (config.LoadType != UILoadType.AssetBundle)
        {
            Debug.LogError($"[UIManager] OpenPanelAsy only supports AssetBundle canvas: {canvasName}");
            callback?.Invoke(null);
            return;
        }

        BaseCanvas canvas = GetOrOpenCachedCanvas<T>(canvasName, config);
        if (canvas != null)
        {
            callback?.Invoke(canvas as T);
            return;
        }

        UnityAction<BaseCanvas> wrappedCallback = loadedCanvas => callback?.Invoke(loadedCanvas as T);
        if (loadingCallbacks.TryGetValue(canvasName, out List<UnityAction<BaseCanvas>> callbacks))
        {
            callbacks.Add(wrappedCallback);
            return;
        }

        int version = loadingVersions.TryGetValue(canvasName, out int oldVersion) ? oldVersion + 1 : 1;
        loadingVersions[canvasName] = version;
        loadingCallbacks[canvasName] = new List<UnityAction<BaseCanvas>> { wrappedCallback };

        LoadCanvasFromABAsync<T>(canvasName, config, version);
    }

    private void LoadCanvasFromABAsync<T>(string canvasName, CanvasConfig config, int version) where T : BaseCanvas
    {
#if UNITY_EDITOR
        GameObject editorCanvasObj = LoadEditorCanvasObject(config);
        if (editorCanvasObj != null)
        {
            T editorCanvas = editorCanvasObj.GetComponent<T>();
            if (editorCanvas == null)
            {
                Debug.LogError($"[UIManager] Canvas prefab missing component {typeof(T).Name}: {GetLoadLog(config)}");
                Destroy(editorCanvasObj);
                InvokeLoadingCallbacks(canvasName, null);
                return;
            }

            editorCanvas.name = canvasName;
            OpenLoadedCanvas(canvasName, editorCanvas, config);
            InvokeLoadingCallbacks(canvasName, editorCanvas);
            return;
        }
#endif

        if (ABManager.Instance == null)
        {
            Debug.LogError("[UIManager] ABManager is null, cannot load canvas: " + canvasName);
            InvokeLoadingCallbacks(canvasName, null);
            return;
        }

        ABManager.Instance.LoadResAsync<GameObject>(config.AssetBundleName, config.AssetName, canvasObj =>
        {
            if (!IsValidLoadingVersion(canvasName, version))
            {
                if (canvasObj != null)
                {
                    Destroy(canvasObj);
                }

                return;
            }

            if (canvasObj == null)
            {
                Debug.LogError($"[UIManager] Load AB canvas failed: {config.AssetBundleName}/{config.AssetName}");
                InvokeLoadingCallbacks(canvasName, null);
                return;
            }

            T loadedCanvas = canvasObj.GetComponent<T>();
            if (loadedCanvas == null)
            {
                Debug.LogError($"[UIManager] Canvas prefab missing component {typeof(T).Name}: {config.AssetBundleName}/{config.AssetName}");
                Destroy(canvasObj);
                InvokeLoadingCallbacks(canvasName, null);
                return;
            }

            loadedCanvas.name = canvasName;
            OpenLoadedCanvas(canvasName, loadedCanvas, config);
            InvokeLoadingCallbacks(canvasName, loadedCanvas);
        });
    }

    private bool IsValidLoadingVersion(string canvasName, int version)
    {
        return loadingCallbacks.ContainsKey(canvasName) &&
               loadingVersions.TryGetValue(canvasName, out int activeVersion) &&
               activeVersion == version;
    }

    private GameObject LoadCanvasObject(CanvasConfig config)
    {
        if (config.LoadType == UILoadType.AssetBundle)
        {
#if UNITY_EDITOR
            GameObject editorCanvasObj = LoadEditorCanvasObject(config);
            if (editorCanvasObj != null)
            {
                return editorCanvasObj;
            }
#endif

            if (ABManager.Instance == null)
            {
                Debug.LogError("[UIManager] ABManager is null");
                return null;
            }

            return ABManager.Instance.LoadRes<GameObject>(config.AssetBundleName, config.AssetName);
        }

        if (ResMgr.Instance != null)
        {
            return ResMgr.Instance.load<GameObject>(config.ResourcesPath);
        }

        GameObject prefab = Resources.Load<GameObject>(config.ResourcesPath);
        return prefab != null ? Instantiate(prefab) : null;
    }

#if UNITY_EDITOR
    private GameObject LoadEditorCanvasObject(CanvasConfig config)
    {
        if (config.LoadType != UILoadType.AssetBundle)
        {
            return null;
        }

        // 编辑器优先读取源 prefab 避免测试时吃旧 AssetBundle
        string assetPath = $"Assets/Art/ABRes/UI/{config.AssetName}.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        return prefab != null ? Instantiate(prefab) : null;
    }
#endif

    private BaseCanvas GetOrOpenCachedCanvas<T>(string canvasName, CanvasConfig config) where T : BaseCanvas
    {
        if (openedCanvases.TryGetValue(canvasName, out BaseCanvas canvas))
        {
            MoveToFront(canvas, config);
            return canvas;
        }

        if (uiPool.TryGetValue(canvasName, out Queue<BaseCanvas> pool) && pool.Count > 0)
        {
            canvas = pool.Dequeue();
            OpenLoadedCanvas(canvasName, canvas, config);
            return canvas;
        }

        return null;
    }

    private void OpenLoadedCanvas(string canvasName, BaseCanvas canvas, CanvasConfig config)
    {
        if (canvas == null)
        {
            return;
        }

        RectTransform targetRoot = GetLayerRoot(config.Layer, config.UseSafeArea);
        canvas.transform.SetParent(targetRoot, false);
        canvas.StretchFullScreen();
        canvas.transform.SetAsLastSibling();
        openedCanvases[canvasName] = canvas;
        canvas.Show();
        RebuildLayerSorting(config.Layer, config.UseSafeArea);
    }

    private RectTransform GetLayerRoot(UILayer layer, bool useSafeArea)
    {
        EnsureUIRoot();

        if (layer == UILayer.System || !useSafeArea)
        {
            return layerRoots[UILayer.System];
        }

        return layerRoots.TryGetValue(layer, out RectTransform layerRoot) && layerRoot != null
            ? layerRoot
            : layerRoots[UILayer.Normal];
    }

    private void MoveToFront(BaseCanvas canvas, CanvasConfig config)
    {
        canvas.transform.SetAsLastSibling();
        canvas.Show();
        RebuildLayerSorting(config.Layer, config.UseSafeArea);
    }

    private void RebuildLayerSorting(UILayer layer, bool useSafeArea)
    {
        RectTransform layerRoot = GetLayerRoot(layer, useSafeArea);
        if (layerRoot == null || !layerBaseOrders.TryGetValue(layer, out int baseOrder))
        {
            return;
        }

        int sortingOrder = baseOrder;
        for (int i = 0; i < layerRoot.childCount; i++)
        {
            BaseCanvas canvas = layerRoot.GetChild(i).GetComponent<BaseCanvas>();
            if (canvas == null)
            {
                continue;
            }

            canvas.SetSortingOrder(sortingOrder);
            sortingOrder++;
        }
    }

    private void InvokeLoadingCallbacks(string canvasName, BaseCanvas canvas)
    {
        if (loadingCallbacks.TryGetValue(canvasName, out List<UnityAction<BaseCanvas>> callbacks))
        {
            for (int i = 0; i < callbacks.Count; i++)
            {
                callbacks[i]?.Invoke(canvas);
            }
        }

        loadingCallbacks.Remove(canvasName);
        loadingVersions.Remove(canvasName);
    }

    public void ClosePanel<T>() where T : BaseCanvas
    {
        string canvasName = typeof(T).Name;

        if (loadingCallbacks.ContainsKey(canvasName))
        {
            InvokeLoadingCallbacks(canvasName, null);
            return;
        }

        BaseCanvas canvas = GetPanel<T>();
        if (canvas == null)
        {
            return;
        }

        CanvasConfig config = GetCanvasConfig<T>();
        openedCanvases.Remove(canvasName);
        canvas.Hide();

        if (!uiPool.ContainsKey(canvasName))
        {
            uiPool[canvasName] = new Queue<BaseCanvas>();
        }

        uiPool[canvasName].Enqueue(canvas);
        RebuildLayerSorting(config.Layer, config.UseSafeArea);
    }

    public T GetPanel<T>() where T : BaseCanvas
    {
        string canvasName = typeof(T).Name;
        return openedCanvases.TryGetValue(canvasName, out BaseCanvas canvas) ? canvas as T : null;
    }

    public bool IsPanelOpened<T>() where T : BaseCanvas
    {
        return openedCanvases.ContainsKey(typeof(T).Name);
    }

    public void CloseAllPanels()
    {
        List<BaseCanvas> canvases = new List<BaseCanvas>(openedCanvases.Values);
        for (int i = 0; i < canvases.Count; i++)
        {
            BaseCanvas canvas = canvases[i];
            if (canvas == null)
            {
                continue;
            }

            string canvasName = canvas.GetType().Name;
            canvas.Hide();

            if (!uiPool.ContainsKey(canvasName))
            {
                uiPool[canvasName] = new Queue<BaseCanvas>();
            }

            uiPool[canvasName].Enqueue(canvas);
        }

        openedCanvases.Clear();
    }

    public void DestroyAllPanels()
    {
        CloseAllPanels();

        foreach (var kvp in uiPool)
        {
            foreach (BaseCanvas canvas in kvp.Value)
            {
                if (canvas != null)
                {
                    Destroy(canvas.gameObject);
                }
            }
        }

        uiPool.Clear();
    }

    private CanvasConfig GetCanvasConfig<T>() where T : BaseCanvas
    {
        Type canvasType = typeof(T);
        if (canvasConfigs.TryGetValue(canvasType, out CanvasConfig config))
        {
            return config;
        }

        UICanvasAttribute attribute = Attribute.GetCustomAttribute(canvasType, typeof(UICanvasAttribute), true) as UICanvasAttribute;
        if (attribute != null)
        {
            string assetName = string.IsNullOrWhiteSpace(attribute.AssetName) ? canvasType.Name : attribute.AssetName;
            config = new CanvasConfig
            {
                Layer = attribute.Layer,
                LoadType = attribute.LoadType,
                AssetBundleName = string.IsNullOrWhiteSpace(attribute.AssetBundleName) ? "uipanel" : attribute.AssetBundleName,
                AssetName = assetName,
                ResourcesPath = string.IsNullOrWhiteSpace(attribute.ResourcesPath) ? "UI/" + assetName : attribute.ResourcesPath,
                UseSafeArea = attribute.UseSafeArea
            };

            canvasConfigs[canvasType] = config;
            return config;
        }

        GameObject configObj = new GameObject("__UIConfig_" + canvasType.Name);
        configObj.hideFlags = HideFlags.HideAndDontSave;
        configObj.SetActive(false);

        T canvas = configObj.AddComponent<T>();
        config = new CanvasConfig
        {
            Layer = canvas.Layer,
            LoadType = canvas.LoadType,
            AssetBundleName = string.IsNullOrWhiteSpace(canvas.AssetBundleName) ? "uipanel" : canvas.AssetBundleName,
            AssetName = string.IsNullOrWhiteSpace(canvas.AssetName) ? canvasType.Name : canvas.AssetName,
            ResourcesPath = string.IsNullOrWhiteSpace(canvas.ResourcesPath) ? "UI/" + canvasType.Name : canvas.ResourcesPath,
            UseSafeArea = canvas.UseSafeArea
        };

        Destroy(configObj);
        canvasConfigs[canvasType] = config;
        return config;
    }

    private static string GetLoadLog(CanvasConfig config)
    {
        return config.LoadType == UILoadType.AssetBundle
            ? config.AssetBundleName + "/" + config.AssetName
            : config.ResourcesPath;
    }
}
