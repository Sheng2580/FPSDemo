using Enemy;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

public static class EnemyPrefabBuildTools
{
    private const string OutputFolderPath = "Assets/Art/ABRes/Enemies/Prefabs";
    private const string EnemyPrefabBundleName = "enemy_prefabs";
    private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";

    private static readonly EnemyPrefabDefinition SkeletonDefinition = new EnemyPrefabDefinition(
        "Assets/Knife/Zombie Collection/Zombie Skeleton/Prefabs/Zombie Skeleton OneHanded.prefab",
        "Enemy_ZombieSkeleton_LOD2",
        1001,
        "Zombie Skeleton OneHanded",
        "ZombieSkeletonOneHanded",
        "ZombieSkeleton_OneHanded",
        100f,
        2.2f,
        10f,
        1.2f,
        1);

    private static readonly EnemyPrefabDefinition[] CommonEnemyDefinitions =
    {
        SkeletonDefinition,
        new EnemyPrefabDefinition(
            "Assets/Knife/Zombie Collection/Zombie Nerd/Prefabs/Zombie Nerd OneHanded.prefab",
            "Enemy_ZombieNerd_LOD2",
            1002,
            "Zombie Nerd OneHanded",
            "ZombieNerdOneHanded",
            "ZombieNerd_OneHanded",
            80f,
            2.5f,
            8f,
            1.05f,
            1),
        new EnemyPrefabDefinition(
            "Assets/Knife/Zombie Collection/Zombie Old Crone/Prefabs/ZombieOldCrone OneHanded.prefab",
            "Enemy_ZombieOldCrone_LOD2",
            1003,
            "Zombie Old Crone OneHanded",
            "ZombieOldCroneOneHanded",
            "ZombieOldCrone_OneHanded",
            140f,
            1.8f,
            14f,
            1.35f,
            2)
    };

