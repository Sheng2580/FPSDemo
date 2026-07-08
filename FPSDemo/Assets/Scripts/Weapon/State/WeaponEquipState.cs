using UnityEngine;

namespace Weapon.State
{
    public class WeaponEquipState : WeaponState
    {
        private float _timer;

        public WeaponEquipState(WeaponController controller) : base(controller)
        {
        }

        public override void Enter()
        {
            _timer = controller.Config != null ? controller.Config.equipTransition : 0f;
            controller.RuntimeData.isEquipped = false;
            controller.CurrentWeaponView?.PlayEquip();
        }

        public override void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f)
            {
                return;
            }

            controller.RuntimeData.isEquipped = true;
            controller.ChangeState(WeaponStateType.Idle);
        }
    }
}
