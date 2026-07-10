using System.Collections.Generic;
using Combat;
using Enemy;
using UnityEngine;
using Weapon.Data;
using Weapon.State;

namespace Weapon
{
    public class WeaponController : MonoBehaviour
    {
        [Header("武器引用")]
        [SerializeField] private WeaponView currentWeaponView;
        [SerializeField] private PlayerCameraController cameraController;
        [SerializeField] private PlayerInventory playerInventory;

        [Header("武器数据")]
        [SerializeField] private WeaponConfigAsset configAsset;
        [SerializeField] private WeaponConfig fallbackConfig = WeaponConfig.CreateDefaultPistol();

        [Header("调试")]
        [SerializeField] private bool debugWeaponHit = true;

        private readonly Dictionary<WeaponStateType, WeaponState> _states = new Dictionary<WeaponStateType, WeaponState>();
        private WeaponState _currentState;
        private WeaponStateType _currentStateType;
        private Camera _fireCamera;
        private bool _fireInput;
        private bool _reloadInput;
        private bool _aimInput;
        private float _adsAmount;
        private WeaponConfig _config;
        private CarriedWeaponSlot _currentWeaponSlot;
        private bool _hasStarted;
        private bool _waitForAimReleaseAfterSwitch;

        public WeaponView CurrentWeaponView => currentWeaponView;
        public CarriedWeaponSlot CurrentWeaponSlot => _currentWeaponSlot;
        public PlayerCameraController CameraController => cameraController;
        public WeaponConfig Config => _config;
        public WeaponRuntimeData RuntimeData { get; private set; }
        public WeaponStateType CurrentStateType => _currentStateType;
        public bool FireInput => _fireInput;
        public bool ReloadInput => _reloadInput;
        public bool AimInput => _aimInput;

        private void Reset()
        {
            CacheReferences();
        }

        private void Awake()
        {
            CacheReferences();
            InitStateMachine();
            InitCurrentWeapon();
        }

        private void OnEnable()
        {
            EventCenter.Instance.AddEventListener<PlayerWeaponChangedEventData>(GameEvent.PlayerWeaponChanged, OnPlayerWeaponChanged);
        }

        private void Start()
        {
            _hasStarted = true;

            if (currentWeaponView != null && _config != null && RuntimeData != null)
            {
                ChangeState(WeaponStateType.Equip, true);
            }
        }

        private void Update()
        {
            UpdateInput();
            _currentState?.Update();
            UpdateAim();
        }

        private void OnDisable()
        {
            EventCenter.Instance.RemoveEventListener<PlayerWeaponChangedEventData>(GameEvent.PlayerWeaponChanged, OnPlayerWeaponChanged);
            ResetAimVisuals();
        }

        public void ChangeState(WeaponStateType stateType, bool forceReEnter = false)
        {
            if (!forceReEnter && _currentState != null && _currentStateType == stateType)
            {
                return;
            }

            if (!_states.TryGetValue(stateType, out WeaponState newState))
            {
                Debug.LogWarning($"未配置武器状态: {stateType}", this);
                return;
            }

            _currentState?.Exit();
            _currentState = newState;
            _currentStateType = stateType;
            _currentState.Enter();
        }

        public bool CanFire()
        {
            return currentWeaponView != null
                   && _config != null
                   && RuntimeData != null
                   && RuntimeData.isEquipped
                   && !RuntimeData.isReloading
                   && RuntimeData.currentAmmoInMagazine > 0
                   && Time.time >= RuntimeData.nextFireTime;
        }

        public bool CanReload()
        {
            return currentWeaponView != null
                   && _config != null
                   && RuntimeData != null
                   && RuntimeData.isEquipped
                   && !RuntimeData.isReloading
                   && RuntimeData.currentAmmoInMagazine < _config.magazineSize
                   && RuntimeData.currentReserveAmmo > 0;
        }

        public void ApplyRecoil()
        {
            if (cameraController == null || _config == null)
            {
                return;
            }

            float yaw = Random.Range(-_config.recoilYaw, _config.recoilYaw);
            cameraController.AddRecoil(_config.recoilPitch, yaw);
        }

        public void FireRaycast()
        {
            Camera raycastCamera = _fireCamera;
            if (raycastCamera == null || _config == null)
            {
                return;
            }

            Ray ray = new Ray(raycastCamera.transform.position, raycastCamera.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, _config.range, ~0, QueryTriggerInteraction.Collide))
            {
                DamageInfo damageInfo = new DamageInfo(
                    _config.damage,
                    _config.weaponId,
                    _config.weaponName,
                    gameObject,
                    hit.collider,
                    hit.point,
                    hit.normal);

                bool hitEnemy = TryApplyEnemyDamage(hit.collider, ref damageInfo);

                EventCenter.Instance.EventTrigger(GameEvent.WeaponHit, new WeaponHitEventData(damageInfo, hitEnemy));

                if (debugWeaponHit && !hitEnemy)
                {
                    Debug.Log($"[WeaponHit] 命中非敌人 Collider={hit.collider.name}", hit.collider);
                }
            }
        }

