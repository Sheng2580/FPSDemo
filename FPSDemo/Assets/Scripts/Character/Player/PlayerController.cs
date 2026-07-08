using UnityEngine;

public class PlayerController : CharacterBase<PlayerModel>
{
    [SerializeField] private PlayerModel playerModel;

    [Header("基础移动参数")]
    [SerializeField] private float walkSpeed = 4.5f;
    [SerializeField] private float runSpeed = 6.5f;
    [SerializeField] private float moveInputDeadZone = 0.05f;
    [SerializeField] private float moveAcceleration = 18f;
    [SerializeField] private float moveDeceleration = 24f;

    [Header("跳跃参数")]
    [SerializeField] private float jumpHeight = 1.2f;
    [SerializeField] private float jumpBufferTime = 0.15f;
    [SerializeField] private float coyoteTime = 0.12f;
    [SerializeField] private float airMoveControl = 0.65f;
    [SerializeField] private float jumpEndVerticalVelocity = 0f;

    private PlayerStateType _currentStateType;
    private PlayerStateType _previousStateType;
    private float _jumpBufferTimer;
    private float _coyoteTimer;
    private bool _hasLoggedMissingMotor;

    public PlayerStateType CurrentStateType => _currentStateType;
    public PlayerStateType PreviousStateType => _previousStateType;
    public PlayerCameraController CameraController { get; private set; }
    public PlayerMotor Motor { get; private set; }
    public float WalkSpeed => walkSpeed;
    public float RunSpeed => runSpeed;
    public float MoveInputDeadZone => moveInputDeadZone;
    public float MoveAcceleration => moveAcceleration;
    public float MoveDeceleration => moveDeceleration;
    public float JumpHeight => jumpHeight;
    public float JumpBufferTime => jumpBufferTime;
    public float CoyoteTime => coyoteTime;
    public float AirMoveControl => airMoveControl;
    public float JumpEndVerticalVelocity => jumpEndVerticalVelocity;
    public float Gravity => gravity;
    public Vector3 CurrentHorizontalVelocity { get; private set; }
    public bool HasBufferedJump => _jumpBufferTimer > 0f;
    public bool CanUseCoyoteJump => _coyoteTimer > 0f;

    private void Awake()
    {
        CameraController = GetComponent<PlayerCameraController>();
        Motor = GetComponent<PlayerMotor>();

        if (Motor == null && !_hasLoggedMissingMotor)
        {
            Debug.LogError("PlayerController 缺少 PlayerMotor，请在 Player 根节点上挂载 PlayerMotor。", this);
            _hasLoggedMissingMotor = true;
        }
        else if (Motor != null)
        {
            _hasLoggedMissingMotor = false;
        }
    }

    protected override PlayerModel GetTypedCharacterModel()
    {
        if (playerModel != null)
        {
            return playerModel;
        }

        playerModel = GetComponentInChildren<PlayerModel>();
        return playerModel;
    }

    protected override void InitStateMachine()
    {
        stateMachine = new StateMachine();
        stateMachine.Init(this);
    }

    protected override void Start()
    {
        base.Start();
        ChangeState(PlayerStateType.Idle);
    }

    protected override void Update()
    {
        base.Update();
        UpdateJumpTimers();
    }

    /// <summary>
    /// 切换状态
    /// </summary>
    /// <param name="newStateType">状态枚举</param>
    public void ChangeState(PlayerStateType newStateType)
    {
        if (stateMachine == null)
        {
            return;
        }

        bool changed = ChangeStateByType(newStateType);
        if (!changed)
        {
            return;
        }

        _previousStateType = _currentStateType;
        _currentStateType = newStateType;
    }

    public void SetHorizontalVelocity(Vector3 velocity)
    {
        velocity.y = 0f;
        CurrentHorizontalVelocity = velocity;
    }

    public void StopHorizontalVelocity()
    {
        CurrentHorizontalVelocity = Vector3.zero;
    }

    public void SetVerticalVelocity(float velocity)
    {
        SetPlayerVelocityY(velocity);
    }

    public void ConsumeJumpBuffer()
    {
        _jumpBufferTimer = 0f;
    }

    public void ClearCoyoteTimer()
    {
        _coyoteTimer = 0f;
    }

    private void UpdateJumpTimers()
    {
        if (GameInputManger.Instance != null && GameInputManger.Instance.Jump)
        {
            _jumpBufferTimer = jumpBufferTime;
        }
        else
        {
            _jumpBufferTimer = Mathf.Max(0f, _jumpBufferTimer - Time.deltaTime);
        }

        if (IsGrounded)
        {
            _coyoteTimer = coyoteTime;
        }
        else
        {
            _coyoteTimer = Mathf.Max(0f, _coyoteTimer - Time.deltaTime);
        }
    }

    private bool ChangeStateByType(PlayerStateType stateType)
    {
        switch (stateType)
        {
            case PlayerStateType.Idle:
                return stateMachine.ChangeState<PlayerIlde>();
            case PlayerStateType.Move:
                return stateMachine.ChangeState<PlayerMove>();
            case PlayerStateType.Jump:
                return stateMachine.ChangeState<PlayerJump>();
            default:
                Debug.LogWarning($"未配置玩家状态: {stateType}", this);
                return false;
        }
    }
}
