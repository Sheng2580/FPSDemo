using Enemy;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.AI;

public static class PolygonBossZombieEnemyBuildTools
{
    private const string EnemyPrefabBundleName = "enemy_prefabs";
    private const string EnemyLayerName = "Enemy";

    private static readonly ReplacementDefinition[] Definitions =
    {
        new ReplacementDefinition(
            "Enemy_ZombieSkeleton_LOD2",
            "Assets/Art/ABRes/Enemies/Prefabs/Enemy_ZombieSkeleton_LOD2.prefab",
            "Assets/PolygonBossZombies/Prefabs/SM_Chr_ZombieBoss_Slobber_01.prefab",
            "Assets/Art/ABRes/Enemies/Prefabs/Animator/ZombieSkeleton_OneHanded.controller",
            "ZombieSkeleton_OneHanded",
            string.Empty,
            string.Empty,
            Vector3.zero,
            Vector3.zero,
            Vector3.one,
            0.45f,
            2.2f,
            new Vector3(0f, 1.1f, 0f),
            new[]
            {
                new HitBoxDefinition("HeadHitBox", EnemyHitBodyPart.Head, 2f, true, new Vector3(0f, 1.95f, 0.02f), new Vector3(0.48f, 0.38f, 0.42f)),
                new HitBoxDefinition("BodyHitBox", EnemyHitBodyPart.Body, 1f, false, new Vector3(0f, 1.2f, 0f), new Vector3(0.74f, 1.05f, 0.46f)),
                new HitBoxDefinition("LeftArmHitBox", EnemyHitBodyPart.Arm, 0.75f, false, new Vector3(-0.55f, 1.16f, 0f), new Vector3(0.28f, 0.9f, 0.3f)),
                new HitBoxDefinition("RightArmHitBox", EnemyHitBodyPart.Arm, 0.75f, false, new Vector3(0.55f, 1.16f, 0f), new Vector3(0.28f, 0.9f, 0.3f)),
                new HitBoxDefinition("LeftLegHitBox", EnemyHitBodyPart.Leg, 0.6f, false, new Vector3(-0.22f, 0.52f, 0f), new Vector3(0.28f, 0.86f, 0.3f)),
                new HitBoxDefinition("RightLegHitBox", EnemyHitBodyPart.Leg, 0.6f, false, new Vector3(0.22f, 0.52f, 0f), new Vector3(0.28f, 0.86f, 0.3f))
            }),
        new ReplacementDefinition(
            "Enemy_ZombieNerd_LOD2",
            "Assets/Art/ABRes/Enemies/Prefabs/Enemy_ZombieNerd_LOD2.prefab",
            "Assets/PolygonBossZombies/Prefabs/SM_Chr_ZombieBoss_Wretch_01.prefab",
            "Assets/Knife/Zombie Collection/Zombie Old Crone/Animations/TwoHanded/ZombieOldCrone_TwoHanded.controller",
            "ZombieOldCrone_TwoHanded",
            string.Empty,
            string.Empty,
            Vector3.zero,
            Vector3.zero,
            Vector3.one,
            0.36f,
            2f,
            new Vector3(0f, 1f, 0f),
            new[]
            {
                new HitBoxDefinition("HeadHitBox", EnemyHitBodyPart.Head, 2f, true, new Vector3(0f, 1.72f, 0.02f), new Vector3(0.36f, 0.3f, 0.34f)),
                new HitBoxDefinition("BodyHitBox", EnemyHitBodyPart.Body, 1f, false, new Vector3(0f, 1.05f, 0f), new Vector3(0.56f, 0.86f, 0.38f)),
                new HitBoxDefinition("LeftArmHitBox", EnemyHitBodyPart.Arm, 0.75f, false, new Vector3(-0.43f, 1.05f, 0f), new Vector3(0.22f, 0.74f, 0.24f)),
                new HitBoxDefinition("RightArmHitBox", EnemyHitBodyPart.Arm, 0.75f, false, new Vector3(0.43f, 1.05f, 0f), new Vector3(0.22f, 0.74f, 0.24f)),
                new HitBoxDefinition("LeftLegHitBox", EnemyHitBodyPart.Leg, 0.6f, false, new Vector3(-0.17f, 0.46f, 0f), new Vector3(0.22f, 0.78f, 0.24f)),
                new HitBoxDefinition("RightLegHitBox", EnemyHitBodyPart.Leg, 0.6f, false, new Vector3(0.17f, 0.46f, 0f), new Vector3(0.22f, 0.78f, 0.24f))
            }),
        new ReplacementDefinition(
            "Enemy_ZombieOldCrone_LOD2",
            "Assets/Art/ABRes/Enemies/Prefabs/Enemy_ZombieOldCrone_LOD2.prefab",
            "Assets/PolygonBossZombies/Prefabs/SM_Chr_ZombieBoss_Brute_01.prefab",
            "Assets/Knife/Zombie Collection/Zombie Nerd/Animations/Torch/ZombieNerd_Torch.controller",
            "ZombieNerd_Torch",
            string.Empty,
            string.Empty,
            Vector3.zero,
            Vector3.zero,
            Vector3.one,
            0.55f,
            2.45f,
            new Vector3(0f, 1.22f, 0f),
            new[]
            {
                new HitBoxDefinition("HeadHitBox", EnemyHitBodyPart.Head, 2f, true, new Vector3(0f, 2.12f, 0.03f), new Vector3(0.56f, 0.42f, 0.48f)),
                new HitBoxDefinition("BodyHitBox", EnemyHitBodyPart.Body, 1f, false, new Vector3(0f, 1.28f, 0f), new Vector3(0.96f, 1.18f, 0.62f)),
                new HitBoxDefinition("LeftArmHitBox", EnemyHitBodyPart.Arm, 0.75f, false, new Vector3(-0.7f, 1.25f, 0f), new Vector3(0.34f, 1f, 0.34f)),
                new HitBoxDefinition("RightArmHitBox", EnemyHitBodyPart.Arm, 0.75f, false, new Vector3(0.7f, 1.25f, 0f), new Vector3(0.34f, 1f, 0.34f)),
                new HitBoxDefinition("LeftLegHitBox", EnemyHitBodyPart.Leg, 0.6f, false, new Vector3(-0.28f, 0.58f, 0f), new Vector3(0.32f, 0.94f, 0.34f)),
                new HitBoxDefinition("RightLegHitBox", EnemyHitBodyPart.Leg, 0.6f, false, new Vector3(0.28f, 0.58f, 0f), new Vector3(0.32f, 0.94f, 0.34f))
            })
    };

