using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 开始界面静态布局整理工具
/// 所有尺寸和组件在编辑器中写入场景与预制体
/// </summary>
public static class StartUiEditorTools
{
    private const string StartScenePath = "Assets/Scenes/Start.unity";
    private const string FileItemPrefabPath = "Assets/Resources/UI/FileItem.prefab";
    private const string AutoRunMarkerPath = "/tmp/fpsdemo_configure_start_ui_once";

    [InitializeOnLoadMethod]
    private static void TryAutoRun()
    {
        if (!File.Exists(AutoRunMarkerPath))
        {
            return;
        }

        File.Delete(AutoRunMarkerPath);
        EditorApplication.delayCall += ConfigureStartUi;
    }

    [MenuItem("FPSDemo/UI/整理Start UI", priority = 89)]
    public static void ConfigureStartUi()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[StartUI] 请先退出运行模式再整理开始界面");
            return;
        }

        ConfigureFileItemPrefab();

        Scene scene = SceneManager.GetSceneByPath(StartScenePath);
        bool closeSceneAfterSave = !scene.IsValid() || !scene.isLoaded;
        if (closeSceneAfterSave)
        {
            scene = EditorSceneManager.OpenScene(StartScenePath, OpenSceneMode.Additive);
        }

        try
        {
            GameObject canvasObject = FindRoot(scene, "StartCanvas");
            if (canvasObject == null)
            {
                Debug.LogError("[StartUI] 找不到 StartCanvas");
                return;
            }

            ConfigureCanvas(canvasObject);
            ConfigureSceneLeftovers(scene);
            EnsureStartSceneInBuildSettings();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[StartUI] Start 场景和 FileItem 已整理并保存");
        }
        finally
        {
            if (closeSceneAfterSave && scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    private static void ConfigureCanvas(GameObject canvasObject)
    {
        RectTransform canvasRect = EnsureRectTransform(canvasObject);
        SetFullStretch(canvasRect);
        canvasRect.localScale = Vector3.one;
        canvasRect.localRotation = Quaternion.identity;

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = canvasObject.AddComponent<Canvas>();
        }

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.worldCamera = null;
        canvas.pixelPerfect = false;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = canvasObject.AddComponent<CanvasScaler>();
        }

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        if (canvasObject.GetComponent<GraphicRaycaster>() == null)
        {
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        CanvasGroup rootCanvasGroup = EnsureCanvasGroup(canvasObject);
        rootCanvasGroup.alpha = 1f;
        rootCanvasGroup.interactable = true;
        rootCanvasGroup.blocksRaycasts = true;

        RectTransform background = FindDirectChildRect(canvasRect, "Image");
        if (background == null && canvasRect.childCount > 0)
        {
            background = canvasRect.GetChild(0) as RectTransform;
        }

        if (background == null)
        {
            Debug.LogError("[StartUI] 找不到背景 Image");
            return;
        }

        SetFullStretch(background);
        Image backgroundImage = background.GetComponent<Image>();
        if (backgroundImage != null)
        {
            backgroundImage.raycastTarget = false;
        }

        RectTransform safeRoot = background.childCount > 0
            ? background.GetChild(0) as RectTransform
            : null;
        if (safeRoot == null)
        {
            Debug.LogError("[StartUI] 找不到开始界面内容根节点");
            return;
        }

        safeRoot.name = "SafeAreaRoot";
        SetFullStretch(safeRoot);
        SafeAreaAdapter safeAreaAdapter = safeRoot.GetComponent<SafeAreaAdapter>();
        if (safeAreaAdapter != null)
        {
            Object.DestroyImmediate(safeAreaAdapter);
        }

        Image safeRootImage = safeRoot.GetComponent<Image>();
        if (safeRootImage != null)
        {
            safeRootImage.raycastTarget = false;
        }

        DisableDirectDecorativeImageRaycasts(safeRoot);

        TMP_Text title = safeRoot.GetComponentInChildren<TMP_Text>(true);
        ConfigureTitle(title);

        RectTransform buttonColumn = FindDirectChildRect(safeRoot, "StartButtonCenter");
        if (buttonColumn == null)
        {
            Debug.LogError("[StartUI] 找不到按钮列 StartButtonCenter");
            return;
        }

        ConfigureButtonColumn(buttonColumn);
        CanvasGroup mainButtonGroup = EnsureCanvasGroup(buttonColumn.gameObject);
        mainButtonGroup.alpha = 1f;
        mainButtonGroup.interactable = true;
        mainButtonGroup.blocksRaycasts = true;
        Button startButton = ConfigureButton(buttonColumn, "StartGameButton", "开始新游戏");
        Button continueButton = ConfigureButton(buttonColumn, "ContinueGameButton", "继续游戏");
        Button readButton = ConfigureButton(buttonColumn, "ReadFileButton", "读取存档");
        Button quitButton = ConfigureButton(buttonColumn, "QuitButton", "退出游戏");

        RectTransform filePanel = FindDirectChildRect(safeRoot, "FilePanel");
        if (filePanel == null)
        {
            Debug.LogError("[StartUI] 找不到 FilePanel");
            return;
        }

        CanvasGroup filePanelGroup = ConfigureFilePanel(filePanel, buttonColumn);
        Button returnButton = ConfigureReturnButton(filePanel);
        RectTransform content = FindTransformRecursive(filePanel, "Content") as RectTransform;
        GameObject emptyState = ConfigureEmptyState(filePanel, content);

        StartCanvas startCanvas = canvasObject.GetComponent<StartCanvas>();
        if (startCanvas == null)
        {
            startCanvas = canvasObject.AddComponent<StartCanvas>();
        }

        GameObject fileItemPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FileItemPrefabPath);
        StartSaveFileItem fileItem = fileItemPrefab != null
            ? fileItemPrefab.GetComponent<StartSaveFileItem>()
            : null;

        SerializedObject serializedCanvas = new SerializedObject(startCanvas);
        serializedCanvas.FindProperty("hallSceneName").stringValue = "Hall";
        serializedCanvas.FindProperty("rootCanvasGroup").objectReferenceValue = rootCanvasGroup;
        serializedCanvas.FindProperty("titleRect").objectReferenceValue = title != null ? title.rectTransform : null;
        serializedCanvas.FindProperty("titleCanvasGroup").objectReferenceValue = title != null
            ? title.GetComponent<CanvasGroup>()
            : null;
        serializedCanvas.FindProperty("mainButtonPanel").objectReferenceValue = buttonColumn.gameObject;
        serializedCanvas.FindProperty("mainButtonCanvasGroup").objectReferenceValue = mainButtonGroup;
        serializedCanvas.FindProperty("startGameButton").objectReferenceValue = startButton;
        serializedCanvas.FindProperty("continueGameButton").objectReferenceValue = continueButton;
        serializedCanvas.FindProperty("readFileButton").objectReferenceValue = readButton;
        serializedCanvas.FindProperty("quitButton").objectReferenceValue = quitButton;
        serializedCanvas.FindProperty("filePanel").objectReferenceValue = filePanel.gameObject;
        serializedCanvas.FindProperty("filePanelRect").objectReferenceValue = filePanel;
        serializedCanvas.FindProperty("filePanelCanvasGroup").objectReferenceValue = filePanelGroup;
        serializedCanvas.FindProperty("fileContent").objectReferenceValue = content;
        serializedCanvas.FindProperty("fileItemPrefab").objectReferenceValue = fileItem;
        serializedCanvas.FindProperty("emptyState").objectReferenceValue = emptyState;
        serializedCanvas.FindProperty("returnButton").objectReferenceValue = returnButton;
        serializedCanvas.ApplyModifiedPropertiesWithoutUndo();

        buttonColumn.gameObject.SetActive(true);
        filePanel.gameObject.SetActive(false);
        canvasRect.localScale = Vector3.one;
        canvasRect.localRotation = Quaternion.identity;
        EditorUtility.SetDirty(canvasObject);
        EditorUtility.SetDirty(canvasRect);
        EditorUtility.SetDirty(canvas);
        EditorUtility.SetDirty(scaler);
        EditorUtility.SetDirty(startCanvas);
    }

    private static void ConfigureTitle(TMP_Text title)
    {
        if (title == null)
        {
            Debug.LogWarning("[StartUI] 找不到标题 TMP 文本");
            return;
        }

        RectTransform rect = title.rectTransform;
        SetAnchors(rect, new Vector2(0.08f, 0.73f), new Vector2(0.46f, 0.92f), new Vector2(0f, 0.5f));
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
        title.alignment = TextAlignmentOptions.MidlineLeft;
        title.enableAutoSizing = true;
        title.fontSizeMin = 48f;
        title.fontSizeMax = 104f;
        title.enableWordWrapping = false;
        title.raycastTarget = false;
        EnsureCanvasGroup(title.gameObject);
        EditorUtility.SetDirty(title);
    }

    private static void ConfigureButtonColumn(RectTransform buttonColumn)
    {
        SetAnchors(
            buttonColumn,
            new Vector2(0.08f, 0.16f),
            new Vector2(0.39f, 0.68f),
            new Vector2(0.5f, 0.5f));
        buttonColumn.offsetMin = Vector2.zero;
        buttonColumn.offsetMax = Vector2.zero;
        buttonColumn.localScale = Vector3.one;

        VerticalLayoutGroup layout = buttonColumn.GetComponent<VerticalLayoutGroup>();
        if (layout == null)
        {
            layout = buttonColumn.gameObject.AddComponent<VerticalLayoutGroup>();
        }

        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.spacing = 18f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        EditorUtility.SetDirty(layout);
    }

    private static Button ConfigureButton(RectTransform buttonColumn, string objectName, string label)
    {
        Transform root = FindTransformRecursive(buttonColumn, objectName);
        if (root == null)
        {
            Debug.LogWarning($"[StartUI] 找不到按钮 {objectName}");
            return null;
        }

        RectTransform rootRect = EnsureRectTransform(root.gameObject);
        rootRect.localScale = Vector3.one;
        LayoutElement layoutElement = root.GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = root.gameObject.AddComponent<LayoutElement>();
        }

        layoutElement.minHeight = 78f;
        layoutElement.preferredHeight = 96f;
        layoutElement.flexibleHeight = 1f;

        Button button = root.GetComponent<Button>();
        if (button == null)
        {
            button = root.gameObject.AddComponent<Button>();
        }

        Graphic rootGraphic = root.GetComponent<Graphic>();
        if (rootGraphic == null)
        {
            Image rootImage = root.gameObject.AddComponent<Image>();
            rootImage.color = new Color(1f, 1f, 1f, 0f);
            rootGraphic = rootImage;
        }

        rootGraphic.raycastTarget = true;
        button.targetGraphic = rootGraphic;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        EnsureCanvasGroup(button.gameObject);
        SetButtonLabel(root, label);

        Button[] nestedButtons = root.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < nestedButtons.Length; i++)
        {
            Button nestedButton = nestedButtons[i];
            if (nestedButton != null && nestedButton != button)
            {
                nestedButton.enabled = false;
                EditorUtility.SetDirty(nestedButton);
            }
        }

        EditorUtility.SetDirty(rootRect);
        EditorUtility.SetDirty(layoutElement);
        EditorUtility.SetDirty(button);
        return button;
    }

    private static CanvasGroup ConfigureFilePanel(RectTransform filePanel, RectTransform buttonColumn)
    {
        CopyRectLayout(buttonColumn, filePanel);
        filePanel.localScale = Vector3.one;

        Image panelImage = filePanel.GetComponent<Image>();
        if (panelImage != null)
        {
            panelImage.raycastTarget = false;
        }

        CanvasGroup group = EnsureCanvasGroup(filePanel.gameObject);
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        RectTransform scrollView = FindTransformRecursive(filePanel, "Scroll View") as RectTransform;
        if (scrollView != null)
        {
            SetFullStretch(scrollView);
            scrollView.offsetMin = new Vector2(0f, 112f);
            scrollView.offsetMax = new Vector2(-24f, -24f);
        }

        RectTransform viewport = FindTransformRecursive(filePanel, "Viewport") as RectTransform;
        if (viewport != null)
        {
            SetFullStretch(viewport);
        }

        RectTransform content = FindTransformRecursive(filePanel, "Content") as RectTransform;
        if (content != null)
        {
            SetAnchors(content, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;

            VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
            if (layout == null)
            {
                layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            }

            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
            if (fitter == null)
            {
                fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            }

            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Transform sceneTemplate = FindTransformRecursive(content, "FileItem");
            if (sceneTemplate != null)
            {
                sceneTemplate.gameObject.SetActive(false);
            }

            EditorUtility.SetDirty(layout);
            EditorUtility.SetDirty(fitter);
        }

        EditorUtility.SetDirty(filePanel);
        EditorUtility.SetDirty(group);
        return group;
    }

    private static Button ConfigureReturnButton(RectTransform filePanel)
    {
        Transform returnRoot = FindTransformRecursive(filePanel, "ReturnButton");
        if (returnRoot == null)
        {
            Debug.LogWarning("[StartUI] FilePanel 中找不到 ReturnButton");
            return null;
        }

        RectTransform rect = EnsureRectTransform(returnRoot.gameObject);
        SetAnchors(rect, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f));
        rect.anchoredPosition = new Vector2(0f, 0f);
        rect.sizeDelta = new Vector2(96f, 96f);
        rect.localScale = Vector3.one;

        Button button = returnRoot.GetComponent<Button>();
        if (button == null)
        {
            button = returnRoot.gameObject.AddComponent<Button>();
        }

        button.navigation = new Navigation { mode = Navigation.Mode.None };
        EnsureCanvasGroup(returnRoot.gameObject);
        EditorUtility.SetDirty(rect);
        EditorUtility.SetDirty(button);
        return button;
    }

    private static GameObject ConfigureEmptyState(RectTransform filePanel, RectTransform content)
    {
        Transform existing = FindTransformRecursive(filePanel, "EmptyStateText");
        GameObject emptyState = existing != null
            ? existing.gameObject
            : new GameObject("EmptyStateText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));

        if (existing == null)
        {
            emptyState.transform.SetParent(filePanel, false);
        }

        RectTransform rect = EnsureRectTransform(emptyState);
        SetAnchors(rect, new Vector2(0.08f, 0.22f), new Vector2(0.92f, 0.78f), new Vector2(0.5f, 0.5f));
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Text text = emptyState.GetComponent<Text>();
        Text sourceText = content != null ? content.GetComponentInChildren<Text>(true) : null;
        if (sourceText != null)
        {
            text.font = sourceText.font;
        }

        text.text = "暂无存档\n先开始新游戏并在大厅保存";
        text.fontSize = 32;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        emptyState.SetActive(true);
        emptyState.transform.SetAsLastSibling();

        EditorUtility.SetDirty(rect);
        EditorUtility.SetDirty(text);
        return emptyState;
    }

    private static void ConfigureFileItemPrefab()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(FileItemPrefabPath);
        if (prefabRoot == null)
        {
            Debug.LogError("[StartUI] 找不到 FileItem 预制体");
            return;
        }

        try
        {
            RectTransform rootRect = EnsureRectTransform(prefabRoot);
            rootRect.sizeDelta = new Vector2(680f, 154f);
            rootRect.localScale = Vector3.one;

            CanvasGroup canvasGroup = EnsureCanvasGroup(prefabRoot);
            LayoutElement rootLayout = prefabRoot.GetComponent<LayoutElement>();
            if (rootLayout == null)
            {
                rootLayout = prefabRoot.AddComponent<LayoutElement>();
            }

            rootLayout.minHeight = 136f;
            rootLayout.preferredHeight = 154f;
            rootLayout.flexibleHeight = 0f;

            HorizontalLayoutGroup layout = prefabRoot.GetComponent<HorizontalLayoutGroup>();
            if (layout == null)
            {
                layout = prefabRoot.AddComponent<HorizontalLayoutGroup>();
            }

            layout.padding = new RectOffset(24, 24, 18, 18);
            layout.spacing = 20f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            Button button = prefabRoot.GetComponent<Button>();
            if (button == null)
            {
                button = prefabRoot.AddComponent<Button>();
            }

            button.navigation = new Navigation { mode = Navigation.Mode.None };

            Image[] images = prefabRoot.GetComponentsInChildren<Image>(true);
            Image icon = null;
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] != null && images[i].gameObject != prefabRoot)
                {
                    icon = images[i];
                    break;
                }
            }

            if (icon != null)
            {
                icon.raycastTarget = false;
                LayoutElement iconLayout = icon.GetComponent<LayoutElement>();
                if (iconLayout == null)
                {
                    iconLayout = icon.gameObject.AddComponent<LayoutElement>();
                }

                iconLayout.minWidth = 92f;
                iconLayout.preferredWidth = 92f;
                iconLayout.minHeight = 92f;
                iconLayout.preferredHeight = 92f;
                iconLayout.flexibleWidth = 0f;
                iconLayout.flexibleHeight = 0f;
                EditorUtility.SetDirty(iconLayout);
            }

            Text text = prefabRoot.GetComponentInChildren<Text>(true);
            if (text != null)
            {
                text.fontSize = 27;
                text.resizeTextForBestFit = true;
                text.resizeTextMinSize = 20;
                text.resizeTextMaxSize = 28;
                text.alignment = TextAnchor.MiddleLeft;
                text.lineSpacing = 0.92f;
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.verticalOverflow = VerticalWrapMode.Truncate;
                text.raycastTarget = false;

                LayoutElement textLayout = text.GetComponent<LayoutElement>();
                if (textLayout == null)
                {
                    textLayout = text.gameObject.AddComponent<LayoutElement>();
                }

                textLayout.minWidth = 300f;
                textLayout.preferredWidth = 520f;
                textLayout.flexibleWidth = 1f;
                textLayout.minHeight = 112f;
                textLayout.preferredHeight = 118f;
                EditorUtility.SetDirty(textLayout);
            }

            StartSaveFileItem item = prefabRoot.GetComponent<StartSaveFileItem>();
            if (item == null)
            {
                item = prefabRoot.AddComponent<StartSaveFileItem>();
            }

            SerializedObject serializedItem = new SerializedObject(item);
            serializedItem.FindProperty("button").objectReferenceValue = button;
            serializedItem.FindProperty("legacyText").objectReferenceValue = text;
            serializedItem.FindProperty("tmpText").objectReferenceValue = prefabRoot.GetComponentInChildren<TMP_Text>(true);
            serializedItem.FindProperty("canvasGroup").objectReferenceValue = canvasGroup;
            serializedItem.FindProperty("itemRect").objectReferenceValue = rootRect;
            serializedItem.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, FileItemPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static void ConfigureSceneLeftovers(Scene scene)
    {
        GameObject dropdown = FindRoot(scene, "Dropdown");
        if (dropdown != null)
        {
            dropdown.SetActive(false);
        }

        GameObject extraCamera = FindRoot(scene, "Camera (1)");
        if (extraCamera != null)
        {
            extraCamera.SetActive(false);
        }

        EventSystem eventSystem = FindInScene<EventSystem>(scene);
        if (eventSystem != null)
        {
            eventSystem.gameObject.SetActive(true);
            eventSystem.enabled = true;
            EditorUtility.SetDirty(eventSystem);
        }
    }

    private static void EnsureStartSceneInBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        int existingIndex = scenes.FindIndex(scene => scene.path == StartScenePath);
        if (existingIndex >= 0)
        {
            EditorBuildSettingsScene existing = scenes[existingIndex];
            scenes.RemoveAt(existingIndex);
            scenes.Insert(0, new EditorBuildSettingsScene(existing.path, true));
        }
        else
        {
            scenes.Insert(0, new EditorBuildSettingsScene(StartScenePath, true));
        }

        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static void SetButtonLabel(Transform root, string label)
    {
        TMP_Text tmpText = root.GetComponentInChildren<TMP_Text>(true);
        if (tmpText != null)
        {
            tmpText.text = label;
            tmpText.raycastTarget = false;
            EditorUtility.SetDirty(tmpText);
        }

        Text legacyText = root.GetComponentInChildren<Text>(true);
        if (legacyText != null)
        {
            legacyText.text = label;
            legacyText.raycastTarget = false;
            EditorUtility.SetDirty(legacyText);
        }
    }

    private static CanvasGroup EnsureCanvasGroup(GameObject target)
    {
        CanvasGroup group = target.GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = target.AddComponent<CanvasGroup>();
        }

        EditorUtility.SetDirty(group);
        return group;
    }

    private static void DisableDirectDecorativeImageRaycasts(RectTransform root)
    {
        if (root == null)
        {
            return;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Image image = root.GetChild(i).GetComponent<Image>();
            if (image == null)
            {
                continue;
            }

            image.raycastTarget = false;
            EditorUtility.SetDirty(image);
        }
    }

    private static void CopyRectLayout(RectTransform source, RectTransform target)
    {
        if (source == null || target == null)
        {
            return;
        }

        target.anchorMin = source.anchorMin;
        target.anchorMax = source.anchorMax;
        target.anchoredPosition = source.anchoredPosition;
        target.sizeDelta = source.sizeDelta;
        target.pivot = source.pivot;
        target.localRotation = Quaternion.identity;
        target.localScale = Vector3.one;
    }

    private static RectTransform EnsureRectTransform(GameObject target)
    {
        RectTransform rect = target.transform as RectTransform;
        return rect != null ? rect : target.AddComponent<RectTransform>();
    }

    private static void SetFullStretch(RectTransform rect)
    {
        if (rect == null)
        {
            return;
        }

        SetAnchors(rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    private static void SetAnchors(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
    }

    private static RectTransform FindDirectChildRect(Transform parent, string childName)
    {
        if (parent == null)
        {
            return null;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName)
            {
                return child as RectTransform;
            }
        }

        return null;
    }

    private static Transform FindTransformRecursive(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == childName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform result = FindTransformRecursive(root.GetChild(i), childName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private static GameObject FindRoot(Scene scene, string objectName)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return null;
        }

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i] != null && roots[i].name == objectName)
            {
                return roots[i];
            }
        }

        return null;
    }

    private static T FindInScene<T>(Scene scene) where T : Component
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            T component = roots[i].GetComponentInChildren<T>(true);
            if (component != null)
            {
                return component;
            }
        }

        return null;
    }
}
