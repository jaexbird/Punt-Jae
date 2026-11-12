using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BeatEmUpPlayerController : MonoBehaviour
{
    // 1. Add this property inside BeatEmUpPlayerController:
    public bool IsAttacking { get; set; }
    public bool FacingRight = true;

    [Header("Movement Settings")]
    [Tooltip("Horizontal speed (Left/Right)")]
    [SerializeField] private float moveSpeedX = 6f;
    [Tooltip("Depth speed (Up/Down into the screen - usually slower for 2.5D perspective)")]
    [SerializeField] private float moveSpeedZ = 4f;

    [Header("Jump & Gravity Settings")]
    [SerializeField] private float jumpForce = 8.5f;
    [SerializeField] private float fallGravityMultiplier = 2.5f;
    [SerializeField] private Transform groundCheckPoint;
    [SerializeField] private float groundCheckRadius = 0.2f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Visuals & Shadow References")]
    [SerializeField] private SpriteRenderer characterSprite;
    [SerializeField] private Animator animator;
    [SerializeField] private Transform shadowTransform;
    [SerializeField] private float shadowGroundOffset = 0.01f;
    [SerializeField] private float maxShadowHeight = 4f;

    private Rigidbody rb;
    private Vector2 moveInput;
    private bool isGrounded;
    private bool jumpRequested;
    private bool facingRight = true;
    private Vector3 initialShadowScale;

    // Cached Animator parameter hashes for performance
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int VerticalVelocityHash = Animator.StringToHash("VerticalVelocity");

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        if (shadowTransform != null)
        {
            initialShadowScale = shadowTransform.localScale;
        }
    }

    private void Update()
    {
        // 1. Read X (Horizontal) and Z (Vertical/Depth) input
        moveInput.x = Input.GetAxisRaw("Horizontal");
        moveInput.y = Input.GetAxisRaw("Vertical");

        // Normalize diagonal input so moving diagonally isn't faster
        if (moveInput.sqrMagnitude > 1f)
        {
            moveInput.Normalize();
        }

        // 2. Check for Jump input
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            jumpRequested = true;
        }

        // 3. Handle Sprite Flipping

        // Read the raw horizontal input (-1 for left, 1 for right)
        float moveX = Input.GetAxisRaw("Horizontal");

        // Only update the flip if the player is actively pressing Left or Right
        if (Mathf.Abs(moveX) > 0.01f && !IsAttacking)
        {
            FacingRight = moveX > 0;

            if (characterSprite != null)
            {
                // flipX is true when facing left, false when facing right
                characterSprite.flipX = !FacingRight;
            }
        }

        // 4. Update Animator & Grounded Shadow
        UpdateAnimations();
        UpdateShadowPosition();
    }

    private void Flip()
    {
        // Toggle the boolean so PlayerCombat and other scripts know which way we are facing
        FacingRight = !FacingRight;

        // Flip the actual sprite image
        if (characterSprite != null)
        {
            characterSprite.flipX = !FacingRight;
        }
    }

    private void FixedUpdate()
    {
        // 1. Ground Check
        Vector3 checkPos = groundCheckPoint != null ? groundCheckPoint.position : transform.position;
        isGrounded = Physics.CheckSphere(checkPos, groundCheckRadius, groundLayer, QueryTriggerInteraction.Ignore);

        // 2. Apply X and Z Movement (Unity 6 uses linearVelocity instead of velocity)
        Vector3 targetVelocity = new Vector3(
            moveInput.x * moveSpeedX,
            rb.linearVelocity.y,
            moveInput.y * moveSpeedZ
        );
        rb.linearVelocity = targetVelocity;

        // 3. Execute Jump
        if (jumpRequested)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpForce, rb.linearVelocity.z);
            jumpRequested = false;
        }

        // 4. Apply Snappy Arcade Fall Gravity
        if (rb.linearVelocity.y < 0f && !isGrounded)
        {
            rb.linearVelocity += Vector3.up * (Physics.gravity.y * (fallGravityMultiplier - 1f) * Time.fixedDeltaTime);
        }

        // 2. Inside FixedUpdate(), modify the velocity calculation to lock movement while grounded and attacking:
        float currentMoveX = (IsAttacking && isGrounded) ? 0f : moveInput.x * moveSpeedX;
        float currentMoveZ = (IsAttacking && isGrounded) ? 0f : moveInput.y * moveSpeedZ;

        rb.linearVelocity = new Vector3(
            currentMoveX,
            rb.linearVelocity.y,
            currentMoveZ
        );
    }

    private void FlipSprite(bool faceRight)
    {
        facingRight = faceRight;
        if (characterSprite != null)
        {
            characterSprite.flipX = !facingRight;
        }
    }

    private void UpdateAnimations()
    {
        if (animator == null) return;

        animator.SetFloat(SpeedHash, moveInput.magnitude);
        animator.SetBool(IsGroundedHash, isGrounded);
        animator.SetFloat(VerticalVelocityHash, rb.linearVelocity.y);
    }

    private void UpdateShadowPosition()
    {
        if (shadowTransform == null) return;

        // Raycast down from the player to keep the shadow pinned to the street while jumping
        Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 20f, groundLayer, QueryTriggerInteraction.Ignore))
        {
            shadowTransform.position = new Vector3(
                transform.position.x,
                hit.point.y + shadowGroundOffset,
                transform.position.z
            );

            // Shrink the shadow slightly as the player jumps higher
            float heightAboveGround = Mathf.Max(0f, transform.position.y - hit.point.y);
            float scaleFactor = Mathf.Lerp(1f, 0.5f, heightAboveGround / maxShadowHeight);
            shadowTransform.localScale = initialShadowScale * scaleFactor;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 checkPos = groundCheckPoint != null ? groundCheckPoint.position : transform.position;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(checkPos, groundCheckRadius);
    }
}