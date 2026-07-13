using System.Collections;
using PlayerData;
using UnityEngine;

public class PlayerCameraController : MonoBehaviour
{
    [Header("引用组件")]
    [SerializeField] private Transform playerRoot;
    [SerializeField] private Transform cameraRoot;
    [SerializeField] private Camera playerCamera;

    [Header("灵敏度参数")]
    [SerializeField] private float mouseSensitivity = 0.12f;
    [SerializeField] private float controllerSensitivity = 120f;

    [Header("俯仰角限制")]
    [SerializeField] private float minPitch = -80f;
    [SerializeField] private float maxPitch = 80f;

    [Header("基础选项")]
    [SerializeField] private bool invertY;
    [SerializeField] private bool lockCursorOnStart = false;

    [Header("开镜FOV参数")]
    [SerializeField] private float aimFovSmoothTime = 0.06f;

    [Header("技能FOV表现")]
    [SerializeField] private float dodgeFovBoost = 9f;
    [SerializeField] private float dodgeFovFadeInTime = 0.045f;
    [SerializeField] private float dodgeFovHoldTime = 0.035f;
    [SerializeField] private float dodgeFovFadeOutTime = 0.18f;

    [Header("技能视差表现")]
    [SerializeField] private float dodgeForwardParallax = 0.055f;
    [SerializeField] private float dodgeSideParallax = 0.035f;
    [SerializeField] private float dodgeDownParallax = 0.025f;

    [Header("后坐力参数")]
    [SerializeField] private Vector2 recoilOffset;
    [SerializeField] private float recoilKickMultiplier = 0.2f;
    [SerializeField] private float recoilFollowSmoothTime = 0.045f;
    [SerializeField] private float recoilTargetReturnSpeed = 2.5f;
    [SerializeField] private float maxRecoilPitchOffset = 8f;
    [SerializeField] private float maxRecoilYawOffset = 4f;

    // 水平旋转角度 作用在玩家根节点上
    private float _yaw;
    // 垂直旋转角度 作用在 CameraRoot 上
    private float _pitch;
    // 避免在缺少引用时每帧重复输出错误日志
    private bool _hasLoggedMissingReferences;
    private float _defaultFov;
    private float _targetAimFov;
    private float _aimFovAmount;
    private float _smoothedAimFovAmount;
    private float _aimFovAmountVelocity;
    private bool _hasCachedDefaultFov;
    private Vector3 _defaultCameraRootLocalPosition;
    private bool _hasCachedDefaultCameraRootPosition;
    private float _skillFovOffset;
    private Vector3 _skillParallaxOffset;
    private Coroutine _skillCameraRoutine;
    private Vector2 _recoilTarget;
    private Vector2 _recoilVelocity;

    public Transform PlayerRoot => playerRoot;
    public Transform CameraRoot => cameraRoot;
    public Camera PlayerCamera => playerCamera;
    public float MouseSensitivity => mouseSensitivity;
    public float ControllerSensitivity => controllerSensitivity;
    public Vector2 RecoilOffset => recoilOffset;

    private void Reset()
    {
        // 组件刚挂载时自动尝试补齐常用引用
        AutoBindReferences();
    }

    private void Awake()
    {
        AutoBindReferences();
        CacheInitialAngles();
        CacheDefaultFov();
        CacheDefaultCameraRootPosition();
    }

    private void OnEnable()
    {
        EventCenter.Instance.AddEventListener<WeaponAimCameraEventData>(GameEvent.WeaponAimCameraChanged, OnWeaponAimCameraChanged);
        EventCenter.Instance.AddEventListener<SkillCastEventData>(GameEvent.SkillCastStarted, OnSkillCastStarted);
    }

    private void OnDisable()
    {
        EventCenter.Instance.RemoveEventListener<WeaponAimCameraEventData>(GameEvent.WeaponAimCameraChanged, OnWeaponAimCameraChanged);
        EventCenter.Instance.RemoveEventListener<SkillCastEventData>(GameEvent.SkillCastStarted, OnSkillCastStarted);
        StopSkillCameraPulse();
    }

