using UnityEngine;

namespace Weapon.State
{
    public class WeaponFireState : WeaponState
    {
        private float _timer;

        public WeaponFireState(WeaponController controller) : base(controller)
        {
        }

        public override void Enter()
        {
            if (!controller.CanFire())
            {
                controller.ChangeState(WeaponStateType.Idle);
                return;
            }

            _timer = controller.Config.fireInterval;
            controller.RuntimeData.currentAmmoInMagazine--;
            controller.RuntimeData.nextFireTime = Time.time + controller.Config.fireInterval;

            controller.CurrentWeaponView?.PlayFire();
            controller.CurrentWeaponView?.SetAmmo(controller.RuntimeData.currentAmmoInMagazine);
            controller.ApplyRecoil();
            controller.FireRaycast();
        }

        public override void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f)
            {
                return;
            }

            controller.ChangeState(WeaponStateType.Idle);
        }
    }
}