        private bool TryApplyEnemyDamage(Collider hitCollider, ref DamageInfo damageInfo)
        {
            if (hitCollider == null)
            {
                return false;
            }

            EnemyHitBox hitBox = hitCollider.GetComponent<EnemyHitBox>();
            if (hitBox != null)
            {
                bool applied = hitBox.TryApplyDamage(ref damageInfo);
                if (debugWeaponHit)
                {
                    Debug.Log(
                        $"[WeaponHit] 命中敌人部位 Collider={hitCollider.name} Part={damageInfo.hitPart} Damage={damageInfo.finalDamage:0.##} Critical={damageInfo.isCritical}",
                        hitCollider);
                }

                return applied;
            }

            EnemyHealth enemyHealth = hitCollider.GetComponentInParent<EnemyHealth>();
            enemyHealth ??= hitCollider.GetComponentInChildren<EnemyHealth>();
            if (enemyHealth == null)
            {
                return false;
            }

            damageInfo.ApplyBodyPart(EnemyHitBodyPart.Body, 1f, false);
            enemyHealth.TakeDamage(damageInfo);

            if (debugWeaponHit)
            {
                Debug.Log(
                    $"[WeaponHit] 命中敌人根物体 Collider={hitCollider.name} 按 Body 兜底伤害 Damage={damageInfo.finalDamage:0.##}",
                    hitCollider);
            }

            return true;
        }

        private void CacheReferences()
        {
            cameraController ??= GetComponent<PlayerCameraController>();
            playerInventory ??= GetComponent<PlayerInventory>();
            currentWeaponView ??= GetComponentInChildren<WeaponView>(true);

            if (cameraController != null)
            {
                _fireCamera = cameraController.PlayerCamera;
            }

            if (_fireCamera == null)
            {
                _fireCamera = GetComponentInChildren<Camera>(true);
            }
        }

        private void InitCurrentWeapon()
        {
            if (TryEquipInventoryCurrentWeapon(false))
            {
                return;
            }

            InitDefaultConfig();
            InitRuntimeData();
            InitWeaponView();
        }

        private void InitDefaultConfig()
        {
            _config = CreateRuntimeConfig();
        }

        private WeaponConfig CreateRuntimeConfig()
        {
            // 优先读取武器数据资产 每把枪的开镜 FOV 都从这里来
            if (configAsset != null)
            {
                WeaponConfig assetConfig = configAsset.CreateRuntimeConfig();
                if (!IsConfigInvalid(assetConfig))
                {
                    return assetConfig;
                }
            }

            // 没有配置资产时使用 Inspector 内联兜底数据
            if (IsConfigInvalid(fallbackConfig))
            {
                fallbackConfig = WeaponConfig.CreateDefaultPistol();
            }

            WeaponConfig runtimeConfig = fallbackConfig.Clone();
            runtimeConfig.ApplyMissingDefaults();
            return runtimeConfig;
        }

        private static bool IsConfigInvalid(WeaponConfig weaponConfig)
        {
            return weaponConfig == null
                   || weaponConfig.weaponId <= 0
                   || weaponConfig.magazineSize <= 0
                   || weaponConfig.fireInterval <= 0f
                   || string.IsNullOrEmpty(weaponConfig.fireStateName)
                   || string.IsNullOrEmpty(weaponConfig.reloadStateName)
                   || string.IsNullOrEmpty(weaponConfig.equipStateName);
        }

        private void InitRuntimeData()
        {
            RuntimeData = new WeaponRuntimeData
            {
                currentAmmoInMagazine = _config.magazineSize,
                currentReserveAmmo = _config.maxReserveAmmo,
                nextFireTime = 0f,
                isReloading = false,
                isEquipped = false
            };
        }

        private void InitStateMachine()
        {
            _states.Clear();
            _states.Add(WeaponStateType.Equip, new WeaponEquipState(this));
            _states.Add(WeaponStateType.Idle, new WeaponIdleState(this));
            _states.Add(WeaponStateType.Fire, new WeaponFireState(this));
            _states.Add(WeaponStateType.Reload, new WeaponReloadState(this));
        }

        private void InitWeaponView()
        {
            if (currentWeaponView == null)
            {
                Debug.LogWarning("WeaponController 缺少当前武器视图，请检查背包里的武器槽位", this);
                return;
            }

            if (_config == null || RuntimeData == null)
            {
                Debug.LogWarning("WeaponController 缺少当前武器数据，请检查背包里的武器配置", this);
                return;
            }

            currentWeaponView.gameObject.SetActive(true);
            currentWeaponView.Init(_config);
            currentWeaponView.SetAmmo(RuntimeData.currentAmmoInMagazine);
            currentWeaponView.SetReloading(RuntimeData.isReloading);
            _adsAmount = 0f;
            currentWeaponView.SetADSAmount(_adsAmount);
            currentWeaponView.SetSprintAmount(0f);
            TriggerAimCameraEvent();
        }

        private bool TryEquipInventoryCurrentWeapon(bool playEquip)
        {
            playerInventory ??= GetComponent<PlayerInventory>();
            if (playerInventory == null)
            {
                return false;
            }

            playerInventory.InitForNewRun();
            return EquipWeaponSlot(playerInventory.CurrentWeapon, playEquip);
        }

