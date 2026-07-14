using System;
using UnityEngine;
using PlayerData;

public class PlayerController : CharacterBase<PlayerModel>
{
    [SerializeField] private PlayerModel playerModel;
    [SerializeField] private PlayerDefaultConfigAsset defaultConfigAsset;
    [SerializeField] private PlayerInventory inventory;

    [Header("调试")]
    [Tooltip("玩家受伤日志 默认关闭 避免被围攻时刷屏")]
    [SerializeField] private bool debugDamageLog;

    private PlayerStateType _currentStateType;
    private PlayerStateType _previousStateType;
    private float _jumpBufferTimer;
    private float _coyoteTimer;
    private bool _hasLoggedMissingMotor;
    private bool _isDead;

    public PlayerStateType CurrentStateType => _currentStateType;
    public PlayerStateType PreviousStateType => _previousStateType;
    public PlayerCameraController CameraController { get; private set; }
    public PlayerMotor Motor { get; private set; }
    public PlayerInventory Inventory { get; private set; }
    public PlayerStats Stats { get; private set; }
    public MonoBehaviour SkillController { get; private set; }
    public float Gravity => gravity;
    public Vector3 CurrentHorizontalVelocity { get; private set; }
    public bool HasBufferedJump => _jumpBufferTimer > 0f;
    public bool CanUseCoyoteJump => _coyoteTimer > 0f;
    public bool IsSkillMovementLocked { get; private set; }
    public bool IsDead => _isDead;

    private void Awake()
    {
        InitPlayerStats();

        CameraController = GetComponent<PlayerCameraController>();
        Motor = GetComponent<PlayerMotor>();
        Inventory = inventory != null ? inventory : GetComponent<PlayerInventory>();
        SkillController = ResolveSkillController();

        if (Motor == null && !_hasLoggedMissingMotor)
        {
            Debug.LogError("PlayerController 缺少 PlayerMotor，请在 Player 根节点上挂载 PlayerMotor", this);
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

    private void OnEnable()
    {
        if (stateMachine != null && stateMachine.CurrentState == null)
        {
            ChangeState(PlayerStateType.Idle);
        }
    }

    private void OnDisable()
    {
        // 场景退出时注销全局更新 避免旧玩家状态继续运行
        stateMachine?.Stop(false);
    }

    protected override void Update()
    {
        base.Update();
        UpdateJumpTimers();
        TryStartBufferedJump();
    }

    /// <summary>
    /// 初始化玩家数值层
    /// </summary>
    private void InitPlayerStats()
    {
        PlayerBaseConfig baseConfig = defaultConfigAsset != null && defaultConfigAsset.config != null
            ? defaultConfigAsset.config
            : PlayerDefaultConfigAsset.LoadRuntimeConfig();

        PlayerSaveData saveData = PlayerProgressSaveService.Load();

        Stats = new PlayerStats();
        Stats.Init(baseConfig, saveData);
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

    public void SetSkillMovementLocked(bool isLocked)
    {
        IsSkillMovementLocked = isLocked;
        if (isLocked)
        {
            Motor?.Stop();
        }
    }

    private MonoBehaviour ResolveSkillController()
    {
        MonoBehaviour existingController = GetComponent("PlayerSkillController") as MonoBehaviour;
        if (existingController != null)
        {
            return existingController;
        }

        Type skillControllerType = FindRuntimeType("PlayerSkillController");
        if (skillControllerType == null || !typeof(MonoBehaviour).IsAssignableFrom(skillControllerType))
        {
            Debug.LogWarning("没有找到 PlayerSkillController，技能系统会在脚本导入完成后再挂载", this);
            return null;
        }

        return gameObject.AddComponent(skillControllerType) as MonoBehaviour;
    }

    private static Type FindRuntimeType(string typeName)
    {
        foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(typeName);
            if (type != null)
            {
                return type;
            }
        }

        return null;
    }

    public void TakeDamage(float damage)
    {
        if (_isDead || Stats == null || Stats.RuntimeData == null)
        {
            return;
        }

        int damageAmount = Mathf.CeilToInt(Mathf.Max(0f, damage));
        if (damageAmount <= 0 || Stats.RuntimeData.isInvincible)
        {
            return;
        }

        Stats.RuntimeData.currentHp = Mathf.Max(0, Stats.RuntimeData.currentHp - damageAmount);
        EventCenter.Instance.EventTrigger(
            GameEvent.PlayerDamaged,
            new PlayerDamagedEventData(this, damageAmount, Stats.RuntimeData.currentHp, Stats.RuntimeData.maxHp));
        EventCenter.Instance.EventTrigger(
            GameEvent.PlayerHealthChanged,
            new PlayerHealthChangedEventData(this, Stats.RuntimeData.currentHp, Stats.RuntimeData.maxHp, -damageAmount, 0));
        if (debugDamageLog)
        {
            Debug.Log($"玩家受到伤害 {damageAmount} 当前生命 {Stats.RuntimeData.currentHp}", this);
        }

        if (Stats.RuntimeData.currentHp <= 0)
        {
            _isDead = true;
            EventCenter.Instance.EventTrigger(GameEvent.PlayerDied, new PlayerDiedEventData(this));
        }
    }

    public int Heal(int amount)
    {
        if (_isDead || Stats == null || Stats.RuntimeData == null || amount <= 0)
        {
            return 0;
        }

        int previousHp = Stats.RuntimeData.currentHp;
        Stats.RuntimeData.currentHp = Mathf.Min(Stats.RuntimeData.maxHp, Stats.RuntimeData.currentHp + amount);
        int healedAmount = Stats.RuntimeData.currentHp - previousHp;
        if (healedAmount <= 0)
        {
            return 0;
        }

        EventCenter.Instance.EventTrigger(
            GameEvent.PlayerHealthChanged,
            new PlayerHealthChangedEventData(this, Stats.RuntimeData.currentHp, Stats.RuntimeData.maxHp, healedAmount, 0));
        return healedAmount;
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

    public bool TryStartBufferedJump()
    {
        if (IsSkillMovementLocked)
        {
            return false;
        }

        if (!HasBufferedJump || (!IsGrounded && !CanUseCoyoteJump))
        {
            return false;
        }

        if (Motor == null)
        {
            return false;
        }

        float jumpHeight = Stats != null
            ? Stats.JumpHeight
            : PlayerDefaultConfigAsset.LoadRuntimeConfig().jumpHeight;

        ConsumeJumpBuffer();
        ClearCoyoteTimer();

        // 跳跃速度由高度和重力反推 保证配置的是高度不是随手速度
        float jumpVelocity = Mathf.Sqrt(2f * Gravity * jumpHeight);
        Motor.Jump(jumpVelocity);
        ChangeState(PlayerStateType.Jump);
        return true;
    }

    private void UpdateJumpTimers()
    {
        if (IsSkillMovementLocked)
        {
            _jumpBufferTimer = 0f;
            return;
        }

        if (Stats == null)
        {
            InitPlayerStats();
            if (Stats == null)
            {
                return;
            }
        }

        if (GameInputManger.Instance != null && GameInputManger.Instance.ConsumeJumpInput())
        {
            _jumpBufferTimer = Stats.JumpBufferTime;
        }
        else
        {
            _jumpBufferTimer = Mathf.Max(0f, _jumpBufferTimer - Time.deltaTime);
        }

        if (IsGrounded)
        {
            _coyoteTimer = Stats.CoyoteTime;
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
