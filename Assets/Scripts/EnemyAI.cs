using System.Collections;
using UnityEngine;

// Forces Unity to automatically add a Rigidbody and EnemyHurtbox component when this script is added to a GameObject
[RequireComponent(typeof(Rigidbody), typeof(EnemyHurtbox))]
// Declares the EnemyAI class, inheriting from MonoBehaviour so it can be attached as a Unity component
public class EnemyAI : MonoBehaviour
{
    // A visual header in the Unity Inspector to group movement-related settings
    [Header("Movement & Z-Lane Tracking")]
    // The enemy's horizontal movement speed (left/right)
    [SerializeField] private float moveSpeedX = 3.5f;
    // The enemy's depth movement speed (up/down/in/out of the screen)
    [SerializeField] private float moveSpeedZ = 2.5f;
    // The maximum distance the player can be before the enemy starts chasing them
    [SerializeField] private float aggroRange = 14f;
    // Adds a hover-tooltip in the Inspector explaining the attackRangeX variable
    [Tooltip("Horizontal distance from the player where the enemy stops to punch")]
    // The desired X-axis distance from the player to initiate an attack
    [SerializeField] private float attackRangeX = 1.15f;
    // Adds a hover-tooltip explaining zLaneTolerance
    [Tooltip("How closely aligned on the Z-axis the enemy must be to throw a punch")]
    // How close the enemy needs to be to the player's exact Z-depth to attack (preventing attacks from the wrong depth)
    [SerializeField] private float zLaneTolerance = 0.35f;
    // How quickly the enemy decelerates when sliding across the ground after being knocked back
    [SerializeField] private float knockbackGroundDrag = 18f;

    // A visual header in the Inspector for grounding and drop shadow settings
    [Header("Ground & Shadow Settings")]
    // A specific empty GameObject/Transform placed at the enemy's feet to check for the ground
    [SerializeField] private Transform groundCheckPoint;
    // The radius of the invisible sphere used to detect the ground
    [SerializeField] private float groundCheckRadius = 0.2f;
    // Defines which physics layers count as solid "Ground"
    [SerializeField] private LayerMask groundLayer;
    // A reference to the visual drop-shadow sprite beneath the enemy
    [SerializeField] private Transform shadowTransform;
    // A slight vertical offset to prevent the shadow sprite from clipping (Z-fighting) into the floor
    [SerializeField] private float shadowGroundOffset = 0.02f;

    // A visual header in the Inspector for attack data
    [Header("Attack & Hitbox Settings")]
    // How much health the enemy removes from the player on a successful hit
    [SerializeField] private int attackDamage = 10;
    // The physical force (X, Y, Z) applied to the player when hit
    [SerializeField] private Vector3 attackKnockback = new Vector3(3f, 0f, 0f);
    // The delay in seconds before the punch actually lands (startup animation time)
    [SerializeField] private float windupTime = 0.35f;
    // How long in seconds the hitbox remains active and dangerous
    [SerializeField] private float activeTime = 0.12f;
    // The delay in seconds after a punch before the enemy can move again
    [SerializeField] private float recoveryTime = 0.4f;
    // The total time in seconds the enemy must wait before starting a new attack cycle
    [SerializeField] private float attackCooldown = 1.1f;
    // Where the attack hitbox is placed relative to the enemy's center
    [SerializeField] private Vector3 hitboxOffset = new Vector3(0.8f, 0.8f, 0f);
    // The width, height, and depth of the invisible attack hitbox
    [SerializeField] private Vector3 hitboxSize = new Vector3(1.0f, 1.0f, 0.5f);
    // Defines which physics layer the player is on so the hitbox only hits the player
    [SerializeField] private LayerMask playerLayer;

    // A visual header in the Inspector for visual/animation components
    [Header("Visuals References")]
    // A reference to a child GameObject that holds the sprite (allows for squash/stretch animations without moving the physics body)
    [SerializeField] private Transform visualsTransform;
    // The component that actually renders the 2D enemy image
    [SerializeField] private SpriteRenderer enemySprite;
    // The component that handles playing animations
    [SerializeField] private Animator animator;

