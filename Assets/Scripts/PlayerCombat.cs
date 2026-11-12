// Imports the standard system collections namespace, needed for using IEnumerator and Coroutines
using System.Collections;
// Imports the core Unity engine API
using UnityEngine;

// Tells Unity to make this custom struct visible and editable in the Unity Inspector
[System.Serializable]
// Defines a data structure to hold all the unique properties of a single attack in a combo
public struct ComboAttackData
{
    // The exact name of the Animator trigger parameter to play this specific attack animation
    public string animationTriggerName;
    // How much health this specific attack removes from enemies
    public int damage;
    // The 3D physical force applied to the enemy when hit by this attack
    public Vector3 knockbackVelocity;
    // The delay in seconds before the punch actually lands (wind-up time)
    public float startupTime;
    // How long in seconds the hitbox remains active and capable of dealing damage
    public float activeTime;
    // How long in seconds the player is stuck recovering after the punch finishes
    public float recoveryTime;

    // A visual header in the Inspector for hit feel settings
    [Header("Impact Feel")]
    // Hover tooltip explaining the math behind hitstop frames
    [Tooltip("Freeze duration in seconds (0.05 = ~3 frames, 0.14 = ~8 frames)")]
    // How long the game completely pauses when this attack hits an enemy (adds weight to the hit)
    public float hitstopDuration;
    // Hover tooltip explaining the camera shake setting
    [Tooltip("Cinemachine Impulse force multiplier")]
    // How intensely the camera shakes when this attack connects
    public float shakeForce;

    // A visual header in the Inspector for procedural animation settings
    [Header("Procedural Lunge Visuals")]
    // How far forward the player physically lunges during this specific punch
    public float lungeDistance;
    // How much the player's sprite rotates/tilts forward or backward during the punch
    public float punchTiltAngle;
    // How the player's sprite squashes and stretches during the punch
    public Vector3 punchScale;
}

// Automatically adds a BeatEmUpPlayerController to the GameObject if it's missing, since this script relies on it
[RequireComponent(typeof(BeatEmUpPlayerController))]
// Declares the PlayerCombat class, inheriting from MonoBehaviour so it can act as a Unity component
public class PlayerCombat : MonoBehaviour
{
    // Creates a bold label in the Inspector for component references
    [Header("References")]
    // The transform acting as the center/root of the attack hitboxes
    [SerializeField] private Transform hitboxPivot;
    // A reference to the script managing the actual physics collision box for the attack
    [SerializeField] private AttackHitbox attackHitbox;
    // A reference to the Animator component playing the player's sprite animations
    [SerializeField] private Animator animator;
    // A child object holding the sprite (allows for procedural rotation/scaling without breaking physics)
    [SerializeField] private Transform visualsTransform;
    // A reference to a secondary sprite used to flash a "swoosh" or impact effect during the punch
    [SerializeField] private SpriteRenderer strikeEffectSprite;

    // Creates a bold label in the Inspector for combo arrays
    [Header("Combo Configuration")]
    // Exposes the array to the Inspector and initializes it with exactly 3 predefined attacks
    [SerializeField]
    private ComboAttackData[] lightCombo = new ComboAttackData[3]
    {
        // First punch data: Fast startup, low damage, light knockback, slight lunge
        new ComboAttackData {
            animationTriggerName = "Attack1", damage = 10, knockbackVelocity = new Vector3(1.5f, 0f, 0f),
            startupTime = 0.06f, activeTime = 0.1f, recoveryTime = 0.18f, hitstopDuration = 0.05f, shakeForce = 0.25f,
            lungeDistance = 0.2f, punchTiltAngle = -8f, punchScale = new Vector3(1.15f, 0.9f, 1f)
        },
        // Second punch data: Slightly slower, more damage, opposite tilt to look like the other arm
        new ComboAttackData {
            animationTriggerName = "Attack2", damage = 12, knockbackVelocity = new Vector3(2.0f, 0f, 0f),
            startupTime = 0.07f, activeTime = 0.1f, recoveryTime = 0.20f, hitstopDuration = 0.06f, shakeForce = 0.35f,
            lungeDistance = 0.28f, punchTiltAngle = 10f, punchScale = new Vector3(0.9f, 1.15f, 1f)
        },
        // Third punch data (Combo Finisher): Slowest startup, massive damage, heavy knockback, huge hitstop/shake
        new ComboAttackData {
            animationTriggerName = "Attack3", damage = 25, knockbackVelocity = new Vector3(6.5f, 5.5f, 0f),
            startupTime = 0.10f, activeTime = 0.14f, recoveryTime = 0.30f, hitstopDuration = 0.14f, shakeForce = 1.0f,
            lungeDistance = 0.45f, punchTiltAngle = -18f, punchScale = new Vector3(1.3f, 0.8f, 1f)
        }
    };

