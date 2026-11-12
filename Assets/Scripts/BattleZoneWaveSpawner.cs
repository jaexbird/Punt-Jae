using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;

// Tells Unity to make this custom struct visible and editable in the Inspector window
[System.Serializable]
// Defines a custom data structure to hold the configuration for a single wave of enemies
public struct EnemyWave
{
    // A friendly name for the wave (e.g., "Wave 1") to help organize in the Inspector
    public string waveName;
    // The specific enemy GameObject (prefab) that will be spawned during this wave
    public GameObject enemyPrefab;
    // The total number of enemies to spawn in this wave
    public int enemyCount;
    // The time delay (in seconds) between spawning each individual enemy in this wave
    public float spawnInterval;
}

// Automatically adds a BoxCollider component to the GameObject if it doesn't already have one
[RequireComponent(typeof(BoxCollider))]
// Declares the main spawner class, inheriting from Unity's base component class (MonoBehaviour)
public class BattleZoneWaveSpawner : MonoBehaviour
{
    // Creates a bold label in the Unity Inspector to organize camera and wall settings
    [Header("Camera & Arena Barriers")]
    // Exposes the Cinemachine virtual camera to the Inspector while keeping it private to this script
    [SerializeField] private CinemachineCamera cinemachineCamera;
    // The specific transform/point the camera should look at or follow during the arena fight
    [SerializeField] private Transform cameraLockPoint;
    // The invisible wall collider on the left side to prevent the player from leaving
    [SerializeField] private BoxCollider leftLockWall;
    // The invisible wall collider on the right side to prevent the player from leaving
    [SerializeField] private BoxCollider rightLockWall;

    // Creates a bold label in the Inspector for wave-related settings
    [Header("Wave Configuration")]
    // An array of transforms representing the specific physical locations where enemies can spawn
    [SerializeField] private Transform[] spawnPoints;
    // Exposes the array of enemy waves to the Inspector and sets a default size of 2
    [SerializeField]
    private EnemyWave[] waves = new EnemyWave[2]
    {
        // Initializes the first default wave with 2 enemies and a 0.4s spawn delay
        new EnemyWave { waveName = "Wave 1", enemyCount = 2, spawnInterval = 0.4f },
        // Initializes the second default wave with 3 enemies and a 0.4s spawn delay
        new EnemyWave { waveName = "Wave 2", enemyCount = 3, spawnInterval = 0.4f }
    };
    // The time (in seconds) to wait after all enemies in a wave die before starting the next wave
    [SerializeField] private float delayBetweenWaves = 1.0f;

    // A private reference to the BoxCollider used to detect when the player enters the zone
    private BoxCollider triggerCollider;
    // Stores the camera's original target so it can be restored after the battle is over
    private Transform originalCameraTarget;
    // A dynamic list tracking all currently living enemies in the active wave
    private readonly List<EnemyHurtbox> aliveEnemies = new List<EnemyHurtbox>();
    // A boolean flag to ensure the arena sequence only triggers once
    private bool hasTriggered = false;

    // Unity's built-in initialization method, called once before the game starts (before Start)
    private void Awake()
    {
        // Grabs the BoxCollider component attached to this GameObject and stores it in the variable
        triggerCollider = GetComponent<BoxCollider>();
        // Ensures the collider acts as an overlapping trigger zone rather than a solid physical wall
        triggerCollider.isTrigger = true;

        // Checks if a left wall was assigned, and if so, turns its collision off initially
        if (leftLockWall != null) leftLockWall.enabled = false;
        // Checks if a right wall was assigned, and if so, turns its collision off initially
        if (rightLockWall != null) rightLockWall.enabled = false;
    }

    // Unity's built-in physics method, called automatically when another collider enters this trigger
    private void OnTriggerEnter(Collider other)
    {
        // If the battle sequence has already been triggered, exit the method immediately
        if (hasTriggered) return;

        // Checks if the object entering the trigger has the player's controller script attached
        if (other.GetComponent<BeatEmUpPlayerController>() != null)
        {
            // Marks the battle as triggered so it cannot be activated again
            hasTriggered = true;
            // Disables the trigger zone so it stops detecting collisions
            triggerCollider.enabled = false;
            // Starts the Coroutine that handles the time-based sequence of the arena battle
            StartCoroutine(ArenaEncounterRoutine());
        }
    }

