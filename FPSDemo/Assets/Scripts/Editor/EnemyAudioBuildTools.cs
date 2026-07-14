using Enemy;
using UnityEditor;
using UnityEngine;

public static class EnemyAudioBuildTools
{
    private static readonly string[] EnemyPrefabPaths =
    {
        "Assets/Art/ABRes/Enemies/Prefabs/Enemy_ZombieSkeleton_LOD2.prefab",
        "Assets/Art/ABRes/Enemies/Prefabs/Enemy_ZombieNerd_LOD2.prefab",
        "Assets/Art/ABRes/Enemies/Prefabs/Enemy_ZombieOldCrone_LOD2.prefab"
    };

    [MenuItem("FPSDemo/Enemy/配置敌人音效组件")]
    public static void ConfigureEnemyAudioComponents()
    {
        ConfigureEnemyAudioComponents(true);
    }

    public static bool ConfigureEnemyAudioComponents(bool logResult)
    {
        bool success = true;
        bool changed = false;

        for (int i = 0; i < EnemyPrefabPaths.Length; i++)
        {
            string prefabPath = EnemyPrefabPaths[i];
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null)
            {
                Debug.LogError($"[EnemyAudioBuildTools] 找不到敌人Prefab {prefabPath}");
                success = false;
                continue;
            }

            try
            {
                bool prefabChanged = false;
                EnemyController controller = root.GetComponent<EnemyController>();
                if (controller == null)
                {
                    Debug.LogError($"[EnemyAudioBuildTools] 敌人缺少EnemyController {prefabPath}");
                    success = false;
                    continue;
                }

                EnemyAudioController audioController = root.GetComponent<EnemyAudioController>();
                if (audioController == null)
                {
                    audioController = root.AddComponent<EnemyAudioController>();
                    prefabChanged = true;
                }

                prefabChanged |= SetObject(audioController, "controller", controller);
                prefabChanged |= SetObject(controller, "audioController", audioController);
                changed |= prefabChanged;

                if (prefabChanged)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        if (changed)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        if (logResult)
        {
            Debug.Log(success
                ? "[EnemyAudioBuildTools] 敌人音效组件配置完成"
                : "[EnemyAudioBuildTools] 敌人音效组件配置失败");
        }

        return success;
    }

    private static bool SetObject(Object target, string propertyName, Object value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null || property.objectReferenceValue == value)
        {
            return false;
        }

        property.objectReferenceValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }
}