    private void OnValidate()
    {
        // 在 Inspector 修改参数时同步修正引用和数值范围
        AutoBindReferences();
        minPitch = Mathf.Min(minPitch, maxPitch);
        maxPitch = Mathf.Max(maxPitch, minPitch);
        aimFovSmoothTime = Mathf.Max(0.001f, aimFovSmoothTime);
        recoilFollowSmoothTime = Mathf.Max(0.001f, recoilFollowSmoothTime);
        recoilKickMultiplier = Mathf.Max(0f, recoilKickMultiplier);
        recoilTargetReturnSpeed = Mathf.Max(0f, recoilTargetReturnSpeed);
        maxRecoilPitchOffset = Mathf.Max(0f, maxRecoilPitchOffset);
        maxRecoilYawOffset = Mathf.Max(0f, maxRecoilYawOffset);
        dodgeFovBoost = Mathf.Max(0f, dodgeFovBoost);
        dodgeFovFadeInTime = Mathf.Max(0.001f, dodgeFovFadeInTime);
        dodgeFovHoldTime = Mathf.Max(0f, dodgeFovHoldTime);
        dodgeFovFadeOutTime = Mathf.Max(0.001f, dodgeFovFadeOutTime);
        dodgeForwardParallax = Mathf.Max(0f, dodgeForwardParallax);
        dodgeSideParallax = Mathf.Max(0f, dodgeSideParallax);
        dodgeDownParallax = Mathf.Max(0f, dodgeDownParallax);
    }

    private void Start()
    {
        AutoBindReferences();
        CacheInitialAngles();
        CacheDefaultFov();
        CacheDefaultCameraRootPosition();
        SetCursorLock(lockCursorOnStart);
    }

    private void LateUpdate()
    {
        if (!HasRequiredReferences())
        {
            return;
        }

        Vector2 lookInput = Vector2.zero;
        if (GameInputManger.Instance != null)
        {
            lookInput = GameInputManger.Instance.CameraLook;
        }

        // 当前先统一按鼠标灵敏度处理输入 后续可再区分鼠标和手柄
        _yaw += lookInput.x * mouseSensitivity;

        // 默认向上移动鼠标时抬头 若勾选反转 Y 则采用相反方向
        float pitchInput = invertY ? lookInput.y : -lookInput.y;
        _pitch = Mathf.Clamp(_pitch + pitchInput * mouseSensitivity, minPitch, maxPitch);

        UpdateRecoil();

        float finalYaw = _yaw + recoilOffset.y;
        float finalPitch = Mathf.Clamp(_pitch + recoilOffset.x, minPitch, maxPitch);

        // 玩家根节点只处理 Y 轴旋转 避免影响角色直立和重力逻辑
        playerRoot.localRotation = Quaternion.Euler(0f, finalYaw, 0f);
        ApplySkillParallax();
        // CameraRoot 只处理 X 轴俯仰 Main Camera 保持作为其子物体
        cameraRoot.localRotation = Quaternion.Euler(finalPitch, 0f, 0f);

        ApplyAimFov();
    }

    public void SetCursorLock(bool isLocked)
    {
        Cursor.lockState = isLocked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !isLocked;
    }

    public void AddRecoil(float pitchAmount, float yawAmount)
    {
        // 参考 Akila 的 AddLookValue 思路 开火直接推真实视角让准星产生爬升
        _pitch = Mathf.Clamp(_pitch + pitchAmount, minPitch, maxPitch);
        _yaw += yawAmount;

        // 额外保留一层短促 Kick 负责开火瞬间的冲击感
        _recoilTarget += new Vector2(pitchAmount, yawAmount) * recoilKickMultiplier;
        ClampRecoilTarget();
    }

    public void ClearRecoil()
    {
        recoilOffset = Vector2.zero;
        _recoilTarget = Vector2.zero;
        _recoilVelocity = Vector2.zero;
    }

    private void UpdateRecoil()
    {
        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0f)
        {
            return;
        }

        // 当前后坐力追随目标后坐力 连发时会慢慢向上飘
        recoilOffset.x = Mathf.SmoothDamp(
            recoilOffset.x,
            _recoilTarget.x,
            ref _recoilVelocity.x,
            recoilFollowSmoothTime,
            Mathf.Infinity,
            deltaTime);

        recoilOffset.y = Mathf.SmoothDamp(
            recoilOffset.y,
            _recoilTarget.y,
            ref _recoilVelocity.y,
            recoilFollowSmoothTime,
            Mathf.Infinity,
            deltaTime);

