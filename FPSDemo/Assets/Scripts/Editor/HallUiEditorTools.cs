using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class HallUiEditorTools
{
    private const string HallScenePath = "Assets/Scenes/Hall.unity";
    private const string AutoRunMarkerPath = "/tmp/fpsdemo_configure_hall_ui_once";
    private const string HallFontPath = "Assets/Art/Font/AaFengKuangYuanShiRen-2.ttf";

    [InitializeOnLoadMethod]
    private static void TryAutoRun()
    {
        if (!File.Exists(AutoRunMarkerPath))
        {
            return;
        }

        File.Delete(AutoRunMarkerPath);
        EditorApplication.delayCall += ConfigureHallUi;
    }

    [MenuItem("FPSDemo/UI/整理Hall UI", priority = 90)]
    public static void ConfigureHallUi()
    {
        Scene scene = EnsureHallScene();
        GameObject canvasObject = GameObject.Find("UpgradeCanvas");
        if (canvasObject == null)
        {
            Debug.LogError("[HallUI] 找不到 UpgradeCanvas");
            return;
        }

        RectTransform canvasRect = EnsureRectTransform(canvasObject);
        SetFullStretch(canvasRect);
        EditorUtility.SetDirty(canvasRect);

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = canvasObject.AddComponent<CanvasScaler>();
        }

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        EditorUtility.SetDirty(scaler);

        GraphicRaycaster raycaster = canvasObject.GetComponent<GraphicRaycaster>();
        if (raycaster == null)
        {
            raycaster = canvasObject.AddComponent<GraphicRaycaster>();
        }
        EditorUtility.SetDirty(raycaster);

        RectTransform root = GetChildRect(canvasRect, 0, "Root");
        SetFullStretch(root);
        Image rootImage = root.GetComponent<Image>();
        if (rootImage != null)
        {
            rootImage.raycastTarget = false;
        }

        RectTransform topNav = GetChildRect(root, 0, "TopNav");
        SetTopNav(topNav);

        GameObject playerPanel = FindRequired(root, "PlayerUPPanel");
        GameObject weaponPanel = FindRequired(root, "WeapponUPPanel");
        GameObject gamePanel = FindRequired(root, "GamePanel");

        EnsureCanvasGroup(playerPanel);
        EnsureCanvasGroup(weaponPanel);
        EnsureCanvasGroup(gamePanel);

        SetContentPanel(EnsureRectTransform(playerPanel));
        SetContentPanel(EnsureRectTransform(weaponPanel));
        SetContentPanel(EnsureRectTransform(gamePanel));

        playerPanel.SetActive(false);
        weaponPanel.SetActive(false);
        gamePanel.SetActive(true);

        ConfigureGamePanel(gamePanel.transform);
        ConfigurePlayerPanel(playerPanel.transform);
        ConfigureWeaponPanel(weaponPanel.transform);

        HallCanvas hallCanvas = canvasObject.GetComponent<HallCanvas>();
        if (hallCanvas == null)
        {
            hallCanvas = canvasObject.AddComponent<HallCanvas>();
        }

        SerializedObject hallCanvasObject = new SerializedObject(hallCanvas);
        hallCanvasObject.FindProperty("playerPanel").objectReferenceValue = playerPanel;
        hallCanvasObject.FindProperty("weaponPanel").objectReferenceValue = weaponPanel;
        hallCanvasObject.FindProperty("gamePanel").objectReferenceValue = gamePanel;
        hallCanvasObject.FindProperty("playerTabGraphic").objectReferenceValue = topNav.GetChild(0).GetComponent<Graphic>();
        hallCanvasObject.FindProperty("weaponTabGraphic").objectReferenceValue = topNav.GetChild(1).GetComponent<Graphic>();
        hallCanvasObject.FindProperty("gameTabGraphic").objectReferenceValue = topNav.GetChild(2).GetComponent<Graphic>();
        hallCanvasObject.FindProperty("combatSceneName").stringValue = "Combat";
        hallCanvasObject.FindProperty("enableKeyboardTest").boolValue = true;
        hallCanvasObject.ApplyModifiedPropertiesWithoutUndo();

        ConfigureTabButtons(topNav);
        ConfigureStartButton(gamePanel.transform);
        ConfigureEventSystem();

        EditorUtility.SetDirty(canvasObject);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[HallUI] Hall UI 已整理并保存");
    }

    [MenuItem("FPSDemo/UI/整理Hall玩家数据", priority = 91)]
    public static void ConfigureHallPlayerData()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[HallUI] 请先退出运行模式再整理玩家数据");
            return;
        }

        Scene scene = EnsureHallScene();
        GameObject canvasObject = GameObject.Find("UpgradeCanvas");
        if (canvasObject == null)
        {
            Debug.LogError("[HallUI] 找不到 UpgradeCanvas");
            return;
        }

        RectTransform canvasRect = EnsureRectTransform(canvasObject);
        RectTransform root = GetChildRect(canvasRect, 0, "Root");
        GameObject playerPanel = FindRequired(root, "PlayerUPPanel");
        ConfigurePlayerPanel(playerPanel.transform);

        EditorUtility.SetDirty(playerPanel);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[HallUI] 玩家数据面板已整理并保存");
    }

    private static Scene EnsureHallScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path == HallScenePath)
        {
            return scene;
        }

        return EditorSceneManager.OpenScene(HallScenePath);
    }

    private static void ConfigureTabButtons(RectTransform topNav)
    {
        Button playerButton = EnsureButton(topNav.GetChild(0).gameObject);
        Button weaponButton = EnsureButton(topNav.GetChild(1).gameObject);
        Button gameButton = EnsureButton(topNav.GetChild(2).gameObject);

        ClearButtonClick(playerButton);
        ClearButtonClick(weaponButton);
        ClearButtonClick(gameButton);
    }

    private static void ConfigureStartButton(Transform gamePanel)
    {
        Transform startButtonTransform = FindChildWithText(gamePanel, "开始游戏");
        if (startButtonTransform == null)
        {
            Debug.LogWarning("[HallUI] 找不到开始游戏按钮");
            return;
        }

        RectTransform buttonRect = EnsureRectTransform(startButtonTransform.gameObject);
        SetAnchor(buttonRect, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0.5f));
        buttonRect.anchoredPosition = new Vector2(-190f, 88f);
        buttonRect.sizeDelta = new Vector2(320f, 96f);

        Button button = EnsureButton(startButtonTransform.gameObject);
        EnsureCanvasGroup(startButtonTransform.gameObject);
        ClearButtonClick(button);
    }

    private static CanvasGroup EnsureCanvasGroup(GameObject target)
    {
        CanvasGroup canvasGroup = target.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = target.AddComponent<CanvasGroup>();
        }

        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
        EditorUtility.SetDirty(canvasGroup);
        return canvasGroup;
    }

    private static void ConfigureEventSystem()
    {
        EventSystem eventSystem = UnityEngine.Object.FindObjectOfType<EventSystem>(true);
        GameObject eventSystemObject;
        if (eventSystem == null)
        {
            eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
            eventSystem = eventSystemObject.GetComponent<EventSystem>();
        }
        else
        {
            eventSystemObject = eventSystem.gameObject;
        }

        StandaloneInputModule inputModule = eventSystemObject.GetComponent<StandaloneInputModule>();
        if (inputModule == null)
        {
            inputModule = eventSystemObject.AddComponent<StandaloneInputModule>();
        }

        eventSystemObject.SetActive(true);
        eventSystem.enabled = true;
        inputModule.enabled = true;
        EditorUtility.SetDirty(eventSystemObject);
        EditorUtility.SetDirty(eventSystem);
        EditorUtility.SetDirty(inputModule);
    }

    private static void ConfigureGamePanel(Transform gamePanel)
    {
        RectTransform startButton = FindChildWithText(gamePanel, "开始游戏") as RectTransform;
        if (startButton != null)
        {
            SetAnchor(startButton, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0.5f));
            startButton.anchoredPosition = new Vector2(-190f, 88f);
            startButton.sizeDelta = new Vector2(320f, 96f);
        }

        RectTransform weaponSelection = FindDirectChildRect(gamePanel, "WeaponSelection");
        if (weaponSelection != null)
        {
            SetAnchor(weaponSelection, new Vector2(0.08f, 0.24f), new Vector2(0.72f, 0.78f), new Vector2(0.5f, 0.5f));
            weaponSelection.offsetMin = Vector2.zero;
            weaponSelection.offsetMax = Vector2.zero;
            ConfigureWeaponSelectionItems(weaponSelection);
        }
    }

    private static void ConfigurePlayerPanel(Transform playerPanel)
    {
        RectTransform numerical = FindDirectChildRect(playerPanel, "PlayeNumerical");
        if (numerical == null)
        {
            Debug.LogWarning("[HallUI] 找不到 PlayeNumerical");
            return;
        }

        SetAnchor(numerical, new Vector2(0.10f, 0.22f), new Vector2(0.90f, 0.82f), new Vector2(0.5f, 0.5f));
        numerical.offsetMin = Vector2.zero;
        numerical.offsetMax = Vector2.zero;
        ClearChildren(numerical);
        RemoveLayoutComponents(numerical.gameObject);

        VerticalLayoutGroup layout = numerical.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding.left = 0;
        layout.padding.right = 0;
        layout.padding.top = 0;
        layout.padding.bottom = 0;
        layout.spacing = 14f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        EditorUtility.SetDirty(layout);

        Font font = ResolveFont(numerical);
        CreatePlayerStatRow(numerical, "PlayerStat_MaxHp", "最大生命 LV.0", "100", font, 0);
        CreatePlayerStatRow(numerical, "PlayerStat_MoveSpeed", "移动速度 LV.0", "4.50 / 6.50", font, 1);
        CreatePlayerStatRow(numerical, "PlayerStat_JumpHeight", "跳跃高度 LV.0", "1.20", font, 2);
        CreatePlayerStatRow(numerical, "PlayerStat_SkillCooldown", "技能冷却缩减 LV.0", "0%", font, 3);
        CreatePlayerStatRow(numerical, "PlayerStat_EnergyGain", "充能效率 LV.0", "0%", font, 4);
    }

    private static void CreatePlayerStatRow(
        RectTransform parent,
        string rowName,
        string labelValue,
        string statValue,
        Font font,
        int index)
    {
        RectTransform row = CreateUiObject(rowName, parent, typeof(Image), typeof(LayoutElement), typeof(HorizontalLayoutGroup));
        Image image = row.GetComponent<Image>();
        image.color = index % 2 == 0
            ? new Color(1f, 1f, 1f, 0.96f)
            : new Color(1f, 0.90f, 0.60f, 0.92f);
        image.raycastTarget = false;

        LayoutElement rowLayout = row.GetComponent<LayoutElement>();
        rowLayout.minHeight = 92f;
        rowLayout.preferredHeight = 102f;
        rowLayout.flexibleHeight = 0f;

        HorizontalLayoutGroup rowGroup = row.GetComponent<HorizontalLayoutGroup>();
        rowGroup.padding.left = 34;
        rowGroup.padding.right = 34;
        rowGroup.padding.top = 8;
        rowGroup.padding.bottom = 8;
        rowGroup.spacing = 24f;
        rowGroup.childAlignment = TextAnchor.MiddleCenter;
        rowGroup.childControlWidth = true;
        rowGroup.childControlHeight = true;
        rowGroup.childForceExpandWidth = false;
        rowGroup.childForceExpandHeight = true;

        Text label = EnsureChildText(row, "Label", font);
        ConfigureSelectionText(label, labelValue, 34, TextAnchor.MiddleLeft, new Color(0.18f, 0.18f, 0.18f, 1f));
        SetTextLayout(label, 620f, 1f);

        Text value = EnsureChildText(row, "Value", font);
        ConfigureSelectionText(value, statValue, 36, TextAnchor.MiddleRight, new Color(1f, 0.60f, 0f, 1f));
        SetTextLayout(value, 300f, 0f);

        EditorUtility.SetDirty(row);
        EditorUtility.SetDirty(image);
        EditorUtility.SetDirty(rowLayout);
        EditorUtility.SetDirty(rowGroup);
        EditorUtility.SetDirty(label);
        EditorUtility.SetDirty(value);
    }

    private static void ConfigureWeaponPanel(Transform weaponPanel)
    {
        RectTransform list = FindDirectChildRect(weaponPanel, "WeaponUP");
        if (list == null)
        {
            list = FindDirectChildRect(weaponPanel, "Image");
        }

        if (list != null)
        {
            SetAnchor(list, new Vector2(0.04f, 0.12f), new Vector2(0.29f, 0.92f), new Vector2(0.5f, 0.5f));
            list.offsetMin = Vector2.zero;
            list.offsetMax = Vector2.zero;
            ConfigureWeaponUpgradeList(list);
        }

        RectTransform detail = FindDirectChildRect(weaponPanel, "Image (1)");
        if (detail != null)
        {
            SetAnchor(detail, new Vector2(0.32f, 0.12f), new Vector2(0.96f, 0.92f), new Vector2(0.5f, 0.5f));
            detail.offsetMin = Vector2.zero;
            detail.offsetMax = Vector2.zero;
            ConfigureWeaponUpgradeDetail(detail);
        }
    }

    private static void ConfigureWeaponUpgradeList(RectTransform list)
    {
        ClearChildren(list);
        RemoveLayoutComponents(list.gameObject);

        Image listImage = list.GetComponent<Image>();
        if (listImage == null)
        {
            listImage = list.gameObject.AddComponent<Image>();
        }

        listImage.color = new Color(1f, 1f, 1f, 0.95f);
        listImage.raycastTarget = true;
        EditorUtility.SetDirty(listImage);

        VerticalLayoutGroup layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding.left = 24;
        layout.padding.right = 24;
        layout.padding.top = 32;
        layout.padding.bottom = 32;
        layout.spacing = 28f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        EditorUtility.SetDirty(layout);

        Font font = ResolveFont(list);
        string[] names = { "手枪", "步枪", "霰弹枪" };
        for (int i = 0; i < names.Length; i++)
        {
            RectTransform item = CreateUiObject($"UpgradeWeaponItem_{i}", list, typeof(Image), typeof(Button), typeof(LayoutElement));
            Image image = item.GetComponent<Image>();
            image.color = i == 0 ? new Color(1f, 0.78f, 0.78f, 0.85f) : new Color(1f, 1f, 1f, 0.65f);
            image.raycastTarget = true;

            LayoutElement itemLayout = item.GetComponent<LayoutElement>();
            itemLayout.minHeight = 116f;
            itemLayout.preferredHeight = 132f;
            itemLayout.flexibleHeight = 0f;

            Button button = item.GetComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            button.interactable = true;

            Text nameText = EnsureChildText(item, "Name", font);
            ConfigureSelectionText(nameText, names[i], 46, TextAnchor.MiddleCenter, new Color(0.18f, 0.18f, 0.18f, 1f));
            SetFullStretch(nameText.rectTransform);

            EditorUtility.SetDirty(item);
            EditorUtility.SetDirty(image);
            EditorUtility.SetDirty(itemLayout);
            EditorUtility.SetDirty(button);
        }
    }

    private static void ConfigureWeaponUpgradeDetail(RectTransform detail)
    {
        ClearChildren(detail);
        RemoveLayoutComponents(detail.gameObject);

        Image detailImage = detail.GetComponent<Image>();
        if (detailImage == null)
        {
            detailImage = detail.gameObject.AddComponent<Image>();
        }

        detailImage.color = new Color(0.35f, 0.35f, 0.35f, 1f);
        detailImage.raycastTarget = true;
        EditorUtility.SetDirty(detailImage);

        RectTransform card = CreateUiObject("WeaponDetailCard", detail, typeof(Image));
        SetAnchor(card, new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.92f), new Vector2(0.5f, 0.5f));
        card.offsetMin = Vector2.zero;
        card.offsetMax = Vector2.zero;

        Image cardImage = card.GetComponent<Image>();
        cardImage.color = Color.white;
        cardImage.raycastTarget = false;
        EditorUtility.SetDirty(cardImage);

        Font font = ResolveFont(detail);
        CreateDetailText(card, "Title", "手枪", 54, TextAnchor.MiddleLeft, new Vector2(0.08f, 0.78f), new Vector2(0.42f, 0.94f), new Color(0.18f, 0.18f, 0.18f, 1f), font);
        CreateDetailText(card, "DamageLabel", "伤害:", 30, TextAnchor.MiddleLeft, new Vector2(0.08f, 0.62f), new Vector2(0.22f, 0.74f), new Color(0.18f, 0.18f, 0.18f, 1f), font);
        CreateDetailText(card, "DamageValue", "100", 30, TextAnchor.MiddleLeft, new Vector2(0.22f, 0.62f), new Vector2(0.36f, 0.74f), new Color(1f, 0.73f, 0f, 1f), font);
        CreateDetailText(card, "FireRateLabel", "射速:", 30, TextAnchor.MiddleLeft, new Vector2(0.44f, 0.62f), new Vector2(0.58f, 0.74f), new Color(0.18f, 0.18f, 0.18f, 1f), font);
        CreateDetailText(card, "FireRateValue", "1.8", 30, TextAnchor.MiddleLeft, new Vector2(0.58f, 0.62f), new Vector2(0.72f, 0.74f), new Color(1f, 0.73f, 0f, 1f), font);
        CreateDetailText(card, "Desc", "点射简单好用", 38, TextAnchor.MiddleLeft, new Vector2(0.08f, 0.40f), new Vector2(0.92f, 0.58f), new Color(0.18f, 0.18f, 0.18f, 1f), font);
        CreateDetailText(card, "UpgradeLabel", "升级:", 34, TextAnchor.MiddleLeft, new Vector2(0.24f, 0.16f), new Vector2(0.40f, 0.30f), new Color(0.18f, 0.18f, 0.18f, 1f), font);
        CreateDetailText(card, "GoldNum", "10000", 34, TextAnchor.MiddleLeft, new Vector2(0.56f, 0.16f), new Vector2(0.78f, 0.30f), new Color(1f, 0.73f, 0f, 1f), font);
    }

    private static void CreateDetailText(RectTransform parent, string name, string value, int fontSize, TextAnchor alignment, Vector2 anchorMin, Vector2 anchorMax, Color color, Font font)
    {
        Text text = EnsureChildText(parent, name, font);
        ConfigureSelectionText(text, value, fontSize, alignment, color);
        RectTransform rect = text.rectTransform;
        SetAnchor(rect, anchorMin, anchorMax, new Vector2(0.5f, 0.5f));
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static RectTransform CreateUiObject(string name, RectTransform parent, params Type[] components)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        gameObject.transform.SetParent(parent, false);

        for (int i = 0; i < components.Length; i++)
        {
            if (gameObject.GetComponent(components[i]) == null)
            {
                gameObject.AddComponent(components[i]);
            }
        }

        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.localScale = Vector3.one;
        EditorUtility.SetDirty(gameObject);
        return rect;
    }

    private static void ClearChildren(RectTransform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            UnityEngine.Object.DestroyImmediate(parent.GetChild(i).gameObject);
        }
    }

    private static void RemoveLayoutComponents(GameObject target)
    {
        RemoveComponent<HorizontalLayoutGroup>(target);
        RemoveComponent<VerticalLayoutGroup>(target);
        RemoveComponent<GridLayoutGroup>(target);
        RemoveComponent<ContentSizeFitter>(target);
        RemoveComponent<LayoutElement>(target);
    }

    private static void RemoveComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        if (component != null)
        {
            UnityEngine.Object.DestroyImmediate(component);
        }
    }

    private static void ConfigureWeaponSelectionItems(RectTransform weaponSelection)
    {
        VerticalLayoutGroup layout = weaponSelection.GetComponent<VerticalLayoutGroup>();
        if (layout == null)
        {
            layout = weaponSelection.gameObject.AddComponent<VerticalLayoutGroup>();
        }

        layout.padding.left = 0;
        layout.padding.right = 0;
        layout.padding.top = 0;
        layout.padding.bottom = 0;
        layout.spacing = 28f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        EditorUtility.SetDirty(layout);

        Font font = ResolveFont(weaponSelection);
        string[] names = { "手枪", "步枪", "霰弹枪" };
        string[] descriptions = { "点射简单好用", "连续输出稳定", "近距离爆发" };

        for (int i = 0; i < names.Length; i++)
        {
            RectTransform item = EnsureWeaponSelectionItem(weaponSelection, i);
            ConfigureWeaponSelectionItem(item, names[i], descriptions[i], font);
        }

        for (int i = weaponSelection.childCount - 1; i >= names.Length; i--)
        {
            Transform child = weaponSelection.GetChild(i);
            if (child.name.StartsWith("HallWeaponItem_", StringComparison.Ordinal))
            {
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }
    }

    private static RectTransform EnsureWeaponSelectionItem(RectTransform parent, int index)
    {
        Transform child = parent.Find($"HallWeaponItem_{index}");
        if (child == null && index < parent.childCount)
        {
            child = parent.GetChild(index);
        }

        GameObject itemObject;
        if (child == null)
        {
            itemObject = new GameObject($"HallWeaponItem_{index}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            itemObject.transform.SetParent(parent, false);
        }
        else
        {
            itemObject = child.gameObject;
            itemObject.name = $"HallWeaponItem_{index}";
        }

        RectTransform rect = EnsureRectTransform(itemObject);
        rect.localScale = Vector3.one;
        rect.SetSiblingIndex(index);

        Image image = itemObject.GetComponent<Image>();
        if (image == null)
        {
            image = itemObject.AddComponent<Image>();
        }

        image.color = index == 0 ? new Color(1f, 0.78f, 0.78f, 0.78f) : new Color(1f, 1f, 1f, 0.30f);
        image.raycastTarget = true;
        EditorUtility.SetDirty(image);

        Button button = itemObject.GetComponent<Button>();
        if (button == null)
        {
            button = itemObject.AddComponent<Button>();
        }

        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;
        button.interactable = true;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        EditorUtility.SetDirty(button);

        LayoutElement layoutElement = itemObject.GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = itemObject.AddComponent<LayoutElement>();
        }

        layoutElement.minHeight = 132f;
        layoutElement.preferredHeight = 156f;
        layoutElement.flexibleHeight = 0f;
        EditorUtility.SetDirty(layoutElement);

        HorizontalLayoutGroup horizontalLayout = itemObject.GetComponent<HorizontalLayoutGroup>();
        if (horizontalLayout == null)
        {
            horizontalLayout = itemObject.AddComponent<HorizontalLayoutGroup>();
        }

        horizontalLayout.padding.left = 34;
        horizontalLayout.padding.right = 34;
        horizontalLayout.padding.top = 12;
        horizontalLayout.padding.bottom = 12;
        horizontalLayout.spacing = 36f;
        horizontalLayout.childAlignment = TextAnchor.MiddleCenter;
        horizontalLayout.childControlWidth = true;
        horizontalLayout.childControlHeight = true;
        horizontalLayout.childForceExpandWidth = false;
        horizontalLayout.childForceExpandHeight = true;
        EditorUtility.SetDirty(horizontalLayout);

        return rect;
    }

    private static void ConfigureWeaponSelectionItem(RectTransform item, string weaponName, string description, Font font)
    {
        Text nameText = EnsureChildText(item, "WeaponName", font);
        Text descriptionText = EnsureChildText(item, "WeaponDescription", font);

        ConfigureSelectionText(nameText, weaponName, 48, TextAnchor.MiddleLeft, new Color(0.18f, 0.18f, 0.18f, 1f));
        ConfigureSelectionText(descriptionText, description, 40, TextAnchor.MiddleCenter, new Color(0.18f, 0.18f, 0.18f, 1f));

        SetTextLayout(nameText, 260f, 0f);
        SetTextLayout(descriptionText, 560f, 1f);
    }

    private static Text EnsureChildText(RectTransform parent, string childName, Font font)
    {
        Transform child = parent.Find(childName);
        GameObject textObject;
        if (child == null)
        {
            textObject = new GameObject(childName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(parent, false);
        }
        else
        {
            textObject = child.gameObject;
        }

        Text text = textObject.GetComponent<Text>();
        if (text == null)
        {
            text = textObject.AddComponent<Text>();
        }

        if (font != null)
        {
            text.font = font;
        }

        return text;
    }

    private static void ConfigureSelectionText(Text text, string value, int fontSize, TextAnchor alignment, Color color)
    {
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        EditorUtility.SetDirty(text);
    }

    private static void SetTextLayout(Text text, float preferredWidth, float flexibleWidth)
    {
        LayoutElement layoutElement = text.GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = text.gameObject.AddComponent<LayoutElement>();
        }

        layoutElement.minWidth = preferredWidth;
        layoutElement.preferredWidth = preferredWidth;
        layoutElement.flexibleWidth = flexibleWidth;
        EditorUtility.SetDirty(layoutElement);
    }

    private static Font ResolveFont(Transform root)
    {
        Font font = AssetDatabase.LoadAssetAtPath<Font>(HallFontPath);
        if (font != null)
        {
            return font;
        }

        return Font.CreateDynamicFontFromOSFont("Arial", 32);
    }

    private static void SetTopNav(RectTransform rect)
    {
        SetAnchor(rect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(0f, 128f);

        HorizontalLayoutGroup layout = rect.GetComponent<HorizontalLayoutGroup>();
        if (layout != null)
        {
            layout.padding.left = 120;
            layout.padding.right = 120;
            layout.padding.top = 16;
            layout.padding.bottom = 16;
            layout.spacing = 24f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
        }
    }

    private static void SetContentPanel(RectTransform rect)
    {
        SetAnchor(rect, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f));
        rect.offsetMin = new Vector2(96f, 64f);
        rect.offsetMax = new Vector2(-96f, -168f);
    }

    private static void SetFullStretch(RectTransform rect)
    {
        SetAnchor(rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
        ForceLocalScaleOne(rect);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        EditorUtility.SetDirty(rect);
    }

    private static void SetAnchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 pivot)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.pivot = pivot;
        ForceLocalScaleOne(rect);
        EditorUtility.SetDirty(rect);
    }

    private static void ForceLocalScaleOne(RectTransform rect)
    {
        rect.localScale = Vector3.one;

        SerializedObject serializedObject = new SerializedObject(rect);
        SerializedProperty scaleProperty = serializedObject.FindProperty("m_LocalScale");
        if (scaleProperty != null)
        {
            scaleProperty.vector3Value = Vector3.one;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static Button EnsureButton(GameObject target)
    {
        Button button = target.GetComponent<Button>();
        if (button == null)
        {
            button = target.AddComponent<Button>();
        }

        button.transition = Selectable.Transition.ColorTint;
        button.targetGraphic = target.GetComponent<Graphic>();
        button.colors = ColorBlock.defaultColorBlock;
        button.interactable = true;
        return button;
    }

    private static void ClearButtonClick(Button button)
    {
        button.onClick = new Button.ButtonClickedEvent();
        EditorUtility.SetDirty(button);
    }

    private static RectTransform GetChildRect(RectTransform parent, int index, string debugName)
    {
        if (parent.childCount <= index)
        {
            throw new InvalidOperationException($"[HallUI] {parent.name} 缺少 {debugName}");
        }

        return EnsureRectTransform(parent.GetChild(index).gameObject);
    }

    private static GameObject FindRequired(RectTransform parent, string childName)
    {
        Transform child = parent.Find(childName);
        if (child == null)
        {
            throw new InvalidOperationException($"[HallUI] 找不到 {childName}");
        }

        return child.gameObject;
    }

    private static RectTransform FindDirectChildRect(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        return child != null ? child as RectTransform : null;
    }

    private static RectTransform EnsureRectTransform(GameObject target)
    {
        RectTransform rect = target.GetComponent<RectTransform>();
        if (rect == null)
        {
            rect = target.AddComponent<RectTransform>();
        }

        return rect;
    }

    private static Transform FindChildWithText(Transform parent, string text)
    {
        Text[] texts = parent.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] != null && texts[i].text == text)
            {
                return texts[i].transform.parent;
            }
        }

        return null;
    }

    [MenuItem("FPSDemo/UI/修复Hall交互组件")]
    private static void RepairHallInteractionComponents()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[HallUI] 请先退出运行模式再修复交互组件");
            return;
        }

        Scene scene = EnsureHallScene();
        GameObject canvasObject = GameObject.Find("UpgradeCanvas");
        HallCanvas hallCanvas = canvasObject != null ? canvasObject.GetComponent<HallCanvas>() : null;
        if (hallCanvas == null)
        {
            Debug.LogError("[HallUI] 找不到 HallCanvas");
            return;
        }

        RectTransform canvasRect = EnsureRectTransform(canvasObject);
        RectTransform root = GetChildRect(canvasRect, 0, "Root");
        Transform gamePanel = FindRequired(root, "GamePanel").transform;

        ConfigureStartButton(gamePanel);
        RectTransform weaponSelection = FindDirectChildRect(gamePanel, "WeaponSelection");
        if (weaponSelection != null)
        {
            for (int i = 0; i < weaponSelection.childCount; i++)
            {
                Button button = weaponSelection.GetChild(i).GetComponent<Button>();
                if (button == null)
                {
                    continue;
                }

                button.transition = Selectable.Transition.None;
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                button.interactable = true;
                EditorUtility.SetDirty(button);
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[HallUI] Hall 交互组件已修复并保存");
    }
}
