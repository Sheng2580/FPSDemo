using Unity.AI.Navigation;
using Combat;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

public static class CombatNavigationTools
{
    private const string ClimbableLayerName = "Climbable";
    private const string GroundLayerName = "Ground";
    private const string BarrierLayerName = "barrier";
    private const string ClimbableRootName = "Climbable";
    private const string GeneratedLinksRootName = "GeneratedEnemyJumpLinks";
    private const float JumpLinkMinHeight = 0.45f;
    private const float JumpLinkMaxHeight = 5.6f;
    private const float JumpSourceMaxHeight = 6.2f;
    private const float JumpLinkStartOutset = 0.85f;
    private const float JumpLinkEndInset = 0.35f;
    private const float JumpLinkVerticalPadding = 0.05f;
    private const float JumpLinkWidth = 1.1f;
    private const float JumpLinkSampleRadius = 2.2f;
    private const float JumpLinkMaxEndpointVerticalError = 0.8f;
    private const float JumpLinkMaxHorizontalDistance = 5.4f;
    private const float JumpLinkDuplicateDistance = 0.65f;
    private const int JumpLinkMaxEdgeSamples = 5;
    private const int JumpLinkMaxPreviewCount = 40;

    [MenuItem("FPSDemo/Navigation/配置Climbable并烘焙当前场景")]
    public static void ConfigureClimbableAndBakeCurrentScene()
    {
        int climbableLayer = EnsureLayer(ClimbableLayerName);
        ConfigureClimbableObjects(climbableLayer);
        ConfigureNavMeshSurfaces(climbableLayer);
        BakeCurrentSceneNavMesh();
        GenerateClimbableJumpLinks(rebuildNavMeshBeforeSampling: false);
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

    [MenuItem("FPSDemo/Navigation/生成Climbable敌人跳跃Link")]
    public static void GenerateClimbableJumpLinks()
    {
        GenerateClimbableJumpLinks(rebuildNavMeshBeforeSampling: true);
    }

    private static void GenerateClimbableJumpLinks(bool rebuildNavMeshBeforeSampling)
    {
        GameObject climbableRoot = GameObject.Find(ClimbableRootName);
        if (climbableRoot == null)
        {
            Debug.LogWarning("[CombatNav] 场景中没有找到 Climbable 根物体，无法生成跳跃 Link");
            return;
        }

        int climbableLayer = EnsureLayer(ClimbableLayerName);
        ConfigureClimbableObjects(climbableLayer);
        ConfigureNavMeshSurfaces(climbableLayer);

        if (rebuildNavMeshBeforeSampling)
        {
            BakeCurrentSceneNavMesh();
        }

        Transform oldLinksRoot = climbableRoot.transform.Find(GeneratedLinksRootName);
        if (oldLinksRoot != null)
        {
            Object.DestroyImmediate(oldLinksRoot.gameObject);
        }

        GameObject linksRoot = new GameObject(GeneratedLinksRootName);
        linksRoot.transform.SetParent(climbableRoot.transform, false);
        linksRoot.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        HashSet<Transform> handledTransforms = new HashSet<Transform>();
        JumpLinkBuildStats stats = new JumpLinkBuildStats();
        List<JumpLinkCandidate> previewCandidates = new List<JumpLinkCandidate>();
        List<Vector3> createdMidpoints = new List<Vector3>();
        int linkCount = 0;

        Collider[] colliders = climbableRoot.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider targetCollider = colliders[i];
            if (targetCollider == null || targetCollider.transform.IsChildOf(linksRoot.transform))
            {
                continue;
            }

            Bounds bounds = targetCollider.bounds;
            if (!ShouldCreateJumpLinks(bounds))
            {
                stats.skippedByBounds++;
                continue;
            }

            handledTransforms.Add(targetCollider.transform);
            linkCount += CreateLinksForBounds(
                linksRoot.transform,
                targetCollider.name,
                bounds,
                ref stats,
                previewCandidates,
                createdMidpoints);
        }

        Renderer[] renderers = climbableRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer targetRenderer = renderers[i];
            if (targetRenderer == null
                || targetRenderer.transform.IsChildOf(linksRoot.transform)
                || handledTransforms.Contains(targetRenderer.transform))
            {
                continue;
            }

            Bounds bounds = targetRenderer.bounds;
            if (!ShouldCreateJumpLinks(bounds))
            {
                stats.skippedByBounds++;
                continue;
            }

            linkCount += CreateLinksForBounds(
                linksRoot.transform,
                targetRenderer.name,
                bounds,
                ref stats,
                previewCandidates,
                createdMidpoints);
        }

        int previewCount = 0;
        if (linkCount == 0 && previewCandidates.Count > 0)
        {
            previewCount = CreatePreviewLinks(linksRoot.transform, previewCandidates);
        }

