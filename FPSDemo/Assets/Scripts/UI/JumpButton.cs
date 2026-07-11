using UnityEngine;
using UnityEngine.EventSystems;

public class JumpButton : MonoBehaviour, IPointerDownHandler
{
    public void OnPointerDown(PointerEventData eventData)
    {
        // 跳跃按钮只负责发输入事件
        EventCenter.Instance.EventTrigger(GameEvent.MobileJumpPressed);
    }
}
