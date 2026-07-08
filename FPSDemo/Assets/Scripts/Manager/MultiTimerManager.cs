using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

// 单个计时器的状态
public enum TimerState
{
    Running,   // 运行中
    Paused,    // 已暂停
    Stopped    // 已停止（未运行）
}

// 单个计时器的封装类
public class Timer
{
    public string Id { get; private set; }               // 计时器唯一ID
    public float CurrentTime { get; private set; }        // 当前累计时间（秒）
    public float TargetTime { get; private set; }         // 目标时间（-1表示无目标）
    public TimerState State { get; private set; }         // 当前状态
    public bool UseRealtime { get; private set; }         // 是否使用实时时间

    // 事件：每帧更新时触发（传递当前时间）
    public event Action<float> OnUpdate;
    // 事件：达到目标时间时触发
    public event Action OnTimeUp;

    public Timer(string id, bool useRealtime = false)
    {
        Id = id;
        UseRealtime = useRealtime;
        CurrentTime = 0f;
        TargetTime = -1f;
        State = TimerState.Stopped;
    }

    // 每帧更新（由管理器调用）
    internal void Update()
    {
        if (State != TimerState.Running) return;

        // 累加时间（根据是否实时选择时间增量）
        float deltaTime = UseRealtime ? Time.unscaledDeltaTime : Time.deltaTime;
        CurrentTime += deltaTime;

        // 触发更新事件
        OnUpdate?.Invoke(CurrentTime);

        // 检查是否达到目标时间
        if (TargetTime > 0 && CurrentTime >= TargetTime)
        {
            Stop();
            OnTimeUp?.Invoke();
        }
    }

    // 开始计时（可选择是否重置当前时间）
    public void Start(bool reset = true)
    {
        if (reset)
        {
            CurrentTime = 0f;
        }
        State = TimerState.Running;
    }

    // 暂停计时
    public void Pause()
    {
        if (State == TimerState.Running)
        {
            State = TimerState.Paused;
        }
    }

    // 继续计时（从暂停状态恢复）
    public void Resume()
    {
        if (State == TimerState.Paused)
        {
            State = TimerState.Running;
        }
    }

    // 停止计时（不重置时间）
    public void Stop()
    {
        State = TimerState.Stopped;
    }

    // 重置计时器（时间归零，状态设为停止）
    public void Reset()
    {
        CurrentTime = 0f;
        State = TimerState.Stopped;
    }

    // 设置目标时间
    public void SetTargetTime(float target)
    {
        if (target < 0)
        {
            Debug.LogWarning($"计时器[{Id}]：目标时间不能为负数");
            return;
        }
        TargetTime = target;
    }

    // 获取格式化时间（分:秒.毫秒）
    public string GetFormattedTime()
    {
        int minutes = Mathf.FloorToInt(CurrentTime / 60);
        float secondsRaw = CurrentTime % 60;
        int seconds = Mathf.FloorToInt(secondsRaw);
        int milliseconds = Mathf.FloorToInt((secondsRaw - seconds) * 1000);

        return $"{minutes:D2}:{seconds:D2}.{milliseconds:D3}";
    }

    // 清除当前计时器的所有事件监听
    public void ClearEvents()
    {
        OnUpdate = null;
        OnTimeUp = null;
    }
}

// 多计时器管理器（继承单例基类）
public class MultiTimerManager : UnitySingleTonMono<MultiTimerManager>
{
    // 存储所有计时器（key：计时器ID，value：计时器实例）
    private Dictionary<string, Timer> timers = new Dictionary<string, Timer>();

    private void Update()
    {
        // 关键修复：将 Values 转为数组副本，避免遍历中修改集合导致异常
        foreach (var timer in timers.Values.ToArray()) 
        {
            timer.Update();
        }
    }

    /// <summary>
    /// 创建一个新的计时器（如果ID已存在则覆盖）
    /// </summary>
    /// <param name="timerId">计时器唯一ID</param>
    /// <param name="useRealtime">是否使用实时时间（不受Time.timeScale影响）</param>
    /// <returns>创建的计时器实例</returns>
    public Timer CreateTimer(string timerId, bool useRealtime = true)
    {
        // 如果ID已存在，先移除旧的
        if (timers.ContainsKey(timerId))
        {
            RemoveTimer(timerId);
        }

        Timer newTimer = new Timer(timerId, useRealtime);
        timers.Add(timerId, newTimer);
        return newTimer;
    }

    /// <summary>
    /// 获取指定ID的计时器
    /// </summary>
    /// <param name="timerId">计时器ID</param>
    /// <returns>计时器实例（不存在则返回null）</returns>
    public Timer GetTimer(string timerId)
    {
        if (timers.TryGetValue(timerId, out Timer timer))
        {
            return timer;
        }
        Debug.LogWarning($"计时器[{timerId}]不存在");
        return null;
    }

    /// <summary>
    /// 移除指定ID的计时器
    /// </summary>
    /// <param name="timerId">计时器ID</param>
    public void RemoveTimer(string timerId)
    {
        if (timers.TryGetValue(timerId, out Timer timer))
        {
            timer.ClearEvents(); // 清除事件监听，避免内存泄漏
            timers.Remove(timerId);
        }
        else
        {
            Debug.LogWarning($"移除失败：计时器[{timerId}]不存在");
        }
    }

    /// <summary>
    /// 移除所有计时器
    /// </summary>
    public void RemoveAllTimers()
    {
        foreach (var timer in timers.Values)
        {
            timer.ClearEvents();
        }
        timers.Clear();
    }
    
    /// <summary>
    /// 添加一次性计时器（延迟指定时间后执行一次函数，自动删除）
    /// </summary>
    /// <param name="delay">延迟时间（秒）</param>
    /// <param name="callback">计时结束后执行的函数</param>
    /// <param name="useRealtime">是否使用实时时间（不受Time.timeScale影响）</param>
    public void AddOneShotTimer(float delay, Action callback, bool useRealtime = true)
    {
        if (delay < 0)
        {
            Debug.LogWarning("延迟时间不能为负数");
            return;
        }
        if (callback == null)
        {
            Debug.LogWarning("回调函数不能为空");
            return;
        }

        // 生成唯一ID（避免冲突）
        string tempId = $"OneShot_{Guid.NewGuid().ToString().Substring(0, 8)}";

        // 创建临时计时器
        Timer tempTimer = CreateTimer(tempId, useRealtime);
        // 设置目标时间（延迟时间）
        tempTimer.SetTargetTime(delay);
        // 绑定结束事件：执行回调后自动删除计时器
        tempTimer.OnTimeUp += () =>
        {
            callback.Invoke(); // 执行用户传入的函数
            RemoveTimer(tempId); // 自动删除当前计时器
        };
        // 立即启动计时器（从0开始计时）
        tempTimer.Start();
    }
    
}

