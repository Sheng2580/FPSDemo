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

    [Header("后坐力参数")]
    [SerializeField] private Vector2 recoilOffset;
    [SerializeField] private float recoilReturnSpeed = 8f;

    // 水平旋转角度，作用在玩家根节点上。
    private float _yaw;
    // 垂直旋转角度，作用在 CameraRoot 上。
    private float _pitch;
    // 避免在缺少引用时每帧重复输出错误日志。
    private bool _hasLoggedMissingReferences;

    public Transform PlayerRoot => playerRoot;
    public Transform CameraRoot => cameraRoot;
    public Camera PlayerCamera => playerCamera;
    public float MouseSensitivity => mouseSensitivity;
    public float ControllerSensitivity => controllerSensitivity;
    public Vector2 RecoilOffset => recoilOffset;

    private void Reset()
    {
        // 组件刚挂载时自动尝试补齐常用引用。
        AutoBindReferences();
    }

    private void Awake()
    {
        AutoBindReferences();
        CacheInitialAngles();
    }

    private void OnValidate()
    {
        // 在 Inspector 修改参数时同步修正引用和数值范围。
        AutoBindReferences();
        minPitch = Mathf.Min(minPitch, maxPitch);
        maxPitch = Mathf.Max(maxPitch, minPitch);
        recoilReturnSpeed = Mathf.Max(0f, recoilReturnSpeed);
    }

    private void Start()
    {
        AutoBindReferences();
        CacheInitialAngles();
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

        // 当前先统一按鼠标灵敏度处理输入，后续可再区分鼠标和手柄。
        _yaw += lookInput.x * mouseSensitivity;

        // 默认向上移动鼠标时抬头；若勾选反转 Y，则采用相反方向。
        float pitchInput = invertY ? lookInput.y : -lookInput.y;
        _pitch = Mathf.Clamp(_pitch + pitchInput * mouseSensitivity, minPitch, maxPitch);

        // 后坐力偏移会缓慢回正，为后续武器系统预留接口。
        recoilOffset = Vector2.Lerp(recoilOffset, Vector2.zero, recoilReturnSpeed * Time.deltaTime);

        float finalYaw = _yaw + recoilOffset.y;
        float finalPitch = Mathf.Clamp(_pitch + recoilOffset.x, minPitch, maxPitch);

        // 玩家根节点只处理 Y 轴旋转，避免影响角色直立和重力逻辑。
        playerRoot.localRotation = Quaternion.Euler(0f, finalYaw, 0f);
        // CameraRoot 只处理 X 轴俯仰，Main Camera 保持作为其子物体。
        cameraRoot.localRotation = Quaternion.Euler(finalPitch, 0f, 0f);
    }

    public void SetCursorLock(bool isLocked)
    {
        Cursor.lockState = isLocked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !isLocked;
    }

    public void AddRecoil(float pitchAmount, float yawAmount)
    {
        recoilOffset += new Vector2(pitchAmount, yawAmount);
    }

    public void ClearRecoil()
    {
        recoilOffset = Vector2.zero;
    }

    private void AutoBindReferences()
    {
        // 玩家根节点默认使用脚本所在物体。
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

    private void CacheInitialAngles()
    {
        if (playerRoot != null)
        {
            _yaw = playerRoot.localEulerAngles.y;
        }

        if (cameraRoot != null)
        {
            // Unity 的 Euler 角可能落在 0~360，需要先转成 -180~180 再做俯仰限制。
            _pitch = NormalizePitch(cameraRoot.localEulerAngles.x);
            _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
        }
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
