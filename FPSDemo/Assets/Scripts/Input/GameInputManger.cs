using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class GameInputManger : UnitySingleTon<GameInputManger>
{
   private InputAcyions _gameInputActions;
   private Vector2 _touchMovement;
   private Vector2 _touchLookDelta;
   private bool _isMobileFireHeld;
   private bool _isMobileSightHeld;
   [SerializeField] private float mobileJumpInputBufferTime = 0.18f;
   private int _mobileFirePressedFrame = -1;
   private int _mobileFireReleasedFrame = -1;
   private int _mobileReloadPressedFrame = -1;
   private int _mobileJumpPressedFrame = -1;
   private float _mobileJumpInputExpireTime = -1f;
   private int _mobileSightPressedFrame = -1;
   private int _mobileSightReleasedFrame = -1;
   private int _mobileDodgePressedFrame = -1;
   private int _mobilePushPressedFrame = -1;
   private int _mobileGrenadePressedFrame = -1;
   private bool _isListeningMobileEvents;
   public bool IsPlayerInputEnabled { get; private set; } = true;
   public bool IsMobileMoveLocked { get; private set; }

   //移动
   public Vector2 Movement => GetMovementInput();
   public Vector2 CameraLook => GetCameraLookInput();

   public bool Run => IsPlayerInputEnabled
                      && TryGetGameInput(out InputAcyions.GameInputActions input)
                      && IsPressed(input.Run);
   public bool Climb => TryGetGameInput(out InputAcyions.GameInputActions input) && IsTriggered(input.Climb);

   public bool FireDown => IsPlayerInputEnabled
                           && (DirectFireDown
                               || MobileFirePressed);
   public bool FireHeld => IsPlayerInputEnabled
                           && (DirectFireHeld
                               || MobileFireHeld);
   public bool FireUp => IsPlayerInputEnabled
                         && (DirectFireUp
                             || MobileFireReleased);
   public bool MobileFirePressed => _mobileFirePressedFrame == Time.frameCount;
   public bool MobileFireReleased => _mobileFireReleasedFrame == Time.frameCount;
   public bool MobileFireHeld => _isMobileFireHeld;
   public bool ReloadDown => IsPlayerInputEnabled
                             && MobileReloadPressed;
   public bool MobileReloadPressed => _mobileReloadPressedFrame == Time.frameCount;
   public bool MobileJumpPressed => HasBufferedMobileJumpInput();
   public bool AimDown => IsPlayerInputEnabled
                          && (DirectAimDown
                              || MobileSightPressed);
   public bool AimHeld => IsPlayerInputEnabled
                          && (DirectAimHeld
                              || MobileSightHeld);
   public bool AimUp => IsPlayerInputEnabled
                        && (DirectAimUp
                            || MobileSightReleased);
   public bool MobileSightPressed => _mobileSightPressedFrame == Time.frameCount;
   public bool MobileSightReleased => _mobileSightReleasedFrame == Time.frameCount;
   public bool MobileSightHeld => _isMobileSightHeld;
   private bool DirectFireDown => CanUseDirectFireInput()
                                  && TryGetGameInput(out InputAcyions.GameInputActions input)
                                  && WasPressedThisFrame(input.Fire);
   private bool DirectFireHeld => CanUseDirectFireInput()
                                  && TryGetGameInput(out InputAcyions.GameInputActions input)
                                  && IsPressed(input.Fire);
   private bool DirectFireUp => CanUseDirectFireInput()
                                && TryGetGameInput(out InputAcyions.GameInputActions input)
                                && WasReleasedThisFrame(input.Fire);
   private bool DirectAimDown => CanUseDirectAimInput()
                                 && TryGetGameInput(out InputAcyions.GameInputActions input)
                                 && WasPressedThisFrame(input.Sight);
   private bool DirectAimHeld => CanUseDirectAimInput()
                                 && TryGetGameInput(out InputAcyions.GameInputActions input)
                                 && IsPressed(input.Sight);
   private bool DirectAimUp => CanUseDirectAimInput()
                               && TryGetGameInput(out InputAcyions.GameInputActions input)
                               && WasReleasedThisFrame(input.Sight);

   public bool F => TryGetGameInput(out InputAcyions.GameInputActions input) && IsTriggered(input.F);
   public bool Tab => TryGetGameInput(out InputAcyions.GameInputActions input) && IsTriggered(input.Tab);
   public bool Skill => TryGetGameInput(out InputAcyions.GameInputActions input) && IsTriggered(input.Skill);
   public bool DodgeDown => IsPlayerInputEnabled
                            && ((TryGetGameInput(out InputAcyions.GameInputActions input) && IsTriggered(input.Skill))
                                || MobileDodgePressed);
   public bool PushDown => IsPlayerInputEnabled
                           && MobilePushPressed;
   public bool GrenadeDown => IsPlayerInputEnabled
                              && MobileGrenadePressed;
   public bool MobileDodgePressed => _mobileDodgePressedFrame == Time.frameCount;
   public bool MobilePushPressed => _mobilePushPressedFrame == Time.frameCount;
   public bool MobileGrenadePressed => _mobileGrenadePressedFrame == Time.frameCount;
   public bool Jump => IsPlayerInputEnabled
                       && ((TryGetGameInput(out InputAcyions.GameInputActions input) && IsTriggered(input.Jump))
                           || MobileJumpPressed);
   public bool Slide => TryGetGameInput(out InputAcyions.GameInputActions input) && IsTriggered(input.Slide);
   public bool LockCamera => IsPlayerInputEnabled
                             && TryGetGameInput(out InputAcyions.GameInputActions input)
                             && WasPressedThisFrame(input.lockCamera);

   public bool Esc => TryGetGameInput(out InputAcyions.GameInputActions input) && IsTriggered(input.ESC);
   public bool UseTheCombat => IsPlayerInputEnabled
                               && TryGetGameInput(out InputAcyions.GameInputActions input)
                               && IsTriggered(input.UseTheCombat);

   public override void Awake()
   {
      base.Awake();
      EnsureInputActions();
   }

   private void OnEnable()
   {
      if (EnsureInputActions())
      {
         _gameInputActions.Enable();
      }

      if (_isListeningMobileEvents)
      {
         return;
      }

      EventCenter.Instance.AddEventListener<Vector2>(GameEvent.MobileMoveInputChanged, OnMobileMoveInputChanged);
      EventCenter.Instance.AddEventListener<bool>(GameEvent.MobileMoveLockChanged, OnMobileMoveLockChanged);
      EventCenter.Instance.AddEventListener<Vector2>(GameEvent.MobileLookDeltaChanged, OnMobileLookDeltaChanged);
      EventCenter.Instance.AddEventListener(GameEvent.MobileFirePressed, OnMobileFirePressed);
      EventCenter.Instance.AddEventListener(GameEvent.MobileFireReleased, OnMobileFireReleased);
      EventCenter.Instance.AddEventListener(GameEvent.MobileFireHolding, OnMobileFireHolding);
      EventCenter.Instance.AddEventListener(GameEvent.MobileReloadPressed, OnMobileReloadPressed);
      EventCenter.Instance.AddEventListener(GameEvent.MobileJumpPressed, OnMobileJumpPressed);
      EventCenter.Instance.AddEventListener(GameEvent.MobileSightPressed, OnMobileSightPressed);
      EventCenter.Instance.AddEventListener(GameEvent.MobileSightReleased, OnMobileSightReleased);
      EventCenter.Instance.AddEventListener(GameEvent.MobileSightCanceled, OnMobileSightCanceled);
      EventCenter.Instance.AddEventListener(GameEvent.MobileDodgePressed, OnMobileDodgePressed);
      EventCenter.Instance.AddEventListener(GameEvent.MobilePushPressed, OnMobilePushPressed);
      EventCenter.Instance.AddEventListener(GameEvent.MobileGrenadePressed, OnMobileGrenadePressed);
      _isListeningMobileEvents = true;
   }

   private void OnDisable()
   {
      if (_isListeningMobileEvents)
      {
         EventCenter.Instance.RemoveEventListener<Vector2>(GameEvent.MobileMoveInputChanged, OnMobileMoveInputChanged);
         EventCenter.Instance.RemoveEventListener<bool>(GameEvent.MobileMoveLockChanged, OnMobileMoveLockChanged);
         EventCenter.Instance.RemoveEventListener<Vector2>(GameEvent.MobileLookDeltaChanged, OnMobileLookDeltaChanged);
         EventCenter.Instance.RemoveEventListener(GameEvent.MobileFirePressed, OnMobileFirePressed);
         EventCenter.Instance.RemoveEventListener(GameEvent.MobileFireReleased, OnMobileFireReleased);
         EventCenter.Instance.RemoveEventListener(GameEvent.MobileFireHolding, OnMobileFireHolding);
         EventCenter.Instance.RemoveEventListener(GameEvent.MobileReloadPressed, OnMobileReloadPressed);
         EventCenter.Instance.RemoveEventListener(GameEvent.MobileJumpPressed, OnMobileJumpPressed);
         EventCenter.Instance.RemoveEventListener(GameEvent.MobileSightPressed, OnMobileSightPressed);
         EventCenter.Instance.RemoveEventListener(GameEvent.MobileSightReleased, OnMobileSightReleased);
         EventCenter.Instance.RemoveEventListener(GameEvent.MobileSightCanceled, OnMobileSightCanceled);
         EventCenter.Instance.RemoveEventListener(GameEvent.MobileDodgePressed, OnMobileDodgePressed);
         EventCenter.Instance.RemoveEventListener(GameEvent.MobilePushPressed, OnMobilePushPressed);
         EventCenter.Instance.RemoveEventListener(GameEvent.MobileGrenadePressed, OnMobileGrenadePressed);
         _isListeningMobileEvents = false;
      }

      _gameInputActions?.Disable();
   }

   public void SetPlayerInputEnabled(bool isEnabled)
   {
      IsPlayerInputEnabled = isEnabled;
   }

   public bool ConsumeJumpInput()
   {
      if (!IsPlayerInputEnabled)
      {
         return false;
      }

      if (TryGetGameInput(out InputAcyions.GameInputActions input) && IsTriggered(input.Jump))
      {
         return true;
      }

      if (!HasBufferedMobileJumpInput())
      {
         return false;
      }

      // 手机 UI 事件可能晚于玩家 Update 执行 所以跳跃输入需要消费式缓存
      _mobileJumpInputExpireTime = -1f;
      return true;
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

   private void OnMobileJumpPressed()
   {
      // 记录跳跃按下帧
      _mobileJumpPressedFrame = Time.frameCount;
      _mobileJumpInputExpireTime = Time.unscaledTime + Mathf.Max(0.05f, mobileJumpInputBufferTime);
   }

   private bool HasBufferedMobileJumpInput()
   {
      return _mobileJumpInputExpireTime >= Time.unscaledTime;
   }

   private void OnMobileSightPressed()
   {
      // 记录瞄准按下帧并保持瞄准状态
      _isMobileSightHeld = true;
      _mobileSightPressedFrame = Time.frameCount;
   }

   private void OnMobileSightReleased()
   {
      // 记录瞄准松开帧并退出瞄准状态
      _isMobileSightHeld = false;
      _mobileSightReleasedFrame = Time.frameCount;
   }

   private void OnMobileSightCanceled()
   {
      // 切枪等外部流程会强制退出移动端开镜
      _isMobileSightHeld = false;
      _mobileSightPressedFrame = -1;
      _mobileSightReleasedFrame = Time.frameCount;
   }

   private void OnMobileDodgePressed()
   {
      // 记录闪避按钮按下帧
      _mobileDodgePressedFrame = Time.frameCount;
   }

   private void OnMobilePushPressed()
   {
      // 记录推敌按钮按下帧
      _mobilePushPressedFrame = Time.frameCount;
   }

   private void OnMobileGrenadePressed()
   {
      // 记录手雷按钮按下帧
      _mobileGrenadePressedFrame = Time.frameCount;
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
      // 编辑器和电脑端允许 InputActions 开火，点 UI 不开火
      return !IsPointerOverUI();
#endif
   }

   private bool CanUseDirectLookInput()
   {
#if UNITY_ANDROID || UNITY_IOS
      // 真机只允许右侧 UI 事件触发视角
      return false;
#else
      // 编辑器和电脑端保留 InputActions 视角，点 UI 时不读取
      return !IsPointerOverUI();
#endif
   }

   private bool CanUseDirectAimInput()
   {
#if UNITY_ANDROID || UNITY_IOS
      // 真机只允许 SightButton 通过事件触发瞄准
      return false;
#else
      // 编辑器和电脑端保留 InputActions 瞄准，点 UI 时不读取
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

      Vector2 actionInput = TryGetGameInput(out InputAcyions.GameInputActions input)
         ? ReadVector2(input.Movement)
         : Vector2.zero;
      if (actionInput.sqrMagnitude > 0.0001f)
      {
         return actionInput;
      }

      return Vector2.zero;
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

      Vector2 lookInput = TryGetGameInput(out InputAcyions.GameInputActions input)
         ? ReadVector2(input.CameraLook)
         : Vector2.zero;
      if (lookInput != Vector2.zero)
      {
         return lookInput;
      }

      return Vector2.zero;
   }

   private bool EnsureInputActions()
   {
      // 场景切换或单例自动创建时可能先读输入再走完整 Awake，这里统一兜底
      _gameInputActions ??= new InputAcyions();
      return _gameInputActions != null;
   }

   private bool TryGetGameInput(out InputAcyions.GameInputActions input)
   {
      input = default;
      if (!EnsureInputActions())
      {
         return false;
      }

      input = _gameInputActions.GameInput;
      return true;
   }

#if ENABLE_INPUT_SYSTEM
   private bool IsTriggered(InputAction action)
   {
      return action != null && action.triggered;
   }

   private bool IsPressed(InputAction action)
   {
      return action != null && action.IsPressed();
   }

   private bool WasPressedThisFrame(InputAction action)
   {
      return action != null && action.WasPressedThisFrame();
   }

   private bool WasReleasedThisFrame(InputAction action)
   {
      return action != null && action.WasReleasedThisFrame();
   }

   private Vector2 ReadVector2(InputAction action)
   {
      return action != null ? action.ReadValue<Vector2>() : Vector2.zero;
   }
#else
   private bool IsTriggered(object action)
   {
      return false;
   }

   private bool IsPressed(object action)
   {
      return false;
   }

   private bool WasPressedThisFrame(object action)
   {
      return false;
   }

   private bool WasReleasedThisFrame(object action)
   {
      return false;
   }

   private Vector2 ReadVector2(object action)
   {
      return Vector2.zero;
   }
#endif

}
