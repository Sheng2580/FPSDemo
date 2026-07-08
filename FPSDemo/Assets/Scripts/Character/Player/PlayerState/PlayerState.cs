using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//玩家状态基类
public class PlayerState : StateBase
{
   private float _rotationAngle;
   private Transform _mainCamera;
   private float _angleVelocity = 0f;
   protected PlayerController player;

   public override void Init(IStateMachineOwner owner)
   {
      base.Init(owner);
      player=(PlayerController)owner;
      _mainCamera= Camera.main.transform;
   }

   protected Vector2 GetMoveInput()
   {
      if (GameInputManger.Instance == null)
      {
         return Vector2.zero;
      }

      return GameInputManger.Instance.Movement;
   }

   protected bool HasMoveInput()
   {
      Vector2 moveInput = GetMoveInput();
      float deadZone = player.MoveInputDeadZone;
      return moveInput.sqrMagnitude > deadZone * deadZone;
   }

   protected float GetCurrentMoveSpeed()
   {
      if (GameInputManger.Instance != null && GameInputManger.Instance.Run)
      {
         return player.RunSpeed;
      }

      return player.WalkSpeed;
   }

   protected bool ShouldStartJump()
   {
      return player.HasBufferedJump && (player.IsGrounded || player.CanUseCoyoteJump);
   }

   protected void StartJump()
   {
      if (player.Motor == null)
      {
         return;
      }

      player.ConsumeJumpBuffer();
      player.ClearCoyoteTimer();

      float jumpVelocity = Mathf.Sqrt(2f * player.Gravity * player.JumpHeight);
      player.Motor.Jump(jumpVelocity);
   }

   protected void MoldRotate()
   {
      // _rotationAngle=Mathf.Atan2(GameInputManger.Instance.Movement.x,GameInputManger.Instance.Movement.y)*Mathf.Rad2Deg;//转为角度旋转量
      // _rotationAngle+=_mainCamera.eulerAngles.y;
      // player.transform.eulerAngles = Vector3.up *
      //                                Mathf.SmoothDampAngle(player.transform.eulerAngles.y, _rotationAngle,
      //                                   ref _angleVelocity, 0.1f);
      // 原角度计算（可能有循环问题）
      _rotationAngle = Mathf.Atan2(GameInputManger.Instance.Movement.x, GameInputManger.Instance.Movement.y) * Mathf.Rad2Deg;
      _rotationAngle += _mainCamera.eulerAngles.y;

      // 优化：将目标角度修正到 0~360° 范围内，避免循环
      _rotationAngle = Mathf.Repeat(_rotationAngle, 360f);

      // 计算当前角度到目标角度的“最短路径”差值（-180~180°）
      float targetAngle = player.transform.eulerAngles.y + Mathf.DeltaAngle(player.transform.eulerAngles.y, _rotationAngle);

      // 用修正后的 targetAngle 做 SmoothDamp
      player.transform.eulerAngles = Vector3.up *
                                     Mathf.SmoothDampAngle(
                                        player.transform.eulerAngles.y,
                                        targetAngle,  // 用修正后的目标角度
                                        ref _angleVelocity,
                                        0.1f,
                                        Mathf.Infinity,
                                        Time.unscaledDeltaTime
                                     );
   }

   //通过名字判断当前状态 并获得当前状态进行的值
   protected virtual bool CurrAnimationStateName(string stateName , out float normalizedTime ,int layer = 0)
   {
      AnimatorStateInfo nextInfo = player.Model.animator.GetNextAnimatorStateInfo(layer);
      if (nextInfo.IsName(stateName))
      {
         normalizedTime = nextInfo.normalizedTime;
         return true;
      }
      AnimatorStateInfo info =player.Model.animator.GetCurrentAnimatorStateInfo(layer);
      normalizedTime = info.normalizedTime;
      return info.IsName(stateName);
   }

   protected virtual bool CurrAnimationStateTag(string tag, out float normalizedTime)
   {
      AnimatorStateInfo nextInfo = player.Model.animator.GetNextAnimatorStateInfo(0);
      if (nextInfo.IsTag(tag))
      {
         normalizedTime = nextInfo.normalizedTime;
         return true;
      }
      AnimatorStateInfo info = player.Model.animator.GetCurrentAnimatorStateInfo(0);
      normalizedTime = info.normalizedTime;
      return info.IsTag(tag);
   }

   protected virtual void OnRootMotionAction(Vector3 dir, Quaternion rot)
   {
      player.characterController.Move(dir);
   }
}
