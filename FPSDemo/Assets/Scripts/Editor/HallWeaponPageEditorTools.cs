using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class HallWeaponPageEditorTools
{
    private const string HallScenePath = "Assets/Scenes/Hall.unity";
    private const string HallFontPath = "Assets/Art/Font/AaFengKuangYuanShiRen-2.ttf";

    [MenuItem("FPSDemo/UI/整理Hall武器页", priority = 91)]
    public static void ConfigureHallWeaponPage()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != HallScenePath)
        {
            EditorSceneManager.OpenScene(HallScenePath);
        }

        GameObject canvas = GameObject.Find("UpgradeCanvas");
        if (canvas == null)
        {
            Debug.LogError("[HallWeaponPage] 找不到 UpgradeCanvas");
            return;
        }

        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        if (canvasRect != null)
        {
            SetFullStretch(canvasRect);
        }

        RectTransform weaponPanel = FindDescendantRect(canvas.transform, "WeapponUPPanel");
        if (weaponPanel == null)
        {
            Debug.LogError("[HallWeaponPage] 找不到 WeapponUPPanel");
            return;
        }

        RectTransform list = FindDirectChildRect(weaponPanel, "WeaponUP");
        RectTransform detail = FindDirectChildRect(weaponPanel, "Image (1)");

        if (list == null || detail == null)
        {
            Debug.LogError("[HallWeaponPage] 武器页缺少左侧列表或右侧详情");
            return;
        }

        SetFullPanel(weaponPanel);
        ConfigureList(list);
        ConfigureDetail(detail);

        EditorUtility.SetDirty(canvas);
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        Debug.Log("[HallWeaponPage] Hall 武器页已整理并保存");
    }

    private static void ConfigureList(RectTransform list)
    {
        SetAnchor(list, new Vector2(0.04f, 0.12f), new Vector2(0.29f, 0.92f), new Vector2(0.5f, 0.5f));
        list.offsetMin = Vector2.zero;
        list.offsetMax = Vector2.zero;
        ClearChildren(list);
        RemoveLayoutComponents(list.gameObject);

        Image image = EnsureImage(list.gameObject);
        image.color = new Color(1f, 1f, 1f, 0.95f);
        image.raycastTarget = true;

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

        Font font = LoadHallFont();
        string[] names = { "手枪", "步枪", "霰弹枪" };

        for (int i = 0; i < names.Length; i++)
        {
            RectTransform item = CreateUiObject($"UpgradeWeaponItem_{i}", list, typeof(Image), typeof(Button), typeof(LayoutElement));
            Image itemImage = item.GetComponent<Image>();
            itemImage.color = i == 0 ? new Color(1f, 0.78f, 0.78f, 0.85f) : new Color(1f, 1f, 1f, 0.65f);
            itemImage.raycastTarget = true;

            Button button = item.GetComponent<Button>();
            button.targetGraphic = itemImage;
            button.transition = Selectable.Transition.ColorTint;
            button.interactable = true;

            LayoutElement layoutElement = item.GetComponent<LayoutElement>();
            layoutElement.minHeight = 116f;
            layoutElement.preferredHeight = 132f;
            layoutElement.flexibleHeight = 0f;

            Text text = CreateText("Name", item, names[i], 46, TextAnchor.MiddleCenter, new Color(0.18f, 0.18f, 0.18f, 1f), font);
            SetFullStretch(text.rectTransform);
        }
    }

    private static void ConfigureDetail(RectTransform detail)
    {
        SetAnchor(detail, new Vector2(0.32f, 0.12f), new Vector2(0.96f, 0.92f), new Vector2(0.5f, 0.5f));
        detail.offsetMin = Vector2.zero;
        detail.offsetMax = Vector2.zero;
        ClearChildren(detail);
        RemoveLayoutComponents(detail.gameObject);

        Image detailImage = EnsureImage(detail.gameObject);
        detailImage.color = new Color(0.35f, 0.35f, 0.35f, 1f);
        detailImage.raycastTarget = true;

        RectTransform card = CreateUiObject("WeaponDetailCard", detail, typeof(Image));
        SetAnchor(card, new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.92f), new Vector2(0.5f, 0.5f));
        card.offsetMin = Vector2.zero;
        card.offsetMax = Vector2.zero;

        Image cardImage = card.GetComponent<Image>();
        cardImage.color = Color.white;
        cardImage.raycastTarget = false;

        Font font = LoadHallFont();
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
        Text text = CreateText(name, parent, value, fontSize, alignment, color, font);
        SetAnchor(text.rectTransform, anchorMin, anchorMax, new Vector2(0.5f, 0.5f));
        text.rectTransform.offsetMin = Vector2.zero;
        text.rectTransform.offsetMax = Vector2.zero;
    }

    private static Text CreateText(string name, RectTransform parent, string value, int fontSize, TextAnchor alignment, Color color, Font font)
    {
        RectTransform rect = CreateUiObject(name, parent, typeof(Text));
        Text text = rect.GetComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    private static void SetFullPanel(RectTransform rect)
    {
        if (rect == null)
        {
            return;
        }

        SetAnchor(rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
        rect.offsetMin = new Vector2(96f, 64f);
        rect.offsetMax = new Vector2(-96f, -168f);
    }

    private static void SetFullStretch(RectTransform rect)
    {
        SetAnchor(rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static RectTransform CreateUiObject(string name, RectTransform parent, params Type[] components)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        gameObject.transform.SetParent(parent, false);
        gameObject.layer = parent.gameObject.layer;

        for (int i = 0; i < components.Length; i++)
        {
            if (gameObject.GetComponent(components[i]) == null)
            {
                gameObject.AddComponent(components[i]);
            }
        }

        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.localScale = Vector3.one;
        return rect;
    }

    private static RectTransform FindDirectChildRect(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        return child != null ? child as RectTransform : null;
    }

    private static RectTransform FindDescendantRect(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        Transform child = root.Find(childName);
        if (child != null)
        {
            return child as RectTransform;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            RectTransform result = FindDescendantRect(root.GetChild(i), childName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private static void SetAnchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 pivot)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.pivot = pivot;
        ForceLocalScaleOne(rect);
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

    private static Image EnsureImage(GameObject target)
    {
        Image image = target.GetComponent<Image>();
        if (image == null)
        {
            image = target.AddComponent<Image>();
        }

        return image;
    }

    private static Font LoadHallFont()
    {
        Font font = AssetDatabase.LoadAssetAtPath<Font>(HallFontPath);
        return font != null ? font : Font.CreateDynamicFontFromOSFont("Arial", 32);
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
}
