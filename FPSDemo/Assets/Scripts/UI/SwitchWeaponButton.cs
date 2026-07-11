using UnityEngine;
using UnityEngine.EventSystems;

public class SwitchWeaponButton : MonoBehaviour, IPointerDownHandler
{
    public void OnPointerDown(PointerEventData eventData)
    {
        // 切枪按钮只负责发输入事件
        EventCenter.Instance.EventTrigger(GameEvent.MobileSwitchWeaponPressed);
    }
}
