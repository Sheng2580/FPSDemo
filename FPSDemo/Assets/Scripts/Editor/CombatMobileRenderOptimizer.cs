using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class CombatMobileRenderOptimizer
{
    private const string CombatScenePath = "Assets/Scenes/Combat.unity";
    private const string ProbeGridName = "CombatLightProbeGrid";

    [MenuItem("FPSDemo/性能/优化Combat移动端渲染配置")]
    public static void OptimizeCombatSceneForMobileMenu()
    {
        OptimizeCombatSceneForMobile(saveScene: true);
    }

    [MenuItem("FPSDemo/性能/优化并烘焙Combat灯光")]
    public static void OptimizeAndBakeCombatLightingMenu()
    {
        OptimizeCombatSceneForMobile(saveScene: false);
        BakeCombatLighting(saveScene: true);
    }

    public static void OptimizeCombatSceneForMobileBatch()
    {
        OptimizeCombatSceneForMobile(saveScene: true);
    }

    public static void OptimizeAndBakeCombatLightingBatch()
    {
        OptimizeCombatSceneForMobile(saveScene: false);
        BakeCombatLighting(saveScene: true);
    }

    private static void OptimizeCombatSceneForMobile(bool saveScene)
    {
        Scene scene = OpenCombatScene();

        int lightCount = ConfigureBakedLights();
        int rendererCount = ConfigureStaticSceneRenderers();
        int probeCount = EnsureLightProbeGrid(scene);

        if (saveScene)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        Debug.Log($"[CombatRenderOptimizer] 移动端渲染配置完成 Lights={lightCount} Renderers={rendererCount} Probes={probeCount}");
    }

    private static Scene OpenCombatScene()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.path == CombatScenePath)
        {
            return activeScene;
        }

        return EditorSceneManager.OpenScene(CombatScenePath, OpenSceneMode.Single);
    }

    private static int ConfigureBakedLights()
    {
        Light[] lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Light light in lights)
        {
            // 场景灯光固定不动 全部改为烘焙灯 避免手机运行时计算大量点光和阴影
            light.lightmapBakeType = LightmapBakeType.Baked;

            // 烘焙灯保留阴影设置用于生成 Lightmap 运行时不会产生实时阴影开销
            if (light.shadows == LightShadows.None)
            {
                light.shadows = LightShadows.Soft;
            }

            light.renderMode = LightRenderMode.Auto;
            EditorUtility.SetDirty(light);
        }

        return lights.Length;
    }

    private static int ConfigureStaticSceneRenderers()
    {
        int changedCount = 0;
        Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Renderer renderer in renderers)
        {
            if (renderer is ParticleSystemRenderer)
            {
                continue;
            }

            if (!IsStaticSceneRenderer(renderer))
            {
                continue;
            }

            // 静态场景不需要运动向量和反射探针 移动端可直接省掉这部分 GPU 成本
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            EditorUtility.SetDirty(renderer);
            changedCount++;
        }

        return changedCount;
    }

    private static bool IsStaticSceneRenderer(Renderer renderer)
    {
        GameObject go = renderer.gameObject;
        if (!go.scene.IsValid() || go.scene.path != CombatScenePath)
        {
            return false;
        }

        if (go.GetComponentInParent<Canvas>() != null)
        {
            return false;
        }

        return GameObjectUtility.AreStaticEditorFlagsSet(go, StaticEditorFlags.ContributeGI)
               || GameObjectUtility.AreStaticEditorFlagsSet(go, StaticEditorFlags.BatchingStatic)
               || go.isStatic;
    }

    private static int EnsureLightProbeGrid(Scene scene)
    {
        Bounds bounds = CalculateStaticSceneBounds();
        if (bounds.size.sqrMagnitude <= 0.01f)
        {
            return 0;
        }

        GameObject probeObject = GameObject.Find(ProbeGridName);
        if (probeObject == null)
        {
            probeObject = new GameObject(ProbeGridName);
            EditorSceneManager.MoveGameObjectToScene(probeObject, scene);
        }

        probeObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        LightProbeGroup probeGroup = probeObject.GetComponent<LightProbeGroup>();
        if (probeGroup == null)
        {
            probeGroup = probeObject.AddComponent<LightProbeGroup>();
        }

        probeGroup.probePositions = CreateProbePositions(bounds);
        EditorUtility.SetDirty(probeObject);
        EditorUtility.SetDirty(probeGroup);
        return probeGroup.probePositions.Length;
    }

    private static Bounds CalculateStaticSceneBounds()
    {
        bool hasBounds = false;
        Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
        Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (Renderer renderer in renderers)
        {
            if (renderer is ParticleSystemRenderer || !IsStaticSceneRenderer(renderer))
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return bounds;
    }

    private static Vector3[] CreateProbePositions(Bounds bounds)
    {
        int xCount = Mathf.Clamp(Mathf.CeilToInt(bounds.size.x / 12f) + 1, 3, 9);
        int zCount = Mathf.Clamp(Mathf.CeilToInt(bounds.size.z / 12f) + 1, 3, 9);
        float[] yOffsets = { 1.2f, 2.6f, 4.2f };

        List<Vector3> positions = new List<Vector3>(xCount * zCount * yOffsets.Length);
        for (int x = 0; x < xCount; x++)
        {
            float tx = xCount == 1 ? 0.5f : x / (float)(xCount - 1);
            for (int z = 0; z < zCount; z++)
            {
                float tz = zCount == 1 ? 0.5f : z / (float)(zCount - 1);
                foreach (float yOffset in yOffsets)
                {
                    positions.Add(new Vector3(
                        Mathf.Lerp(bounds.min.x, bounds.max.x, tx),
                        bounds.min.y + yOffset,
                        Mathf.Lerp(bounds.min.z, bounds.max.z, tz)));
                }
            }
        }

        return positions.ToArray();
    }

    private static void BakeCombatLighting(bool saveScene)
    {
        Scene scene = OpenCombatScene();

        // 使用按需烘焙 避免编辑器自动反复烘焙
        Lightmapping.giWorkflowMode = Lightmapping.GIWorkflowMode.OnDemand;

        bool bakeStarted = Lightmapping.Bake();
        if (!bakeStarted)
        {
            Debug.LogWarning("[CombatRenderOptimizer] Lightmapping.Bake 未启动 请在 Unity Lighting 面板手动 Generate Lighting");
            return;
        }

        if (saveScene)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        Debug.Log("[CombatRenderOptimizer] Combat 灯光烘焙完成");
    }
}