    // Private reference to the physics body component
    private Rigidbody rb;
    // Private reference to the script handling this enemy's health and damage taken
    private EnemyHurtbox hurtbox;
    // Private reference tracking where the player is in the world
    private Transform playerTransform;

    // Tracks if the enemy is currently touching the floor
    private bool isGrounded;
    // Tracks if the enemy is currently in the middle of an attack animation/coroutine
    private bool isAttacking;
    // Tracks which direction the sprite is currently facing
    private bool facingRight = false;
    // A timer counting down until the enemy is allowed to attack again
    private float cooldownTimer;
    // Stores the calculated X and Z movement speed for the current frame
    private Vector2 desiredVelocityXZ;
    // Stores a reference to the running attack Coroutine so it can be stopped if the enemy gets hit
    private Coroutine attackCoroutine;

    // Stores the original local position of the visuals to reset them after squash/stretch attacks
    private Vector3 initialVisualsPos;
    // Stores the original local scale of the visuals to reset them
    private Vector3 initialVisualsScale;
    // Stores the original scale of the drop shadow
    private Vector3 initialShadowScale;

    // Caches the string "Speed" into an integer ID for faster Animator performance
    private static readonly int SpeedHash = Animator.StringToHash("Speed");

    // Unity's initialization method, called exactly once before the game starts
    private void Awake()
    {
        // Grabs the Rigidbody component attached to this GameObject
        rb = GetComponent<Rigidbody>();
        // Tells the physics engine to smooth out movement between frames for this body (prevents visual stuttering)
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        // Grabs the EnemyHurtbox component attached to this GameObject
        hurtbox = GetComponent<EnemyHurtbox>();

        // Checks if the visuals transform was assigned in the Inspector
        if (visualsTransform != null)
        {
            // Saves the starting position of the visuals child object
            initialVisualsPos = visualsTransform.localPosition;
            // Saves the starting size/scale of the visuals child object
            initialVisualsScale = visualsTransform.localScale;
        }

        // Checks if the shadow transform was assigned in the Inspector
        if (shadowTransform != null)
        {
            // Saves the starting size/scale of the shadow object
            initialShadowScale = shadowTransform.localScale;
        }
    }

    // Called automatically whenever this GameObject/script is turned on
    private void OnEnable()
    {
        // Subscribes the HandleHitInterrupted method to the hurtbox's interruption event
        // This means when the hurtbox says "I got hit!", HandleHitInterrupted will run automatically
        hurtbox.OnHitInterrupted += HandleHitInterrupted;
    }

    // Called automatically whenever this GameObject/script is turned off or destroyed
    private void OnDisable()
    {
        // Unsubscribes from the event to prevent memory leaks and errors when the enemy dies
        hurtbox.OnHitInterrupted -= HandleHitInterrupted;
    }

    // Called exactly once right before the first Update frame
    private void Start()
    {
        // Searches the entire active scene to find the one script controlling the player
        BeatEmUpPlayerController player = FindFirstObjectByType<BeatEmUpPlayerController>();
        // If the player was found successfully...
        if (player != null)
        {
            // Store a direct reference to the player's Transform (position data) for quick access later
            playerTransform = player.transform;
        }
    }

