using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// UI 面板层级。
/// </summary>
public enum UILayer
{
    /// <summary>
    /// 静态层：适合背景、固定 HUD 等变化较少的 UI。
    /// </summary>
    Static,

    /// <summary>
    /// 普通动态层：适合背包、对话、商店等常规交互面板。
    /// </summary>
    Dynamic,

    /// <summary>
    /// 顶层：适合弹窗、提示、遮罩等需要盖在最上面的 UI。
    /// </summary>
    Top
}

/// <summary>
/// 项目统一 UI 管理器。
///
/// 主要职责：
/// 1. 创建并维护全局 UI_Root 和三层 Canvas。
/// 2. 通过 AssetBundle 异步加载 UI 面板预制体。
/// 3. 记录已经打开的面板，避免同一个面板重复创建。
/// 4. 关闭面板时放入对象池，下次打开时复用实例。
///
/// 重要约定：
/// 1. UI 面板预制体统一放在 uipanel 这个 AB 包中。
/// 2. 面板资源名必须和面板脚本类名一致，例如 StartPlayerPanel。
/// 3. 面板预制体上必须挂对应的 BasePanel 子类脚本。
///
/// 注意：
/// OpenPanel 只是兼容旧代码的接口。第一次从 AB 包加载面板时，它会立即返回 null。
/// 新代码如果打开后要马上操作面板对象，应使用 OpenPanelAsync。
/// </summary>
public class UIManager : UnitySingleTonMono<UIManager>
{
    /// <summary>
    /// 所有 UI 面板预制体所在的 AssetBundle 包名。
    /// </summary>
    private const string PanelAbName = "uipanel";
    private const string ResourcesPanelPath = "UI/";

    private GameObject rootCanvasObj;
    private Canvas staticCanvas;
    private Canvas lowFreqCanvas;
    private Canvas highFreqCanvas;
    
    // 面板对象池。
    private readonly Dictionary<string, Queue<BasePanel>> uiPool = new Dictionary<string, Queue<BasePanel>>();

    /// <summary>
    /// 当前已经打开的面板。
    /// 用于避免同一个面板重复实例化，也方便 GetPanel / ClosePanel 查询。
    /// </summary>
    private readonly Dictionary<string, BasePanel> openedPanels = new Dictionary<string, BasePanel>();

    /// <summary>
    /// 正在异步加载中的面板回调列表。
    ///
    /// 同一个面板在加载完成前可能被多处请求打开。
    /// 这里把这些请求的回调合并，等 AB 加载完成后统一通知，
    /// 避免重复加载同一个面板预制体。
    /// </summary>
    private readonly Dictionary<string, List<UnityAction<BasePanel>>> loadingPanelCallbacks = new Dictionary<string, List<UnityAction<BasePanel>>>();

    /// <summary>
    /// 记录正在加载中的面板最终应该挂到哪一层 Canvas。
    /// </summary>
    private readonly Dictionary<string, UILayer> loadingPanelLayers = new Dictionary<string, UILayer>();

    /// <summary>
    /// 面板异步加载版本号。
    ///
    /// 用于处理“面板还在异步加载时又被关闭”的情况。
    /// 如果旧加载请求完成时版本不匹配，说明这次加载已经被取消，
    /// UIManager 会销毁加载出来的对象，不再显示它。
    /// </summary>
    private readonly Dictionary<string, int> loadingPanelVersions = new Dictionary<string, int>();

    private readonly Vector2 designResolution = new Vector2(1920, 1080);
    private readonly CanvasScaler.ScaleMode scaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

