using PlayerData;
using UnityEngine;
using UnityEngine.UI;

[UICanvas(UILoadType.AssetBundle, UILayer.Touch)]
public class TounchControllerCanvas : BaseCanvas
{
    [Header("技能按钮")]
    [SerializeField] private bool ensureSkillButtons = true;
    [SerializeField] private Vector2 skillButtonSize = new Vector2(86f, 86f);
    [SerializeField] private Vector2 dodgeButtonPosition = new Vector2(40f, 30f);
    [SerializeField] private Vector2 pushButtonPosition = new Vector2(145f, -30f);
    [SerializeField] private Vector2 grenadeButtonPosition = new Vector2(40f, -90f);
    [SerializeField] private Color skillButtonColor = new Color(1f, 1f, 1f, 0.82f);
    [SerializeField] private Color skillLabelColor = new Color(1f, 1f, 1f, 0.92f);

    private AkilaCrosshairDriver _crosshairDriver;
    private RectTransform _rightActionGroup;
    private MoveJoystick _moveJoystick;
    private FireAimStick _fireAimStick;
    private RightLookArea _rightLookArea;
    private RectTransform _moveTouchArea;
    private RectTransform _fireButtonRect;
    private RectTransform _rightLookAreaRect;

    public override void Awake()
    {
        base.Awake();
        EnsureTouchRuntimeLayout();
        EnsureAkilaCrosshair();
        EnsureSkillButtons();
    }

    private void OnEnable()
    {
        EnsureTouchRuntimeLayout();
        EnsureAkilaCrosshair();
        EnsureSkillButtons();
    }

    private void EnsureTouchRuntimeLayout()
    {
        // 大面积触控区只负责接收空白区域输入
        // 按钮和开火圆盘必须排在它上面才能收到点击
        RectTransform rightLookArea = transform.Find("RightLookArea") as RectTransform;
        if (rightLookArea != null)
        {
            _rightLookAreaRect = rightLookArea;
            _rightLookArea ??= rightLookArea.GetComponent<RightLookArea>();
            rightLookArea.SetAsFirstSibling();
            SetImageRaycastTarget(rightLookArea, true);
        }

        RectTransform moveJoystick = transform.Find("MoveJoystick") as RectTransform;
        if (moveJoystick != null)
        {
            _moveJoystick ??= moveJoystick.GetComponent<MoveJoystick>();
            _moveTouchArea = moveJoystick.Find("TouchArea") as RectTransform;
            SetImageRaycastTarget(_moveTouchArea, true);
        }

        RectTransform fireRange = transform.Find("FireRange") as RectTransform;
        if (fireRange != null)
        {
            fireRange.SetAsLastSibling();
            _fireAimStick ??= fireRange.GetComponentInChildren<FireAimStick>(true);
            _fireButtonRect = fireRange.Find("FireButton") as RectTransform;
            if (_fireButtonRect == null && _fireAimStick != null)
            {
                _fireButtonRect = _fireAimStick.transform as RectTransform;
            }

            SetImageRaycastTarget(_fireButtonRect, true);
        }

        _rightActionGroup ??= transform.Find("RightActionGroup") as RectTransform;
        if (_rightActionGroup != null)
        {
            _rightActionGroup.SetAsLastSibling();
            EnsureActionButtonRaycast("ReloadButton");
            EnsureActionButtonRaycast("JumpButton");
            EnsureActionButtonRaycast("SightButton");
            EnsureActionButtonRaycast("SwitchWeaponButton");
            EnsureActionButtonRaycast("DodgeButton");
            EnsureActionButtonRaycast("PushButton");
            EnsureActionButtonRaycast("GrenadeButton");
        }
    }

    private void EnsureActionButtonRaycast(string buttonName)
    {
        if (_rightActionGroup == null)
        {
            return;
        }

        SetImageRaycastTarget(_rightActionGroup.Find(buttonName) as RectTransform, true);
    }

