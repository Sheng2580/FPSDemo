using UnityEngine;

namespace Enemy
{
    public class EnemyView : EnemyModel
    {
        public Animator Animator => animator;

        public void ResetView()
        {
            ResetModel();
        }

        public void PlayIdle(bool force = false)
        {
            PlayState(IdleStateName, LocomotionTransition, force);
        }

        public void PlayMove(bool run)
        {
            PlayState(run ? RunStateName : WalkStateName, LocomotionTransition, false);
        }

        public void PlayAttack()
        {
            PlayState(AttackStateName, AttackTransition, true);
        }

        public void PlayDamage()
        {
            PlayState(DamageStateName, HitTransition, true);
        }

        public void PlayDeath()
        {
            PlayState(DeathStateName, DeathTransition, true);
        }

        private void PlayState(string stateName, float transition, bool force)
        {
            if (!TryGetAnimator(out Animator targetAnimator) || string.IsNullOrEmpty(stateName))
            {
                return;
            }

            if (!force && IsCurrentAnimationState(targetAnimator, stateName))
            {
                return;
            }

            targetAnimator.CrossFadeInFixedTime(stateName, transition);
        }

        private static bool IsCurrentAnimationState(Animator targetAnimator, string stateName)
        {
            if (targetAnimator.GetNextAnimatorStateInfo(0).IsName(stateName))
            {
                return true;
            }

            return targetAnimator.GetCurrentAnimatorStateInfo(0).IsName(stateName);
        }
    }
}
