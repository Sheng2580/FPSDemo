using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMove : PlayerState
{
    public override void Update()
    {
        if (ShouldStartJump())
        {
            StartJump();
            player.ChangeState(PlayerStateType.Jump);
            return;
        }

        if (HasMoveInput())
        {
            player.Motor.Move(GetMoveInput(), GetCurrentMoveSpeed());
            return;
        }

        player.Motor.Decelerate();

        if (player.Motor.IsHorizontalStopped)
        {
            player.ChangeState(PlayerStateType.Idle);
        }
    }
}
