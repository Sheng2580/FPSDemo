using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PlayerData;

public class PlayerJump : PlayerState
{
    public override void Update()
    {
        float airMoveControl = player != null && player.Stats != null
            ? player.Stats.AirMoveControl
            : PlayerBaseConfig.CreateDefault().airMoveControl;
        float jumpEndVerticalVelocity = player != null && player.Stats != null
            ? player.Stats.JumpEndVerticalVelocity
            : PlayerBaseConfig.CreateDefault().jumpEndVerticalVelocity;
        

        if (HasMoveInput())
        {
            player.Motor.Move(GetMoveInput(), GetCurrentMoveSpeed(), airMoveControl);
        }
        else
        {
            player.Motor.Decelerate();
        }

        if (player.IsGrounded && player.VelocityY <= jumpEndVerticalVelocity)
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