    [MenuItem("FPSDemo/Enemy/替换普通敌人为 PolygonBossZombies 模型")]
    public static void GeneratePolygonBossZombieEnemyPrefabs()
    {
        for (int i = 0; i < Definitions.Length; i++)
        {
            ReplaceEnemyVisual(Definitions[i]);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[PolygonBossZombieEnemyBuildTools] PolygonBossZombies 敌人模型替换完成");
    }

    [MenuItem("FPSDemo/Enemy/绑定普通敌人 HitBox 到骨骼")]
    public static void BindExistingHitBoxesToBones()
    {
        int boundPrefabCount = 0;
        for (int i = 0; i < Definitions.Length; i++)
        {
            ReplacementDefinition definition = Definitions[i];
            GameObject root = PrefabUtility.LoadPrefabContents(definition.OutputPrefabPath);
            try
            {
                Animator animator = root.GetComponentInChildren<Animator>(true);
                if (animator == null)
                {
                    Debug.LogError($"[PolygonBossZombieEnemyBuildTools] 找不到 Animator {definition.OutputPrefabName}");
                    continue;
                }

                if (!BindHitBoxesToBones(root, animator.transform, definition))
                {
                    continue;
                }

                PrefabUtility.SaveAsPrefabAsset(root, definition.OutputPrefabPath);
                SetAssetBundleName(definition.OutputPrefabPath, EnemyPrefabBundleName);
                boundPrefabCount++;
                Debug.Log($"[PolygonBossZombieEnemyBuildTools] HitBox 已绑定到骨骼 {definition.OutputPrefabName}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[PolygonBossZombieEnemyBuildTools] HitBox 骨骼绑定完成 PrefabCount={boundPrefabCount}");
    }

    private static void ReplaceEnemyVisual(ReplacementDefinition definition)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(definition.OutputPrefabPath);
        try
        {
            GameObject visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(definition.VisualPrefabPath);
            RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(definition.AnimatorControllerPath);
            if (visualPrefab == null || controller == null)
            {
                Debug.LogError($"[PolygonBossZombieEnemyBuildTools] 缺少资源 {definition.OutputPrefabName}");
                return;
            }

            RemoveOldVisual(root);
            GameObject visualRoot = (GameObject)PrefabUtility.InstantiatePrefab(visualPrefab, root.transform);
            visualRoot.name = "Visual_" + visualPrefab.name;
            visualRoot.transform.localPosition = Vector3.zero;
            visualRoot.transform.localRotation = Quaternion.identity;

            SetLayerRecursively(root, ResolveEnemyLayer());
            OptimizeRenderers(visualRoot);

            Animator animator = visualRoot.GetComponent<Animator>();
            animator ??= visualRoot.GetComponentInChildren<Animator>(true);
            ConfigureAnimator(animator, controller);

            EnemyView view = visualRoot.GetComponent<EnemyView>();
            if (view == null)
            {
                view = visualRoot.AddComponent<EnemyView>();
            }

            EnemyAnimationEventReceiver receiver = visualRoot.GetComponent<EnemyAnimationEventReceiver>();
            if (receiver == null)
            {
                receiver = visualRoot.AddComponent<EnemyAnimationEventReceiver>();
            }

            ConfigureEnemyView(view, animator, definition);
            ConfigureRootComponents(root, view, receiver, definition);
            AttachWeaponIfNeeded(visualRoot, definition);
            RebuildHitBoxes(root, visualRoot, definition);

            PrefabUtility.SaveAsPrefabAsset(root, definition.OutputPrefabPath);
            SetAssetBundleName(definition.OutputPrefabPath, EnemyPrefabBundleName);
            Debug.Log($"[PolygonBossZombieEnemyBuildTools] 已替换 {definition.OutputPrefabName}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void RemoveOldVisual(GameObject root)
    {
        Animator rootAnimator = root.GetComponent<Animator>();
        if (rootAnimator != null)
        {
            Object.DestroyImmediate(rootAnimator);
        }

        EnemyView rootView = root.GetComponent<EnemyView>();
        if (rootView != null)
        {
            Object.DestroyImmediate(rootView);
        }

        EnemyAnimationEventReceiver rootReceiver = root.GetComponent<EnemyAnimationEventReceiver>();
        if (rootReceiver != null)
        {
            Object.DestroyImmediate(rootReceiver);
        }

        for (int i = root.transform.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
        }
    }

    private static void ConfigureAnimator(Animator animator, RuntimeAnimatorController controller)
    {
        if (animator == null)
        {
            return;
        }

        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = true;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.updateMode = AnimatorUpdateMode.Normal;
    }

    private static void ConfigureEnemyView(EnemyView view, Animator animator, ReplacementDefinition definition)
    {
        SetObject(view, "animator", animator);
        SetBool(view, "useRootMotion", true);
        SetString(view, "idleStateName", definition.AnimationPrefix + "_Idle");
        SetString(view, "walkStateName", definition.AnimationPrefix + "_Walk");
        SetString(view, "runStateName", definition.AnimationPrefix + "_Run");
        SetString(view, "linkTraverseStateName", definition.AnimationPrefix + "_Dodge");
        SetString(view, "attackStateName", definition.AnimationPrefix + "_Attack_1");
        SetString(view, "damageStateName", definition.AnimationPrefix + "_Damage");
        SetString(view, "deathStateName", definition.AnimationPrefix + "_Death");
        float locomotionTransition = 0.18f;
        float attackTransition = 0.1f;
        float hitTransition = 0.14f;
        float deathTransition = 0.18f;
        float recoverTransition = 0.18f;

        if (definition.AnimationPrefix == "ZombieNerd_Torch")
        {
            locomotionTransition = 0.08f;
            attackTransition = 0.06f;
            hitTransition = 0.08f;
            recoverTransition = 0.08f;
        }
        else if (definition.AnimationPrefix == "ZombieOldCrone_TwoHanded")
        {
            locomotionTransition = 0.28f;
            attackTransition = 0.16f;
            hitTransition = 0.2f;
            deathTransition = 0.28f;
            recoverTransition = 0.25f;
        }

        SetFloat(view, "locomotionTransition", locomotionTransition);
        SetFloat(view, "attackTransition", attackTransition);
        SetFloat(view, "hitTransition", hitTransition);
        SetFloat(view, "deathTransition", deathTransition);
        SetFloat(view, "recoverTransition", recoverTransition);
    }

    private static void ConfigureRootComponents(
        GameObject root,
        EnemyView view,
        EnemyAnimationEventReceiver receiver,
        ReplacementDefinition definition)
    {
        CharacterController characterController = root.GetComponent<CharacterController>();
        if (characterController != null)
        {
            characterController.radius = definition.CharacterRadius;
            characterController.height = definition.CharacterHeight;
            characterController.center = definition.CharacterCenter;
            characterController.stepOffset = Mathf.Min(0.35f, definition.CharacterHeight * 0.18f);
        }

        NavMeshAgent agent = root.GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.radius = definition.CharacterRadius;
            agent.height = definition.CharacterHeight;
            agent.autoTraverseOffMeshLink = false;
        }

        EnemyController controller = root.GetComponent<EnemyController>();
        EnemyHealth health = root.GetComponent<EnemyHealth>();
        EnemyMotor motor = root.GetComponent<EnemyMotor>();
        EnemyAttack attack = root.GetComponent<EnemyAttack>();
        EnemyBrain brain = root.GetComponent<EnemyBrain>();
        EnemyStateMachine stateMachine = root.GetComponent<EnemyStateMachine>();
        EnemyAudioController audioController = root.GetComponent<EnemyAudioController>();
        if (audioController == null)
        {
            audioController = root.AddComponent<EnemyAudioController>();
        }

        SetObject(controller, "view", view);
        SetObject(controller, "audioController", audioController);
        SetObject(audioController, "controller", controller);
        SetObject(motor, "view", view);
        SetObject(attack, "view", view);
        SetObject(brain, "view", view);
        SetObject(brain, "stateMachine", stateMachine);
        SetObject(stateMachine, "view", view);
        SetObject(receiver, "attack", attack);
    }

    private static void AttachWeaponIfNeeded(GameObject visualRoot, ReplacementDefinition definition)
    {
        if (string.IsNullOrEmpty(definition.WeaponPrefabPath))
        {
            return;
        }

        GameObject weaponPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(definition.WeaponPrefabPath);
        if (weaponPrefab == null)
        {
            Debug.LogWarning($"[PolygonBossZombieEnemyBuildTools] 找不到武器 {definition.WeaponPrefabPath}");
            return;
        }

        Transform hand = FindDeepChild(visualRoot.transform, definition.WeaponHandName);
        if (hand == null)
        {
            hand = visualRoot.transform;
        }

        GameObject weapon = (GameObject)PrefabUtility.InstantiatePrefab(weaponPrefab, hand);
        weapon.name = "Weapon_" + weaponPrefab.name;
        weapon.transform.localPosition = definition.WeaponLocalPosition;
        weapon.transform.localRotation = Quaternion.Euler(definition.WeaponLocalEulerAngles);
        weapon.transform.localScale = definition.WeaponLocalScale;
        SetLayerRecursively(weapon, ResolveEnemyLayer());
        OptimizeRenderers(weapon);
    }

    private static void RebuildHitBoxes(GameObject root, GameObject visualRoot, ReplacementDefinition definition)
    {
        Transform oldHitBoxRoot = root.transform.Find("HitBoxes");
        if (oldHitBoxRoot != null)
        {
            Object.DestroyImmediate(oldHitBoxRoot.gameObject);
        }

        GameObject hitBoxRoot = new GameObject("HitBoxes");
        hitBoxRoot.transform.SetParent(root.transform);
        hitBoxRoot.transform.localPosition = Vector3.zero;
        hitBoxRoot.transform.localRotation = Quaternion.identity;
        hitBoxRoot.transform.localScale = Vector3.one;
        hitBoxRoot.layer = ResolveEnemyLayer();

        EnemyHealth health = root.GetComponent<EnemyHealth>();
        for (int i = 0; i < definition.HitBoxes.Length; i++)
        {
            CreateHitBox(hitBoxRoot.transform, definition.HitBoxes[i], health);
        }

        BindHitBoxesToBones(root, visualRoot.transform, definition);
    }

    private static bool BindHitBoxesToBones(
        GameObject root,
        Transform visualRoot,
        ReplacementDefinition definition)
    {
        if (root == null || visualRoot == null)
        {
            return false;
        }

        bool allBound = true;
        for (int i = 0; i < definition.HitBoxes.Length; i++)
        {
            HitBoxDefinition hitBoxDefinition = definition.HitBoxes[i];
            Transform hitBox = FindDeepChild(root.transform, hitBoxDefinition.Name);
            Transform bone = FindDeepChild(visualRoot, ResolveHitBoxBoneName(hitBoxDefinition.Name));
            if (hitBox == null || bone == null)
            {
                Debug.LogError(
                    $"[PolygonBossZombieEnemyBuildTools] HitBox 骨骼绑定失败 Prefab={definition.OutputPrefabName} HitBox={hitBoxDefinition.Name}",
                    root);
                allBound = false;
                continue;
            }

            // 保持当前世界位置和尺寸 只把判定盒静态挂到对应骨骼
            hitBox.SetParent(bone, true);
            hitBox.gameObject.layer = ResolveEnemyLayer();
        }

        Transform hitBoxRoot = root.transform.Find("HitBoxes");
        if (hitBoxRoot != null && hitBoxRoot.childCount == 0)
        {
            Object.DestroyImmediate(hitBoxRoot.gameObject);
        }

        return allBound;
    }

    private static string ResolveHitBoxBoneName(string hitBoxName)
    {
        switch (hitBoxName)
        {
            case "HeadHitBox":
                return "Head";
            case "BodyHitBox":
                return "Spine_02";
            case "LeftArmHitBox":
                return "Shoulder_L";
            case "RightArmHitBox":
                return "Shoulder_R";
            case "LeftLegHitBox":
                return "UpperLeg_L";
            case "RightLegHitBox":
                return "UpperLeg_R";
            default:
                return string.Empty;
        }
    }

    private static void CreateHitBox(Transform parent, HitBoxDefinition definition, EnemyHealth health)
    {
        GameObject hitBoxObject = new GameObject(definition.Name);
        hitBoxObject.transform.SetParent(parent);
        hitBoxObject.transform.localPosition = definition.LocalPosition;
        hitBoxObject.transform.localRotation = Quaternion.identity;
        hitBoxObject.transform.localScale = Vector3.one;
        hitBoxObject.layer = ResolveEnemyLayer();

        BoxCollider boxCollider = hitBoxObject.AddComponent<BoxCollider>();
        boxCollider.isTrigger = true;
        boxCollider.size = definition.Size;

        EnemyHitBox hitBox = hitBoxObject.AddComponent<EnemyHitBox>();
        SetEnum(hitBox, "bodyPart", (int)definition.BodyPart);
        SetFloat(hitBox, "damageMultiplier", definition.DamageMultiplier);
        SetBool(hitBox, "criticalPart", definition.CriticalPart);
        SetObject(hitBox, "health", health);
    }

    private static void OptimizeRenderers(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.allowOcclusionWhenDynamic = true;

            if (renderer is SkinnedMeshRenderer skinnedMeshRenderer)
            {
                skinnedMeshRenderer.updateWhenOffscreen = false;
                skinnedMeshRenderer.skinnedMotionVectors = false;
                skinnedMeshRenderer.quality = SkinQuality.Auto;
            }
        }
    }

    private static Transform FindDeepChild(Transform parent, string childName)
    {
        if (parent == null || string.IsNullOrEmpty(childName))
        {
            return null;
        }

        if (parent.name == childName)
        {
            return parent;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform result = FindDeepChild(parent.GetChild(i), childName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        if (root == null || layer < 0)
        {
            return;
        }

        root.layer = layer;
        for (int i = 0; i < root.transform.childCount; i++)
        {
            SetLayerRecursively(root.transform.GetChild(i).gameObject, layer);
        }
    }

    private static int ResolveEnemyLayer()
    {
        int layer = LayerMask.NameToLayer(EnemyLayerName);
        return layer >= 0 ? layer : 0;
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
        if (target == null)
        {
            return;
        }

        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void SetString(Object target, string propertyName, string value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.stringValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void SetFloat(Object target, string propertyName, float value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.floatValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void SetBool(Object target, string propertyName, bool value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.boolValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void SetEnum(Object target, string propertyName, int value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.enumValueIndex = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private sealed class ReplacementDefinition
    {
        public readonly string OutputPrefabName;
        public readonly string OutputPrefabPath;
        public readonly string VisualPrefabPath;
        public readonly string AnimatorControllerPath;
        public readonly string AnimationPrefix;
        public readonly string WeaponPrefabPath;
        public readonly string WeaponHandName;
        public readonly Vector3 WeaponLocalPosition;
        public readonly Vector3 WeaponLocalEulerAngles;
        public readonly Vector3 WeaponLocalScale;
        public readonly float CharacterRadius;
        public readonly float CharacterHeight;
        public readonly Vector3 CharacterCenter;
        public readonly HitBoxDefinition[] HitBoxes;

        public ReplacementDefinition(
            string outputPrefabName,
            string outputPrefabPath,
            string visualPrefabPath,
            string animatorControllerPath,
            string animationPrefix,
            string weaponPrefabPath,
            string weaponHandName,
            Vector3 weaponLocalPosition,
            Vector3 weaponLocalEulerAngles,
            Vector3 weaponLocalScale,
            float characterRadius,
            float characterHeight,
            Vector3 characterCenter,
            HitBoxDefinition[] hitBoxes)
        {
            OutputPrefabName = outputPrefabName;
            OutputPrefabPath = outputPrefabPath;
            VisualPrefabPath = visualPrefabPath;
            AnimatorControllerPath = animatorControllerPath;
            AnimationPrefix = animationPrefix;
            WeaponPrefabPath = weaponPrefabPath;
            WeaponHandName = weaponHandName;
            WeaponLocalPosition = weaponLocalPosition;
            WeaponLocalEulerAngles = weaponLocalEulerAngles;
            WeaponLocalScale = weaponLocalScale;
            CharacterRadius = characterRadius;
            CharacterHeight = characterHeight;
            CharacterCenter = characterCenter;
            HitBoxes = hitBoxes;
        }
    }

    private readonly struct HitBoxDefinition
    {
        public readonly string Name;
        public readonly EnemyHitBodyPart BodyPart;
        public readonly float DamageMultiplier;
        public readonly bool CriticalPart;
        public readonly Vector3 LocalPosition;
        public readonly Vector3 Size;

        public HitBoxDefinition(
            string name,
            EnemyHitBodyPart bodyPart,
            float damageMultiplier,
            bool criticalPart,
            Vector3 localPosition,
            Vector3 size)
        {
            Name = name;
            BodyPart = bodyPart;
            DamageMultiplier = damageMultiplier;
            CriticalPart = criticalPart;
            LocalPosition = localPosition;
            Size = size;
        }
    }
}
