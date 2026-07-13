using UnityEngine;
using Weapon.Data;

namespace Weapon.State
{
    public class WeaponReloadState : WeaponState
    {
        private float _timer;
        private bool _singleRoundReload;

        public WeaponReloadState(WeaponController controller) : base(controller)
        {
        }

        public override void Enter()
        {
            if (!controller.CanReload())
            {
                controller.ChangeState(WeaponStateType.Idle);
                return;
            }

            _singleRoundReload = controller.Config.reloadMode == WeaponReloadMode.SingleRound;
            controller.RuntimeData.isReloading = true;
            controller.CurrentWeaponView?.SetReloading(true);

            if (_singleRoundReload)
            {
                BeginSingleRoundStep();
                return;
            }

            float reloadTime = Mathf.Max(0.01f, controller.Config.reloadTime);
            _timer = reloadTime;
            controller.CurrentWeaponView?.PlayReload(reloadTime);
        }

        public override void Update()
        {
            if (_singleRoundReload)
            {
                UpdateSingleRoundReload();
                return;
            }

            _timer -= Time.deltaTime;
            if (_timer > 0f)
            {
                return;
            }

            FillMagazine();
            controller.RuntimeData.isReloading = false;
            controller.CurrentWeaponView?.SetAmmo(controller.RuntimeData.currentAmmoInMagazine);
            controller.CurrentWeaponView?.SetReloading(false);
            controller.TriggerWeaponAmmoChanged();
            controller.ChangeState(WeaponStateType.Idle);
        }

        public override void Exit()
        {
            controller.CurrentWeaponView?.ResetAnimationSpeed();

            if (!controller.RuntimeData.isReloading)
            {
                return;
            }

            controller.RuntimeData.isReloading = false;
            controller.CurrentWeaponView?.SetReloading(false);
        }

        private void FillMagazine()
        {
            int needAmmo = controller.Config.magazineSize - controller.RuntimeData.currentAmmoInMagazine;
            int loadAmmo = Mathf.Min(needAmmo, controller.RuntimeData.currentReserveAmmo);

            controller.RuntimeData.currentAmmoInMagazine += loadAmmo;
            controller.RuntimeData.currentReserveAmmo -= loadAmmo;
        }

        private void UpdateSingleRoundReload()
        {
            if (ShouldInterruptSingleRoundReload())
            {
                controller.ChangeState(WeaponStateType.Fire);
                return;
            }

            _timer -= Time.deltaTime;
            if (_timer > 0f)
            {
                return;
            }

            LoadSingleRoundStep();
            controller.CurrentWeaponView?.SetAmmo(controller.RuntimeData.currentAmmoInMagazine);
            controller.TriggerWeaponAmmoChanged();

            if (ShouldInterruptSingleRoundReload())
            {
                controller.ChangeState(WeaponStateType.Fire);
                return;
            }

            if (!CanContinueSingleRoundReload())
            {
                FinishReload();
                return;
            }

            BeginSingleRoundStep();
        }

        private void BeginSingleRoundStep()
        {
            float reloadStepTime = Mathf.Max(0.01f, controller.Config.reloadSingleRoundTime);
            _timer = reloadStepTime;
            controller.CurrentWeaponView?.PlayReload(reloadStepTime);
        }

        private void LoadSingleRoundStep()
        {
            int needAmmo = controller.Config.magazineSize - controller.RuntimeData.currentAmmoInMagazine;
            int loadAmmo = Mathf.Min(
                Mathf.Max(1, controller.Config.reloadAmmoPerStep),
                needAmmo,
                controller.RuntimeData.currentReserveAmmo);

            if (loadAmmo <= 0)
            {
                return;
            }

            controller.RuntimeData.currentAmmoInMagazine += loadAmmo;
            controller.RuntimeData.currentReserveAmmo -= loadAmmo;
        }

        private bool ShouldInterruptSingleRoundReload()
        {
            return controller.Config.canInterruptReloadByFire
                   && controller.FireInput
                   && controller.RuntimeData.currentAmmoInMagazine > 0
                   && Time.time >= controller.RuntimeData.nextFireTime;
        }

        private bool CanContinueSingleRoundReload()
        {
            return controller.RuntimeData.currentAmmoInMagazine < controller.Config.magazineSize
                   && controller.RuntimeData.currentReserveAmmo > 0;
        }

        private void FinishReload()
        {
            controller.RuntimeData.isReloading = false;
            controller.CurrentWeaponView?.SetReloading(false);
            controller.ChangeState(WeaponStateType.Idle);
        }
    }
}
