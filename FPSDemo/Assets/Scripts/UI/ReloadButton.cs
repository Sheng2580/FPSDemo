using UnityEngine;
using UnityEngine.EventSystems;

public class ReloadButton : MonoBehaviour, IPointerDownHandler
{
    [SerializeField] private bool logInput;

    public void OnPointerDown(PointerEventData eventData)
    {
        // 换弹按钮只负责发输入事件
        EventCenter.Instance.EventTrigger(GameEvent.MobileReloadPressed);

        if (logInput)
        {
            Debug.Log("移动端换弹按下", this);
        }
    }
}