        EditorUtility.SetDirty(linksRoot);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log(
            $"[CombatNav] 已生成 Climbable 敌人跳跃 Link Count={linkCount} Preview={previewCount} Colliders={colliders.Length} Renderers={renderers.Length} BoundsSkip={stats.skippedByBounds} Attempts={stats.attempts} StartNoNav={stats.startNoNav} EndNoNav={stats.endNoNav} HeightFail={stats.heightFail} TooFar={stats.tooFar} Duplicate={stats.duplicate}",
            linksRoot);
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

            if (!IsSurfaceLayer(child.gameObject.layer))
            {
                child.gameObject.layer = climbableLayer;
            }

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
        AddLayerToMask(ref layerMask, CombatLayerNames.SurfaceStone);
        AddLayerToMask(ref layerMask, CombatLayerNames.SurfaceMetal);
        AddLayerToMask(ref layerMask, CombatLayerNames.SurfaceWood);
        AddLayerToMask(ref layerMask, CombatLayerNames.SurfaceGlass);
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

    private static bool ShouldCreateJumpLinks(Bounds bounds)
    {
        float height = bounds.size.y;
        if (height < JumpLinkMinHeight || height > JumpSourceMaxHeight)
        {
            return false;
        }

        return bounds.size.x >= 0.6f || bounds.size.z >= 0.6f;
    }

    private static bool IsSurfaceLayer(int layer)
    {
        return layer == LayerMask.NameToLayer(CombatLayerNames.SurfaceStone)
               || layer == LayerMask.NameToLayer(CombatLayerNames.SurfaceMetal)
               || layer == LayerMask.NameToLayer(CombatLayerNames.SurfaceWood)
               || layer == LayerMask.NameToLayer(CombatLayerNames.SurfaceGlass);
    }

    private static int CreateLinksForBounds(
        Transform linksRoot,
        string sourceName,
        Bounds bounds,
        ref JumpLinkBuildStats stats,
        List<JumpLinkCandidate> previewCandidates,
        List<Vector3> createdMidpoints)
    {
        int createdCount = 0;
        createdCount += CreateJumpLinksForEdge(linksRoot, sourceName, bounds, Vector3.forward, bounds.extents.z, bounds.extents.x, ref stats, previewCandidates, createdMidpoints);
        createdCount += CreateJumpLinksForEdge(linksRoot, sourceName, bounds, Vector3.back, bounds.extents.z, bounds.extents.x, ref stats, previewCandidates, createdMidpoints);
        createdCount += CreateJumpLinksForEdge(linksRoot, sourceName, bounds, Vector3.right, bounds.extents.x, bounds.extents.z, ref stats, previewCandidates, createdMidpoints);
        createdCount += CreateJumpLinksForEdge(linksRoot, sourceName, bounds, Vector3.left, bounds.extents.x, bounds.extents.z, ref stats, previewCandidates, createdMidpoints);
        return createdCount;
    }

    private static int CreateJumpLinksForEdge(
        Transform linksRoot,
        string sourceName,
        Bounds bounds,
        Vector3 direction,
        float edgeExtent,
        float tangentExtent,
        ref JumpLinkBuildStats stats,
        List<JumpLinkCandidate> previewCandidates,
        List<Vector3> createdMidpoints)
    {
        if (edgeExtent <= 0.3f)
        {
            return 0;
        }

        int createdCount = 0;
        Vector3 tangent = Mathf.Abs(direction.x) > 0.01f ? Vector3.forward : Vector3.right;
        int sampleCount = Mathf.Clamp(Mathf.CeilToInt(tangentExtent * 2f), 1, JumpLinkMaxEdgeSamples);

        for (int i = 0; i < sampleCount; i++)
        {
            float lerp = sampleCount <= 1 ? 0.5f : i / (sampleCount - 1f);
            float tangentOffset = Mathf.Lerp(-tangentExtent * 0.75f, tangentExtent * 0.75f, lerp);
            Vector3 edgeCenter = bounds.center + direction * edgeExtent + tangent * tangentOffset;
            Vector3 start = edgeCenter + direction * JumpLinkStartOutset;
            Vector3 end = edgeCenter - direction * JumpLinkEndInset;
            start.y = bounds.min.y + JumpLinkVerticalPadding;
            end.y = bounds.max.y + JumpLinkVerticalPadding;

            JumpLinkCandidate candidate = new JumpLinkCandidate
            {
                sourceName = sourceName,
                directionName = DirectionToName(direction),
                start = start,
                end = end
            };
            previewCandidates.Add(candidate);

            createdCount += CreateSampledJumpLink(
                linksRoot,
                candidate,
                ref stats,
                createdMidpoints);
        }

        return createdCount;
    }