    // Called once every single visual frame (used for input, timers, and logic)
    private void Update()
    {
        // Calls the custom method to keep the drop shadow pinned to the floor directly beneath the enemy
        UpdateShadowPosition();

        // If the attack cooldown timer is running...
        if (cooldownTimer > 0f)
        {
            // Reduce the timer by the amount of time that passed since the last frame
            cooldownTimer -= Time.deltaTime;
        }

        // Safety/State Check: Abort movement/chase logic if there is no player, if the enemy is stunned, or if currently attacking
        if (playerTransform == null || hurtbox.IsInHitstun || isAttacking)
        {
            // Set intended movement speed to zero
            desiredVelocityXZ = Vector2.zero;
            // Tell the animator to stop playing the walk animation
            UpdateAnimator(0f);
            // Exit the Update method early
            return;
        }

        // Calculates the straight-line 3D distance between the enemy and the player
        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        // If the player is too far away to care about (outside aggro range)...
        if (distanceToPlayer > aggroRange)
        {
            // Stop moving
            desiredVelocityXZ = Vector2.zero;
            // Stop the walk animation
            UpdateAnimator(0f);
            // Exit the Update method early
            return;
        }

        // Calculates the pure horizontal (X-axis) distance to the player
        float deltaXToPlayer = playerTransform.position.x - transform.position.x;
        // If the player is not perfectly overlapping horizontally (0.05f deadzone)...
        if (Mathf.Abs(deltaXToPlayer) > 0.05f)
        {
            // Call the FlipSprite method to face left or right based on whether deltaX is positive or negative
            FlipSprite(deltaXToPlayer > 0f);
        }

        // Determines which side of the player the enemy is on (-1 for left, 1 for right)
        float sideSign = (transform.position.x < playerTransform.position.x) ? -1f : 1f;
        // Calculates the ideal X position to stand at: just outside the player's body on the current side
        float targetX = playerTransform.position.x + (sideSign * attackRangeX);
        // Calculates the ideal Z position to stand at: exactly the player's depth layer (Z-lane)
        float targetZ = playerTransform.position.z;

        // How far the enemy currently is from their ideal X position
        float diffX = targetX - transform.position.x;
        // How far the enemy currently is from their ideal Z position
        float diffZ = targetZ - transform.position.z;

        // Evaluates to true if the enemy is close enough on the X axis to punch
        bool inXRange = Mathf.Abs(deltaXToPlayer) <= (attackRangeX + 0.2f);
        // Evaluates to true if the enemy is close enough on the Z axis to punch
        bool inZLane = Mathf.Abs(diffZ) <= zLaneTolerance;

        // If the enemy is in position on both axes...
        if (inXRange && inZLane)
        {
            // Stop trying to move
            desiredVelocityXZ = Vector2.zero;
            // Stop the walk animation
            UpdateAnimator(0f);

            // If the attack is not on cooldown, and the enemy isn't mid-air...
            if (cooldownTimer <= 0f && isGrounded)
            {
                // Start the attack sequence coroutine and save a reference to it
                attackCoroutine = StartCoroutine(AttackRoutine());
            }
        }
        else
        {
            // The enemy is NOT in position, so calculate movement toward the target spots
            // Determines X movement direction (-1, 0, or 1) based on diffX
            float moveDirX = Mathf.Abs(diffX) > 0.1f ? Mathf.Sign(diffX) : 0f;
            // Determines Z movement direction (-1, 0, or 1) based on diffZ
            float moveDirZ = Mathf.Abs(diffZ) > 0.08f ? Mathf.Sign(diffZ) : 0f;

            // Combines X and Z directions into a 2D vector
            Vector2 moveDir = new Vector2(moveDirX, moveDirZ);
            // Normalizes the vector if moving diagonally so the enemy doesn't move faster diagonally
            if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();

            // Multiplies the normalized direction by the specific X and Z movement speeds to get final velocity
            desiredVelocityXZ = new Vector2(moveDir.x * moveSpeedX, moveDir.y * moveSpeedZ);
            // Tells the animator to play the walk animation based on how fast we are trying to move
            UpdateAnimator(moveDir.magnitude);
        }
    }

    // Called on a strict time interval, strictly for Physics calculations (moving the Rigidbody)
    private void FixedUpdate()
    {
        // Determines where to cast the ground check from (either the assigned point, or the enemy center)
        Vector3 checkPos = groundCheckPoint != null ? groundCheckPoint.position : transform.position;
        // Casts an invisible sphere; returns true if it overlaps with anything on the 'groundLayer'
        isGrounded = Physics.CheckSphere(checkPos, groundCheckRadius, groundLayer, QueryTriggerInteraction.Ignore);

        // If the enemy has recently been hit and is stunned...
        if (hurtbox.IsInHitstun)
        {
            // If the enemy is touching the ground and has stopped flying upward (y velocity <= 0.1)...
            if (isGrounded && rb.linearVelocity.y <= 0.1f)
            {
                // Smoothly reduce X-axis knockback sliding speed toward zero (simulating friction)
                float newX = Mathf.MoveTowards(rb.linearVelocity.x, 0f, knockbackGroundDrag * Time.fixedDeltaTime);
                // Smoothly reduce Z-axis knockback sliding speed toward zero
                float newZ = Mathf.MoveTowards(rb.linearVelocity.z, 0f, knockbackGroundDrag * Time.fixedDeltaTime);
                // Apply the newly slowed velocities back to the Rigidbody, maintaining current Y (falling) speed
                rb.linearVelocity = new Vector3(newX, rb.linearVelocity.y, newZ);
            }
            // Skip the normal movement code while stunned
            return;
        }

        // If the enemy is currently in the middle of an attack...
        if (isAttacking)
        {
            // Halt all horizontal movement immediately, but allow falling (Y velocity)
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            // Skip the normal movement code
            return;
        }

        // Apply the calculated chase movement speed to the Rigidbody, keeping the current Y (falling) speed
        rb.linearVelocity = new Vector3(desiredVelocityXZ.x, rb.linearVelocity.y, desiredVelocityXZ.y);
    }

