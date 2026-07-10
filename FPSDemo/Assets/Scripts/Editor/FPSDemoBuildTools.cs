using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.UI;

public static class FPSDemoBuildTools
{
    private const string TouchCanvasPath = "Assets/Art/ABRes/UI/TounchControllerCanvas.prefab";
    private const string TestCanvasPath = "Assets/Art/ABRes/UI/TestCanvas.prefab";
    private const string TouchCanvasBundleName = "uipanel";
    private const string CombatFeedbackBundleName = "combat_feedback";
    private const string EnemyPrefabBundleName = "enemy_prefabs";
    private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
    private const string StreamingAssetsPath = "Assets/StreamingAssets";
    private const string AndroidBuildPath = "Builds/Android/FPSDemo.apk";
    private const int InvalidActiveInputHandler = -1;
    private const int InputSystemPackageOnly = 1;

    private static readonly string[] ObsoleteAssetBundleAssetPaths =
    {
        "Assets/Art/ABRes/Cube.prefab"
    };

    private static readonly string[] UIAssetPaths =
    {
        TouchCanvasPath,
        TestCanvasPath
    };

    private static readonly string[] CombatFeedbackAssetPaths =
    {
        "Assets/Art/ABRes/CombatFeedback/CombatFeedbackResources.asset",
        "Assets/Art/ABRes/CombatFeedback/Effects/Muzzle Flash.prefab",
        "Assets/Art/ABRes/CombatFeedback/Effects/Muzzle Smoke.prefab",
        "Assets/Art/ABRes/CombatFeedback/Effects/Blood Impact.prefab",
        "Assets/Art/ABRes/CombatFeedback/Effects/Stone Impact.prefab",
        "Assets/Art/ABRes/CombatFeedback/Effects/Metal Impact.prefab",
        "Assets/Art/ABRes/CombatFeedback/Effects/Wood Impact.prefab",
        "Assets/Art/ABRes/CombatFeedback/Effects/MuzzleFlashes/MuzzleFlash1.prefab",
        "Assets/Art/ABRes/CombatFeedback/Effects/MuzzleFlashes/MuzzleFlash2.prefab",
        "Assets/Art/ABRes/CombatFeedback/Effects/MuzzleFlashes/MuzzleFlash3.prefab",
        "Assets/Art/ABRes/CombatFeedback/Effects/MuzzleFlashes/MuzzleFlash4.prefab",
        "Assets/Art/ABRes/CombatFeedback/Effects/MuzzleFlashes/MuzzleFlash5.prefab",
        "Assets/Art/ABRes/CombatFeedback/Effects/MuzzleFlashes/MuzzleFlash6.prefab",
        "Assets/Art/ABRes/CombatFeedback/Effects/MuzzleFlashes/MuzzleFlash7.prefab",
        "Assets/Art/ABRes/CombatFeedback/Effects/MuzzleFlashes/MuzzleFlash8.prefab",
        "Assets/Art/ABRes/CombatFeedback/Effects/MuzzleFlashes/MuzzleFlash9.prefab",
        "Assets/Art/ABRes/CombatFeedback/Effects/MuzzleFlashes/MuzzleFlash10.prefab",
        "Assets/KriptoFX/MuzzleFlashes/Prefab/Impacts/Mobile/ConcreteImpact.prefab",
        "Assets/KriptoFX/MuzzleFlashes/Prefab/Impacts/Mobile/MetalImpact.prefab",
        "Assets/KriptoFX/MuzzleFlashes/Prefab/Impacts/Mobile/WoodImpact.prefab",
        "Assets/Art/ABRes/CombatFeedback/Audio/Pistol_1 Fire.wav",
        "Assets/Art/ABRes/CombatFeedback/Audio/Assault Rifle_1 Fire.wav",
        "Assets/Art/ABRes/CombatFeedback/Audio/Hitmarker.wav",
        "Assets/Art/ABRes/CombatFeedback/Audio/Impacts/SFX_Vefects_Shots_Squib_Concrete.wav",
        "Assets/Art/ABRes/CombatFeedback/Audio/Impacts/SFX_Vefects_Shots_Squib_Metal.wav",
        "Assets/Art/ABRes/CombatFeedback/Audio/Impacts/SFX_Vefects_Shots_Squib_Wood.wav",
        "Assets/Art/ABRes/CombatFeedback/Audio/Impacts/SFX_Vefects_Shots_Destruction_Fruit.wav",
        "Assets/Art/ABRes/CombatFeedback/Audio/Impacts/SFX_Vefects_Shots_Squib_Glass.wav"
    };