    public override void Awake()
    {
        base.Awake();
        if (Instance != this)
        {
            return;
        }

        if (rootCanvasObj != null)
        {
            return;
        }

        // 创建全局 UI 根节点。DontDestroyOnLoad 让它切换场景时不被销毁
        rootCanvasObj = new GameObject("UI_Root", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas rootCanvas = rootCanvasObj.GetComponent<Canvas>();
        rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        rootCanvas.pixelPerfect = true;
        DontDestroyOnLoad(rootCanvasObj);

        CanvasScaler rootScaler = rootCanvasObj.GetComponent<CanvasScaler>();
        SetupCanvasScaler(rootScaler);

        staticCanvas = CreateSubCanvas("Canvas_Static", rootCanvas.transform, false);
        lowFreqCanvas = CreateSubCanvas("Canvas_LowFreq", rootCanvas.transform, true);
        highFreqCanvas = CreateSubCanvas("Canvas_HighFreq", rootCanvas.transform, true);
        EnsureEventSystem();
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

    protected override void OnDestroy()
    {
        if (rootCanvasObj != null)
        {
            Destroy(rootCanvasObj);
            rootCanvasObj = null;
        }

        base.OnDestroy();
    }

    /// <summary>
    /// 创建 UI 子 Canvas
    /// </summary>
    private Canvas CreateSubCanvas(string canvasName, Transform parent, bool needRaycaster)
    {
        GameObject subCanvasObj = new GameObject(canvasName);
        subCanvasObj.transform.SetParent(parent, false);

        RectTransform rect = subCanvasObj.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;

        Canvas subCanvas = subCanvasObj.AddComponent<Canvas>();
        subCanvas.overrideSorting = false;

        // 只有需要接收点击的 UI 层才添加 GraphicRaycaster。
        if (needRaycaster)
        {
            subCanvasObj.AddComponent<GraphicRaycaster>();
        }

        return subCanvas;
    }

    /// <summary>
    /// 配置 UI 缩放规则
    /// </summary>
    private void SetupCanvasScaler(CanvasScaler scaler)
    {
        scaler.uiScaleMode = scaleMode;
        scaler.referenceResolution = designResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        scaler.matchWidthOrHeight = 0;
        scaler.referencePixelsPerUnit = 100;
    }

    /// <summary>
    /// 打开面板的兼容接口。
    /// 如果面板已经打开，或者对象池中已有该面板实例，会立即返回面板
    /// 如果面板第一次从 AB 包加载，本方法只会启动异步加载，并立即返回 null
    /// </summary>
    public T OpenPanel<T>(UILayer layer = UILayer.Dynamic) where T : BasePanel
    {
        BasePanel panel = GetOrOpenCachedPanel<T>(layer);
        if (panel != null)
        {
            return panel as T;
        }

        OpenPanelAsync<T>(layer);
        return null;
    }

    /// <summary>
    /// 打开面板。useResManager 为 true 时从 Resources/UI 加载，否则沿用 AssetBundle 加载。
    /// </summary>
    public T OpenPanel<T>(UILayer layer, bool useResManager) where T : BasePanel
    {
        return useResManager ? OpenPanelByRes<T>(layer) : OpenPanel<T>(layer);
    }

    /// <summary>
    /// 从 Resources/UI 同步加载并打开面板。
    /// 面板预制体名需要和面板类名一致，例如 Resources/UI/StartPlayerPanel。
    /// </summary>
    public T OpenPanelByRes<T>(UILayer layer = UILayer.Dynamic) where T : BasePanel
    {
        string panelName = typeof(T).Name;

        BasePanel panel = GetOrOpenCachedPanel<T>(layer);
        if (panel != null)
        {
            return panel as T;
        }

        if (ResMgr.Instance == null)
        {
            Debug.LogError("[UIManager] ResMgr is null, cannot load panel: " + panelName);
            return null;
        }

        GameObject panelObj = ResMgr.Instance.load<GameObject>(ResourcesPanelPath + panelName);
        if (panelObj == null)
        {
            Debug.LogError($"[UIManager] Load Resources panel failed: {ResourcesPanelPath}{panelName}");
            return null;
        }

        T loadedPanel = panelObj.GetComponent<T>();
        if (loadedPanel == null)
        {
            Debug.LogError($"[UIManager] Panel prefab missing component {typeof(T).Name}: {ResourcesPanelPath}{panelName}");
            Destroy(panelObj);
            return null;
        }

        loadedPanel.name = panelName;
        OpenLoadedPanel(panelName, loadedPanel, layer);
        return loadedPanel;
    }
    
    

    /// <summary>
    /// 异步打开面板
    /// 推荐作为主要打开 UI 的接口
    /// 面板加载完成并挂到对应 Canvas 后，会通过 callback 返回面板实例
    /// </summary>
    public void OpenPanelAsync<T>(UILayer layer = UILayer.Dynamic, UnityAction<T> callback = null) where T : BasePanel
    {
        string panelName = typeof(T).Name;

        // 优先复用已经打开或对象池中的面板。
        // 这种情况下不需要走 AB 加载，回调会立刻执行。
        BasePanel panel = GetOrOpenCachedPanel<T>(layer);
        if (panel != null)
        {
            callback?.Invoke(panel as T);
            return;
        }

        // 如果同一个面板已经在加载中，不再重复请求 AB
        // 只把新的回调加入列表，等第一次加载完成后一起通知
        UnityAction<BasePanel> wrappedCallback = loadedPanel => callback?.Invoke(loadedPanel as T);
        if (loadingPanelCallbacks.TryGetValue(panelName, out List<UnityAction<BasePanel>> callbacks))
        {
            callbacks.Add(wrappedCallback);
            return;
        }

        // 记录本次加载版本
        // ClosePanel 会清理当前版本，AB 回调回来时可以据此判断加载结果是否还有效
        int loadVersion = loadingPanelVersions.TryGetValue(panelName, out int currentVersion) ? currentVersion + 1 : 1;
        loadingPanelVersions[panelName] = loadVersion;
        loadingPanelCallbacks.Add(panelName, new List<UnityAction<BasePanel>> { wrappedCallback });
        loadingPanelLayers.Add(panelName, layer);

        if (ABManager.Instance == null)
        {
            Debug.LogError("[UIManager] ABManager is null, cannot load panel: " + panelName);
            InvokeLoadingCallbacks(panelName, null);
            return;
        }

        // ABManager.LoadResAsync 对 GameObject 会返回已经 Instantiate 出来的实例
        // 因此这里拿到的 prefabObj 实际上就是面板实例对象，不需要再次 Instantiate
        ABManager.Instance.LoadResAsync<GameObject>(PanelAbName, panelName, prefabObj =>
        {
            // 面板加载期间可能已经被关闭或取消
            // 这种情况下不再显示，直接销毁异步加载出来的实例
            if (!loadingPanelCallbacks.ContainsKey(panelName) ||
                !loadingPanelVersions.TryGetValue(panelName, out int activeVersion) ||
                activeVersion != loadVersion)
            {
                if (prefabObj != null)
                {
                    Destroy(prefabObj);
                }

                return;
            }

            // AB 包中没有对应资源，或资源名和面板类名不一致时会进入这里
            if (prefabObj == null)
            {
                Debug.LogError($"[UIManager] Load panel failed: {PanelAbName}/{panelName}");
                InvokeLoadingCallbacks(panelName, null);
                return;
            }

            // 面板预制体上必须挂对应的 BasePanel 子类脚本
            T loadedPanel = prefabObj.GetComponent<T>();
            if (loadedPanel == null)
            {
                Debug.LogError($"[UIManager] Panel prefab missing component {typeof(T).Name}: {panelName}");
                Destroy(prefabObj);
                InvokeLoadingCallbacks(panelName, null);
                return;
            }

            loadedPanel.name = panelName;
            UILayer targetLayer = loadingPanelLayers.TryGetValue(panelName, out UILayer pendingLayer) ? pendingLayer : layer;
            OpenLoadedPanel(panelName, loadedPanel, targetLayer);
            InvokeLoadingCallbacks(panelName, loadedPanel);
        });
    }

    /// <summary>
    /// 尝试从“已打开列表”或“对象池”中获取面板
    ///
    /// 返回 null 表示当前没有可复用实例，需要走 AB 异步加载
    /// </summary>
    private BasePanel GetOrOpenCachedPanel<T>(UILayer layer) where T : BasePanel
    {
        string panelName = typeof(T).Name;

        // 面板已经打开时，不重复创建，只把它移动到同层最上面。
        if (openedPanels.TryGetValue(panelName, out BasePanel panel))
        {
            panel.transform.SetAsLastSibling();
            return panel;
        }

        // 面板之前关闭过，优先从对象池复用。
        if (uiPool.ContainsKey(panelName) && uiPool[panelName].Count > 0)
        {
            panel = uiPool[panelName].Dequeue();
            panel.gameObject.SetActive(true);
            OpenLoadedPanel(panelName, panel, layer);
            return panel;
        }

        return null;
    }

    /// <summary>
    /// 真正执行打开面板的公共流程。
    ///
    /// 不负责加载资源，只负责：
    /// 1. 根据 UILayer 选择 Canvas。
    /// 2. 设置父节点。
    /// 3. 放到同层最上面。
    /// 4. 登记为已打开。
    /// 5. 调用面板 Show。
    /// </summary>
    private void OpenLoadedPanel(string panelName, BasePanel panel, UILayer layer)
    {
        Canvas targetCanvas = layer switch
        {
            UILayer.Static => staticCanvas,
            UILayer.Dynamic => lowFreqCanvas,
            UILayer.Top => highFreqCanvas,
            _ => lowFreqCanvas
        };

        panel.transform.SetParent(targetCanvas.transform, false);
        panel.transform.SetAsLastSibling();
        openedPanels[panelName] = panel;

        panel.Show();
    }

    /// <summary>
    /// 执行并清理某个面板的异步加载回调。
    ///
    /// panel 为 null 时表示加载失败或加载被取消，调用方需要自行判空。
    /// </summary>
    private void InvokeLoadingCallbacks(string panelName, BasePanel panel)
    {
        if (loadingPanelCallbacks.TryGetValue(panelName, out List<UnityAction<BasePanel>> callbacks))
        {
            for (int i = 0; i < callbacks.Count; i++)
            {
                callbacks[i]?.Invoke(panel);
            }
        }

        loadingPanelCallbacks.Remove(panelName);
        loadingPanelLayers.Remove(panelName);
        loadingPanelVersions.Remove(panelName);
    }

    /// <summary>
    /// 关闭面板。
    ///
    /// 如果面板正在加载但还没显示，会取消本次加载请求。
    /// 如果面板已经显示，会 Hide 并放入对象池，等待下次复用。
    /// </summary>
    public void ClosePanel<T>() where T : BasePanel
    {
        string panelName = typeof(T).Name;

        // 面板还在 AB 异步加载过程中时，关闭操作表示取消显示。
        // 已经发出的 AB 请求不能真正停止，但回调回来时会通过版本号判断并销毁实例。
        if (loadingPanelCallbacks.ContainsKey(panelName))
        {
            InvokeLoadingCallbacks(panelName, null);
            return;
        }

        BasePanel panel = GetPanel<T>();
        if (panel == null)
        {
            return;
        }

        openedPanels.Remove(panelName);
        panel.Hide();
        if (!uiPool.ContainsKey(panelName))
        {
            uiPool.Add(panelName, new Queue<BasePanel>());
        }

        uiPool[panelName].Enqueue(panel);
    }

    /// <summary>
    /// 获取当前已经打开的面板。
    ///
    /// 只查 openedPanels，不会触发加载，也不会从对象池取。
    /// </summary>
    public T GetPanel<T>() where T : BasePanel
    {
        string panelName = typeof(T).Name;
        if (openedPanels.TryGetValue(panelName, out BasePanel panel))
        {
            return panel as T;
        }

        return null;
    }

    /// <summary>
    /// 判断某个面板当前是否处于打开状态。
    /// </summary>
    public bool IsPanelOpened<T>() where T : BasePanel
    {
        return openedPanels.ContainsKey(typeof(T).Name);
    }

    /// <summary>
    /// 关闭所有已经打开的面板，并放入对象池。
    ///
    /// 注意：这里只处理已经显示在 Canvas 下的面板，
    /// 不会销毁对象，也不会卸载 AB 包。
    /// </summary>
    public void CloseAllPanels()
    {
        ClosePanelsInCanvas(staticCanvas);
        ClosePanelsInCanvas(lowFreqCanvas);
        ClosePanelsInCanvas(highFreqCanvas);
        openedPanels.Clear();
    }

    /// <summary>
    /// 销毁所有池中的面板实例。
    ///
    /// 一般用于退出游戏、切换大模块、清理 UI 缓存。
    /// 这里只销毁 UI 实例，不负责卸载 uipanel AB 包。
    /// </summary>
    public void DestroyAllPanels()
    {
        CloseAllPanels();

        foreach (var kvp in uiPool)
        {
            foreach (BasePanel panel in kvp.Value)
            {
                if (panel != null)
                {
                    Destroy(panel.gameObject);
                }
            }
        }

        uiPool.Clear();
    }

    /// <summary>
    /// 关闭指定 Canvas 下的全部面板，并放入对象池。
    /// </summary>
    private void ClosePanelsInCanvas(Canvas canvas)
    {
        if (canvas == null)
        {
            return;
        }

        List<BasePanel> panelsToClose = new List<BasePanel>();

        // 先收集再关闭，避免遍历 Transform 子节点时修改层级导致漏处理。
        foreach (Transform child in canvas.transform)
        {
            BasePanel panel = child.GetComponent<BasePanel>();
            if (panel != null)
            {
                panelsToClose.Add(panel);
            }
        }

        foreach (BasePanel panel in panelsToClose)
        {
            string panelName = panel.GetType().Name;
            panel.Hide();

            if (!uiPool.ContainsKey(panelName))
            {
                uiPool.Add(panelName, new Queue<BasePanel>());
            }

            uiPool[panelName].Enqueue(panel);
        }
    }
}
