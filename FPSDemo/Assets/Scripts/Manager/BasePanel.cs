

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
public class BasePanel : MonoBehaviour
{
    protected bool isShow = false;
    protected Button closeBtn;

    // 仅绑定关闭按钮，不修改任何UI布局
    public virtual void Awake()
    {
        Transform closeBtnTrans = transform.Find("CloseBtn");
        if (closeBtnTrans != null)
        {
            closeBtn = closeBtnTrans.GetComponent<Button>();
            if (closeBtn != null) 
                closeBtn.onClick.AddListener(Hide);
        }
    }

    // 仅显示，不改布局
    public virtual void Show()
    {
        if (isShow) return;
        gameObject.SetActive(true);
        isShow = true;
    }

    // 仅隐藏，不改布局
    public virtual void Hide()
    {
        if (!isShow) return;
        gameObject.SetActive(false);
        isShow = false;
    }

    public bool IsShow() => isShow;
}
