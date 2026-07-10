using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.Events;

public class IEventInfoMY
{
    public virtual bool HasPayload => false;
    public virtual Type PayloadType => null;
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
    public override bool HasPayload => true;
    public override Type PayloadType => typeof(T);

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
        if (eventDict.TryGetValue(sGameEvent, out IEventInfoMY eventInfo))
        {
            if (eventInfo is MyEventInfoMy myEventInfo)
            {
                myEventInfo.actions += action;
                return;
            }

            ReplaceMismatchedEvent(sGameEvent, eventInfo, null);
        }

        eventDict[sGameEvent] = new MyEventInfoMy(action);
    }

    public void AddEventListener<T>(GameEvent sGameEvent, UnityAction<T> action)
    {
        if (eventDict.TryGetValue(sGameEvent, out IEventInfoMY eventInfo))
        {
            if (eventInfo is EventInfoMy<T> typedEventInfo)
            {
                typedEventInfo.actions += action;
                return;
            }

            ReplaceMismatchedEvent(sGameEvent, eventInfo, typeof(T));
        }

        eventDict[sGameEvent] = new EventInfoMy<T>(action);
    }

    /// <summary>
    /// 移除事件监听  
    /// </summary>
    /// <param name="eventName"></param>
    /// <param name="action"></param>
    public void RemoveEventListener(GameEvent sGameEvent, UnityAction action)
    {
        if (eventDict.TryGetValue(sGameEvent, out IEventInfoMY eventInfo) && eventInfo is MyEventInfoMy myEventInfo)
        {
            myEventInfo.actions -= action;
        }
    }

    public void RemoveEventListener<T>(GameEvent sGameEvent, UnityAction<T> action)
    {
        if (eventDict.TryGetValue(sGameEvent, out IEventInfoMY eventInfo) && eventInfo is EventInfoMy<T> typedEventInfo)
        {
            typedEventInfo.actions -= action;
        }
    }
/// <summary>
/// 事件触发 
/// </summary>
/// <param name="eventName"></param>
    public void EventTrigger(GameEvent sGameEvent)
    {
        if (eventDict.TryGetValue(sGameEvent, out IEventInfoMY eventInfo))
        {
            if (eventInfo is MyEventInfoMy myEventInfo)
            {
                myEventInfo.actions?.Invoke();
                return;
            }

            LogMismatchedEvent(sGameEvent, eventInfo, null);
        }
        
    }
public void EventTrigger<T>(GameEvent sGameEvent,T info)
    {
        if (eventDict.TryGetValue(sGameEvent, out IEventInfoMY eventInfo))
        {
            if (eventInfo is EventInfoMy<T> typedEventInfo)
            {
                typedEventInfo.actions?.Invoke(info);
                return;
            }

            LogMismatchedEvent(sGameEvent, eventInfo, typeof(T));
        }
        
    }
    
    /// <summary>
    /// 清空字典 
    /// </summary>
    public void Clear()
    {
        eventDict.Clear();
    }

    private void ReplaceMismatchedEvent(GameEvent sGameEvent, IEventInfoMY eventInfo, Type newPayloadType)
    {
        Debug.LogWarning($"[EventCenter] 事件 {sGameEvent} 监听参数类型不一致 已清理旧监听 Old={GetPayloadName(eventInfo)} New={GetPayloadName(newPayloadType)}");
    }

    private void LogMismatchedEvent(GameEvent sGameEvent, IEventInfoMY eventInfo, Type triggerPayloadType)
    {
        Debug.LogError($"[EventCenter] 事件 {sGameEvent} 触发参数类型不一致 Listener={GetPayloadName(eventInfo)} Trigger={GetPayloadName(triggerPayloadType)}");
    }

    private string GetPayloadName(IEventInfoMY eventInfo)
    {
        return eventInfo != null && eventInfo.HasPayload ? GetPayloadName(eventInfo.PayloadType) : "None";
    }

    private string GetPayloadName(Type payloadType)
    {
        return payloadType != null ? payloadType.Name : "None";
    }
    
    
    
}
