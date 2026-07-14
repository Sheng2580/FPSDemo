using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMotor : MonoBehaviour
{
    [SerializeField] private PlayerController player;

    private const float StopVelocityThreshold = 0.0001f;
    private Vector3 currentHorizontalVelocity;
    private bool _hasLoggedMissingPlayer;

    public Vector3 CurrentHorizontalVelocity => currentHorizontalVelocity;
    public bool IsHorizontalStopped => currentHorizontalVelocity.sqrMagnitude < 0.01f;

    private void Reset()
    {
        AutoBindPlayer();
    }

    private void Awake()
    {
        AutoBindPlayer();
    }

    public void Move(Vector2 input, float speed, float controlMultiplier = 1f)
    {
        if (!HasValidController())
        {
            return;
        }

        if (player.Stats == null)
        {
            return;
        }

        if (input.sqrMagnitude > 1f)
        {
            input.Normalize();
        }

        Transform playerTransform = player.transform;
        Vector3 forward = playerTransform.forward;
        Vector3 right = playerTransform.right;

        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 direction = forward * input.y + right * input.x;
        direction.y = 0f;

        if (direction.sqrMagnitude > 1f)
        {
            direction.Normalize();
        }

        Vector3 targetVelocity = direction * speed * controlMultiplier;
        float step = player.Stats.MoveAcceleration * Time.deltaTime;

        currentHorizontalVelocity = Vector3.MoveTowards(currentHorizontalVelocity, targetVelocity, step);
        currentHorizontalVelocity.y = 0f;

        player.SetHorizontalVelocity(currentHorizontalVelocity);
        player.characterController.Move(currentHorizontalVelocity * Time.deltaTime);
    }

    public void Decelerate()
    {
        if (!HasValidController())
        {
            return;
        }

        if (player.Stats == null)
        {
            return;
        }

        currentHorizontalVelocity = Vector3.MoveTowards(
            currentHorizontalVelocity,
            Vector3.zero,
            player.Stats.MoveDeceleration * Time.deltaTime);
        currentHorizontalVelocity.y = 0f;

        if (currentHorizontalVelocity.sqrMagnitude <= StopVelocityThreshold)
        {
            Stop();
        }
        else
        {
            player.SetHorizontalVelocity(currentHorizontalVelocity);
        }

        player.characterController.Move(currentHorizontalVelocity * Time.deltaTime);
    }

    public void Stop()
    {
        currentHorizontalVelocity = Vector3.zero;

        if (player != null)
        {
            player.StopHorizontalVelocity();
        }
    }

    public void Jump(float jumpVelocity)
    {
        if (player == null)
        {
            AutoBindPlayer();
            if (player == null)
            {
                return;
            }
        }

        player.SetVerticalVelocity(jumpVelocity);
    }

    private void AutoBindPlayer()
    {
        if (player == null)
        {
            player = GetComponent<PlayerController>();
        }

        if (player == null)
        {
            if (!_hasLoggedMissingPlayer)
            {
                Debug.LogError("PlayerMotor 缺少 PlayerController，请确认脚本挂在 Player 根节点上", this);
                _hasLoggedMissingPlayer = true;
            }

            return;
        }

        _hasLoggedMissingPlayer = false;
    }

    private bool HasValidController()
    {
        AutoBindPlayer();
        if (player == null)
        {
            return false;
        }

        if (player.characterController == null || !player.characterController.enabled)
        {
            return false;
        }

        return true;
    }
}