    // A Coroutine method that allows execution to pause (yield) over time without freezing the game
    private IEnumerator ArenaEncounterRoutine()
    {
        // 1. Lock the camera to the arena center and turn on left/right walls
        // Checks if both the camera and the lock point were properly assigned in the Inspector
        if (cinemachineCamera != null && cameraLockPoint != null)
        {
            // Saves the object the camera was originally following (usually the player)
            originalCameraTarget = cinemachineCamera.Follow;
            // Changes the camera's follow target to the fixed arena center point
            cinemachineCamera.Follow = cameraLockPoint;
        }

        // Enables the left invisible wall to trap the player
        if (leftLockWall != null) leftLockWall.enabled = true;
        // Enables the right invisible wall to trap the player
        if (rightLockWall != null) rightLockWall.enabled = true;

        // Prints a debug message to the Unity console for developer tracking
        Debug.Log("Battle Zone Locked! Defeat all enemies!");

        // 2. Loop through each wave
        // A for-loop that iterates through every wave defined in the 'waves' array
        for (int w = 0; w < waves.Length; w++)
        {
            // Gets the current wave data based on the loop index
            EnemyWave currentWave = waves[w];
            // Prints the name of the starting wave to the console
            Debug.Log($"Spawning {currentWave.waveName}...");

            // An inner for-loop that repeats based on the number of enemies specified in this wave
            for (int i = 0; i < currentWave.enemyCount; i++)
            {
                // Calls the custom method to create the enemy, passing the prefab and the loop index
                SpawnEnemy(currentWave.enemyPrefab, i);

                // Checks if there is a delay specified between enemy spawns
                if (currentWave.spawnInterval > 0f)
                {
                    // Pauses the coroutine execution for the specified number of seconds before continuing the loop
                    yield return new WaitForSeconds(currentWave.spawnInterval);
                }
            }

            // Wait here until every enemy in the current wave is defeated
            // Pauses the coroutine indefinitely until the condition (list count equals 0) evaluates to true
            yield return new WaitUntil(() => aliveEnemies.Count == 0);

            // Checks if there are more waves remaining after this one
            if (w < waves.Length - 1)
            {
                // Pauses execution to give the player a brief breather before the next wave starts
                yield return new WaitForSeconds(delayBetweenWaves);
            }
        }

        // 3. All waves cleared: unlock barriers and return camera to the player
        // Disables the left wall so the player can move forward
        if (leftLockWall != null) leftLockWall.enabled = false;
        // Disables the right wall so the player can move forward
        if (rightLockWall != null) rightLockWall.enabled = false;

        // Checks if the camera and the original target are valid
        if (cinemachineCamera != null && originalCameraTarget != null)
        {
            // Restores the camera's follow target back to the player
            cinemachineCamera.Follow = originalCameraTarget;
        }

        // Prints a victory message to the console indicating the player can proceed
        Debug.Log("All waves defeated! GO! >>");
    }

    // A custom method to instantiate an enemy at a specific spawn point
    private void SpawnEnemy(GameObject prefab, int index)
    {
        // Safety check: aborts if no prefab is provided or if no spawn points exist
        if (prefab == null || spawnPoints == null || spawnPoints.Length == 0) return;

        // Determines which spawn point to use by getting the remainder of index divided by total spawn points (cycles through them)
        Transform spawnPoint = spawnPoints[index % spawnPoints.Length];
        // Creates a new instance of the enemy prefab at the chosen spawn point's position with no rotation
        GameObject enemyObj = Instantiate(prefab, spawnPoint.position, Quaternion.identity);

        // Attempts to get the EnemyHurtbox component from the newly spawned enemy
        if (enemyObj.TryGetComponent(out EnemyHurtbox hurtbox))
        {
            // Adds the successfully retrieved hurtbox to the tracking list
            aliveEnemies.Add(hurtbox);
            // Subscribes the HandleEnemyDied method to the enemy's death event so we know when it dies
            hurtbox.OnEnemyDied += HandleEnemyDied;
        }
    }

    // The method that gets called automatically via the event when an enemy's health reaches zero
    private void HandleEnemyDied(EnemyHurtbox defeatedEnemy)
    {
        // Unsubscribes from the death event to prevent memory leaks or calling this multiple times
        defeatedEnemy.OnEnemyDied -= HandleEnemyDied;
        // Removes the defeated enemy from the active tracking list
        aliveEnemies.Remove(defeatedEnemy);
    }

    // Unity's built-in editor method for drawing visual debug aids in the Scene view
    private void OnDrawGizmos()
    {
        // Gets the BoxCollider attached to the spawner
        BoxCollider col = GetComponent<BoxCollider>();
        // Checks if the collider actually exists
        if (col != null)
        {
            // Sets the drawing color to a semi-transparent yellow
            Gizmos.color = new Color(1f, 0.9f, 0f, 0.25f);
            // Aligns the gizmo's position and rotation with the GameObject's current transform matrix
            Gizmos.matrix = transform.localToWorldMatrix;
            // Draws a solid semi-transparent cube matching the trigger collider's bounds
            Gizmos.DrawCube(col.center, col.size);
            // Draws a solid wireframe outline around the cube for better visibility
            Gizmos.DrawWireCube(col.center, col.size);
        }
    }
}