        // 目标值缓慢恢复 停火后视角不会瞬间弹回
        _recoilTarget = Vector2.MoveTowards(_recoilTarget, Vector2.zero, recoilTargetReturnSpeed * deltaTime);
        ClampRecoilTarget();
    }

    private void ClampRecoilTarget()
    {
        if (maxRecoilPitchOffset > 0f)
        {
            _recoilTarget.x = Mathf.Clamp(_recoilTarget.x, -maxRecoilPitchOffset, maxRecoilPitchOffset);
        }

        if (maxRecoilYawOffset > 0f)
        {
            _recoilTarget.y = Mathf.Clamp(_recoilTarget.y, -maxRecoilYawOffset, maxRecoilYawOffset);
        }
    }

    private void OnWeaponAimCameraChanged(WeaponAimCameraEventData eventData)
    {
        // 相机只消费武器传来的开镜参数
        _aimFovAmount = eventData.aimAmount;

        // 退出开镜时保留旧目标 FOV 等当前 FOV 回到默认后再切换 避免切枪时硬跳
        if (_aimFovAmount > 0.001f || _smoothedAimFovAmount <= 0.001f || _targetAimFov <= 1f)
        {
            _targetAimFov = eventData.targetFov;
        }
    }

    private void OnSkillCastStarted(SkillCastEventData eventData)
    {
        if (eventData.skillType != SkillType.Dodge || string.IsNullOrEmpty(eventData.fovEffectKey))
        {
            return;
        }

        PlayDodgeCameraPulse(eventData.direction);
    }

    private void PlayDodgeCameraPulse(Vector3 worldDirection)
    {
        StopSkillCameraPulse();
        _skillCameraRoutine = StartCoroutine(DodgeCameraPulseRoutine(worldDirection));
    }

    private IEnumerator DodgeCameraPulseRoutine(Vector3 worldDirection)
    {
        Vector3 targetParallax = ResolveDodgeParallax(worldDirection);

        yield return FadeSkillCamera(Vector3.zero, targetParallax, 0f, dodgeFovBoost, dodgeFovFadeInTime);

        float holdTimer = 0f;
        while (holdTimer < dodgeFovHoldTime)
        {
            holdTimer += Time.deltaTime;
            _skillParallaxOffset = targetParallax;
            _skillFovOffset = dodgeFovBoost;
            yield return null;
        }

        yield return FadeSkillCamera(targetParallax, Vector3.zero, dodgeFovBoost, 0f, dodgeFovFadeOutTime);
        _skillCameraRoutine = null;
    }

    private IEnumerator FadeSkillCamera(
        Vector3 fromParallax,
        Vector3 toParallax,
        float fromFov,
        float toFov,
        float duration)
    {
        duration = Mathf.Max(0.001f, duration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Smooth01(elapsed / duration);
            _skillParallaxOffset = Vector3.Lerp(fromParallax, toParallax, t);
            _skillFovOffset = Mathf.Lerp(fromFov, toFov, t);
            yield return null;
        }

        _skillParallaxOffset = toParallax;
        _skillFovOffset = toFov;
    }

    private Vector3 ResolveDodgeParallax(Vector3 worldDirection)
    {
        Vector3 safeDirection = worldDirection.sqrMagnitude > 0.0001f ? worldDirection.normalized : Vector3.forward;
        Vector3 localDirection = playerRoot != null
            ? playerRoot.InverseTransformDirection(safeDirection)
            : safeDirection;

        return new Vector3(
            -localDirection.x * dodgeSideParallax,
            -dodgeDownParallax,
            -Mathf.Max(0.25f, Mathf.Abs(localDirection.z)) * dodgeForwardParallax);
    }

    private void StopSkillCameraPulse()
    {
        if (_skillCameraRoutine != null)
        {
            StopCoroutine(_skillCameraRoutine);
            _skillCameraRoutine = null;
        }

        _skillFovOffset = 0f;
        _skillParallaxOffset = Vector3.zero;
        ApplySkillParallax();
    }

    private void AutoBindReferences()
    {
        // 玩家根节点默认使用脚本所在物体
        playerRoot ??= transform;

        if (cameraRoot == null)
        {
            cameraRoot = transform.Find("CameraRoot");
        }

        if (cameraRoot == null)
        {
            cameraRoot = FindChildRecursive(transform, "CameraRoot");
        }

        Camera[] childCameras = null;

        if (cameraRoot == null)
        {
            childCameras = transform.GetComponentsInChildren<Camera>(true);
            if (childCameras.Length == 1 && childCameras[0] != null)
            {
                cameraRoot = childCameras[0].transform.parent;
            }
        }

        if (cameraRoot == null)
        {
            Camera mainCameraInChildren = FindMainCameraInChildren(childCameras);
            if (mainCameraInChildren == null)
            {
                childCameras ??= transform.GetComponentsInChildren<Camera>(true);
                mainCameraInChildren = FindMainCameraInChildren(childCameras);
            }

            if (mainCameraInChildren != null)
            {
                cameraRoot = mainCameraInChildren.transform.parent;
            }
        }

        if (playerCamera == null && cameraRoot != null)
        {
            playerCamera = cameraRoot.GetComponentInChildren<Camera>(true);
        }

        if (playerCamera == null)
        {
            playerCamera = transform.GetComponentInChildren<Camera>(true);
        }

        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        if (cameraRoot == null && playerCamera != null && playerCamera.transform.IsChildOf(transform))
        {
            cameraRoot = playerCamera.transform.parent;
        }
    }

    private void CacheDefaultCameraRootPosition()
    {
        if (cameraRoot == null || _hasCachedDefaultCameraRootPosition)
        {
            return;
        }

        _defaultCameraRootLocalPosition = cameraRoot.localPosition;
        _hasCachedDefaultCameraRootPosition = true;
    }

    private void ApplySkillParallax()
    {
        if (cameraRoot == null)
        {
            return;
        }

        CacheDefaultCameraRootPosition();
        cameraRoot.localPosition = _defaultCameraRootLocalPosition + _skillParallaxOffset;
    }

    private void CacheInitialAngles()
    {
        if (playerRoot != null)
        {
            _yaw = playerRoot.localEulerAngles.y;
        }

        if (cameraRoot != null)
        {
            // Unity 的 Euler 角可能落在 0 到 360 需要先转成 -180 到 180 再做俯仰限制
            _pitch = NormalizePitch(cameraRoot.localEulerAngles.x);
            _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
        }
    }

    private void CacheDefaultFov()
    {
        if (playerCamera == null || _hasCachedDefaultFov)
        {
            return;
        }

        // 默认 FOV 只能缓存一次 开镜后不能被当前 FOV 覆盖
        _defaultFov = playerCamera.fieldOfView;
        _hasCachedDefaultFov = true;
        if (_targetAimFov <= 1f)
        {
            _targetAimFov = _defaultFov;
        }
    }

    private void ApplyAimFov()
    {
        if (playerCamera == null)
        {
            return;
        }

        CacheDefaultFov();
        _smoothedAimFovAmount = Mathf.SmoothDamp(
            _smoothedAimFovAmount,
            _aimFovAmount,
            ref _aimFovAmountVelocity,
            aimFovSmoothTime);

        if (Mathf.Abs(_smoothedAimFovAmount - _aimFovAmount) <= 0.001f)
        {
            _smoothedAimFovAmount = _aimFovAmount;
            _aimFovAmountVelocity = 0f;
        }

        playerCamera.fieldOfView = Mathf.Max(1f, Mathf.Lerp(_defaultFov, _targetAimFov, _smoothedAimFovAmount) + _skillFovOffset);
    }

    private static float Smooth01(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }

    private bool HasRequiredReferences()
    {
        AutoBindReferences();

        if (playerRoot != null && cameraRoot != null && playerCamera != null)
        {
            _hasLoggedMissingReferences = false;
            return true;
        }

        if (!_hasLoggedMissingReferences)
        {
            if (cameraRoot == null)
            {
                Debug.LogError("PlayerCameraController 缺少 CameraRoot，请在 Player 下创建 CameraRoot 并赋值。", this);
            }
            else if (playerCamera == null)
            {
                Debug.LogError("PlayerCameraController 缺少 Camera，请在 CameraRoot 下挂载 Camera 或手动赋值。", this);
            }
            else if (playerRoot == null)
            {
                Debug.LogError("PlayerCameraController 缺少 PlayerRoot，请检查组件挂载位置。", this);
            }

            _hasLoggedMissingReferences = true;
        }

        return false;
    }

    private static float NormalizePitch(float pitch)
    {
        if (pitch > 180f)
        {
            pitch -= 360f;
        }

        return pitch;
    }

    private static Camera FindMainCameraInChildren(Camera[] cameras)
    {
        if (cameras == null)
        {
            return null;
        }

        foreach (Camera childCamera in cameras)
        {
            if (childCamera != null && childCamera.CompareTag("MainCamera"))
            {
                return childCamera;
            }
        }

        return null;
    }

    private static Transform FindChildRecursive(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        foreach (Transform child in root)
        {
            if (child.name == childName)
            {
                return child;
            }

            Transform foundChild = FindChildRecursive(child, childName);
            if (foundChild != null)
            {
                return foundChild;
            }
        }

        return null;
    }
}
