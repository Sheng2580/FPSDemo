using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PlayerData;

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
      if (player == null || player.Stats == null)
      {
         float defaultDeadZone = PlayerBaseConfig.CreateDefault().moveInputDeadZone;
         return GetMoveInput().sqrMagnitude > defaultDeadZone * defaultDeadZone;
      }

      Vector2 moveInput = GetMoveInput();
      float deadZone = player.Stats.MoveInputDeadZone;
      return moveInput.sqrMagnitude > deadZone * deadZone;
   }

   protected float GetCurrentMoveSpeed()
   {
      if (player == null || player.Stats == null)
      {
         PlayerBaseConfig defaultConfig = PlayerBaseConfig.CreateDefault();
         if (GameInputManger.Instance != null && GameInputManger.Instance.Run)
         {
            return defaultConfig.runSpeed;
         }

         return defaultConfig.walkSpeed;
      }

      if (GameInputManger.Instance != null && GameInputManger.Instance.Run)
      {
         return player.Stats.RunSpeed;
      }

      return player.Stats.WalkSpeed;
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

      float jumpHeight = player.Stats != null
         ? player.Stats.JumpHeight
         : PlayerBaseConfig.CreateDefault().jumpHeight;

      player.ConsumeJumpBuffer();
      player.ClearCoyoteTimer();

      float jumpVelocity = Mathf.Sqrt(2f * player.Gravity * jumpHeight);
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

   protected virtual void OnRootMotionAction(Vector3 dir, Quaternion rot)
   {
      player.characterController.Move(dir);
   }
}