    private void SetImageRaycastTarget(RectTransform rect, bool raycastTarget)
    {
        if (rect == null)
        {
            return;
        }

        Image image = rect.GetComponent<Image>();
        if (image != null)
        {
            image.raycastTarget = raycastTarget;
        }
    }

    private void EnsureAkilaCrosshair()
    {
        _crosshairDriver ??= GetComponentInChildren<AkilaCrosshairDriver>(true);
        if (_crosshairDriver == null)
        {
            _crosshairDriver = gameObject.AddComponent<AkilaCrosshairDriver>();
        }

        _crosshairDriver.EnsureBuilt();
    }

    private void EnsureSkillButtons()
    {
        if (!ensureSkillButtons)
        {
            return;
        }

        _rightActionGroup ??= transform.Find("RightActionGroup") as RectTransform;
        if (_rightActionGroup == null)
        {
            return;
        }

        Sprite buttonSprite = FindReferenceButtonSprite();
        CreateOrUpdateSkillButton("DodgeButton", SkillType.Dodge, "闪", dodgeButtonPosition, buttonSprite);
        CreateOrUpdateSkillButton("PushButton", SkillType.Push, "推", pushButtonPosition, buttonSprite);
        CreateOrUpdateSkillButton("GrenadeButton", SkillType.Grenade, "雷", grenadeButtonPosition, buttonSprite);
    }

    private void CreateOrUpdateSkillButton(
        string objectName,
        SkillType skillType,
        string labelText,
        Vector2 anchoredPosition,
        Sprite buttonSprite)
    {
        RectTransform buttonRect = FindOrCreateChildRect(_rightActionGroup, objectName);
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = anchoredPosition;
        buttonRect.sizeDelta = skillButtonSize;
        buttonRect.localScale = Vector3.one;
        buttonRect.localRotation = Quaternion.identity;

        Image buttonImage = buttonRect.GetComponent<Image>();
        if (buttonImage == null)
        {
            buttonImage = buttonRect.gameObject.AddComponent<Image>();
        }

        buttonImage.sprite = buttonSprite;
        buttonImage.color = skillButtonColor;
        buttonImage.raycastTarget = true;

        Text label = EnsureSkillLabel(buttonRect, labelText);
        MobileSkillButton skillButton = buttonRect.GetComponent<MobileSkillButton>();
        if (skillButton == null)
        {
            skillButton = buttonRect.gameObject.AddComponent<MobileSkillButton>();
        }

        skillButton.Configure(skillType, labelText, buttonImage, label);
    }

    private Text EnsureSkillLabel(RectTransform buttonRect, string labelText)
    {
        RectTransform labelRect = buttonRect.Find("Label") as RectTransform;
        if (labelRect == null)
        {
            GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelRect = labelObject.transform as RectTransform;
            labelRect.SetParent(buttonRect, false);
        }

        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        labelRect.localScale = Vector3.one;
        labelRect.localRotation = Quaternion.identity;

        Text label = labelRect.GetComponent<Text>();
        label.text = labelText;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = skillLabelColor;
        label.fontSize = 32;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = 18;
        label.resizeTextMaxSize = 34;
        label.raycastTarget = false;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return label;
    }

    private RectTransform FindOrCreateChildRect(RectTransform parent, string objectName)
    {
        Transform child = parent.Find(objectName);
        RectTransform rect = child as RectTransform;
        if (rect != null)
        {
            return rect;
        }

        GameObject obj = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        rect = obj.transform as RectTransform;
        rect.SetParent(parent, false);
        obj.layer = parent.gameObject.layer;
        return rect;
    }

    private Sprite FindReferenceButtonSprite()
    {
        Image reloadImage = transform.Find("RightActionGroup/ReloadButton")?.GetComponent<Image>();
        if (reloadImage != null && reloadImage.sprite != null)
        {
            return reloadImage.sprite;
        }

        Image jumpImage = transform.Find("RightActionGroup/JumpButton")?.GetComponent<Image>();
        return jumpImage != null ? jumpImage.sprite : null;
    }
}
