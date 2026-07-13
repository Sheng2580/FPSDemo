using PlayerData;
using UnityEngine;
using UnityEngine.UI;

[UICanvas(UILoadType.AssetBundle, UILayer.Touch)]
public class TounchControllerCanvas : BaseCanvas
{
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
        BindSkillButtonsFromPrefab();
    }

    private void OnEnable()
    {
        EnsureTouchRuntimeLayout();
        EnsureAkilaCrosshair();
        BindSkillButtonsFromPrefab();
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

    private void BindSkillButtonsFromPrefab()
    {
        _rightActionGroup ??= transform.Find("RightActionGroup") as RectTransform;
        if (_rightActionGroup == null)
        {
            return;
        }

        BindSkillButton("DodgeButton", SkillType.Dodge, "闪");
        BindSkillButton("PushButton", SkillType.Push, "推");
        BindSkillButton("GrenadeButton", SkillType.Grenade, "雷");
    }

    private void BindSkillButton(string objectName, SkillType skillType, string labelText)
    {
        // 技能按钮必须在预制体里配置，脚本只绑定输入和状态刷新
        RectTransform buttonRect = _rightActionGroup.Find(objectName) as RectTransform;
        if (buttonRect == null)
        {
            Debug.LogWarning($"技能按钮 {objectName} 没有在预制体里配置", this);
            return;
        }

        Image buttonImage = buttonRect.GetComponent<Image>();
        SetImageRaycastTarget(buttonRect, true);
        Text label = buttonRect.GetComponentInChildren<Text>(true);
        MobileSkillButton skillButton = buttonRect.GetComponent<MobileSkillButton>();
        if (skillButton == null)
        {
            Debug.LogWarning($"技能按钮 {objectName} 缺少 MobileSkillButton", this);
            return;
        }

        skillButton.Configure(skillType, labelText, buttonImage, label);
    }
}
