using UnityEngine;
using UnityEngine.EventSystems;

public class JumpButton : MonoBehaviour, IPointerDownHandler
{
    [SerializeField] private bool logInput;

    public void OnPointerDown(PointerEventData eventData)
    {
        // 跳跃按钮只负责发输入事件
        EventCenter.Instance.EventTrigger(GameEvent.MobileJumpPressed);

        if (logInput)
        {
            Debug.Log("移动端跳跃按下", this);
        }
    }
}
