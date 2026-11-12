using System.Collections;
// Required to use generic collections like HashSet to track multiple enemies
using System.Collections.Generic;
using UnityEngine;

// Forces Unity to automatically add a BoxCollider component if one doesn't exist on this GameObject
[RequireComponent(typeof(BoxCollider))]
// Declares the AttackHitbox class, inheriting from MonoBehaviour so it acts as a Unity component
public class AttackHitbox : MonoBehaviour
{
    // A private reference to the actual BoxCollider component used to detect hits
    private BoxCollider hitboxCollider;
    // A HashSet (a list that only allows unique items) used to keep track of every enemy hit during a single punch.
    // This prevents a single punch from damaging the same enemy multiple times over multiple frames.
    private readonly HashSet<EnemyHurtbox> hitTargetsThisSwing = new HashSet<EnemyHurtbox>();

    // Stores the original starting local position of the hitbox object
    private Vector3 initialLocalPos;
    // Stores the original starting local offset center of the BoxCollider itself
    private Vector3 initialColliderCenter;

    // Tracks how much damage the currently active attack should deal
    private int currentDamage = 10;
    // Tracks the physical force to apply to enemies when they are hit
    private Vector3 currentKnockback = Vector3.zero;
    // Tracks how long the game should pause when an enemy is hit
    private float currentHitstop = 0f;
    // Tracks how hard the camera should shake when an enemy is hit
    private float currentShakeForce = 0f;
    // Stores a reference to the active freeze-frame coroutine (used as a fallback if HitImpactManager is missing)
    private Coroutine hitstopCoroutine;

    // Unity's built-in initialization method, called exactly once when the object is created
    private void Awake()
    {
        // Grabs the BoxCollider component attached to this same GameObject
        hitboxCollider = GetComponent<BoxCollider>();
        // Sets the collider to be a 'Trigger', meaning it detects overlap but doesn't act as a solid wall
        hitboxCollider.isTrigger = true;
        // Ensures the hitbox is turned OFF immediately when the game starts
        hitboxCollider.enabled = false;

        // Saves the starting transform position so we can flip it left and right later
        initialLocalPos = transform.localPosition;
        // Saves the starting collider offset so we can flip it left and right later
        initialColliderCenter = hitboxCollider.center;
    }

    // A public method called by the PlayerCombat script exactly when a punch animation extends
    public void ActivateHitbox(int damage, Vector3 localKnockback, bool facingRight, float hitstopDuration = 0f, float shakeForce = 0f)
    {
        // Empties the list of hit enemies, ensuring a brand new punch can hit enemies that were hit by previous punches
        hitTargetsThisSwing.Clear();
        // Caches the damage value passed in by the combo data
        currentDamage = damage;
        // Caches the hitstop duration passed in by the combo data
        currentHitstop = hitstopDuration;
        // Caches the screen shake intensity passed in by the combo data
        currentShakeForce = shakeForce;

        // Determines a math multiplier: 1 if facing right, -1 if facing left
        float facingDirection = facingRight ? 1f : -1f;

        // Flips the hitbox's local position to match the player's facing direction
        // Mathf.Abs ensures we always start from a positive number before applying the direction multiplier
        transform.localPosition = new Vector3(
            Mathf.Abs(initialLocalPos.x) * facingDirection,
            initialLocalPos.y,
            initialLocalPos.z
        );
        // Flips the internal offset center of the BoxCollider to match the facing direction
        // (Changing scale to -1 can break physics/triggers, so shifting position and center is safer)
        hitboxCollider.center = new Vector3(
            Mathf.Abs(initialColliderCenter.x) * facingDirection,
            initialColliderCenter.y,
            initialColliderCenter.z
        );

        // Flips the horizontal knockback force so enemies always get knocked away from the player
        currentKnockback = new Vector3(
            localKnockback.x * facingDirection,
            localKnockback.y,
            localKnockback.z
        );

        // Actually turns the physics collider ON, allowing it to start detecting overlaps in the next frame
        hitboxCollider.enabled = true;
    }

    // A public method called by the PlayerCombat script when the punch animation retracts
    public void DeactivateHitbox()
    {
        // Turns the physics collider OFF, making the punch harmless again
        hitboxCollider.enabled = false;
        // Clears the list of hit enemies to free up memory
        hitTargetsThisSwing.Clear();
    }

    // Unity's built-in physics event, called automatically on any frame where this trigger overlaps another collider
    private void OnTriggerEnter(Collider other)
    {
        // Tries to find an EnemyHurtbox script on whatever the hitbox just touched
        if (other.TryGetComponent(out EnemyHurtbox hurtbox))
        {
            // Tries to add this enemy to the HashSet. 
            // .Add() returns true if the enemy wasn't in the list, and false if they were already in it.
            if (hitTargetsThisSwing.Add(hurtbox))
            {
                // If the enemy was successfully added (meaning they haven't been hit by THIS specific punch yet), deal damage
                hurtbox.TakeDamage(currentDamage, currentKnockback);

                // Check if this is the very first enemy hit by this swing (we don't want to stack 5 camera shakes if we hit 5 enemies at once)
                if (hitTargetsThisSwing.Count == 1)
                {
                    // Check if the centralized impact manager exists in the scene
                    if (HitImpactManager.Instance != null)
                    {
                        // Tell the manager to freeze the game and shake the camera
                        HitImpactManager.Instance.TriggerImpact(currentHitstop, currentShakeForce);
                    }
                    // Fallback: If there is no manager, but we still requested a freeze frame...
                    else if (currentHitstop > 0f)
                    {
                        // Stop any existing local hitstop coroutine to prevent bugs
                        if (hitstopCoroutine != null) StopCoroutine(hitstopCoroutine);
                        // Start a local hitstop coroutine just for this script
                        hitstopCoroutine = StartCoroutine(HitstopRoutine(currentHitstop));
                    }
                }
            }
        }
    }

    // A fallback Coroutine that freezes the game locally if HitImpactManager isn't used
    private IEnumerator HitstopRoutine(float duration)
    {
        // Pauses all physics and regular Update timers globally
        Time.timeScale = 0f;
        // Pauses this specific method for 'duration' seconds, counting using real-world time (ignores timeScale)
        yield return new WaitForSecondsRealtime(duration);
        // Restores game speed to normal
        Time.timeScale = 1f;
        // Clears the coroutine reference
        hitstopCoroutine = null;
    }

    // Called automatically if this script or GameObject is turned off or destroyed
    private void OnDisable()
    {
        // If the game was frozen via the fallback routine when this object was disabled...
        if (hitstopCoroutine != null)
        {
            // Force time back to normal so the game doesn't stay frozen forever
            Time.timeScale = 1f;
            // Clear the reference
            hitstopCoroutine = null;
        }
    }

    // Unity's built-in editor method to draw visual debug guides in the Scene view
    private void OnDrawGizmos()
    {
        // Gets the BoxCollider attached to this object
        BoxCollider col = GetComponent<BoxCollider>();
        // Failsafe if it doesn't exist
        if (col == null) return;

        // Sets the draw color to solid red if the hitbox is currently lethal (enabled), or faint orange if it's inactive
        Gizmos.color = col.enabled ? new Color(1f, 0f, 0f, 0.5f) : new Color(1f, 0.5f, 0f, 0.2f);
        // Aligns the drawing grid with the GameObject's position, rotation, and scale
        Gizmos.matrix = transform.localToWorldMatrix;
        // Draws a translucent solid box exactly where the BoxCollider is
        Gizmos.DrawCube(col.center, col.size);
        // Draws a solid outline around the box to make the edges clear
        Gizmos.DrawWireCube(col.center, col.size);
    }
}