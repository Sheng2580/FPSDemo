using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class IEventInfoMY
{
}

public class MyEventInfoMy : IEventInfoMY
{
    public UnityAction actions;

    public MyEventInfoMy(UnityAction action)
    {
        actions += action;
    }
}

public class EventInfoMy<T> : IEventInfoMY
{
    public UnityAction<T> actions;

    public EventInfoMy(UnityAction<T> action)
    {
        actions += action;
    }
}


public class EventCenter : SingleTon<EventCenter>
{
    //事件字典 
    public Dictionary<GameEvent, IEventInfoMY> eventDict = new Dictionary<GameEvent, IEventInfoMY>();

    /// <summary>
    /// 添加事件监听
    /// </summary>
    /// <param name="eventName">事件名字</param>
    /// <param name="action">要执行的方法</param>
    public void AddEventListener(GameEvent sGameEvent, UnityAction action)
    {
        if (eventDict.ContainsKey(sGameEvent))
        {
            (eventDict[sGameEvent] as MyEventInfoMy).actions += action;
        }
        else
        {
            eventDict.Add(sGameEvent, new MyEventInfoMy(action));
        }
    }

    public void AddEventListener<T>(GameEvent sGameEvent, UnityAction<T> action)
    {
        if (eventDict.ContainsKey(sGameEvent))
        {
            (eventDict[sGameEvent] as EventInfoMy<T>).actions += action;
        }
        else
        {
            eventDict.Add(sGameEvent, new EventInfoMy<T>(action));
        }
    }

    /// <summary>
    /// 移除事件监听  
    /// </summary>
    /// <param name="eventName"></param>
    /// <param name="action"></param>
    public void RemoveEventListener(GameEvent sGameEvent, UnityAction action)
    {
        if (eventDict.ContainsKey(sGameEvent))
        {
            (eventDict[sGameEvent] as MyEventInfoMy).actions -= action;
        }
    }

    public void RemoveEventListener<T>(GameEvent sGameEvent, UnityAction<T> action)
    {
        if (eventDict.ContainsKey(sGameEvent))
        {
            (eventDict[sGameEvent] as EventInfoMy<T>).actions -= action;
        }
    }
/// <summary>
/// 事件触发 
/// </summary>
/// <param name="eventName"></param>
    public void EventTrigger(GameEvent sGameEvent)
    {
        if (eventDict.ContainsKey(sGameEvent))
        {
            (eventDict[sGameEvent] as MyEventInfoMy).actions?.Invoke();  
        }
        
    }
public void EventTrigger<T>(GameEvent sGameEvent,T info)
    {
        if (eventDict.ContainsKey(sGameEvent))
        {
            (eventDict[sGameEvent] as EventInfoMy<T>).actions?.Invoke(info);  
        }
        
    }
    
    /// <summary>
    /// 清空字典 
    /// </summary>
    public void Clear()
    {
        eventDict.Clear();
    }
    
    
    
}