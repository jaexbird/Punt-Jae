using System.Collections;
using UnityEngine;
// Imports the Cinemachine namespace to control camera behaviors and screen shakes (updated for Unity 6 / Cinemachine 3.x)
using Unity.Cinemachine;

// Tells Unity to automatically add a CinemachineImpulseSource component to this GameObject if it's missing
[RequireComponent(typeof(CinemachineImpulseSource))]
// Declares the HitImpactManager class, inheriting from MonoBehaviour so it can be attached to a GameObject in the scene
public class HitImpactManager : MonoBehaviour
{
    // A public static property that creates a "Singleton" pattern
    // This allows any other script in the game to call HitImpactManager.Instance to use it without needing a direct reference
    public static HitImpactManager Instance { get; private set; }

    // A private reference to the component that generates the physical camera shake signals
    private CinemachineImpulseSource impulseSource;
    // A private reference tracking the currently active freeze-frame coroutine
    private Coroutine hitstopCoroutine;
    // A boolean flag tracking if the game's time is currently frozen
    private bool isFrozen;

    // Unity's built-in initialization method, called exactly once when the object is created (before Start)
    private void Awake()
    {
        // Singleton setup: Checks if an Instance already exists and if it's not this specific script
        if (Instance != null && Instance != this)
        {
            // If a duplicate exists, destroy this newly created duplicate GameObject immediately
            Destroy(gameObject);
            // Exit the Awake method so no further setup runs on the destroyed object
            return;
        }
        // If this is the first and only HitImpactManager, set the static Instance to 'this'
        Instance = this;

        // Grabs the CinemachineImpulseSource component attached to this GameObject
        impulseSource = GetComponent<CinemachineImpulseSource>();

        // Crucial for Unity 6: Allows Cinemachine shakes to play while Time.timeScale is 0
        // Without this, the camera shake would freeze in place just like the rest of the game during a hitstop
        CinemachineImpulseManager.Instance.IgnoreTimeScale = true;
    }

    // A public method that other scripts (like player/enemy attacks) can call to trigger an impact effect
    public void TriggerImpact(float hitstopDuration, float shakeForce)
    {
        // 1. Trigger Cinemachine camera shake
        // Checks if the impulse source exists and if the requested shake force is actually greater than 0
        if (impulseSource != null && shakeForce > 0f)
        {
            // Fires a camera shake signal with the specified intensity/force
            impulseSource.GenerateImpulseWithForce(shakeForce);
        }

        // 2. Trigger Hitstop freeze frames
        // Checks if the requested freeze duration is greater than 0
        if (hitstopDuration > 0f)
        {
            // If a hitstop is already currently happening...
            if (hitstopCoroutine != null)
            {
                // Stop the old coroutine so they don't overlap and cause weird timing issues
                StopCoroutine(hitstopCoroutine);
            }
            // Start a fresh Hitstop coroutine and store its reference
            hitstopCoroutine = StartCoroutine(HitstopRoutine(hitstopDuration));
        }
    }

    // A Coroutine method that handles the timing of freezing and unfreezing the game
    private IEnumerator HitstopRoutine(float duration)
    {
        // Mark the manager's state as frozen
        isFrozen = true;
        // Sets Unity's global time scale to 0, which pauses all physics, animations, and normal Update timers
        Time.timeScale = 0f;

        // Wait in real-world time so the timer still counts down while timeScale is 0
        // Normal WaitForSeconds relies on timeScale, so it would freeze forever. Realtime ignores timeScale.
        yield return new WaitForSecondsRealtime(duration);

        // After the wait is over, reset Unity's global time scale back to 1 (normal speed)
        Time.timeScale = 1f;
        // Mark the manager's state as no longer frozen
        isFrozen = false;
        // Clear the coroutine reference since it has finished naturally
        hitstopCoroutine = null;
    }

    // Unity's built-in cleanup method, called right before this GameObject is destroyed or the scene changes
    private void OnDestroy()
    {
        // Safety reset if the scene reloads mid-hitstop
        // Prevents a bug where the player restarts a level during a freeze-frame and the new level starts completely paused
        if (isFrozen)
        {
            // Restores time to normal
            Time.timeScale = 1f;
        }
    }
}