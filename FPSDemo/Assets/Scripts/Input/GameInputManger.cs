using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class GameInputManger : UnitySingleTon<GameInputManger>
{
   private InputAcyions _gameInputActions;
   public bool IsPlayerInputEnabled { get; private set; } = true;

   //移动
   public Vector2 Movement => IsPlayerInputEnabled ? _gameInputActions.GameInput.Movement.ReadValue<Vector2>() : Vector2.zero;
   public Vector2 CameraLook => GetCameraLookInput();
   
   public bool Run => _gameInputActions.GameInput.Run.IsPressed();
   public bool Climb => _gameInputActions.GameInput.Climb.triggered;
   
   public bool LAttack => _gameInputActions.GameInput.LAttack.triggered || UnityEngine.Input.GetMouseButtonDown(0);
   public bool RAttack => _gameInputActions.GameInput.RAttack.triggered;

   public bool F => _gameInputActions.GameInput.F.triggered;
   public bool Tab => _gameInputActions.GameInput.Tab.triggered;
   public bool Skill => _gameInputActions.GameInput.Skill.triggered;
   public bool Jump => _gameInputActions.GameInput.Jump.triggered;
   public bool Slide => _gameInputActions.GameInput.Slide.triggered;
   public bool LockCamera => IsPlayerInputEnabled && _gameInputActions.GameInput.lockCamera.WasPressedThisFrame();
   
   public bool Esc => _gameInputActions.GameInput.ESC.triggered;
   public bool UseTheCombat => IsPlayerInputEnabled && _gameInputActions.GameInput.UseTheCombat.triggered;
   
   public override void Awake()
   {
      base.Awake();
      _gameInputActions ??= new InputAcyions();
   }
   
   private void OnEnable()
   {
      _gameInputActions.Enable();
   }

   private void Update()
   {
      if (LockCamera)
      {
         print("ssss");
      }
   }

   private void OnDisable()
   {
      _gameInputActions.Disable();
   }

   public void SetPlayerInputEnabled(bool isEnabled)
   {
      IsPlayerInputEnabled = isEnabled;
   }

   private Vector2 GetCameraLookInput()
   {
      if (!IsPlayerInputEnabled)
      {
         return Vector2.zero;
      }

      Vector2 lookInput = _gameInputActions.GameInput.CameraLook.ReadValue<Vector2>();
      if (lookInput != Vector2.zero)
      {
         return lookInput;
      }

      // 编辑器兜底：Mac / Game 视图焦点异常时，新 Input System 的 Pointer.delta 可能读不到
      // 旧输入只作为鼠标测试兜底，后续手机虚拟摇杆仍然走 Input System
      return new Vector2(UnityEngine.Input.GetAxis("Mouse X"), UnityEngine.Input.GetAxis("Mouse Y")) * 10f;
   }
}
