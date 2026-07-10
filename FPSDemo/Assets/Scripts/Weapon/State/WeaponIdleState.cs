namespace Weapon.State
{
    public class WeaponIdleState : WeaponState
    {
        public WeaponIdleState(WeaponController controller) : base(controller)
        {
        }

        public override void Enter()
        {
            controller.CurrentWeaponView?.PlayIdle();
        }

        public override void Update()
        {
            if (controller.ReloadInput && controller.CanReload())
            {
                controller.ChangeState(WeaponStateType.Reload);
                return;
            }

            if (!controller.FireInput)
            {
                return;
            }

            if (controller.CanFire())
            {
                controller.ChangeState(WeaponStateType.Fire);
                return;
            }

            if (controller.CanAutoReloadOnFire())
            {
                controller.ChangeState(WeaponStateType.Reload);
            }
        }
    }
}