    private static int CreateSampledJumpLink(
        Transform linksRoot,
        JumpLinkCandidate candidate,
        ref JumpLinkBuildStats stats,
        List<Vector3> createdMidpoints)
    {
        stats.attempts++;

        if (!TrySampleNavMeshPoint(candidate.start, candidate.start.y, out Vector3 sampledStart))
        {
            stats.startNoNav++;
            return 0;
        }

        if (!TrySampleNavMeshPoint(candidate.end, candidate.end.y, out Vector3 sampledEnd))
        {
            stats.endNoNav++;
            return 0;
        }

        float heightDelta = sampledEnd.y - sampledStart.y;
        if (heightDelta < JumpLinkMinHeight || heightDelta > JumpLinkMaxHeight)
        {
            stats.heightFail++;
            return 0;
        }

        Vector3 horizontalDelta = sampledEnd - sampledStart;
        horizontalDelta.y = 0f;
        if (horizontalDelta.magnitude > JumpLinkMaxHorizontalDistance)
        {
            stats.tooFar++;
            return 0;
        }

        Vector3 midpoint = (sampledStart + sampledEnd) * 0.5f;
        if (IsDuplicateLink(midpoint, createdMidpoints))
        {
            stats.duplicate++;
            return 0;
        }

        CreateLinkObject(
            linksRoot,
            $"JumpLink_{SanitizeName(candidate.sourceName)}_{candidate.directionName}_{createdMidpoints.Count + 1:00}",
            sampledStart,
            sampledEnd);
        createdMidpoints.Add(midpoint);
        return 1;
    }

    private static int CreatePreviewLinks(Transform linksRoot, List<JumpLinkCandidate> candidates)
    {
        int createdCount = 0;
        int stride = Mathf.Max(1, Mathf.CeilToInt(candidates.Count / (float)JumpLinkMaxPreviewCount));
        for (int i = 0; i < candidates.Count && createdCount < JumpLinkMaxPreviewCount; i += stride)
        {
            JumpLinkCandidate candidate = candidates[i];
            float heightDelta = candidate.end.y - candidate.start.y;
            if (heightDelta < JumpLinkMinHeight || heightDelta > JumpLinkMaxHeight)
            {
                continue;
            }

            CreateLinkObject(
                linksRoot,
                $"JumpLink_Preview_{SanitizeName(candidate.sourceName)}_{candidate.directionName}_{createdCount + 1:00}",
                candidate.start,
                candidate.end);
            createdCount++;
        }

        if (createdCount > 0)
        {
            Debug.LogWarning("[CombatNav] 没有找到能同时贴住上下 NavMesh 的有效 Link，已生成 Preview Link 方便手动检查位置，请优先确认平台顶部也有蓝色 NavMesh");
        }

        return createdCount;
    }

    private static void CreateLinkObject(Transform linksRoot, string linkName, Vector3 start, Vector3 end)
    {
        GameObject linkObject = new GameObject(linkName);
        linkObject.transform.SetPositionAndRotation(start, Quaternion.identity);
        linkObject.transform.SetParent(linksRoot, true);

        NavMeshLink link = linkObject.AddComponent<NavMeshLink>();
        link.startPoint = Vector3.zero;
        link.endPoint = end - start;
        link.width = JumpLinkWidth;
        link.bidirectional = true;
        link.area = GetJumpArea();
        link.costModifier = -1;

        EditorUtility.SetDirty(linkObject);
        EditorUtility.SetDirty(link);
    }

    private static bool TrySampleNavMeshPoint(Vector3 point, float expectedY, out Vector3 sampledPoint)
    {
        sampledPoint = Vector3.zero;
        if (!NavMesh.SamplePosition(point, out NavMeshHit hit, JumpLinkSampleRadius, NavMesh.AllAreas))
        {
            return false;
        }

        if (Mathf.Abs(hit.position.y - expectedY) > JumpLinkMaxEndpointVerticalError)
        {
            return false;
        }

        sampledPoint = hit.position;
        return true;
    }

    private static bool IsDuplicateLink(Vector3 midpoint, List<Vector3> createdMidpoints)
    {
        float minDistanceSqr = JumpLinkDuplicateDistance * JumpLinkDuplicateDistance;
        for (int i = 0; i < createdMidpoints.Count; i++)
        {
            if ((createdMidpoints[i] - midpoint).sqrMagnitude <= minDistanceSqr)
            {
                return true;
            }
        }

        return false;
    }

    private struct JumpLinkBuildStats
    {
        public int skippedByBounds;
        public int attempts;
        public int startNoNav;
        public int endNoNav;
        public int heightFail;
        public int tooFar;
        public int duplicate;
    }

    private struct JumpLinkCandidate
    {
        public string sourceName;
        public string directionName;
        public Vector3 start;
        public Vector3 end;
    }

    private static string DirectionToName(Vector3 direction)
    {
        if (direction == Vector3.forward)
        {
            return "Forward";
        }

        if (direction == Vector3.back)
        {
            return "Back";
        }

        if (direction == Vector3.right)
        {
            return "Right";
        }

        return "Left";
    }

    private static int GetJumpArea()
    {
        int jumpArea = NavMesh.GetAreaFromName("Jump");
        return jumpArea >= 0 ? jumpArea : 0;
    }

    private static string SanitizeName(string sourceName)
    {
        if (string.IsNullOrEmpty(sourceName))
        {
            return "Source";
        }

        char[] chars = sourceName.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (char.IsLetterOrDigit(chars[i]) || chars[i] == '_' || chars[i] == '-')
            {
                continue;
            }

            chars[i] = '_';
        }

        return new string(chars);
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
