namespace Weapon.State
{
    public abstract class WeaponState
    {
        protected readonly WeaponController controller;

        protected WeaponState(WeaponController controller)
        {
            this.controller = controller;
        }

        public virtual void Enter()
        {
        }

        public virtual void Exit()
        {
        }

        public virtual void Update()
        {
        }
    }
}