        private bool EquipWeaponSlot(CarriedWeaponSlot weaponSlot, bool playEquip)
        {
            if (weaponSlot == null || !weaponSlot.HasWeaponView)
            {
                return false;
            }

            weaponSlot.EnsureRuntimeReady();

            WeaponView previousWeaponView = currentWeaponView;
            bool isSwitchingWeapon = previousWeaponView != null && previousWeaponView != weaponSlot.WeaponView;
            if (isSwitchingWeapon)
            {
                CancelAimForWeaponSwitch();
            }

            if (previousWeaponView != null && previousWeaponView != weaponSlot.WeaponView)
            {
                previousWeaponView.ResetPoseInstant();
            }

            _currentState?.Exit();
            _currentState = null;
            _currentWeaponSlot = weaponSlot;
            currentWeaponView = weaponSlot.WeaponView;
            _config = weaponSlot.RuntimeConfig;
            RuntimeData = weaponSlot.RuntimeData;

            if (RuntimeData != null)
            {
                RuntimeData.isReloading = false;
                RuntimeData.isEquipped = false;
            }

            _fireInput = false;
            _reloadInput = false;
            _aimInput = false;
            _waitForAimReleaseAfterSwitch = isSwitchingWeapon;
            _adsAmount = 0f;

            InitWeaponView();

            if (playEquip && isActiveAndEnabled)
            {
                ChangeState(WeaponStateType.Equip, true);
            }

            return true;
        }

        private void OnPlayerWeaponChanged(PlayerWeaponChangedEventData eventData)
        {
            if (playerInventory == null)
            {
                CacheReferences();
            }

            if (playerInventory == null)
            {
                return;
            }

            EquipWeaponSlot(playerInventory.CurrentWeapon, _hasStarted);
        }

        private void UpdateInput()
        {
            _fireInput = false;
            _reloadInput = false;
            _aimInput = false;

            GameInputManger inputManger = GameInputManger.Instance;
            if (inputManger == null)
            {
                return;
            }

            _reloadInput = inputManger.ReloadDown;
            _fireInput = ShouldRequestFire(inputManger);
            _aimInput = ShouldHoldAim(inputManger);
        }

        private void UpdateAim()
        {
            float targetAmount = CanAim() ? 1f : 0f;
            float speed = targetAmount > _adsAmount ? GetAimInSpeed() : GetAimOutSpeed();

            _adsAmount = Mathf.MoveTowards(_adsAmount, targetAmount, speed * Time.deltaTime);

            if (currentWeaponView != null)
            {
                currentWeaponView.SetADSAmount(_adsAmount);
            }

            TriggerAimCameraEvent();
        }

        private bool CanAim()
        {
            return _aimInput
                   && _config != null
                   && RuntimeData != null
                   && RuntimeData.isEquipped
                   && !RuntimeData.isReloading;
        }

        private float GetAimInSpeed()
        {
            return _config != null && _config.aimInSpeed > 0f
                ? _config.aimInSpeed
                : WeaponConfig.DefaultAimInSpeed;
        }

        private float GetAimOutSpeed()
        {
            return _config != null && _config.aimOutSpeed > 0f
                ? _config.aimOutSpeed
                : WeaponConfig.DefaultAimOutSpeed;
        }

        private float GetAimCameraFov()
        {
            return _config != null && _config.aimCameraFov > 1f
                ? _config.aimCameraFov
                : WeaponConfig.DefaultAimCameraFov;
        }

        private void ResetAimVisuals()
        {
            _aimInput = false;
            _adsAmount = 0f;

            if (currentWeaponView != null)
            {
                currentWeaponView.SetADSAmount(_adsAmount);
            }

            TriggerAimCameraEvent();
        }

        private void CancelAimForWeaponSwitch()
        {
            // 切枪时强制退出开镜 避免新枪模型贴到相机前
            ResetAimVisuals();
            EventCenter.Instance.EventTrigger(GameEvent.MobileSightCanceled);
        }

        private void TriggerAimCameraEvent()
        {
            // 武器只把自己的开镜比例和目标 FOV 发出去
            EventCenter.Instance.EventTrigger(
                GameEvent.WeaponAimCameraChanged,
                new WeaponAimCameraEventData(_adsAmount, GetAimCameraFov()));
        }

        private bool ShouldRequestFire(GameInputManger inputManger)
        {
            if (_config == null)
            {
                return false;
            }

            switch (_config.fireMode)
            {
                case WeaponFireMode.FullAuto:
                    return inputManger.FireHeld;
                case WeaponFireMode.SemiAuto:
                default:
                    return inputManger.FireDown;
            }
        }

        private bool ShouldHoldAim(GameInputManger inputManger)
        {
            bool aimHeld = inputManger.AimHeld;
            if (!_waitForAimReleaseAfterSwitch)
            {
                return aimHeld;
            }

            if (aimHeld)
            {
                return false;
            }

            _waitForAimReleaseAfterSwitch = false;
            return false;
        }
    }
}