    private static readonly string[] EnemyPrefabAssetPaths =
    {
        "Assets/Art/ABRes/Enemies/Prefabs/Enemy_ZombieSkeleton_LOD2.prefab"
    };

    private static readonly string[] RequiredRuntimeBundleNames =
    {
        TouchCanvasBundleName,
        CombatFeedbackBundleName,
        EnemyPrefabBundleName
    };

    [MenuItem("FPSDemo/Build/检查移动端UI", priority = 0)]
    public static void CheckMobileUI()
    {
        ValidateMobileUI(true);
    }

    [MenuItem("FPSDemo/Build/修复移动端UI Prefab", priority = 1)]
    public static void FixMobileUIPrefab()
    {
        FixMobileUIPrefabInternal(true);
        ValidateMobileUI(true);
    }

    [MenuItem("FPSDemo/Build/修复Android兼容设置", priority = 2)]
    public static void FixAndroidCompatibilitySettings()
    {
        FixAndroidCompatibilitySettingsInternal(true);
    }

    [MenuItem("FPSDemo/Build/配置运行时AssetBundle资源", priority = 3)]
    public static void FixRuntimeAssetBundleResources()
    {
        FixRuntimeAssetBundleResourcesInternal(true);
        ValidateRuntimeAssetBundleResources(true);
    }

    [MenuItem("FPSDemo/Build/打当前平台AssetBundle", priority = 20)]
    public static void BuildCurrentPlatformAssetBundles()
    {
        CombatFeedbackMaterialTools.FixCombatFeedbackMaterials(false);
        FixMobileUIPrefabInternal(false);
        FixRuntimeAssetBundleResourcesInternal(false);
        BuildAssetBundles(EditorUserBuildSettings.activeBuildTarget);
    }

    [MenuItem("FPSDemo/Build/打Android AssetBundle", priority = 21)]
    public static void BuildAndroidAssetBundles()
    {
        FixAndroidCompatibilitySettingsInternal(false);
        CombatFeedbackMaterialTools.FixCombatFeedbackMaterials(false);
        FixMobileUIPrefabInternal(false);
        FixRuntimeAssetBundleResourcesInternal(false);
        BuildAssetBundles(BuildTarget.Android, true);
        ValidateStreamingBundle("Android", true);
    }

    [MenuItem("FPSDemo/Build/检查并打Android AssetBundle", priority = 22)]
    public static void CheckAndBuildAndroidAssetBundles()
    {
        FixAndroidCompatibilitySettingsInternal(false);
        CombatFeedbackMaterialTools.FixCombatFeedbackMaterials(false);
        FixMobileUIPrefabInternal(false);
        FixRuntimeAssetBundleResourcesInternal(false);

        if (!ValidateMobileUI(true) || !ValidateRuntimeAssetBundleResources(true))
        {
            throw new BuildFailedException("运行时 AssetBundle 资源检查失败");
        }

        BuildAssetBundles(BuildTarget.Android, true);
        ValidateStreamingBundle("Android", true);
    }

    [MenuItem("FPSDemo/Build/准备编辑器和Android AssetBundle", priority = 23)]
    public static void PrepareEditorAndAndroidAssetBundles()
    {
        FixAndroidCompatibilitySettingsInternal(false);
        CombatFeedbackMaterialTools.FixCombatFeedbackMaterials(false);
        FixMobileUIPrefabInternal(false);
        FixRuntimeAssetBundleResourcesInternal(false);

        if (!ValidateMobileUI(true) || !ValidateRuntimeAssetBundleResources(true))
        {
            throw new BuildFailedException("运行时 AssetBundle 资源检查失败");
        }

        string editorMainBundleName = GetMainBundleName(EditorUserBuildSettings.activeBuildTarget);
        BuildAssetBundles(EditorUserBuildSettings.activeBuildTarget);
        ValidateStreamingBundle(editorMainBundleName, true);

        BuildAssetBundles(BuildTarget.Android);
        ValidateStreamingBundle("Android", true);
    }

