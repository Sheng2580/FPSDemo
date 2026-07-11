using UnityEngine;

public abstract class CharacterBase : MonoBehaviour, IStateMachineOwner
{
    #region Gravity Settings
    [Header("地面检测")]
    [SerializeField] private float groundDetectionRadius = 0.2f;
    [SerializeField] private float groundDetectionOffset = 0.1f;
    [SerializeField] private LayerMask groundLayerMask;

    [Header("重力参数")]
    [SerializeField] protected bool isEnableGravity = true;
    [SerializeField] protected float gravity = 11.8f;
    [SerializeField] protected float terminalVelocity = -54f;
    [SerializeField] protected float groundedVelocity = -2f;
    [SerializeField] protected float fallOutTime = 0.15f;

    protected float fallOutDeltaTime;
    [SerializeField]
    protected float velocityY;
    [SerializeField]
    protected bool characterIsGrounded;

    public CharacterController characterController;

    [SerializeField] private CharacterModleBase model;

    public bool IsGrounded => characterIsGrounded;
    public float VelocityY => velocityY;
    public bool ShouldPlayFallAnimation => !characterIsGrounded && fallOutDeltaTime <= 0f;
    public CharacterModleBase ModelBase => model;
    
    protected StateMachine stateMachine;

    #endregion

    protected virtual void Start()
    {
        characterController = GetComponent<CharacterController>();
        fallOutDeltaTime = fallOutTime;
        model = GetCharacterModel();
        InitStateMachine();
    }
    

    protected virtual void Update()
    {
        UpdateGravity();
        ApplyVerticalMovement();
    }


    protected abstract CharacterModleBase GetCharacterModel();
    
    protected abstract void InitStateMachine();
    
    
    protected void SetPlayerIsEnableGravity(bool isEnable)
    {
        isEnableGravity = isEnable;

        if (!isEnable)
        {
            velocityY = 0f;
        }
    }

    protected void SetPlayerVelocityY(float newVelocityY)
    {
        velocityY = newVelocityY;
    }

    /// <summary>
    /// 检测地面
    /// </summary>
    /// <returns></returns>
    protected virtual bool GroundedDetection()
    {
        if (characterController != null && characterController.isGrounded)
        {
            return true;
        }

        return Physics.CheckSphere(
            GetGroundDetectionPosition(),
            groundDetectionRadius,
            groundLayerMask,
            QueryTriggerInteraction.Ignore
        );
    }

    /// <summary>
    /// 计算位移
    /// </summary>
    /// <returns></returns>
    protected Vector3 GetGroundDetectionPosition()
    {
        if (characterController != null)
        {
            Bounds controllerBounds = characterController.bounds;
            Vector3 center = controllerBounds.center;
            // 地面检测点按 CharacterController 底部计算 避免玩家根节点高度导致检测球悬空
            return new Vector3(center.x, controllerBounds.min.y + groundDetectionOffset, center.z);
        }

        return transform.position + Vector3.down * groundDetectionOffset;
    }

    
    /// <summary>
    /// 计算
    /// </summary>
    public virtual void UpdateGravity()
    {
        characterIsGrounded = GroundedDetection();

        if (characterIsGrounded)
        {
            if (velocityY < 0f)
            {
                velocityY = groundedVelocity;
            }

            fallOutDeltaTime = fallOutTime;
            return;
        }

        if (fallOutDeltaTime > 0f)
        {
            fallOutDeltaTime -= Time.deltaTime;
        }

        if (!isEnableGravity)
        {
            return;
        }

        velocityY -= gravity * Time.deltaTime;
        velocityY = Mathf.Max(velocityY, terminalVelocity);
    }

    /// <summary>
    /// 应用位移
    /// </summary>
    protected virtual void ApplyVerticalMovement()
    {
        if (!isEnableGravity || characterController == null || !characterController.enabled)
        {
            return;
        }

        Vector3 verticalMove = Vector3.up * velocityY;
        characterController.Move(verticalMove * Time.deltaTime);
    }

    public virtual void PlayAnimation(string animationName, int layer = 0, float fixedTransitionTime = 0.25f)
    {
        if (ModelBase == null || ModelBase.animator == null)
        {
            return;
        }

        ModelBase.animator.CrossFadeInFixedTime(animationName, fixedTransitionTime, layer);
    }

    protected virtual void OnDrawGizmosSelected()
    {
        if (groundDetectionRadius <= 0f)
        {
            return;
        }

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(GetGroundDetectionPosition(), groundDetectionRadius);
    }
}

public abstract class CharacterBase<TModel> : CharacterBase where TModel : CharacterModleBase
{
    public TModel Model { get; private set; }

    protected sealed override CharacterModleBase GetCharacterModel()
    {
        Model = GetTypedCharacterModel();
        return Model;
    }

    protected abstract TModel GetTypedCharacterModel();
}
