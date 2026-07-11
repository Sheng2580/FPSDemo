namespace Weapon.State
{
    public class WeaponIdleState : WeaponState
    {
        public WeaponIdleState(WeaponController controller) : base(controller)
        {
        }

        public override void Enter()
        {
            // 换弹结束回到待机时保留过渡时间 避免动画直接切断
            float transition = controller.PreviousStateType == WeaponStateType.Reload && controller.Config != null
                ? controller.Config.reloadTransition
                : 0f;

            controller.CurrentWeaponView?.PlayIdle(transition);
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
