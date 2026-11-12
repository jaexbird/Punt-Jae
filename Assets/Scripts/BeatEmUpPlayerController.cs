using UnityEngine;

// Forces Unity to automatically add a Rigidbody component to this GameObject if it doesn't already have one
[RequireComponent(typeof(Rigidbody))]

// Declares the BeatEmUpPlayerController class, inheriting from MonoBehaviour so it can be attached to a GameObject
public class BeatEmUpPlayerController : MonoBehaviour
{
    // 1. Add this property inside BeatEmUpPlayerController:
    // A public auto-property that allows other scripts (like PlayerCombat) to get and set whether the player is currently attacking
    public bool IsAttacking { get; set; }

    // A public variable tracking which way the player is facing, accessible by other scripts
    public bool FacingRight = true;

    // Creates a bold label in the Unity Inspector for organization
    [Header("Movement Settings")]

    // Adds a hover-tooltip in the Inspector explaining what this variable does
    [Tooltip("Horizontal speed (Left/Right)")]

    // The player's movement speed along the X-axis, exposed to the Inspector
    [SerializeField] private float moveSpeedX = 6f;

    // Adds a hover-tooltip explaining the depth speed
    [Tooltip("Depth speed (Up/Down into the screen - usually slower for 2.5D perspective)")]

    // The player's movement speed along the Z-axis (depth), exposed to the Inspector
    [SerializeField] private float moveSpeedZ = 4f;

    // Creates a bold label in the Inspector for jump settings
    [Header("Jump & Gravity Settings")]

    // The upward force applied when the player jumps
    [SerializeField] private float jumpForce = 8.5f;

    // A multiplier to make the player fall faster than they go up, creating a snappy, arcade-style jump
    [SerializeField] private float fallGravityMultiplier = 2.5f;

    // A reference to an empty GameObject placed at the player's feet to check for the ground
    [SerializeField] private Transform groundCheckPoint;

    // The radius of the invisible sphere used to check if the player is touching the ground
    [SerializeField] private float groundCheckRadius = 0.2f;

    // Defines which physics layers count as solid ground
    [SerializeField] private LayerMask groundLayer;

    // Creates a bold label in the Inspector for visual references
    [Header("Visuals & Shadow References")]

    // The component that actually renders the 2D character image
    [SerializeField] private SpriteRenderer characterSprite;

    // The component responsible for playing the character's animations
    [SerializeField] private Animator animator;

    // The Transform of the drop shadow sprite placed beneath the character
    [SerializeField] private Transform shadowTransform;

    // A slight vertical offset to prevent the shadow from clipping (Z-fighting) into the floor
    [SerializeField] private float shadowGroundOffset = 0.01f;

    // The maximum height the player can jump before the shadow stops shrinking
    [SerializeField] private float maxShadowHeight = 4f;

    // Private reference to the physics Rigidbody component
    private Rigidbody rb;

    // Stores the player's current X and Y (mapped to Z) input from the controller/keyboard
    private Vector2 moveInput;

    // Tracks whether the player is currently touching the floor
    private bool isGrounded;

    // A flag to pass the jump command from Update (input) to FixedUpdate (physics)
    private bool jumpRequested;

    // An internal boolean tracking facing direction (similar to the public FacingRight variable)
    private bool facingRight = true;

    // Stores the shadow's original size so it can be scaled up and down smoothly based on jump height
    private Vector3 initialShadowScale;

    // Cached Animator parameter hashes for performance (converting strings to integers is faster for the Animator to process)
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int VerticalVelocityHash = Animator.StringToHash("VerticalVelocity");

    // Unity's built-in initialization method, called exactly once before the game starts
    private void Awake()
    {
        // Grabs the Rigidbody component attached to this GameObject
        rb = GetComponent<Rigidbody>();
        // Tells the physics engine to smooth out movement between frames to prevent visual stuttering
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        // Ensures the physics engine checks for collisions constantly to prevent clipping through walls at high speeds
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // Safety check to ensure the shadow transform was assigned in the Inspector
        if (shadowTransform != null)
        {
            // Saves the shadow's starting scale
            initialShadowScale = shadowTransform.localScale;
        }
    }