    // Private reference to the main player controller script
    private BeatEmUpPlayerController playerController;
    // Tracks which step of the combo the player is currently on (0, 1, or 2)
    private int currentComboIndex = 0;
    // Tracks if the current attack has progressed enough to allow the player to queue up the next attack
    private bool canBufferInput = false;
    // Tracks if the player pressed the attack button early (meaning the next attack should fire immediately after this one)
    private bool inputBuffered = false;

    // Stores the starting local position of the visuals to revert to after a punch lunge
    private Vector3 initialVisualsPos;
    // Stores the starting local rotation of the visuals to revert to after tilting
    private Quaternion initialVisualsRot;
    // Stores the starting local scale of the visuals to revert to after squash/stretch
    private Vector3 initialVisualsScale;
    // Stores the starting local position of the strike effect sprite
    private Vector3 initialStrikeEffectPos;

    // Unity's built-in initialization method, called exactly once when the object is created
    private void Awake()
    {
        // Grabs the BeatEmUpPlayerController attached to this same GameObject
        playerController = GetComponent<BeatEmUpPlayerController>();

        // Failsafe check: ensures the attackHitbox was assigned in the Inspector
        if (attackHitbox == null)
        {
            // Logs a red error in the Unity console if it's missing
            Debug.LogError("PlayerCombat requires an AttackHitbox reference.", this);
            // Disables this script entirely to prevent fatal crashes
            enabled = false;
            // Exits Awake early
            return;
        }

        // Checks if a visuals transform was assigned
        if (visualsTransform != null)
        {
            // Saves the default position, rotation, and scale of the visuals child object
            initialVisualsPos = visualsTransform.localPosition;
            initialVisualsRot = visualsTransform.localRotation;
            initialVisualsScale = visualsTransform.localScale;
        }

        // Checks if a strike effect sprite was assigned
        if (strikeEffectSprite != null)
        {
            // Saves its default position
            initialStrikeEffectPos = strikeEffectSprite.transform.localPosition;
            // Ensures the strike effect is invisible when the game starts
            strikeEffectSprite.enabled = false;
        }
    }

    // Unity's built-in method called once every frame
    private void Update()
    {
        // Listens for input: either the default virtual "Fire1" button (Left Mouse / Controller X) or the 'J' key
        if (Input.GetButtonDown("Fire1") || Input.GetKeyDown(KeyCode.J))
        {
            // If pressed, triggers the attack input logic
            HandleAttackInput();
        }
    }

    // Handles logic for starting attacks and buffering combo inputs
    private void HandleAttackInput()
    {
        // Failsafe: exit immediately if the combo array is completely empty or missing
        if (lightCombo == null || lightCombo.Length == 0)
        {
            return;
        }

        // If the player is NOT currently attacking...
        if (!playerController.IsAttacking)
        {
            // Reset the combo index back to the first punch (0)
            currentComboIndex = 0;
            // Start the coroutine that handles the exact timing sequence of the attack
            StartCoroutine(ExecuteAttackStep(currentComboIndex));
        }
        // Else, if they ARE attacking, BUT the current frame allows buffering, AND they haven't reached the end of the combo...
        else if (canBufferInput && currentComboIndex < lightCombo.Length - 1)
        {
            // Mark the input as buffered. The currently running coroutine will see this flag and auto-start the next punch later
            inputBuffered = true;
        }
    }

