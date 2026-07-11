using UnityEngine;

namespace Weapon.State
{
    public class WeaponReloadState : WeaponState
    {
        private float _timer;

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

            float reloadTime = Mathf.Max(0.01f, controller.Config.reloadTime);
            _timer = reloadTime;
            controller.RuntimeData.isReloading = true;
            controller.CurrentWeaponView?.SetReloading(true);
            controller.CurrentWeaponView?.PlayReload(reloadTime);
        }

        public override void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f)
            {
                return;
            }

            FillMagazine();
            controller.RuntimeData.isReloading = false;
            controller.CurrentWeaponView?.SetAmmo(controller.RuntimeData.currentAmmoInMagazine);
            controller.CurrentWeaponView?.SetReloading(false);
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
    }
}