    [MenuItem("FPSDemo/Build/打Android APK", priority = 40)]
    public static void BuildAndroidApk()
    {
        int previousActiveInputHandler = GetActiveInputHandler();
        FixAndroidCompatibilitySettingsInternal(false);

        try
        {
            SetActiveInputHandler(InputSystemPackageOnly);
            CombatFeedbackMaterialTools.FixCombatFeedbackMaterials(false);
            FixMobileUIPrefabInternal(false);
            FixRuntimeAssetBundleResourcesInternal(false);

            if (!ValidateMobileUI(true) || !ValidateRuntimeAssetBundleResources(true))
            {
                throw new BuildFailedException("运行时 AssetBundle 资源检查失败");
            }

            DeleteOldAndroidApk();
            BuildAssetBundles(BuildTarget.Android, true);
            ValidateStreamingBundle("Android", true);
            ValidateOnlyTargetStreamingAssets(BuildTarget.Android, true);

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            }

            string[] scenes = GetEnabledBuildScenes();
            if (scenes.Length == 0)
            {
                throw new BuildFailedException("Build Settings 里没有启用场景");
            }

            string outputDirectory = Path.GetDirectoryName(AndroidBuildPath);
            if (!string.IsNullOrEmpty(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            BuildPlayerOptions buildOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = AndroidBuildPath,
                target = BuildTarget.Android,
                options = BuildOptions.CleanBuildCache
            };

            BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException($"Android APK 打包失败 {report.summary.result}");
            }

            Debug.Log($"[FPSDemoBuildTools] Android APK 打包完成: {AndroidBuildPath}");
        }
        finally
        {
            RestoreActiveInputHandler(previousActiveInputHandler);
        }
    }

    private static void DeleteOldAndroidApk()
    {
        if (File.Exists(AndroidBuildPath))
        {
            File.Delete(AndroidBuildPath);
        }

        string symbolsPath = Path.ChangeExtension(AndroidBuildPath, ".symbols.zip");
        if (File.Exists(symbolsPath))
        {
            File.Delete(symbolsPath);
        }
    }

    private static int GetActiveInputHandler()
    {
        SerializedObject serializedProjectSettings = GetSerializedProjectSettings();
        SerializedProperty activeInputHandler = serializedProjectSettings?.FindProperty("activeInputHandler");
        return activeInputHandler != null ? activeInputHandler.intValue : InvalidActiveInputHandler;
    }

    private static void SetActiveInputHandler(int activeInputHandlerValue)
    {
        SerializedObject serializedProjectSettings = GetSerializedProjectSettings();
        SerializedProperty activeInputHandler = serializedProjectSettings?.FindProperty("activeInputHandler");
        if (activeInputHandler == null || activeInputHandler.intValue == activeInputHandlerValue)
        {
            return;
        }

        serializedProjectSettings.Update();
        activeInputHandler.intValue = activeInputHandlerValue;
        serializedProjectSettings.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
    }

    private static void RestoreActiveInputHandler(int previousActiveInputHandler)
    {
        if (previousActiveInputHandler == InvalidActiveInputHandler)
        {
            return;
        }

        SetActiveInputHandler(previousActiveInputHandler);
    }

    private static SerializedObject GetSerializedProjectSettings()
    {
        UnityEngine.Object[] projectSettingsAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
        if (projectSettingsAssets == null || projectSettingsAssets.Length == 0)
        {
            return null;
        }

        return new SerializedObject(projectSettingsAssets[0]);
    }

    private static void FixAndroidCompatibilitySettingsInternal(bool logResult)
    {
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel23;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.buildApkPerCpuArchitecture = false;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;

        UnityEngine.Object[] projectSettingsAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
        if (projectSettingsAssets.Length > 0)
        {
            EditorUtility.SetDirty(projectSettingsAssets[0]);
        }

        AssetDatabase.SaveAssets();

        if (logResult)
        {
            Debug.Log("[FPSDemoBuildTools] Android 兼容设置已修复: ARM64, IL2CPP, Min API 23, Target API Auto");
        }
    }

    private static bool FixMobileUIPrefabInternal(bool logResult)
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(TouchCanvasPath);
        bool changed = false;

        try
        {
            if (prefabRoot == null)
            {
                Debug.LogError($"[FPSDemoBuildTools] 找不到移动端 UI Prefab: {TouchCanvasPath}");
                return false;
            }

            if (prefabRoot.name != nameof(TounchControllerCanvas))
            {
                prefabRoot.name = nameof(TounchControllerCanvas);
                changed = true;
            }

            RectTransform rect = prefabRoot.GetComponent<RectTransform>();
            if (rect == null)
            {
                rect = prefabRoot.AddComponent<RectTransform>();
                changed = true;
            }

            changed |= SetFullScreenRect(rect);

            if (prefabRoot.GetComponent<TounchControllerCanvas>() == null)
            {
                prefabRoot.AddComponent<TounchControllerCanvas>();
                changed = true;
            }

            if (prefabRoot.GetComponent<Canvas>() == null)
            {
                prefabRoot.AddComponent<Canvas>();
                changed = true;
            }

            if (prefabRoot.GetComponent<GraphicRaycaster>() == null)
            {
                prefabRoot.AddComponent<GraphicRaycaster>();
                changed = true;
            }

            if (prefabRoot.GetComponent<CanvasGroup>() == null)
            {
                prefabRoot.AddComponent<CanvasGroup>();
                changed = true;
            }

            CanvasScaler scaler = prefabRoot.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                UnityEngine.Object.DestroyImmediate(scaler, true);
                changed = true;
            }

            AssetImporter importer = AssetImporter.GetAtPath(TouchCanvasPath);
            if (importer != null && importer.assetBundleName != TouchCanvasBundleName)
            {
                importer.assetBundleName = TouchCanvasBundleName;
                changed = true;
            }

            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, TouchCanvasPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }
        finally
        {
            if (prefabRoot != null)
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        if (logResult)
        {
            Debug.Log(changed
                ? "[FPSDemoBuildTools] 移动端 UI Prefab 已修复"
                : "[FPSDemoBuildTools] 移动端 UI Prefab 不需要修复");
        }

        return true;
    }

    private static bool FixRuntimeAssetBundleResourcesInternal(bool logResult)
    {
        bool changed = false;
        bool success = true;

        success &= TrySetAssetBundleNames(UIAssetPaths, TouchCanvasBundleName, ref changed);
        success &= TrySetAssetBundleNames(CombatFeedbackAssetPaths, CombatFeedbackBundleName, ref changed);
        success &= TrySetAssetBundleNames(EnemyPrefabAssetPaths, EnemyPrefabBundleName, ref changed);
        success &= TryClearAssetBundleNames(ObsoleteAssetBundleAssetPaths, ref changed);

        if (changed)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        if (logResult)
        {
            Debug.Log(success
                ? "[FPSDemoBuildTools] 运行时 AssetBundle 资源包名已配置"
                : "[FPSDemoBuildTools] 运行时 AssetBundle 资源包名配置失败");
        }

        return success;
    }

    private static bool TrySetAssetBundleNames(IEnumerable<string> assetPaths, string bundleName, ref bool changed)
    {
        bool success = true;
        foreach (string assetPath in assetPaths)
        {
            success &= TrySetAssetBundleName(assetPath, bundleName, ref changed);
        }

        return success;
    }

    private static bool TryClearAssetBundleNames(IEnumerable<string> assetPaths, ref bool changed)
    {
        bool success = true;
        foreach (string assetPath in assetPaths)
        {
            success &= TrySetAssetBundleName(assetPath, string.Empty, ref changed);
        }

        return success;
    }

    private static bool TrySetAssetBundleName(string assetPath, string bundleName, ref bool changed)
    {
        AssetImporter importer = AssetImporter.GetAtPath(assetPath);
        if (importer == null)
        {
            Debug.LogError($"[FPSDemoBuildTools] 找不到 AB 资源: {assetPath}");
            return false;
        }

        if (importer.assetBundleName == bundleName)
        {
            return true;
        }

        importer.assetBundleName = bundleName;
        changed = true;
        return true;
    }

    private static bool ValidateMobileUI(bool logResult)
    {
        bool isValid = true;

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TouchCanvasPath);
        if (prefab == null)
        {
            Debug.LogError($"[FPSDemoBuildTools] 找不到移动端 UI Prefab: {TouchCanvasPath}");
            return false;
        }

        isValid &= Check(prefab.GetComponent<TounchControllerCanvas>() != null, "TounchControllerCanvas 组件存在");

        RectTransform rect = prefab.GetComponent<RectTransform>();
        isValid &= Check(rect != null, "根节点 RectTransform 存在");
        if (rect != null)
        {
            isValid &= Check(rect.localScale == Vector3.one, "根节点缩放为 1");
            isValid &= Check(rect.anchorMin == Vector2.zero && rect.anchorMax == Vector2.one, "根节点使用全屏锚点");
            isValid &= Check(rect.offsetMin == Vector2.zero && rect.offsetMax == Vector2.zero, "根节点偏移为 0");
        }

        isValid &= Check(prefab.GetComponent<CanvasScaler>() == null, "面板不单独挂 CanvasScaler");

        AssetImporter importer = AssetImporter.GetAtPath(TouchCanvasPath);
        isValid &= Check(importer != null && importer.assetBundleName == TouchCanvasBundleName, "Prefab 标记到 uipanel AssetBundle");
        isValid &= Check(IsSceneInBuildSettings(SampleScenePath), "SampleScene 已加入 Build Settings");

        if (logResult && isValid)
        {
            Debug.Log("[FPSDemoBuildTools] 移动端 UI 检查通过");
        }

        return isValid;
    }

    private static bool ValidateRuntimeAssetBundleResources(bool logResult)
    {
        bool isValid = true;
        isValid &= ValidateAssetBundleNames(UIAssetPaths, TouchCanvasBundleName);
        isValid &= ValidateAssetBundleNames(CombatFeedbackAssetPaths, CombatFeedbackBundleName);
        isValid &= ValidateAssetBundleNames(EnemyPrefabAssetPaths, EnemyPrefabBundleName);

        if (logResult && isValid)
        {
            Debug.Log("[FPSDemoBuildTools] 运行时 AssetBundle 资源检查通过");
        }

        return isValid;
    }

    private static bool ValidateAssetBundleNames(IEnumerable<string> assetPaths, string bundleName)
    {
        bool isValid = true;
        foreach (string assetPath in assetPaths)
        {
            AssetImporter importer = AssetImporter.GetAtPath(assetPath);
            isValid &= Check(importer != null && importer.assetBundleName == bundleName, $"{assetPath} 标记到 {bundleName}");
        }

        return isValid;
    }

    private static bool Check(bool condition, string message)
    {
        if (condition)
        {
            Debug.Log($"[FPSDemoBuildTools] OK {message}");
            return true;
        }

        Debug.LogError($"[FPSDemoBuildTools] 失败 {message}");
        return false;
    }

    private static bool SetFullScreenRect(RectTransform rect)
    {
        bool changed = false;
        changed |= SetVector2(() => rect.anchorMin, value => rect.anchorMin = value, Vector2.zero);
        changed |= SetVector2(() => rect.anchorMax, value => rect.anchorMax = value, Vector2.one);
        changed |= SetVector2(() => rect.offsetMin, value => rect.offsetMin = value, Vector2.zero);
        changed |= SetVector2(() => rect.offsetMax, value => rect.offsetMax = value, Vector2.zero);
        changed |= SetVector2(() => rect.anchoredPosition, value => rect.anchoredPosition = value, Vector2.zero);
        changed |= SetVector2(() => rect.pivot, value => rect.pivot = value, new Vector2(0.5f, 0.5f));
        changed |= SetVector3(() => rect.localPosition, value => rect.localPosition = value, Vector3.zero);
        changed |= SetVector3(() => rect.localScale, value => rect.localScale = value, Vector3.one);

        if (rect.localRotation != Quaternion.identity)
        {
            rect.localRotation = Quaternion.identity;
            changed = true;
        }

        return changed;
    }

    private static bool SetVector2(Func<Vector2> getter, Action<Vector2> setter, Vector2 targetValue)
    {
        if (getter() == targetValue)
        {
            return false;
        }

        setter(targetValue);
        return true;
    }

    private static bool SetVector3(Func<Vector3> getter, Action<Vector3> setter, Vector3 targetValue)
    {
        if (getter() == targetValue)
        {
            return false;
        }

        setter(targetValue);
        return true;
    }

    private static void BuildAssetBundles(BuildTarget target)
    {
        BuildAssetBundles(target, false);
    }

    private static void BuildAssetBundles(BuildTarget target, bool cleanStreamingAssetsForTarget)
    {
        if (cleanStreamingAssetsForTarget)
        {
            CleanStreamingAssetsBeforeSinglePlatformBuild(target);
        }

        string outputPath = GetPlatformBundleOutputPath(target);
        RemoveLegacyPlatformBundleFile(outputPath);
        Directory.CreateDirectory(outputPath);

        AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(
            outputPath,
            BuildAssetBundleOptions.ForceRebuildAssetBundle,
            target);

        if (manifest == null)
        {
            throw new BuildFailedException($"AssetBundle 打包失败 {target}");
        }

        AssetDatabase.Refresh();
        Debug.Log($"[FPSDemoBuildTools] AssetBundle 打包完成: {target} -> {outputPath}");
    }

    private static void CleanStreamingAssetsBeforeSinglePlatformBuild(BuildTarget target)
    {
        Directory.CreateDirectory(StreamingAssetsPath);

        string[] entries = Directory.GetFileSystemEntries(StreamingAssetsPath);
        for (int i = 0; i < entries.Length; i++)
        {
            DeleteFileOrDirectory(entries[i]);
        }

        AssetDatabase.Refresh();
        Debug.Log($"[FPSDemoBuildTools] 已清理 StreamingAssets 旧平台资源 准备打包 {target}");
    }

    private static void RemoveLegacyPlatformBundleFile(string outputPath)
    {
        if (!File.Exists(outputPath))
        {
            return;
        }

        DeleteAssetIfExists(outputPath);
        DeleteAssetIfExists(outputPath + ".manifest");
        AssetDatabase.Refresh();
    }

    private static void DeleteAssetIfExists(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
        {
            return;
        }

        if (AssetDatabase.DeleteAsset(assetPath))
        {
            return;
        }

        DeleteFileOrDirectory(assetPath);
    }

    private static void DeleteFileOrDirectory(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        if (File.Exists(path))
        {
            FileUtil.DeleteFileOrDirectory(path);
            FileUtil.DeleteFileOrDirectory(path + ".meta");
            return;
        }

        if (Directory.Exists(path))
        {
            FileUtil.DeleteFileOrDirectory(path);
            FileUtil.DeleteFileOrDirectory(path + ".meta");
        }
    }

    private static bool ValidateStreamingBundle(string mainBundleName, bool throwOnFail)
    {
        string platformPath = Path.Combine(StreamingAssetsPath, mainBundleName);
        bool isValid = File.Exists(Path.Combine(platformPath, mainBundleName));
        for (int i = 0; i < RequiredRuntimeBundleNames.Length; i++)
        {
            isValid &= File.Exists(Path.Combine(platformPath, RequiredRuntimeBundleNames[i]));
        }

        if (isValid)
        {
            Debug.Log($"[FPSDemoBuildTools] StreamingAssets 检查通过: {mainBundleName}");
            return true;
        }

        Debug.LogError($"[FPSDemoBuildTools] StreamingAssets 平台目录缺少主包或运行时资源包: {platformPath}");

        if (throwOnFail)
        {
            throw new BuildFailedException("StreamingAssets 资源检查失败");
        }

        return false;
    }

    private static bool ValidateOnlyTargetStreamingAssets(BuildTarget target, bool throwOnFail)
    {
        string targetMainBundleName = GetMainBundleName(target);
        bool isValid = true;

        if (Directory.Exists(StreamingAssetsPath))
        {
            string[] entries = Directory.GetFileSystemEntries(StreamingAssetsPath);
            for (int i = 0; i < entries.Length; i++)
            {
                string entryName = Path.GetFileName(entries[i]);
                if (string.Equals(entryName, targetMainBundleName, StringComparison.Ordinal)
                    || string.Equals(entryName, targetMainBundleName + ".meta", StringComparison.Ordinal))
                {
                    continue;
                }

                isValid = false;
                Debug.LogError($"[FPSDemoBuildTools] StreamingAssets 存在非目标平台残留: {entries[i]}");
            }
        }

        if (isValid)
        {
            Debug.Log($"[FPSDemoBuildTools] StreamingAssets 只保留目标平台资源: {targetMainBundleName}");
            return true;
        }

        if (throwOnFail)
        {
            throw new BuildFailedException("StreamingAssets 存在非目标平台残留");
        }

        return false;
    }

    private static string GetPlatformBundleOutputPath(BuildTarget target)
    {
        return Path.Combine(StreamingAssetsPath, GetMainBundleName(target));
    }

    private static string GetMainBundleName(BuildTarget target)
    {
        switch (target)
        {
            case BuildTarget.Android:
                return "Android";
            case BuildTarget.iOS:
                return "iOS";
            case BuildTarget.StandaloneWindows:
            case BuildTarget.StandaloneWindows64:
                return "StandaloneWindows";
            case BuildTarget.StandaloneOSX:
                return "StandaloneOSXUniversal";
            case BuildTarget.StandaloneLinux64:
                return "StandaloneLinux64";
            default:
                return target.ToString();
        }
    }

    private static bool IsSceneInBuildSettings(string scenePath)
    {
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (scene.enabled && scene.path == scenePath)
            {
                return true;
            }
        }

        return false;
    }

    private static string[] GetEnabledBuildScenes()
    {
        int count = 0;
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (scene.enabled)
            {
                count++;
            }
        }

        string[] scenes = new string[count];
        int index = 0;
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (scene.enabled)
            {
                scenes[index] = scene.path;
                index++;
            }
        }

        return scenes;
    }
}