    // A Coroutine that handles the timing (windup, active, recovery) of a single punch in the combo
    private IEnumerator ExecuteAttackStep(int stepIndex)
    {
        // Safety check: abort if the requested index doesn't exist in the array
        if (lightCombo == null || stepIndex < 0 || stepIndex >= lightCombo.Length)
        {
            // Unlock the player state
            playerController.IsAttacking = false;
            // Exit the coroutine immediately
            yield break;
        }

        // Lock the player state machine so they can't walk/jump while attacking
        playerController.IsAttacking = true;
        // Reset buffering flags at the start of a new attack
        canBufferInput = false;
        inputBuffered = false;

        // Grab the specific attack data from the array for the current combo step
        ComboAttackData attack = lightCombo[stepIndex];
        // Check which direction the player is currently facing via the controller script
        bool facingRight = playerController.FacingRight;
        // Convert the boolean direction into a math multiplier (1 for right, -1 for left)
        float direction = facingRight ? 1f : -1f;

        // If we have an animator and a valid trigger string...
        if (animator != null && !string.IsNullOrEmpty(attack.animationTriggerName))
        {
            // Tell the Animator to transition to this specific attack animation
            animator.SetTrigger(attack.animationTriggerName);
        }

        // 1. WIND-UP PHASE (Anticipation)
        if (visualsTransform != null)
        {
            // Physically pull the sprite backwards slightly opposite of the facing direction to telegraph the punch
            visualsTransform.localPosition = initialVisualsPos + new Vector3(-0.08f * direction, 0f, 0f);
            // Squash the sprite down and pull it wider for anticipation
            visualsTransform.localScale = new Vector3(
                initialVisualsScale.x * 0.9f,
                initialVisualsScale.y * 1.1f,
                initialVisualsScale.z);
        }

        // Pause the coroutine here for the 'startupTime' duration
        yield return new WaitForSeconds(attack.startupTime);


        // 2. ACTIVE PHASE (The punch connects)
        // Now that the punch is flying out, allow the player to press the button to buffer the next attack
        canBufferInput = true;

        if (visualsTransform != null)
        {
            // Thrust the sprite forward by the lunge distance defined in the combo data
            visualsTransform.localPosition =
                initialVisualsPos + new Vector3(attack.lungeDistance * direction, 0f, 0f);
            // Rotate the sprite forward or backward to simulate a heavy swinging motion
            visualsTransform.localRotation =
                initialVisualsRot * Quaternion.Euler(0f, 0f, attack.punchTiltAngle * direction);
            // Apply the specific squash/stretch scale defined for this attack
            visualsTransform.localScale = Vector3.Scale(initialVisualsScale, attack.punchScale);
        }

        if (strikeEffectSprite != null)
        {
            // Position the swoosh effect slightly in front of the player, correctly aligned to the facing direction
            strikeEffectSprite.transform.localPosition = new Vector3(
                Mathf.Abs(initialStrikeEffectPos.x) * direction,
                initialStrikeEffectPos.y,
                initialStrikeEffectPos.z);
            // Flip the swoosh sprite appropriately
            strikeEffectSprite.flipX = !facingRight;
            // Make the swoosh effect visible
            strikeEffectSprite.enabled = true;
        }

        // Send all the attack data into the hitbox script and turn the collision box ON
        attackHitbox.ActivateHitbox(
            attack.damage,
            attack.knockbackVelocity,
            facingRight,
            attack.hitstopDuration,
            attack.shakeForce);

        // Pause the coroutine here for the duration that the hitbox remains lethal
        yield return new WaitForSeconds(attack.activeTime);


        // 3. RECOVERY PHASE (Pulling the arm back)
        // Turn the physics collision box OFF, it can no longer deal damage
        attackHitbox.DeactivateHitbox();

        // Hide the swoosh effect
        if (strikeEffectSprite != null)
        {
            strikeEffectSprite.enabled = false;
        }

        // Store the exact position, rotation, and scale the visuals are currently at from the lunge
        Vector3 recoveryStartPos = visualsTransform != null
            ? visualsTransform.localPosition
            : Vector3.zero;
        Quaternion recoveryStartRot = visualsTransform != null
            ? visualsTransform.localRotation
            : Quaternion.identity;
        Vector3 recoveryStartScale = visualsTransform != null
            ? visualsTransform.localScale
            : Vector3.one;

        // Initialize a timer to smoothly interpolate the visuals back to neutral
        float timer = 0f;
        // Loop every single frame until the recovery time completes
        while (timer < attack.recoveryTime)
        {
            if (visualsTransform != null)
            {
                // Calculate what percentage (0.0 to 1.0) of the recovery time has passed
                float t = attack.recoveryTime > 0f ? timer / attack.recoveryTime : 1f;
                // Smoothly slide the position from the lunge spot back to the neutral starting spot
                visualsTransform.localPosition = Vector3.Lerp(recoveryStartPos, initialVisualsPos, t);
                // Smoothly rotate back to neutral
                visualsTransform.localRotation = Quaternion.Slerp(recoveryStartRot, initialVisualsRot, t);
                // Smoothly scale back to normal proportions
                visualsTransform.localScale = Vector3.Lerp(recoveryStartScale, initialVisualsScale, t);
            }

            // CHECK FOR COMBO CHAINING
            // If the player pressed the button during the active phase, AND this isn't the final punch...
            if (inputBuffered && stepIndex < lightCombo.Length - 1)
            {
                // Instantly snap the visuals to neutral so the next punch starts clean
                ResetVisuals();
                // Move to the next combo step
                currentComboIndex = stepIndex + 1;
                // Start a brand new coroutine for the next punch
                StartCoroutine(ExecuteAttackStep(currentComboIndex));
                // Terminate this current coroutine early so they don't overlap
                yield break;
            }

            // Increment the timer by the time elapsed since the last frame
            timer += Time.deltaTime;
            // Yielding 'null' pauses the coroutine until the exact next frame, allowing the while-loop to animate over time
            yield return null;
        }

        // 4. FULL RECOVERY (Attack finishes completely without another combo input)
        // Failsafe to snap visuals back exactly to neutral in case of minor floating-point math errors
        ResetVisuals();
        // Prevent buffering from carrying over
        canBufferInput = false;
        // Unlock the player controller so they can walk, jump, or start a new combo from step 0
        playerController.IsAttacking = false;
        // Reset the combo sequence tracker
        currentComboIndex = 0;
    }

    // Helper method to completely restore the visual hierarchy to its original Awake state
    private void ResetVisuals()
    {
        if (visualsTransform != null)
        {
            visualsTransform.localPosition = initialVisualsPos;
            visualsTransform.localRotation = initialVisualsRot;
            visualsTransform.localScale = initialVisualsScale;
        }
    }
}