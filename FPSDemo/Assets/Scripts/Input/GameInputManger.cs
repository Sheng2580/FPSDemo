using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;

public class GameInputManger : UnitySingleTon<GameInputManger>
{
   private InputAcyions _gameInputActions;
   private Vector2 _touchMovement;
   private Vector2 _touchLookDelta;
   private bool _isMobileFireHeld;
   private int _mobileFirePressedFrame = -1;
   private int _mobileFireReleasedFrame = -1;
   private int _mobileReloadPressedFrame = -1;
   public bool IsPlayerInputEnabled { get; private set; } = true;
   public bool IsMobileMoveLocked { get; private set; }

   //移动
   public Vector2 Movement => GetMovementInput();
   public Vector2 CameraLook => GetCameraLookInput();
   
   public bool Run => _gameInputActions.GameInput.Run.IsPressed();
   public bool Climb => _gameInputActions.GameInput.Climb.triggered;
   
   public bool FireDown => IsPlayerInputEnabled
                           && (DirectFireDown
                               || MobileFirePressed);
   public bool FireHeld => IsPlayerInputEnabled
                           && (DirectFireHeld
                               || MobileFireHeld);
   public bool FireUp => IsPlayerInputEnabled
                         && (DirectFireUp
                             || MobileFireReleased);
   public bool RAttack => _gameInputActions.GameInput.RAttack.triggered;
   public bool MobileFirePressed => _mobileFirePressedFrame == Time.frameCount;
   public bool MobileFireReleased => _mobileFireReleasedFrame == Time.frameCount;
   public bool MobileFireHeld => _isMobileFireHeld;
   public bool ReloadDown => IsPlayerInputEnabled
                             && (UnityEngine.Input.GetKeyDown(KeyCode.R)
                                 || MobileReloadPressed);
   public bool MobileReloadPressed => _mobileReloadPressedFrame == Time.frameCount;
   private bool DirectFireDown => CanUseDirectFireInput()
                                  && (_gameInputActions.GameInput.Fire.WasPressedThisFrame()
                                      || UnityEngine.Input.GetMouseButtonDown(0));
   private bool DirectFireHeld => CanUseDirectFireInput()
                                  && (_gameInputActions.GameInput.Fire.IsPressed()
                                      || UnityEngine.Input.GetMouseButton(0));
   private bool DirectFireUp => CanUseDirectFireInput()
                                && (_gameInputActions.GameInput.Fire.WasReleasedThisFrame()
                                    || UnityEngine.Input.GetMouseButtonUp(0));

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
      EventCenter.Instance.AddEventListener<Vector2>(GameEvent.MobileMoveInputChanged, OnMobileMoveInputChanged);
      EventCenter.Instance.AddEventListener<bool>(GameEvent.MobileMoveLockChanged, OnMobileMoveLockChanged);
      EventCenter.Instance.AddEventListener<Vector2>(GameEvent.MobileLookDeltaChanged, OnMobileLookDeltaChanged);
      EventCenter.Instance.AddEventListener(GameEvent.MobileFirePressed, OnMobileFirePressed);
      EventCenter.Instance.AddEventListener(GameEvent.MobileFireReleased, OnMobileFireReleased);
      EventCenter.Instance.AddEventListener(GameEvent.MobileFireHolding, OnMobileFireHolding);
      EventCenter.Instance.AddEventListener(GameEvent.MobileReloadPressed, OnMobileReloadPressed);
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
      EventCenter.Instance.RemoveEventListener<Vector2>(GameEvent.MobileMoveInputChanged, OnMobileMoveInputChanged);
      EventCenter.Instance.RemoveEventListener<bool>(GameEvent.MobileMoveLockChanged, OnMobileMoveLockChanged);
      EventCenter.Instance.RemoveEventListener<Vector2>(GameEvent.MobileLookDeltaChanged, OnMobileLookDeltaChanged);
      EventCenter.Instance.RemoveEventListener(GameEvent.MobileFirePressed, OnMobileFirePressed);
      EventCenter.Instance.RemoveEventListener(GameEvent.MobileFireReleased, OnMobileFireReleased);
      EventCenter.Instance.RemoveEventListener(GameEvent.MobileFireHolding, OnMobileFireHolding);
      EventCenter.Instance.RemoveEventListener(GameEvent.MobileReloadPressed, OnMobileReloadPressed);
      _gameInputActions.Disable();
   }

   public void SetPlayerInputEnabled(bool isEnabled)
   {
      IsPlayerInputEnabled = isEnabled;
   }

   public void SetTouchMovement(Vector2 movement)
   {
      _touchMovement = Vector2.ClampMagnitude(movement, 1f);
   }

   private void OnMobileMoveInputChanged(Vector2 movement)
   {
      SetTouchMovement(movement);
   }

   private void OnMobileMoveLockChanged(bool isLocked)
   {
      IsMobileMoveLocked = isLocked;

      if (isLocked)
      {
         SetTouchMovement(Vector2.up);
      }
      else
      {
         SetTouchMovement(Vector2.zero);
      }
   }

   private void OnMobileLookDeltaChanged(Vector2 lookDelta)
   {
      // 右侧滑动和开火拖动都会累加到同一帧视角输入
      _touchLookDelta += lookDelta;
   }

   private void OnMobileFirePressed()
   {
      // 记录按下帧给单发武器使用
      _isMobileFireHeld = true;
      _mobileFirePressedFrame = Time.frameCount;
   }

   private void OnMobileFireReleased()
   {
      // 记录松开帧给后续开火表现使用
      _isMobileFireHeld = false;
      _mobileFireReleasedFrame = Time.frameCount;
   }

   private void OnMobileFireHolding()
   {
      // 长按期间保持开火状态
      _isMobileFireHeld = true;
   }

   private void OnMobileReloadPressed()
   {
      // 记录换弹按下帧
      _mobileReloadPressedFrame = Time.frameCount;
   }

   private bool IsPointerOverUI()
   {
      return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
   }

   private bool CanUseDirectFireInput()
   {
#if UNITY_ANDROID || UNITY_IOS
      // 真机只允许 FireButton 通过事件触发开火
      return false;
#else
      // 编辑器和电脑端允许鼠标空白处开火，点 UI 不开火
      return !IsPointerOverUI();
#endif
   }

   private bool CanUseDirectLookInput()
   {
#if UNITY_ANDROID || UNITY_IOS
      // 真机只允许右侧 UI 事件触发视角
      return false;
#else
      // 编辑器和电脑端保留鼠标看向，点 UI 时不读取直连输入
      return !IsPointerOverUI();
#endif
   }

   private Vector2 GetMovementInput()
   {
      if (!IsPlayerInputEnabled)
      {
         return Vector2.zero;
      }

      if (_touchMovement.sqrMagnitude > 0.0001f)
      {
         return _touchMovement;
      }

      return _gameInputActions.GameInput.Movement.ReadValue<Vector2>();
   }

   private Vector2 GetCameraLookInput()
   {
      if (!IsPlayerInputEnabled)
      {
         return Vector2.zero;
      }

      if (_touchLookDelta.sqrMagnitude > 0.0001f)
      {
         Vector2 lookDelta = _touchLookDelta;
         _touchLookDelta = Vector2.zero;
         return lookDelta;
      }

      if (!CanUseDirectLookInput())
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
