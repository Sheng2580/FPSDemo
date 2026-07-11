using UnityEngine;
using UnityEngine.EventSystems;

public class ReloadButton : MonoBehaviour, IPointerDownHandler
{
    public void OnPointerDown(PointerEventData eventData)
    {
        // 换弹按钮只负责发输入事件
        EventCenter.Instance.EventTrigger(GameEvent.MobileReloadPressed);
    }
}