    // A Coroutine method that handles the sequence and timing of a single attack
    private IEnumerator AttackRoutine()
    {
        // Lock the state machine so the enemy can't move or start another attack
        isAttacking = true;
        // Determine multiplier for local offsets (1 if facing right, -1 if facing left)
        float dir = facingRight ? 1f : -1f;

        // 1. Wind-up phase (visual telegraphing)
        if (visualsTransform != null)
        {
            // Shift the sprite slightly backwards
            visualsTransform.localPosition = initialVisualsPos + new Vector3(-0.12f * dir, 0f, 0f);
            // Squash the sprite down and make it slightly wider
            visualsTransform.localScale = new Vector3(initialVisualsScale.x * 0.9f, initialVisualsScale.y * 1.1f, initialVisualsScale.z);
        }
        // Pause the coroutine here for 'windupTime' seconds before continuing
        yield return new WaitForSeconds(windupTime);

        // 2. Active phase (the actual punch)
        if (visualsTransform != null)
        {
            // Shift the sprite forward to simulate a lunge
            visualsTransform.localPosition = initialVisualsPos + new Vector3(0.3f * dir, 0f, 0f);
            // Stretch the sprite out horizontally and flatten it vertically
            visualsTransform.localScale = new Vector3(initialVisualsScale.x * 1.2f, initialVisualsScale.y * 0.85f, initialVisualsScale.z);
        }

        // Call the custom method to spawn the invisible damage box and check for collisions with the player
        CheckHitboxOverlap(dir);
        // Pause the coroutine for 'activeTime' seconds to let the punch visual linger
        yield return new WaitForSeconds(activeTime);

        // 3. Recovery phase (pulling the arm back)
        // Reset the sprite position and scale to normal
        ResetVisuals();
        // Pause the coroutine for 'recoveryTime' seconds to leave the enemy temporarily vulnerable
        yield return new WaitForSeconds(recoveryTime);

        // Start the cooldown timer preventing the next attack
        cooldownTimer = attackCooldown;
        // Unlock the state machine so the enemy can chase again
        isAttacking = false;
        // Clear the coroutine reference since it has finished naturally
        attackCoroutine = null;
    }

    // Spawns a 3D box in space and damages any player caught inside it
    private void CheckHitboxOverlap(float dir)
    {
        // Calculates the center point of the damage box based on enemy position, offset, and facing direction
        Vector3 boxCenter = transform.position + new Vector3(hitboxOffset.x * dir, hitboxOffset.y, hitboxOffset.z);
        // Asks the physics engine for an array of all colliders on the 'playerLayer' that overlap with a box at that position and size
        Collider[] hits = Physics.OverlapBox(boxCenter, hitboxSize * 0.5f, Quaternion.identity, playerLayer, QueryTriggerInteraction.Ignore);

        // Loop through everything the box hit (usually just 0 or 1 player)
        foreach (Collider hit in hits)
        {
            // Try to find a PlayerHurtbox script on the object we hit
            if (hit.TryGetComponent(out PlayerHurtbox playerHurtbox))
            {
                // Calculate which direction to push the player based on which way the enemy is facing
                Vector3 finalKnockback = new Vector3(attackKnockback.x * dir, attackKnockback.y, attackKnockback.z);
                // Call the player's damage function, passing in damage amount and knockback force
                playerHurtbox.TakeDamage(attackDamage, finalKnockback);
                // Stop checking hits (we only want to hit the player once per punch)
                break;
            }
        }
    }

