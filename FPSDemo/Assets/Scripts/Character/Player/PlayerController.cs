using UnityEngine;

public class PlayerController : CharacterBase<PlayerModel>
{
    [SerializeField] private PlayerModel playerModel;

    private PlayerStateType _currentStateType;
    private PlayerStateType _previousStateType;

    public PlayerStateType CurrentStateType => _currentStateType;
    public PlayerStateType PreviousStateType => _previousStateType;
    
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

    private bool ChangeStateByType(PlayerStateType stateType)
    {
        switch (stateType)
        {
            case PlayerStateType.Idle:
                return stateMachine.ChangeState<PlayerIlde>();
            case PlayerStateType.Move:
                return stateMachine.ChangeState<PlayerMove>();
            default:
                Debug.LogWarning($"未配置玩家状态: {stateType}", this);
                return false;
        }
    }
}
