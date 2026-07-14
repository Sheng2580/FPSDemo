using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PlayerData;

public class PlayerJump : PlayerState
{
    public override void Update()
    {
        if (player.IsSkillMovementLocked)
        {
            player.Motor.Stop();
            return;
        }

        float airMoveControl = player != null && player.Stats != null
            ? player.Stats.AirMoveControl
            : PlayerDefaultConfigAsset.LoadRuntimeConfig().airMoveControl;
        float jumpEndVerticalVelocity = player != null && player.Stats != null
            ? player.Stats.JumpEndVerticalVelocity
            : PlayerDefaultConfigAsset.LoadRuntimeConfig().jumpEndVerticalVelocity;
        

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