    // Called via event when the EnemyHurtbox takes damage
    private void HandleHitInterrupted()
    {
        // If an attack coroutine is currently running...
        if (attackCoroutine != null)
        {
            // Force the coroutine to stop immediately, canceling the attack
            StopCoroutine(attackCoroutine);
            // Clear the reference
            attackCoroutine = null;
        }
        // Force the sprite back to normal position/scale in case they were interrupted mid-squash
        ResetVisuals();
        // Unlock the attack state
        isAttacking = false;
        // Give the enemy a slight cooldown penalty before they can attack again after recovering from hitstun
        cooldownTimer = attackCooldown * 0.5f;
    }

    // Helper method to snap the sprite visuals back to their awake state
    private void ResetVisuals()
    {
        // Safety check
        if (visualsTransform != null)
        {
            // Restore local position
            visualsTransform.localPosition = initialVisualsPos;
            // Restore local scale
            visualsTransform.localScale = initialVisualsScale;
        }
    }

    // Helper method to flip the 2D sprite horizontally
    private void FlipSprite(bool faceRight)
    {
        // Update tracking variable
        facingRight = faceRight;
        // Safety check for sprite renderer
        if (enemySprite != null)
        {
            // Sets the flipX property. If facingRight is true, flipX is false. If facingRight is false (left), flipX is true.
            enemySprite.flipX = !facingRight;
        }
    }

    // Helper method to send speed data to the Animator component
    private void UpdateAnimator(float speed)
    {
        // Safety check
        if (animator != null)
        {
            // Updates the "Speed" parameter in the Unity Animator Controller, which triggers transition to Walk/Run animations
            animator.SetFloat(SpeedHash, speed);
        }
    }

    // Helper method to keep the visual drop shadow pinned to the floor, shrinking as the enemy jumps/flies upward
    private void UpdateShadowPosition()
    {
        // Abort if there is no shadow assigned
        if (shadowTransform == null) return;

        // Calculate a starting point slightly above the enemy's feet to shoot a laser straight down
        Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;
        // Shoot a raycast straight down from the origin. If it hits the 'groundLayer' within 20 units...
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 20f, groundLayer, QueryTriggerInteraction.Ignore))
        {
            // Move the shadow precisely to the impact point of the raycast (the floor), plus the slight vertical offset
            shadowTransform.position = new Vector3(
                transform.position.x,
                hit.point.y + shadowGroundOffset,
                transform.position.z
            );

            // Calculate how far the enemy's center is above the floor impact point
            float heightAboveGround = Mathf.Max(0f, transform.position.y - hit.point.y);
            // Calculate a shrink ratio (between 1.0 and 0.5) based on how high up they are (maxing out at 4 units high)
            float scaleFactor = Mathf.Lerp(1f, 0.5f, heightAboveGround / 4f);
            // Apply the shrink ratio to the shadow's original scale to simulate the shadow shrinking as the enemy gets higher
            shadowTransform.localScale = initialShadowScale * scaleFactor;
        }
    }

    // Unity's built-in editor method for drawing debug wireframes in the Scene view when the object is clicked
    private void OnDrawGizmosSelected()
    {
        // Determine offset direction based on facing
        float dir = facingRight ? 1f : -1f;
        // Calculate where the center of the hitbox will be
        Vector3 boxCenter = transform.position + new Vector3(hitboxOffset.x * dir, hitboxOffset.y, hitboxOffset.z);

        // Set the debug draw color to solid red if currently attacking, or faint yellow if idle
        Gizmos.color = isAttacking ? new Color(1f, 0f, 0f, 0.6f) : new Color(1f, 0.8f, 0f, 0.35f);
        // Draw a solid translucent box at the calculated attack position
        Gizmos.DrawCube(boxCenter, hitboxSize);
        // Draw a solid outline around the box for better visibility
        Gizmos.DrawWireCube(boxCenter, hitboxSize);
    }
}