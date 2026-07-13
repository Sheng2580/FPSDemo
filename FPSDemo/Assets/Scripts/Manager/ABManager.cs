using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;
using Object = UnityEngine.Object;

public class ABManager : UnitySingleTonMono<ABManager>
{
    private readonly Dictionary<string, AssetBundle> assetBundlesDictionary = new Dictionary<string, AssetBundle>();
    private readonly Dictionary<string, List<UnityAction>> loadingCallbacks = new Dictionary<string, List<UnityAction>>();

    private AssetBundle mainAb;
    private AssetBundleManifest manifest;
    private bool isMainBundleLoading;
    private readonly List<UnityAction> mainBundleCallbacks = new List<UnityAction>();

    private string Pathur => JoinStreamingAssetsPath(Application.streamingAssetsPath, MainName);

    private bool ShouldUseWebRequestForStreamingAssets
    {
        get
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return true;
#else
            return false;
#endif
        }
    }

    private string MainName
    {
        get
        {
#if UNITY_IOS
            return "iOS";
#elif UNITY_ANDROID
            return "Android";
#elif UNITY_STANDALONE_WIN
            return "StandaloneWindows";
#elif UNITY_STANDALONE_OSX
            return "StandaloneOSXUniversal";
#elif UNITY_STANDALONE_LINUX
            return "StandaloneLinux64";
#else
            return "StandaloneOSXUniversal";
#endif
        }
    }

    private string NormalizeABName(string abName)
    {
        if (string.IsNullOrEmpty(abName))
        {
            return abName;
        }

        return Path.GetFileName(abName.Replace("\\", "/"));
    }

    private string JoinStreamingAssetsPath(string left, string right)
    {
        if (string.IsNullOrEmpty(left))
        {
            return right;
        }

        if (string.IsNullOrEmpty(right))
        {
            return left;
        }

        return left.TrimEnd('/', '\\') + "/" + right.TrimStart('/', '\\');
    }

    private string GetBundlePath(string abName)
    {
        return JoinStreamingAssetsPath(Pathur, NormalizeABName(abName));
    }

    public void LoadMainAndManifestOfAB(string abName)
    {
        abName = NormalizeABName(abName);
        EnsureMainBundleLoaded();

        if (manifest == null)
        {
            Debug.LogError($"[ABManager] Manifest is null, cannot load AB: {abName}");
            return;
        }

        string[] dependencies = manifest.GetAllDependencies(abName);
        for (int i = 0; i < dependencies.Length; i++)
        {
            LoadBundleSync(NormalizeABName(dependencies[i]));
        }

        LoadBundleSync(abName);
    }

    private void EnsureMainBundleLoaded()
    {
        if (mainAb != null)
        {
            return;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        Debug.LogError("[ABManager] Sync AB loading from StreamingAssets is not supported on Android. Use LoadResAsync instead.");
        return;
#endif

        string mainPath = GetBundlePath(MainName);
        mainAb = AssetBundle.LoadFromFile(mainPath);
        if (mainAb == null)
        {
            Debug.LogError($"[ABManager] Main bundle load failed: {mainPath}");
            return;
        }

        manifest = mainAb.LoadAsset<AssetBundleManifest>("AssetBundleManifest");
        if (manifest == null)
        {
            Debug.LogError($"[ABManager] Main bundle manifest missing: {MainName}");
        }
    }

    private void LoadBundleSync(string abName)
    {
        abName = NormalizeABName(abName);
        if (string.IsNullOrEmpty(abName) || assetBundlesDictionary.ContainsKey(abName))
        {
            return;
        }

        AssetBundle loadedBundle = GetLoadedAssetBundle(abName);
        if (loadedBundle == null)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            Debug.LogError("[ABManager] Sync AB loading from StreamingAssets is not supported on Android. Use LoadResAsync instead.");
            return;
#else
            loadedBundle = AssetBundle.LoadFromFile(GetBundlePath(abName));
#endif
        }

        if (loadedBundle != null)
        {
            assetBundlesDictionary[abName] = loadedBundle;
        }
        else
        {
            Debug.LogError($"[ABManager] AB load failed: {abName}");
        }
    }

    private AssetBundle GetLoadedAssetBundle(string abName)
    {
        string normalizedName = NormalizeABName(abName);
        foreach (AssetBundle loadedBundle in AssetBundle.GetAllLoadedAssetBundles())
        {
            if (loadedBundle == null)
            {
                continue;
            }

            if (string.Equals(NormalizeABName(loadedBundle.name), normalizedName, StringComparison.OrdinalIgnoreCase))
            {
                return loadedBundle;
            }
        }

        return null;
    }

    public object LoadRes(string abName, string resName)
    {
        abName = NormalizeABName(abName);
        LoadMainAndManifestOfAB(abName);
        if (!assetBundlesDictionary.TryGetValue(abName, out AssetBundle ab))
        {
            Debug.LogError($"[ABManager] Sync load failed, AB not loaded: {abName}");
            return null;
        }

        Object obj = ab.LoadAsset(resName);
        return InstantiateIfGameObject(obj, resName);
    }

    public object LoadRes(string abName, string resName, Type type)
    {
        abName = NormalizeABName(abName);
        LoadMainAndManifestOfAB(abName);
        if (!assetBundlesDictionary.TryGetValue(abName, out AssetBundle ab))
        {
            Debug.LogError($"[ABManager] Sync load failed, AB not loaded: {abName}");
            return null;
        }

        Object obj = ab.LoadAsset(resName, type);
        return InstantiateIfGameObject(obj, resName);
    }

    public T LoadRes<T>(string abName, string resName) where T : Object
    {
        abName = NormalizeABName(abName);
        LoadMainAndManifestOfAB(abName);
        if (!assetBundlesDictionary.TryGetValue(abName, out AssetBundle ab))
        {
            Debug.LogError($"[ABManager] Sync load failed, AB not loaded: {abName}");
            return null;
        }

        T obj = ab.LoadAsset<T>(resName);
        return InstantiateIfGameObject(obj, resName) as T;
    }

    private Object InstantiateIfGameObject(Object obj, string resName)
    {
        if (obj == null)
        {
            Debug.LogError($"[ABManager] Asset load failed: {resName}");
            return null;
        }

        if (obj is GameObject prefab)
        {
            GameObject newObj = Instantiate(prefab);
            newObj.name = resName;
            return newObj;
        }

        return obj;
    }

    public void LoadResAsync(string abName, string resName, UnityAction<Object> callback)
    {
        StartCoroutine(Really(abName, resName, callback));
    }

    public void LoadResAsync(string abName, string resName, Type type, UnityAction<Object> callback)
    {
        StartCoroutine(Really(abName, resName, type, callback));
    }

    public void LoadResAsync<T>(string abName, string resName, UnityAction<T> callback) where T : Object
    {
        StartCoroutine(Really(abName, resName, callback));
    }

    // 异步加载 AB 里的原始资源 给对象池缓存预制体模板使用
    public void LoadAssetAsync<T>(string abName, string resName, UnityAction<T> callback) where T : Object
    {
        StartCoroutine(ReallyLoadAsset(abName, resName, callback));
    }

    // 异步加载 AB 里的原始资源 不会自动实例化 GameObject
    public void LoadAssetAsync(string abName, string resName, Type type, UnityAction<Object> callback)
    {
        StartCoroutine(ReallyLoadAsset(abName, resName, type, callback));
    }

    private void LoadABAsync(string abName, UnityAction onLoaded)
    {
        abName = NormalizeABName(abName);
        if (assetBundlesDictionary.ContainsKey(abName))
        {
            onLoaded?.Invoke();
            return;
        }

        AssetBundle loadedBundle = GetLoadedAssetBundle(abName);
        if (loadedBundle != null)
        {
            assetBundlesDictionary[abName] = loadedBundle;
            onLoaded?.Invoke();
            return;
        }

        if (loadingCallbacks.ContainsKey(abName))
        {
            loadingCallbacks[abName].Add(onLoaded);
            return;
        }

        loadingCallbacks[abName] = new List<UnityAction> { onLoaded };
        StartCoroutine(ReallyLoadABAsync(abName));
    }

    private IEnumerator ReallyLoadABAsync(string abName)
    {
        abName = NormalizeABName(abName);

        bool mainLoaded = false;
        LoadMainBundleAsync(() => mainLoaded = true);
        yield return new WaitUntil(() => mainLoaded);

        if (manifest == null)
        {
            Debug.LogError($"[ABManager] Manifest is null, cannot load AB: {abName}");
            ExecuteCallbacks(abName);
            yield break;
        }

        string[] dependencies = manifest.GetAllDependencies(abName);
        for (int i = 0; i < dependencies.Length; i++)
        {
            string depName = NormalizeABName(dependencies[i]);
            bool depLoaded = false;
            LoadABAsync(depName, () => depLoaded = true);
            yield return new WaitUntil(() => depLoaded);
        }

        yield return LoadSingleBundleAsync(abName);
        ExecuteCallbacks(abName);
    }

    private void LoadMainBundleAsync(UnityAction onLoaded)
    {
        if (mainAb != null)
        {
            onLoaded?.Invoke();
            return;
        }

        if (isMainBundleLoading)
        {
            mainBundleCallbacks.Add(onLoaded);
            return;
        }

        isMainBundleLoading = true;
        mainBundleCallbacks.Add(onLoaded);
        StartCoroutine(LoadMainBundleCoroutine());
    }

    private IEnumerator LoadMainBundleCoroutine()
    {
        bool done = false;
        AssetBundle loadedMainBundle = null;
        yield return LoadBundleFromStreamingAssetsAsync(MainName, bundle =>
        {
            loadedMainBundle = bundle;
            done = true;
        });

        yield return new WaitUntil(() => done);

        mainAb = loadedMainBundle;
        if (mainAb != null)
        {
            manifest = mainAb.LoadAsset<AssetBundleManifest>("AssetBundleManifest");
        }
        else
        {
            Debug.LogError($"[ABManager] Main bundle load failed: {GetBundlePath(MainName)}");
        }

        isMainBundleLoading = false;
        for (int i = 0; i < mainBundleCallbacks.Count; i++)
        {
            mainBundleCallbacks[i]?.Invoke();
        }
        mainBundleCallbacks.Clear();
    }

    private IEnumerator LoadSingleBundleAsync(string abName)
    {
        abName = NormalizeABName(abName);
        if (string.IsNullOrEmpty(abName) || assetBundlesDictionary.ContainsKey(abName))
        {
            yield break;
        }

        AssetBundle loadedBundle = GetLoadedAssetBundle(abName);
        if (loadedBundle == null)
        {
            bool done = false;
            yield return LoadBundleFromStreamingAssetsAsync(abName, bundle =>
            {
                loadedBundle = bundle;
                done = true;
            });

            yield return new WaitUntil(() => done);
        }

        if (loadedBundle != null)
        {
            assetBundlesDictionary[abName] = loadedBundle;
        }
        else
        {
            Debug.LogError($"[ABManager] AB load failed: {abName}");
        }
    }

    private IEnumerator LoadBundleFromStreamingAssetsAsync(string abName, UnityAction<AssetBundle> callback)
    {
        string bundlePath = GetBundlePath(abName);

        if (ShouldUseWebRequestForStreamingAssets)
        {
            using UnityWebRequest request = UnityWebRequest.Get(bundlePath);
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[ABManager] AB web request failed: {bundlePath}\n{request.error}");
                callback?.Invoke(null);
                yield break;
            }

            byte[] bundleBytes = request.downloadHandler != null ? request.downloadHandler.data : null;
            if (bundleBytes == null || bundleBytes.Length == 0)
            {
                Debug.LogError($"[ABManager] AB bytes empty: {abName} Path={bundlePath}");
                callback?.Invoke(null);
                yield break;
            }

            AssetBundleCreateRequest createRequest = AssetBundle.LoadFromMemoryAsync(bundleBytes);
            yield return createRequest;
            AssetBundle loadedBundle = createRequest.assetBundle;
            LogAndroidBundleLoad(abName, bundlePath, bundleBytes.Length, loadedBundle);
            callback?.Invoke(loadedBundle);
            yield break;
        }

        AssetBundleCreateRequest fileRequest = AssetBundle.LoadFromFileAsync(bundlePath);
        yield return fileRequest;
        callback?.Invoke(fileRequest.assetBundle);
    }

    private void LogAndroidBundleLoad(string abName, string bundlePath, int byteLength, AssetBundle bundle)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (bundle == null)
        {
            Debug.LogError($"[ABManager] Android AB load failed after bytes: {abName} Bytes={byteLength} Path={bundlePath}");
            return;
        }

        if (!string.Equals(NormalizeABName(abName), "enemy_prefabs", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(NormalizeABName(abName), MainName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string[] assetNames = bundle.GetAllAssetNames();
        int sampleCount = Mathf.Min(assetNames.Length, 12);
        string sample = sampleCount > 0
            ? string.Join(", ", assetNames, 0, sampleCount)
            : "None";
        Debug.Log($"[ABManager] Android AB loaded Name={abName} Bytes={byteLength} AssetCount={assetNames.Length} Sample={sample}");
#endif
    }

    private void ExecuteCallbacks(string abName)
    {
        abName = NormalizeABName(abName);
        if (!loadingCallbacks.TryGetValue(abName, out List<UnityAction> callbacks))
        {
            return;
        }

        loadingCallbacks.Remove(abName);
        for (int i = 0; i < callbacks.Count; i++)
        {
            callbacks[i]?.Invoke();
        }
    }

    private IEnumerator Really(string abName, string resName, UnityAction<Object> callback)
    {
        abName = NormalizeABName(abName);
        bool abLoaded = false;
        LoadABAsync(abName, () => abLoaded = true);
        yield return new WaitUntil(() => abLoaded);

        if (!assetBundlesDictionary.TryGetValue(abName, out AssetBundle ab))
        {
            Debug.LogError($"[ABManager] Async load failed, AB not loaded: {abName}");
            callback?.Invoke(null);
            yield break;
        }

        AssetBundleRequest request = ab.LoadAssetAsync(resName);
        yield return request;
        callback?.Invoke(InstantiateIfGameObject(request.asset, resName));
    }

    private IEnumerator Really<T>(string abName, string resName, UnityAction<T> callback) where T : Object
    {
        abName = NormalizeABName(abName);
        bool abLoaded = false;
        LoadABAsync(abName, () => abLoaded = true);
        yield return new WaitUntil(() => abLoaded);

        if (!assetBundlesDictionary.TryGetValue(abName, out AssetBundle ab))
        {
            Debug.LogError($"[ABManager] Async load failed, AB not loaded: {abName}");
            callback?.Invoke(null);
            yield break;
        }

        AssetBundleRequest request = ab.LoadAssetAsync<T>(resName);
        yield return request;
        callback?.Invoke(InstantiateIfGameObject(request.asset, resName) as T);
    }

    private IEnumerator Really(string abName, string resName, Type type, UnityAction<Object> callback)
    {
        abName = NormalizeABName(abName);
        bool abLoaded = false;
        LoadABAsync(abName, () => abLoaded = true);
        yield return new WaitUntil(() => abLoaded);

        if (!assetBundlesDictionary.TryGetValue(abName, out AssetBundle ab))
        {
            Debug.LogError($"[ABManager] Async load failed, AB not loaded: {abName}");
            callback?.Invoke(null);
            yield break;
        }

        AssetBundleRequest request = ab.LoadAssetAsync(resName, type);
        yield return request;
        callback?.Invoke(InstantiateIfGameObject(request.asset, resName));
    }

    private IEnumerator ReallyLoadAsset<T>(string abName, string resName, UnityAction<T> callback) where T : Object
    {
        abName = NormalizeABName(abName);
        bool abLoaded = false;
        LoadABAsync(abName, () => abLoaded = true);
        yield return new WaitUntil(() => abLoaded);

        if (!assetBundlesDictionary.TryGetValue(abName, out AssetBundle ab))
        {
            Debug.LogError($"[ABManager] Async raw load failed, AB not loaded: {abName}");
            callback?.Invoke(null);
            yield break;
        }

        AssetBundleRequest request = ab.LoadAssetAsync<T>(resName);
        yield return request;
        Object loadedAsset = request.asset;
        if (loadedAsset == null)
        {
            yield return LoadAssetByFileNameAsync(ab, resName, typeof(T), asset => loadedAsset = asset);
        }

        if (loadedAsset == null)
        {
            Debug.LogError($"[ABManager] Raw asset load failed: {abName}/{resName}");
        }

        callback?.Invoke(loadedAsset as T);
    }

    private IEnumerator ReallyLoadAsset(string abName, string resName, Type type, UnityAction<Object> callback)
    {
        abName = NormalizeABName(abName);
        bool abLoaded = false;
        LoadABAsync(abName, () => abLoaded = true);
        yield return new WaitUntil(() => abLoaded);

        if (!assetBundlesDictionary.TryGetValue(abName, out AssetBundle ab))
        {
            Debug.LogError($"[ABManager] Async raw load failed, AB not loaded: {abName}");
            callback?.Invoke(null);
            yield break;
        }

        AssetBundleRequest request = ab.LoadAssetAsync(resName, type);
        yield return request;
        Object loadedAsset = request.asset;
        if (loadedAsset == null)
        {
            yield return LoadAssetByFileNameAsync(ab, resName, type, asset => loadedAsset = asset);
        }

        if (loadedAsset == null)
        {
            Debug.LogError($"[ABManager] Raw asset load failed: {abName}/{resName}");
        }

        callback?.Invoke(loadedAsset);
    }

    private IEnumerator LoadAssetByFileNameAsync(AssetBundle ab, string resName, Type type, UnityAction<Object> callback)
    {
        if (ab == null || string.IsNullOrEmpty(resName))
        {
            callback?.Invoke(null);
            yield break;
        }

        string[] assetNames = ab.GetAllAssetNames();
        string lowerResName = resName.ToLowerInvariant();
        string matchedAssetName = null;
        for (int i = 0; i < assetNames.Length; i++)
        {
            string assetName = assetNames[i];
            if (string.IsNullOrEmpty(assetName))
            {
                continue;
            }

            string fileName = Path.GetFileNameWithoutExtension(assetName);
            if (string.Equals(fileName, lowerResName, StringComparison.OrdinalIgnoreCase))
            {
                matchedAssetName = assetName;
                break;
            }
        }

        if (string.IsNullOrEmpty(matchedAssetName))
        {
            callback?.Invoke(null);
            yield break;
        }

        AssetBundleRequest request = ab.LoadAssetAsync(matchedAssetName, type);
        yield return request;
        callback?.Invoke(request.asset);
    }

    public void UnloadRes(string abName, bool unloadAllLoadedObjects = false)
    {
        abName = NormalizeABName(abName);
        if (assetBundlesDictionary.TryGetValue(abName, out AssetBundle ab))
        {
            ab.Unload(unloadAllLoadedObjects);
            assetBundlesDictionary.Remove(abName);
            Debug.Log($"[ABManager] AB unloaded: {abName}");
        }
    }

    public void UnloadAllRes(bool unloadAllLoadedObjects = false)
    {
        AssetBundle.UnloadAllAssetBundles(unloadAllLoadedObjects);
        assetBundlesDictionary.Clear();
        loadingCallbacks.Clear();
        mainBundleCallbacks.Clear();
        mainAb = null;
        manifest = null;
        isMainBundleLoading = false;
        Debug.Log("[ABManager] All AB unloaded");
    }
}
