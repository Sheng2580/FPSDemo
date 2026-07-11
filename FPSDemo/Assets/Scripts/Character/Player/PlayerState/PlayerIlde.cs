using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerIlde : PlayerState
{
   public override void Update()
   {
      if (player.IsSkillMovementLocked)
      {
         return;
      }

      if (ShouldStartJump())
      {
         StartJump();
         player.ChangeState(PlayerStateType.Jump);
         return;
      }

      if (HasMoveInput())
      {
         player.ChangeState(PlayerStateType.Move);
      }
   }
}
