using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Weapon;

public static class ShotgunWeaponBuildTools
{
    private const string CombatScenePath = "Assets/Scenes/Combat.unity";
    private const string SourceShotgunPrefabPath = "Assets/Art/Animation/Akila/FPS Framework/Prefabs/Weapons/Shotgun_1.prefab";
    private const string OutputShotgunPrefabPath = "Assets/Art/ABRes/Player/PlayerWeapon/ShotgunView.prefab";
    private const string PlayerRuntimeBundleName = "player_runtime";
    private const string PlayerName = "Player";
    private const string WeaponRootName = "WeaponViewRoot";
    private const string ShotgunViewName = "ShotgunView";
    private const string ShotgunModelName = "ShotgunModel";
    private static readonly Vector3 ShotgunModelLocalPosition = new Vector3(0.12f, -0.08f, 0.38f);
    private static readonly Vector3 ShotgunModelLocalScale = new Vector3(0.055f, 0.055f, 0.055f);

    [MenuItem("FPSDemo/Weapon/生成散弹枪测试资源并放入Combat")]
    public static void GenerateShotgunViewAndPlaceInCombat()
    {
        GameObject shotgunPrefab = GenerateShotgunViewPrefab();
        if (shotgunPrefab == null)
        {
            return;
        }

        PlaceShotgunViewInCombatScene(shotgunPrefab);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    [MenuItem("FPSDemo/Weapon/清理散弹枪Akila脚本")]
    public static void CleanupExistingShotgunView()
    {
        int prefabRemovedCount = CleanupShotgunViewPrefab();
        int sceneRemovedCount = CleanupShotgunViewInCurrentScene();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[ShotgunBuild] 散弹枪 Akila 脚本清理完成 PrefabRemoved={prefabRemovedCount} SceneRemoved={sceneRemovedCount}");
    }

    [MenuItem("FPSDemo/Weapon/只生成散弹枪WeaponView")]
    public static void GenerateShotgunViewPrefabMenu()
    {
        GenerateShotgunViewPrefab();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    public static GameObject GenerateShotgunViewPrefab()
    {
        GameObject sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SourceShotgunPrefabPath);
        if (sourcePrefab == null)
        {
            Debug.LogError($"[ShotgunBuild] 找不到散弹枪源预制体 {SourceShotgunPrefabPath}");
            return null;
        }

        EnsureOutputFolder();

        GameObject root = new GameObject(ShotgunViewName);
        GameObject model = null;
        try
        {
            model = PrefabUtility.InstantiatePrefab(sourcePrefab) as GameObject;
            if (model == null)
            {
                Debug.LogError("[ShotgunBuild] 散弹枪源预制体实例化失败");
                return null;
            }

            model.name = ShotgunModelName;
            model.transform.SetParent(root.transform, false);
            model.transform.localPosition = ShotgunModelLocalPosition;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = ShotgunModelLocalScale;

            Animator animator = model.GetComponentInChildren<Animator>(true);
            Transform muzzlePoint = EnsureMuzzlePoint(model.transform);
            WeaponView weaponView = root.AddComponent<WeaponView>();
            ConfigureWeaponView(weaponView, animator, model.transform, muzzlePoint);
            RemoveAkilaGameplayScripts(model);

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, OutputShotgunPrefabPath);
            SetAssetBundleName(OutputShotgunPrefabPath, PlayerRuntimeBundleName);
            Debug.Log($"[ShotgunBuild] 已生成散弹枪 WeaponView {OutputShotgunPrefabPath}", savedPrefab);
            return savedPrefab;
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void PlaceShotgunViewInCombatScene(GameObject shotgunPrefab)
    {
        if (shotgunPrefab == null)
        {
            return;
        }

        Scene activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.path != CombatScenePath)
        {
            EditorSceneManager.OpenScene(CombatScenePath);
        }

        GameObject player = FindScenePlayer();
        if (player == null)
        {
            Debug.LogWarning("[ShotgunBuild] Combat 场景里没有找到 Player");
            return;
        }

        Transform weaponRoot = FindChildRecursive(player.transform, WeaponRootName);
        if (weaponRoot == null)
        {
            Debug.LogWarning($"[ShotgunBuild] Player 下没有找到 {WeaponRootName}", player);
            return;
        }

        RemoveExistingShotgunView(weaponRoot);

        GameObject shotgunView = PrefabUtility.InstantiatePrefab(shotgunPrefab, weaponRoot) as GameObject;
        if (shotgunView == null)
        {
            Debug.LogError("[ShotgunBuild] 散弹枪 WeaponView 放入场景失败");
            return;
        }

        shotgunView.name = ShotgunViewName;
        ResetLocalTransform(shotgunView.transform);
        shotgunView.SetActive(true);
        Selection.activeGameObject = shotgunView;
        EditorGUIUtility.PingObject(shotgunView);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[ShotgunBuild] 已把散弹枪放到 Combat 场景 Player 下", shotgunView);
    }

    private static void ConfigureWeaponView(WeaponView weaponView, Animator animator, Transform viewRoot, Transform muzzlePoint)
    {
        SerializedObject serializedObject = new SerializedObject(weaponView);
        serializedObject.FindProperty("animator").objectReferenceValue = animator;
        serializedObject.FindProperty("viewRoot").objectReferenceValue = viewRoot;
        serializedObject.FindProperty("muzzlePoint").objectReferenceValue = muzzlePoint;
        serializedObject.FindProperty("shellPoint").objectReferenceValue = null;
        serializedObject.FindProperty("disableViewModelShadows").boolValue = true;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Transform EnsureMuzzlePoint(Transform modelRoot)
    {
        Transform muzzle = FindBestMuzzle(modelRoot);
        if (muzzle == null)
        {
            GameObject fallbackMuzzle = new GameObject("MuzzlePoint");
            fallbackMuzzle.transform.SetParent(modelRoot, false);
            fallbackMuzzle.transform.localPosition = new Vector3(0.07f, 0.08f, 0.55f);
            fallbackMuzzle.transform.localRotation = Quaternion.identity;
            fallbackMuzzle.transform.localScale = Vector3.one;
            return fallbackMuzzle.transform;
        }

        Transform existingPoint = FindChildRecursive(muzzle, "MuzzlePoint");
        if (existingPoint != null)
        {
            return existingPoint;
        }

        GameObject point = new GameObject("MuzzlePoint");
        point.transform.SetParent(muzzle, false);
        point.transform.localPosition = Vector3.zero;
        point.transform.localRotation = Quaternion.identity;
        point.transform.localScale = Vector3.one;
        return point.transform;
    }

    private static Transform FindBestMuzzle(Transform root)
    {
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        Transform fallback = null;
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform child = transforms[i];
            if (child == null || child.name != "Muzzle")
            {
                continue;
            }

            fallback = child;
            if (child.childCount == 0)
            {
                return child;
            }
        }

        return fallback;
    }

    private static GameObject FindScenePlayer()
    {
        GameObject namedPlayer = GameObject.Find(PlayerName);
        if (namedPlayer != null)
        {
            return namedPlayer;
        }

        PlayerController playerController = Object.FindObjectOfType<PlayerController>();
        return playerController != null ? playerController.gameObject : null;
    }

    private static void RemoveExistingShotgunView(Transform weaponRoot)
    {
        Transform existing = weaponRoot.Find(ShotgunViewName);
        if (existing != null)
        {
            Object.DestroyImmediate(existing.gameObject);
        }
    }

    private static void ResetLocalTransform(Transform target)
    {
        target.localPosition = Vector3.zero;
        target.localRotation = Quaternion.identity;
        target.localScale = Vector3.one;
    }

    private static Transform FindChildRecursive(Transform root, string childName)
    {
        if (root == null || string.IsNullOrEmpty(childName))
        {
            return null;
        }

        if (root.name == childName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildRecursive(root.GetChild(i), childName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static void EnsureOutputFolder()
    {
        string folderPath = "Assets/Art/ABRes/Player/PlayerWeapon";
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            AssetDatabase.CreateFolder("Assets/Art/ABRes/Player", "PlayerWeapon");
        }
    }

    private static void SetAssetBundleName(string assetPath, string assetBundleName)
    {
        AssetImporter importer = AssetImporter.GetAtPath(assetPath);
        if (importer == null)
        {
            return;
        }

        importer.assetBundleName = assetBundleName;
        importer.SaveAndReimport();
    }

    private static int CleanupShotgunViewPrefab()
    {
        GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(OutputShotgunPrefabPath);
        if (prefabRoot == null)
        {
            Debug.LogWarning($"[ShotgunBuild] 找不到散弹枪 WeaponView {OutputShotgunPrefabPath}");
            return 0;
        }

        GameObject contentsRoot = PrefabUtility.LoadPrefabContents(OutputShotgunPrefabPath);
        try
        {
            int removedCount = RemoveAkilaGameplayScripts(contentsRoot);
            PrefabUtility.SaveAsPrefabAsset(contentsRoot, OutputShotgunPrefabPath);
            SetAssetBundleName(OutputShotgunPrefabPath, PlayerRuntimeBundleName);
            return removedCount;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contentsRoot);
        }
    }

    private static int CleanupShotgunViewInCurrentScene()
    {
        GameObject shotgunView = GameObject.Find(ShotgunViewName);
        if (shotgunView == null)
        {
            return 0;
        }

        int removedCount = RemoveAkilaGameplayScripts(shotgunView);
        if (removedCount > 0)
        {
            EditorSceneManager.MarkSceneDirty(shotgunView.scene);
            EditorSceneManager.SaveOpenScenes();
        }

        return removedCount;
    }

    private static int RemoveAkilaGameplayScripts(GameObject root)
    {
        if (root == null)
        {
            return 0;
        }

        int removedCount = 0;
        bool removedInPass;
        do
        {
            removedInPass = false;
            MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = behaviours.Length - 1; i >= 0; i--)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (!IsAkilaGameplayBehaviour(behaviour))
                {
                    continue;
                }

                Object.DestroyImmediate(behaviour);
                if (behaviour == null)
                {
                    removedCount++;
                    removedInPass = true;
                }
            }
        }
        while (removedInPass);

        MonoBehaviour[] remainingBehaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < remainingBehaviours.Length; i++)
        {
            MonoBehaviour behaviour = remainingBehaviours[i];
            if (!IsAkilaGameplayBehaviour(behaviour))
            {
                continue;
            }

            Debug.LogWarning($"[ShotgunBuild] 散弹枪仍残留 Akila 脚本 {behaviour.GetType().Name}", behaviour);
        }

        return removedCount;
    }

    private static bool IsAkilaGameplayBehaviour(MonoBehaviour behaviour)
    {
        if (behaviour == null || behaviour is WeaponView)
        {
            return false;
        }

        string namespaceName = behaviour.GetType().Namespace;
        return !string.IsNullOrEmpty(namespaceName) && namespaceName.StartsWith("Akila.FPSFramework");
    }
}
