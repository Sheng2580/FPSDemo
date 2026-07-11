using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CombatNavigationTools
{
    private const string ClimbableLayerName = "Climbable";
    private const string GroundLayerName = "Ground";
    private const string BarrierLayerName = "barrier";
    private const string ClimbableRootName = "Climbable";

    [MenuItem("FPSDemo/Navigation/配置Climbable并烘焙当前场景")]
    public static void ConfigureClimbableAndBakeCurrentScene()
    {
        int climbableLayer = EnsureLayer(ClimbableLayerName);
        ConfigureClimbableObjects(climbableLayer);
        ConfigureNavMeshSurfaces(climbableLayer);
        BakeCurrentSceneNavMesh();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    [MenuItem("FPSDemo/Navigation/只配置Climbable导航层")]
    public static void ConfigureClimbableOnly()
    {
        int climbableLayer = EnsureLayer(ClimbableLayerName);
        ConfigureClimbableObjects(climbableLayer);
        ConfigureNavMeshSurfaces(climbableLayer);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
    }

    [MenuItem("FPSDemo/Navigation/只烘焙当前场景NavMesh")]
    public static void BakeCurrentSceneNavMesh()
    {
        NavMeshSurface[] surfaces = Object.FindObjectsOfType<NavMeshSurface>(true);
        if (surfaces == null || surfaces.Length == 0)
        {
            Debug.LogWarning("[CombatNav] 当前场景没有 NavMeshSurface，无法烘焙");
            return;
        }

        for (int i = 0; i < surfaces.Length; i++)
        {
            NavMeshSurface surface = surfaces[i];
            if (surface == null)
            {
                continue;
            }

            surface.BuildNavMesh();
            EditorUtility.SetDirty(surface);
            Debug.Log($"[CombatNav] 已烘焙 NavMeshSurface={surface.name}", surface);
        }
    }

    private static void ConfigureClimbableObjects(int climbableLayer)
    {
        GameObject climbableRoot = GameObject.Find(ClimbableRootName);
        if (climbableRoot == null)
        {
            Debug.LogWarning("[CombatNav] 场景中没有找到 Climbable 根物体");
            return;
        }

        Transform[] children = climbableRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == null)
            {
                continue;
            }

            child.gameObject.layer = climbableLayer;
            EditorUtility.SetDirty(child.gameObject);
        }

        ConfigureClimbableModifier(climbableRoot);

        Debug.Log($"[CombatNav] 已设置 Climbable 层级 Count={children.Length}", climbableRoot);
    }

    private static void ConfigureClimbableModifier(GameObject climbableRoot)
    {
        NavMeshModifier modifier = climbableRoot.GetComponent<NavMeshModifier>();
        if (modifier == null)
        {
            modifier = climbableRoot.AddComponent<NavMeshModifier>();
        }

#if UNITY_2022_2_OR_NEWER
        modifier.applyToChildren = true;
        modifier.overrideGenerateLinks = true;
        modifier.generateLinks = true;
#endif
        modifier.ignoreFromBuild = false;
        EditorUtility.SetDirty(modifier);
    }

    private static void ConfigureNavMeshSurfaces(int climbableLayer)
    {
        int layerMask = 0;
        AddLayerToMask(ref layerMask, GroundLayerName);
        AddLayerToMask(ref layerMask, BarrierLayerName);
        layerMask |= 1 << climbableLayer;

        NavMeshSurface[] surfaces = Object.FindObjectsOfType<NavMeshSurface>(true);
        for (int i = 0; i < surfaces.Length; i++)
        {
            NavMeshSurface surface = surfaces[i];
            if (surface == null)
            {
                continue;
            }

            surface.layerMask = layerMask;
            surface.collectObjects = CollectObjects.All;
            surface.ignoreNavMeshAgent = true;
            surface.ignoreNavMeshObstacle = true;
            EnableGeneratedLinks(surface);
            EditorUtility.SetDirty(surface);
            Debug.Log($"[CombatNav] 已配置 NavMeshSurface={surface.name} LayerMask={layerMask}", surface);
        }
    }

    private static void EnableGeneratedLinks(NavMeshSurface surface)
    {
#if UNITY_2022_2_OR_NEWER
        SerializedObject serializedObject = new SerializedObject(surface);
        SerializedProperty generateLinks = serializedObject.FindProperty("m_GenerateLinks");
        if (generateLinks != null)
        {
            generateLinks.boolValue = true;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
#endif
    }

    private static int EnsureLayer(string layerName)
    {
        int existingLayer = LayerMask.NameToLayer(layerName);
        if (existingLayer >= 0)
        {
            return existingLayer;
        }

        SerializedObject tagManager = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");

        for (int i = 8; i < layers.arraySize; i++)
        {
            SerializedProperty layer = layers.GetArrayElementAtIndex(i);
            if (!string.IsNullOrEmpty(layer.stringValue))
            {
                continue;
            }

            layer.stringValue = layerName;
            tagManager.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"[CombatNav] 已创建 Layer={layerName} Index={i}");
            return i;
        }

        Debug.LogWarning($"[CombatNav] 没有空 Layer 可以创建 {layerName}");
        return 0;
    }

    private static void AddLayerToMask(ref int mask, string layerName)
    {
        int layer = LayerMask.NameToLayer(layerName);
        if (layer < 0)
        {
            Debug.LogWarning($"[CombatNav] 找不到 Layer={layerName}");
            return;
        }

        mask |= 1 << layer;
    }
}