    [MenuItem("FPSDemo/Enemy/生成低模骷髅僵尸 Prefab")]
    public static void GenerateLowLodZombieSkeleton()
    {
        EnsureOutputFolder();
        GenerateLowLodEnemyPrefab(SkeletonDefinition);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    [MenuItem("FPSDemo/Enemy/生成普通敌人低模 Prefabs")]
    public static void GenerateCommonLowLodEnemyPrefabs()
    {
        EnsureOutputFolder();

        for (int i = 0; i < CommonEnemyDefinitions.Length; i++)
        {
            GenerateLowLodEnemyPrefab(CommonEnemyDefinitions[i]);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    [MenuItem("FPSDemo/Enemy/生成敌人测试场景配置")]
    public static void SetupEnemyPrototypeInSampleScene()
    {
        GenerateCommonLowLodEnemyPrefabs();

        GameObject[] enemyPrefabs = LoadGeneratedEnemyPrefabs();
        if (enemyPrefabs.Length == 0)
        {
            Debug.LogError("低模敌人 Prefab 生成失败");
            return;
        }

        EditorSceneManager.OpenScene(SampleScenePath);

        GameObject systemsRoot = GameObject.Find("EnemySystems");
        if (systemsRoot == null)
        {
            systemsRoot = new GameObject("EnemySystems");
        }

        EnemyPool pool = Object.FindObjectOfType<EnemyPool>();
        if (pool == null)
        {
            GameObject poolObject = new GameObject("EnemyPool");
            poolObject.transform.SetParent(systemsRoot.transform);
            pool = poolObject.AddComponent<EnemyPool>();
        }

        EnemySpawnManager spawnManager = Object.FindObjectOfType<EnemySpawnManager>();
        if (spawnManager == null)
        {
            GameObject spawnObject = new GameObject("EnemySpawnManager");
            spawnObject.transform.SetParent(systemsRoot.transform);
            spawnManager = spawnObject.AddComponent<EnemySpawnManager>();
        }

        if (Object.FindObjectOfType<EnemyAIScheduler>() == null)
        {
            GameObject schedulerObject = new GameObject("EnemyAIScheduler");
            schedulerObject.transform.SetParent(systemsRoot.transform);
            schedulerObject.AddComponent<EnemyAIScheduler>();
        }

        Transform player = GameObject.Find("Player")?.transform;
        ConfigureSpawnManager(spawnManager, pool, player, enemyPrefabs);

        EditorSceneManager.MarkSceneDirty(spawnManager.gameObject.scene);
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
    }

    private static void GenerateLowLodEnemyPrefab(EnemyPrefabDefinition definition)
    {
        if (definition == null)
        {
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(definition.SourcePrefabPath);
        try
        {
            root.name = definition.OutputPrefabName;
            RemoveHighLodRenderers(root);
            ConfigureRootComponents(root, definition);
            EnsureHitBoxes(root);
            PrefabUtility.SaveAsPrefabAsset(root, definition.OutputPrefabPath);
            SetAssetBundleName(definition.OutputPrefabPath, EnemyPrefabBundleName);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static GameObject[] LoadGeneratedEnemyPrefabs()
    {
        GameObject[] prefabs = new GameObject[CommonEnemyDefinitions.Length];
        int count = 0;
        for (int i = 0; i < CommonEnemyDefinitions.Length; i++)
        {
            EnemyPrefabDefinition definition = CommonEnemyDefinitions[i];
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(definition.OutputPrefabPath);
            if (prefab == null)
            {
                continue;
            }

            prefabs[count] = prefab;
            count++;
        }

        if (count == prefabs.Length)
        {
            return prefabs;
        }

        GameObject[] compactPrefabs = new GameObject[count];
        for (int i = 0; i < count; i++)
        {
            compactPrefabs[i] = prefabs[i];
        }

        return compactPrefabs;
    }

    private static void RemoveHighLodRenderers(GameObject root)
    {
        LODGroup lodGroup = root.GetComponent<LODGroup>();
        if (lodGroup != null)
        {
            Object.DestroyImmediate(lodGroup);
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = renderers.Length - 1; i >= 0; i--)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            string objectName = renderer.gameObject.name;
            if (objectName.Contains("LOD0") || objectName.Contains("LOD1"))
            {
                Object.DestroyImmediate(renderer.gameObject);
            }
        }
    }

    private static void ConfigureRootComponents(GameObject root, EnemyPrefabDefinition definition)
    {
        Animator animator = root.GetComponent<Animator>();
        if (animator != null)
        {
            animator.applyRootMotion = true;
        }

        CharacterController characterController = GetOrAdd<CharacterController>(root);
        characterController.radius = 0.35f;
        characterController.height = 1.8f;
        characterController.center = new Vector3(0f, 0.9f, 0f);
        characterController.stepOffset = 0.3f;
        characterController.slopeLimit = 45f;

        NavMeshAgent agent = GetOrAdd<NavMeshAgent>(root);
        agent.enabled = false;
        agent.radius = 0.35f;
        agent.height = 1.8f;
        agent.speed = 2.2f;
        agent.angularSpeed = 360f;
        agent.acceleration = 12f;
        agent.stoppingDistance = 1.4f;
        agent.autoTraverseOffMeshLink = false;

        EnemyController controller = GetOrAdd<EnemyController>(root);
        EnemyHealth health = GetOrAdd<EnemyHealth>(root);
        EnemyView view = GetOrAdd<EnemyView>(root);
        EnemyMotor motor = GetOrAdd<EnemyMotor>(root);
        EnemyAttack attack = GetOrAdd<EnemyAttack>(root);
        EnemyBrain brain = GetOrAdd<EnemyBrain>(root);
        EnemyStateMachine stateMachine = GetOrAdd<EnemyStateMachine>(root);
        GameObject animationEventObject = animator != null ? animator.gameObject : root;
        EnemyAnimationEventReceiver animationEventReceiver = GetOrAdd<EnemyAnimationEventReceiver>(animationEventObject);

        SetInt(controller, "enemyId", definition.EnemyId);
        SetString(controller, "enemyName", definition.EnemyName);
        SetInt(controller, "goldReward", definition.GoldReward);
        SetObject(controller, "health", health);
        SetObject(controller, "motor", motor);
        SetObject(controller, "attack", attack);
        SetObject(controller, "view", view);
        SetObject(controller, "brain", brain);
        SetObject(controller, "stateMachine", stateMachine);

        SetFloat(health, "maxHealth", definition.MaxHealth);
        SetObject(health, "controller", controller);
        SetObject(health, "view", view);

        SetObject(view, "animator", animator);
        SetBool(view, "useRootMotion", true);
        SetString(view, "idleStateName", definition.AnimationPrefix + "_Idle");
        SetString(view, "walkStateName", definition.AnimationPrefix + "_Walk");
        SetString(view, "runStateName", definition.AnimationPrefix + "_Run");
        SetString(view, "linkTraverseStateName", definition.AnimationPrefix + "_Dodge");
        SetString(view, "attackStateName", definition.AnimationPrefix + "_Attack_1");
        SetString(view, "damageStateName", definition.AnimationPrefix + "_Damage");
        SetString(view, "deathStateName", definition.AnimationPrefix + "_Death");
        SetFloat(view, "locomotionTransition", 0.18f);
        SetFloat(view, "attackTransition", 0.1f);
        SetFloat(view, "hitTransition", 0.14f);
        SetFloat(view, "deathTransition", 0.18f);
        SetFloat(view, "recoverTransition", 0.18f);
        SetObject(motor, "agent", agent);
        SetObject(motor, "characterController", characterController);
        SetBool(motor, "useRootMotion", true);
        SetObject(motor, "view", view);
        SetObject(attack, "view", view);
        SetObject(animationEventReceiver, "attack", attack);
        SetObject(brain, "controller", controller);
        SetObject(brain, "health", health);
        SetObject(brain, "motor", motor);
        SetObject(brain, "attack", attack);
        SetObject(brain, "view", view);
        SetObject(brain, "stateMachine", stateMachine);
        SetFloat(brain, "hitStunDuration", 0.09f);
        SetFloat(brain, "defaultHitKnockbackDistance", 0.08f);
        SetFloat(brain, "hitKnockbackDuration", 0.06f);
        SetFloat(brain, "fullHitReactionCooldown", 0.2f);
        SetObject(stateMachine, "controller", controller);
        SetObject(stateMachine, "motor", motor);
        SetObject(stateMachine, "attack", attack);
        SetObject(stateMachine, "view", view);
    }

    private static void EnsureHitBoxes(GameObject root)
    {
        Transform hitBoxRoot = root.transform.Find("HitBoxes");
        if (hitBoxRoot != null)
        {
            Object.DestroyImmediate(hitBoxRoot.gameObject);
        }

        GameObject hitBoxRootObject = new GameObject("HitBoxes");
        hitBoxRootObject.transform.SetParent(root.transform);
        hitBoxRootObject.transform.localPosition = Vector3.zero;
        hitBoxRootObject.transform.localRotation = Quaternion.identity;
        hitBoxRootObject.transform.localScale = Vector3.one;

        EnemyHealth health = root.GetComponent<EnemyHealth>();

        CreateHitBox(hitBoxRootObject.transform, "HeadHitBox", EnemyHitBodyPart.Head, 2f, true, health, new Vector3(0f, 1.65f, 0f), new Vector3(0.34f, 0.28f, 0.32f));
        CreateHitBox(hitBoxRootObject.transform, "BodyHitBox", EnemyHitBodyPart.Body, 1f, false, health, new Vector3(0f, 1.05f, 0f), new Vector3(0.58f, 0.86f, 0.36f));
        CreateHitBox(hitBoxRootObject.transform, "LeftArmHitBox", EnemyHitBodyPart.Arm, 0.75f, false, health, new Vector3(-0.44f, 1.08f, 0f), new Vector3(0.22f, 0.72f, 0.24f));
        CreateHitBox(hitBoxRootObject.transform, "RightArmHitBox", EnemyHitBodyPart.Arm, 0.75f, false, health, new Vector3(0.44f, 1.08f, 0f), new Vector3(0.22f, 0.72f, 0.24f));
        CreateHitBox(hitBoxRootObject.transform, "LeftLegHitBox", EnemyHitBodyPart.Leg, 0.6f, false, health, new Vector3(-0.16f, 0.45f, 0f), new Vector3(0.22f, 0.76f, 0.24f));
        CreateHitBox(hitBoxRootObject.transform, "RightLegHitBox", EnemyHitBodyPart.Leg, 0.6f, false, health, new Vector3(0.16f, 0.45f, 0f), new Vector3(0.22f, 0.76f, 0.24f));
    }

    private static void CreateHitBox(
        Transform parent,
        string name,
        EnemyHitBodyPart bodyPart,
        float multiplier,
        bool critical,
        EnemyHealth health,
        Vector3 localPosition,
        Vector3 size)
    {
        GameObject hitBoxObject = new GameObject(name);
        hitBoxObject.transform.SetParent(parent);
        hitBoxObject.transform.localPosition = localPosition;
        hitBoxObject.transform.localRotation = Quaternion.identity;
        hitBoxObject.transform.localScale = Vector3.one;

        BoxCollider boxCollider = hitBoxObject.AddComponent<BoxCollider>();
        boxCollider.isTrigger = true;
        boxCollider.size = size;

        EnemyHitBox hitBox = hitBoxObject.AddComponent<EnemyHitBox>();
        SetEnum(hitBox, "bodyPart", (int)bodyPart);
        SetFloat(hitBox, "damageMultiplier", multiplier);
        SetBool(hitBox, "criticalPart", critical);
        SetObject(hitBox, "health", health);
    }

    private static void ConfigureSpawnManager(
        EnemySpawnManager spawnManager,
        EnemyPool pool,
        Transform player,
        GameObject[] enemyPrefabs)
    {
        SerializedObject serializedObject = new SerializedObject(spawnManager);
        serializedObject.FindProperty("autoSpawn").boolValue = true;
        serializedObject.FindProperty("useWaveConfigs").boolValue = true;
        serializedObject.FindProperty("sceneMaxEnemyCount").intValue = 8;
        serializedObject.FindProperty("spawnCountPerBatch").intValue = 1;
        serializedObject.FindProperty("spawnInterval").floatValue = 3f;
        serializedObject.FindProperty("fallbackSpawnDistance").floatValue = 12f;
        serializedObject.FindProperty("returnToPoolDelay").floatValue = 2.5f;
        serializedObject.FindProperty("playerTarget").objectReferenceValue = player;
        serializedObject.FindProperty("enemyPool").objectReferenceValue = pool;
        serializedObject.FindProperty("loadPrefabsFromAssetBundle").boolValue = true;
        serializedObject.FindProperty("enemyPrefabAssetBundleName").stringValue = EnemyPrefabBundleName;

        SerializedProperty bindings = serializedObject.FindProperty("prefabBindings");
        bindings.arraySize = enemyPrefabs.Length;
        for (int i = 0; i < enemyPrefabs.Length; i++)
        {
            EnemyPrefabDefinition definition = CommonEnemyDefinitions[i];
            SerializedProperty binding = bindings.GetArrayElementAtIndex(i);
            binding.FindPropertyRelative("prefabKey").stringValue = definition.PrefabKey;
            binding.FindPropertyRelative("prefabResourceKey").stringValue = definition.OutputPrefabName;
            binding.FindPropertyRelative("prefab").objectReferenceValue = enemyPrefabs[i];
        }

        SerializedProperty definitions = serializedObject.FindProperty("spawnDefinitions");
        definitions.arraySize = enemyPrefabs.Length;
        for (int i = 0; i < enemyPrefabs.Length; i++)
        {
            EnemyPrefabDefinition enemyDefinition = CommonEnemyDefinitions[i];
            SerializedProperty definition = definitions.GetArrayElementAtIndex(i);
            definition.FindPropertyRelative("enemyId").intValue = enemyDefinition.EnemyId;
            definition.FindPropertyRelative("enemyName").stringValue = enemyDefinition.EnemyName;
            definition.FindPropertyRelative("prefabKey").stringValue = enemyDefinition.PrefabKey;
            definition.FindPropertyRelative("prefabResourceKey").stringValue = enemyDefinition.OutputPrefabName;
            definition.FindPropertyRelative("prefab").objectReferenceValue = enemyPrefabs[i];
            definition.FindPropertyRelative("weight").floatValue = 100f;
            definition.FindPropertyRelative("goldReward").intValue = enemyDefinition.GoldReward;
            definition.FindPropertyRelative("maxHealth").floatValue = enemyDefinition.MaxHealth;
            definition.FindPropertyRelative("moveSpeed").floatValue = enemyDefinition.MoveSpeed;
            definition.FindPropertyRelative("angularSpeed").floatValue = 360f;
            definition.FindPropertyRelative("acceleration").floatValue = 12f;
            definition.FindPropertyRelative("attackDamage").floatValue = enemyDefinition.AttackDamage;
            definition.FindPropertyRelative("attackDistance").floatValue = 1.4f;
            definition.FindPropertyRelative("attackInterval").floatValue = enemyDefinition.AttackInterval;
            definition.FindPropertyRelative("attackHitDelay").floatValue = 0.35f;
        }

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureOutputFolder()
    {
        EnsureFolder("Assets/Art", "ABRes");
        EnsureFolder("Assets/Art/ABRes", "Enemies");
        EnsureFolder("Assets/Art/ABRes/Enemies", "Prefabs");
    }

    private static T GetOrAdd<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
    }

    private static void EnsureFolder(string parentFolder, string newFolder)
    {
        string fullPath = parentFolder + "/" + newFolder;
        if (!AssetDatabase.IsValidFolder(fullPath))
        {
            AssetDatabase.CreateFolder(parentFolder, newFolder);
        }
    }

    private static void SetAssetBundleName(string assetPath, string bundleName)
    {
        AssetImporter importer = AssetImporter.GetAtPath(assetPath);
        if (importer != null)
        {
            importer.assetBundleName = bundleName;
        }
    }

    private static void SetObject(Object target, string propertyName, Object value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        serializedObject.FindProperty(propertyName).objectReferenceValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetString(Object target, string propertyName, string value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        serializedObject.FindProperty(propertyName).stringValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetInt(Object target, string propertyName, int value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        serializedObject.FindProperty(propertyName).intValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetFloat(Object target, string propertyName, float value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        serializedObject.FindProperty(propertyName).floatValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetBool(Object target, string propertyName, bool value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        serializedObject.FindProperty(propertyName).boolValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetEnum(Object target, string propertyName, int value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        serializedObject.FindProperty(propertyName).enumValueIndex = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private sealed class EnemyPrefabDefinition
    {
        public readonly string SourcePrefabPath;
        public readonly string OutputPrefabName;
        public readonly string OutputPrefabPath;
        public readonly int EnemyId;
        public readonly string EnemyName;
        public readonly string PrefabKey;
        public readonly string AnimationPrefix;
        public readonly float MaxHealth;
        public readonly float MoveSpeed;
        public readonly float AttackDamage;
        public readonly float AttackInterval;
        public readonly int GoldReward;

        public EnemyPrefabDefinition(
            string sourcePrefabPath,
            string outputPrefabName,
            int enemyId,
            string enemyName,
            string prefabKey,
            string animationPrefix,
            float maxHealth,
            float moveSpeed,
            float attackDamage,
            float attackInterval,
            int goldReward)
        {
            SourcePrefabPath = sourcePrefabPath;
            OutputPrefabName = outputPrefabName;
            OutputPrefabPath = OutputFolderPath + "/" + outputPrefabName + ".prefab";
            EnemyId = enemyId;
            EnemyName = enemyName;
            PrefabKey = prefabKey;
            AnimationPrefix = animationPrefix;
            MaxHealth = maxHealth;
            MoveSpeed = moveSpeed;
            AttackDamage = attackDamage;
            AttackInterval = attackInterval;
            GoldReward = goldReward;
        }
    }
}
