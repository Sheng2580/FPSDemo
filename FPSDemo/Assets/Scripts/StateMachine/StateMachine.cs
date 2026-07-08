using System;
using System.Collections.Generic;

/// <summary>
/// 通用状态机
/// 
/// 负责：
/// 1. 保存状态对象
/// 2. 切换状态
/// 3. 调用状态 Enter / Exit
/// 4. 把当前状态的 Update / FixedUpdate / LateUpdate 注册到 MonoManager
/// </summary>
public class StateMachine
{
    /// <summary>
    /// 状态机拥有者
    /// 
    /// 比如 GuanLiYuanController、PlayerController 等。
    /// 每个 State 可以通过 owner 访问角色对象。
    /// </summary>
    private IStateMachineOwner owner;

    /// <summary>
    /// 状态缓存字典
    /// 
    /// key：状态类型
    /// value：状态对象
    /// 
    /// 作用：
    /// 同一个状态只创建一次，后面重复使用。
    /// </summary>
    private readonly Dictionary<Type, StateBase> stateDic = new Dictionary<Type, StateBase>();

    /// <summary>
    /// 当前状态
    /// 
    /// 外部只能读取，不能随便修改。
    /// </summary>
    public StateBase CurrentState { get; private set; }

    /// <summary>
    /// 当前状态类型
    /// 
    /// 如果当前没有状态，返回 null。
    /// </summary>
    public Type CurrentStateType => CurrentState?.GetType();

    /// <summary>
    /// 初始化状态机
    /// </summary>
    public void Init(IStateMachineOwner owner)
    {
        this.owner = owner;
    }

    /// <summary>
    /// 切换状态
    /// 
    /// forceReEnter = false：
    /// 如果当前已经是这个状态，就不会重复进入。
    /// 
    /// forceReEnter = true：
    /// 即使当前已经是这个状态，也会重新 Exit 再 Enter。
    /// 
    /// 返回值：
    /// true  = 切换成功
    /// false = 没有切换
    /// </summary>
    public bool ChangeState<T>(bool forceReEnter = false) where T : StateBase, new()
    {
        Type newStateType = typeof(T);

        // 如果不是强制重新进入，并且当前已经是这个状态，就不切换
        if (!forceReEnter && CurrentState != null && CurrentStateType == newStateType)
        {
            return false;
        }

        // 退出当前状态
        ExitCurrentState();

        // 进入新状态
        EnterNewState<T>();

        return true;
    }

    public bool ChangeState(StateBase newState, bool forceReEnter = false)
    {
        if (newState == null)
        {
            return false;
        }

        Type newStateType = newState.GetType();

        if (!forceReEnter && CurrentState != null && CurrentStateType == newStateType)
        {
            return false;
        }

        if (!stateDic.ContainsKey(newStateType))
        {
            newState.Init(owner);
            stateDic.Add(newStateType, newState);
        }

        ExitCurrentState();

        CurrentState = stateDic[newStateType];
        CurrentState.Enter();
        AddStateUpdate(CurrentState);

        return true;
    }

    /// <summary>
    /// 停止状态机
    /// 
    /// callExit = true：
    /// 停止时会调用当前状态的 Exit。
    /// 
    /// callExit = false：
    /// 只移除 Update 监听，不调用 Exit。
    /// </summary>
    public void Stop(bool callExit = true)
    {
        if (CurrentState == null)
        {
            return;
        }

        // 先移除当前状态的 Update 监听
        RemoveStateUpdate(CurrentState);

        // 是否调用 Exit
        if (callExit)
        {
            CurrentState.Exit();
        }

        CurrentState = null;
    }

    /// <summary>
    /// 获取状态
    /// 
    /// 如果字典里已经有这个状态，就直接返回。
    /// 如果没有，就 new 一个，并且初始化。
    /// </summary>
    public T GetState<T>() where T : StateBase, new()
    {
        Type stateType = typeof(T);

        if (!stateDic.TryGetValue(stateType, out StateBase state))
        {
            state = new T();
            state.Init(owner);
            stateDic.Add(stateType, state);
        }

        return state as T;
    }

    /// <summary>
    /// 退出当前状态
    /// </summary>
    private void ExitCurrentState()
    {
        if (CurrentState == null)
        {
            return;
        }

        // 先移除 Update 监听
        // 防止 Exit 里面出现特殊逻辑时，旧状态还继续被更新
        RemoveStateUpdate(CurrentState);

        // 调用状态退出逻辑
        CurrentState.Exit();
    }

    /// <summary>
    /// 进入新状态
    /// </summary>
    private void EnterNewState<T>() where T : StateBase, new()
    {
        // 获取或创建状态对象
        CurrentState = GetState<T>();

        // 调用状态进入逻辑
        CurrentState.Enter();

        // 注册新状态的 Update 监听
        AddStateUpdate(CurrentState);
    }

    /// <summary>
    /// 注册状态的 Update / FixedUpdate / LateUpdate
    /// </summary>
    private void AddStateUpdate(StateBase state)
    {
        if (state == null)
        {
            return;
        }

        MonoManager monoManager = MonoManager.Instance;

        if (monoManager == null)
        {
            return;
        }

        monoManager.AddUpdateListener(state.Update);
        monoManager.AddFixedUpdateListener(state.FixedUpdate);
        monoManager.AddLateUpdateListener(state.LateUpdate);
    }

    /// <summary>
    /// 移除状态的 Update / FixedUpdate / LateUpdate
    /// </summary>
    private void RemoveStateUpdate(StateBase state)
    {
        if (state == null)
        {
            return;
        }

        MonoManager monoManager = MonoManager.Instance;

        if (monoManager == null)
        {
            return;
        }

        monoManager.RemoveUpdateListener(state.Update);
        monoManager.RemoveFixedUpdateListener(state.FixedUpdate);
        monoManager.RemoveLateUpdateListener(state.LateUpdate);
    }
}
