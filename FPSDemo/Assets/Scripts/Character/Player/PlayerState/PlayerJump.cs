using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerJump : PlayerState
{
    public override void Update()
    {
        if (HasMoveInput())
        {
            player.Motor.Move(GetMoveInput(), GetCurrentMoveSpeed(), player.AirMoveControl);
        }
        else
        {
            player.Motor.Decelerate();
        }

        if (player.IsGrounded && player.VelocityY <= player.JumpEndVerticalVelocity)
        {
            if (HasMoveInput())
            {
                player.ChangeState(PlayerStateType.Move);
            }
            else
            {
                player.ChangeState(PlayerStateType.Idle);
            }
        }
    }
}
