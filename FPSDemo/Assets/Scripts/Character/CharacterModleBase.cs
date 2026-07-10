using System;
using UnityEngine;

public class CharacterModleBase : MonoBehaviour
{
    [Header("动画组件")]
    public Animator animator;

    #region 生命周期

    protected virtual void Awake()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }
    }

    #endregion


    #region 动画根运动 RootMotion

    private Action<Vector3, Quaternion> RootMotionAction;

    /// <summary>
    /// 设置根运动回调
    /// 
    /// 作用：
    /// Animator 产生的 deltaPosition 和 deltaRotation
    /// 不直接由模型自己使用，而是交给角色控制器处理。
    /// </summary>
    public void SetRootMotionAction(Action<Vector3, Quaternion> rootMotionAction)
    {
        this.RootMotionAction = rootMotionAction;
    }

    /// <summary>
    /// 清除根运动回调
    /// </summary>
    public void ClearRootMotionAction()
    {
        this.RootMotionAction = null;
    }

    /// <summary>
    /// Unity 自动调用的动画根运动函数
    /// 
    /// 注意：
    /// 只有 Animator 开启 Apply Root Motion 时，
    /// 这个函数才会正常参与根运动逻辑。
    /// </summary>
    protected virtual void OnAnimatorMove()
    {
        if (animator == null || RootMotionAction == null)
        {
            return;
        }

        RootMotionAction.Invoke(animator.deltaPosition, animator.deltaRotation);
    }

    #endregion
}
