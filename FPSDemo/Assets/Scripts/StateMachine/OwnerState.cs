using UnityEngine;

public class OwnerState : StateBase
{
   //使用状态机的人
    protected CharacterBase Owner;
    
    public override void Init(IStateMachineOwner owner)
   {
      base.Init(owner);
      Owner=(CharacterBase)owner;
   }
    
   //通过名字判断当前状态 并获得当前状态进行的值
   protected virtual bool CurrAnimationStateName(string stateName , out float normalizedTime ,int layer = 0)
   {
      normalizedTime = 0f;
      if (!TryGetAnimator(out Animator animator))
      {
         return false;
      }

      AnimatorStateInfo nextInfo = animator.GetNextAnimatorStateInfo(layer);
      if (nextInfo.IsName(stateName))
      {
         normalizedTime = nextInfo.normalizedTime;
         return true;
      }
      AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(layer);
      normalizedTime = info.normalizedTime;
      return info.IsName(stateName);
   }
   
   protected virtual bool CurrAnimationStateName(string stateName ,int layer = 0)
   {
      if (!TryGetAnimator(out Animator animator))
      {
         return false;
      }

      AnimatorStateInfo nextInfo = animator.GetNextAnimatorStateInfo(layer);
      if (nextInfo.IsName(stateName))
      {
         return true;
      }
      AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(layer);
      return info.IsName(stateName);
   }

   protected virtual bool CurrAnimationStateTag(string tag, out float normalizedTime)
   {
      normalizedTime = 0f;
      if (!TryGetAnimator(out Animator animator))
      {
         return false;
      }

      AnimatorStateInfo nextInfo = animator.GetNextAnimatorStateInfo(0);
      if (nextInfo.IsTag(tag))
      {
         normalizedTime = nextInfo.normalizedTime;
         return true;
      }
      AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
      normalizedTime = info.normalizedTime;
      return info.IsTag(tag);
   }

   protected bool TryGetAnimator(out Animator animator)
   {
      animator = null;
      if (Owner == null || Owner.ModelBase == null || Owner.ModelBase.animator == null)
      {
         return false;
      }

      animator = Owner.ModelBase.animator;
      return true;
   }
   
   protected virtual void OnRootMotionAction(Vector3 dir, Quaternion rot)
   {
      Owner.characterController.Move(dir);
   }
    
}
