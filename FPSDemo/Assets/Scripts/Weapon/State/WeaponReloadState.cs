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

            _timer = controller.Config.reloadTime;
            controller.RuntimeData.isReloading = true;
            controller.CurrentWeaponView?.SetReloading(true);
            controller.CurrentWeaponView?.PlayReload();
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
