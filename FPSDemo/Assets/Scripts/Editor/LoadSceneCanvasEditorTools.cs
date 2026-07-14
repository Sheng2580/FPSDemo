using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class LoadSceneCanvasEditorTools
{
    private const string PrefabPath = "Assets/Art/ABRes/UI/LoadSceneCanvas.prefab";
    private const string BundleName = "uipanel";

    [MenuItem("FPSDemo/UI/配置LoadSceneCanvas")]
    public static void ConfigureLoadSceneCanvas()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (root == null)
        {
            Debug.LogError("[LoadSceneCanvas] 找不到 Prefab");
            return;
        }

        try
        {
            if (root.GetComponent<LoadSceneCanvas>() == null)
            {
                root.AddComponent<LoadSceneCanvas>();
            }

            if (root.GetComponent<CanvasGroup>() == null)
            {
                root.AddComponent<CanvasGroup>();
            }

            RectTransform rect = root.transform as RectTransform;
            if (rect != null)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.anchoredPosition = Vector2.zero;
                rect.localScale = Vector3.one;
                rect.localRotation = Quaternion.identity;
            }

            // Prefab 默认失活 由 UIManager 在场景切换时激活
            root.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetImporter importer = AssetImporter.GetAtPath(PrefabPath);
        if (importer != null)
        {
            importer.assetBundleName = BundleName;
            importer.SaveAndReimport();
        }

        LoadSceneCanvas[] sceneInstances = Object.FindObjectsOfType<LoadSceneCanvas>(true);
        for (int i = 0; i < sceneInstances.Length; i++)
        {
            LoadSceneCanvas sceneInstance = sceneInstances[i];
            if (sceneInstance == null || !sceneInstance.gameObject.scene.IsValid())
            {
                continue;
            }

            sceneInstance.gameObject.SetActive(false);
            EditorUtility.SetDirty(sceneInstance.gameObject);
            EditorSceneManager.MarkSceneDirty(sceneInstance.gameObject.scene);
            EditorSceneManager.SaveScene(sceneInstance.gameObject.scene);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[LoadSceneCanvas] Prefab 和 AssetBundle 已配置");
    }
}
