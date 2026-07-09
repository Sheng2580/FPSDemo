using UnityEngine;
using UnityEngine.EventSystems;

public class SwitchWeaponButton : MonoBehaviour, IPointerDownHandler
{
    [SerializeField] private bool logInput;

    public void OnPointerDown(PointerEventData eventData)
    {
        // 切枪按钮只负责发输入事件
        EventCenter.Instance.EventTrigger(GameEvent.MobileSwitchWeaponPressed);

        if (logInput)
        {
            Debug.Log("移动端切换武器", this);
        }
    }
}
