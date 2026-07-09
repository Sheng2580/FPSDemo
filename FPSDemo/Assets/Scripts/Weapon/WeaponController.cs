using System.Collections.Generic;
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
        [SerializeField] private WeaponConfig config;

        private readonly Dictionary<WeaponStateType, WeaponState> _states = new Dictionary<WeaponStateType, WeaponState>();
        private WeaponState _currentState;
        private WeaponStateType _currentStateType;
        private Camera _fireCamera;
        private bool _fireInput;
        private bool _reloadInput;

        public WeaponView CurrentWeaponView => currentWeaponView;
        public PlayerCameraController CameraController => cameraController;
        public WeaponConfig Config => config;
        public WeaponRuntimeData RuntimeData { get; private set; }
        public WeaponStateType CurrentStateType => _currentStateType;
        public bool FireInput => _fireInput;
        public bool ReloadInput => _reloadInput;

        private void Reset()
        {
            CacheReferences();
        }

        private void Awake()
        {
            CacheReferences();
            InitDefaultConfig();
            InitRuntimeData();
            InitStateMachine();
            InitWeaponView();
        }

        private void Start()
        {
            ChangeState(WeaponStateType.Equip, true);
        }

        private void Update()
        {
            UpdateInput();
            _currentState?.Update();
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
                   && config != null
                   && RuntimeData != null
                   && RuntimeData.isEquipped
                   && !RuntimeData.isReloading
                   && RuntimeData.currentAmmoInMagazine > 0
                   && Time.time >= RuntimeData.nextFireTime;
        }

        public bool CanReload()
        {
            return currentWeaponView != null
                   && config != null
                   && RuntimeData != null
                   && RuntimeData.isEquipped
                   && !RuntimeData.isReloading
                   && RuntimeData.currentAmmoInMagazine < config.magazineSize
                   && RuntimeData.currentReserveAmmo > 0;
        }

        public void ApplyRecoil()
        {
            if (cameraController == null || config == null)
            {
                return;
            }

            float yaw = Random.Range(-config.recoilYaw, config.recoilYaw);
            cameraController.AddRecoil(config.recoilPitch, yaw);
        }

        public void FireRaycast()
        {
            Camera raycastCamera = _fireCamera;
            if (raycastCamera == null)
            {
                return;
            }

            Ray ray = new Ray(raycastCamera.transform.position, raycastCamera.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, config.range))
            {
                Debug.Log($"Weapon Raycast Hit: {hit.collider.name}", hit.collider);
            }
        }

        private void CacheReferences()
        {
            cameraController ??= GetComponent<PlayerCameraController>();
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

        private void InitDefaultConfig()
        {
            if (IsConfigInvalid(config))
            {
                config = WeaponConfig.CreateDefaultPistol();
            }
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
                currentAmmoInMagazine = config.magazineSize,
                currentReserveAmmo = config.maxReserveAmmo,
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
                Debug.LogWarning("WeaponController 缺少 CurrentWeaponView，请把 PistolView 上的 WeaponView 拖到这里。", this);
                return;
            }

            currentWeaponView.Init(config);
            currentWeaponView.SetAmmo(RuntimeData.currentAmmoInMagazine);
            currentWeaponView.SetReloading(RuntimeData.isReloading);
            currentWeaponView.SetADSAmount(0f);
            currentWeaponView.SetSprintAmount(0f);
        }

        private void UpdateInput()
        {
            _fireInput = false;
            _reloadInput = false;

            GameInputManger inputManger = GameInputManger.Instance;
            if (inputManger == null)
            {
                return;
            }

            _reloadInput = inputManger.ReloadDown;
            _fireInput = ShouldRequestFire(inputManger);
        }

        private bool ShouldRequestFire(GameInputManger inputManger)
        {
            if (config == null)
            {
                return false;
            }

            switch (config.fireMode)
            {
                case WeaponFireMode.FullAuto:
                    return inputManger.FireHeld;
                case WeaponFireMode.SemiAuto:
                default:
                    return inputManger.FireDown;
            }
        }
    }
}