    // Unity's built-in method called once every visual frame (used for input and timers)
    private void Update()
    {
        // 1. Read X (Horizontal) and Z (Vertical/Depth) input
        // Gets immediate left/right input (-1, 0, or 1) without smoothing
        moveInput.x = Input.GetAxisRaw("Horizontal");
        // Gets immediate up/down input (-1, 0, or 1) without smoothing
        moveInput.y = Input.GetAxisRaw("Vertical");

        // Normalize diagonal input so moving diagonally isn't faster
        // If the combined input vector is longer than 1 (meaning the player is pressing both directions)...
        if (moveInput.sqrMagnitude > 1f)
        {
            // Normalizes the vector so its total length is exactly 1, ensuring consistent speed
            moveInput.Normalize();
        }

        // 2. Check for Jump input
        // If the "Jump" button (Spacebar/Gamepad A) was pressed this exact frame AND the player is on the ground...
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            // Queue the jump to be processed in the next physics step
            jumpRequested = true;
        }

        // 3. Handle Sprite Flipping

        // Read the raw horizontal input (-1 for left, 1 for right)
        float moveX = Input.GetAxisRaw("Horizontal");

        // Only update the flip if the player is actively pressing Left or Right, AND they are not currently attacking
        if (Mathf.Abs(moveX) > 0.01f && !IsAttacking)
        {
            // Set the public FacingRight property to true if moving right, false if moving left
            FacingRight = moveX > 0;

            // Safety check for the sprite renderer
            if (characterSprite != null)
            {
                // flipX is true when facing left, false when facing right. So we set it to the opposite of FacingRight.
                characterSprite.flipX = !FacingRight;
            }
        }

