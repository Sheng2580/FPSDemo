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
            if (controller.InfiniteAmmoActive)
            {
                // 无限弹药状态下保持弹匣可开火 道具限时效果也走这里
                controller.RuntimeData.currentAmmoInMagazine = controller.Config.magazineSize;
            }
            else
            {
                controller.RuntimeData.currentAmmoInMagazine--;
            }

            controller.RuntimeData.nextFireTime = Time.time + controller.Config.fireInterval;

            controller.CurrentWeaponView?.PlayFire();
            controller.CurrentWeaponView?.SetAmmo(controller.RuntimeData.currentAmmoInMagazine);
            controller.TriggerWeaponAmmoChanged();
            // 只在扣弹成功后通知表现系统
            controller.TriggerWeaponFired();
            controller.FireRaycast();
            controller.ApplyRecoil();
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
