using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 拿到玩家的对象 ， 代表玩家
/// </summary>
public interface IStateMachineOwner
{
   
}

//所有状态的基类  有可能是玩家，敌人的
public abstract class StateBase : IStateMachineOwner
{
   //初始化的方法
   public virtual void Init(IStateMachineOwner owner)
   {
      
   }

   
   //反初始化，清楚资源
   public virtual void Unit()
   {
      
   }

   //进入状态触发
   public virtual void Enter()
   {
      
   }

   //退出状态执行
   public virtual void Exit()
   {
      
   }
   
   //自己身上的更新
   public virtual void Update()
   {
      
   }

   public virtual void FixedUpdate()
   {
      
   }

   public virtual void LateUpdate()
   {
      
   }

   public virtual void OnAnimatorIK()
   {
      
   }
}