        // 4. Update Animator & Grounded Shadow
        // Calls the helper method to send variables to the Animator
        UpdateAnimations();
        // Calls the helper method to project the shadow onto the ground
        UpdateShadowPosition();
    }

    // A helper method to force the sprite to flip its facing direction
    private void Flip()
    {
        // Toggle the boolean so PlayerCombat and other scripts know which way we are facing
        FacingRight = !FacingRight;

        // Flip the actual sprite image
        if (characterSprite != null)
        {
            // Sets the sprite's flipX property to the opposite of our new FacingRight state
            characterSprite.flipX = !FacingRight;
        }
    }

    // Unity's built-in physics method, called on a strict, consistent timer
    private void FixedUpdate()
    {
        // 1. Ground Check
        // Determines where to cast the check from: the assigned groundCheckPoint, or the center of the player if unassigned
        Vector3 checkPos = groundCheckPoint != null ? groundCheckPoint.position : transform.position;
        // Casts an invisible sphere. Returns true if it touches anything on the groundLayer, ignoring trigger colliders
        isGrounded = Physics.CheckSphere(checkPos, groundCheckRadius, groundLayer, QueryTriggerInteraction.Ignore);

        // 2. Apply X and Z Movement (Unity 6 uses linearVelocity instead of velocity)
        // Calculates the desired physics velocity based on input and move speeds, maintaining current Y (falling) velocity
        Vector3 targetVelocity = new Vector3(
            moveInput.x * moveSpeedX,
            rb.linearVelocity.y,
            moveInput.y * moveSpeedZ
        );
        // Applies this target velocity to the Rigidbody
        rb.linearVelocity = targetVelocity;

        // 3. Execute Jump
        // If the player pressed jump in the Update loop...
        if (jumpRequested)
        {
            // Instantly set the Y velocity to the jumpForce, maintaining current X and Z speeds
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpForce, rb.linearVelocity.z);
            // Reset the flag so we don't jump again next frame
            jumpRequested = false;
        }

        // 4. Apply Snappy Arcade Fall Gravity
        // If the player is falling downward (Y velocity < 0) and is not touching the ground...
        if (rb.linearVelocity.y < 0f && !isGrounded)
        {
            // Add extra downward gravity force every frame to make the fall faster than the ascent
            rb.linearVelocity += Vector3.up * (Physics.gravity.y * (fallGravityMultiplier - 1f) * Time.fixedDeltaTime);
        }

        // 2. Inside FixedUpdate(), modify the velocity calculation to lock movement while grounded and attacking:
        // Calculates X movement. If grounded and attacking, lock speed to 0. Otherwise, use input * speed.
        float currentMoveX = (IsAttacking && isGrounded) ? 0f : moveInput.x * moveSpeedX;
        // Calculates Z movement. If grounded and attacking, lock speed to 0. Otherwise, use input * speed.
        float currentMoveZ = (IsAttacking && isGrounded) ? 0f : moveInput.y * moveSpeedZ;

        // Overrides the earlier rb.linearVelocity assignment with the new attack-locked values, while preserving Y velocity
        rb.linearVelocity = new Vector3(
            currentMoveX,
            rb.linearVelocity.y,
            currentMoveZ
        );
    }

    // Another helper method to explicitly set the facing direction of the sprite
    private void FlipSprite(bool faceRight)
    {
        // Updates the internal boolean
        facingRight = faceRight;
        // Safety check
        if (characterSprite != null)
        {
            // Sets flipX to the opposite of the requested facing direction
            characterSprite.flipX = !facingRight;
        }
    }

    // Helper method to pass variables from the script into the Animator component
    private void UpdateAnimations()
    {
        // Exit early if there is no animator assigned
        if (animator == null) return;

        // Passes the length of the movement input vector (0 for idle, > 0 for walking) into the "Speed" parameter
        animator.SetFloat(SpeedHash, moveInput.magnitude);
        // Passes the true/false grounded state into the "IsGrounded" parameter to transition between grounded and aerial animations
        animator.SetBool(IsGroundedHash, isGrounded);
        // Passes the current Y velocity into the "VerticalVelocity" parameter to transition between jumping up and falling down animations
        animator.SetFloat(VerticalVelocityHash, rb.linearVelocity.y);
    }

    // Helper method to pin the visual drop shadow to the floor directly beneath the player
    private void UpdateShadowPosition()
    {
        // Exit early if there is no shadow assigned
        if (shadowTransform == null) return;

        // Raycast down from the player to keep the shadow pinned to the street while jumping
        // Sets the starting point of the raycast slightly above the player's center
        Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;
        // Shoots an invisible ray straight down. If it hits the groundLayer within 20 units...
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 20f, groundLayer, QueryTriggerInteraction.Ignore))
        {
            // Move the shadow sprite to the exact X and Z of the player, but lock its Y to the floor (plus a tiny offset)
            shadowTransform.position = new Vector3(
                transform.position.x,
                hit.point.y + shadowGroundOffset,
                transform.position.z
            );

            // Shrink the shadow slightly as the player jumps higher
            // Calculates how far the player is above the floor impact point
            float heightAboveGround = Mathf.Max(0f, transform.position.y - hit.point.y);
            // Calculates a shrink scale (from 1.0 down to 0.5) based on the height ratio
            float scaleFactor = Mathf.Lerp(1f, 0.5f, heightAboveGround / maxShadowHeight);
            // Applies the calculated scale to the shadow's original size
            shadowTransform.localScale = initialShadowScale * scaleFactor;
        }
    }

    // Unity's built-in editor method to draw visual debug guides in the Scene view when the object is clicked
    private void OnDrawGizmosSelected()
    {
        // Calculates the center of the ground check sphere
        Vector3 checkPos = groundCheckPoint != null ? groundCheckPoint.position : transform.position;
        // Sets the debug drawing color to green
        Gizmos.color = Color.green;
        // Draws a wireframe sphere showing exactly where the ground check is happening and how big it is
        Gizmos.DrawWireSphere(checkPos, groundCheckRadius);
    }
}