using UnityEngine;

namespace Enemy
{
    /// <summary>
    /// 敌人表现入口
    /// 当前只负责复位模型和暴露 Animator
    /// 动画播放统一交给 EnemyStateMachine
    /// </summary>
    public class EnemyView : EnemyModel
    {
        public Animator Animator => animator;

        public void ResetView()
        {
            ResetModel();
        }
    }
}
