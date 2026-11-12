using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;

[System.Serializable]
public struct EnemyWave
{
    public string waveName;
    public GameObject enemyPrefab;
    public int enemyCount;
    public float spawnInterval;
}

[RequireComponent(typeof(BoxCollider))]
public class BattleZoneWaveSpawner : MonoBehaviour
{
    [Header("Camera & Arena Barriers")]
    [SerializeField] private CinemachineCamera cinemachineCamera;
    [SerializeField] private Transform cameraLockPoint;
    [SerializeField] private BoxCollider leftLockWall;
    [SerializeField] private BoxCollider rightLockWall;

    [Header("Wave Configuration")]
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField]
    private EnemyWave[] waves = new EnemyWave[2]
    {
        new EnemyWave { waveName = "Wave 1", enemyCount = 2, spawnInterval = 0.4f },
        new EnemyWave { waveName = "Wave 2", enemyCount = 3, spawnInterval = 0.4f }
    };
    [SerializeField] private float delayBetweenWaves = 1.0f;

    private BoxCollider triggerCollider;
    private Transform originalCameraTarget;
    private readonly List<EnemyHurtbox> aliveEnemies = new List<EnemyHurtbox>();
    private bool hasTriggered = false;

    private void Awake()
    {
        triggerCollider = GetComponent<BoxCollider>();
        triggerCollider.isTrigger = true;

        // Keep arena lock walls disabled until the player enters the zone
        if (leftLockWall != null) leftLockWall.enabled = false;
        if (rightLockWall != null) rightLockWall.enabled = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasTriggered) return;

        if (other.GetComponent<BeatEmUpPlayerController>() != null)
        {
            hasTriggered = true;
            triggerCollider.enabled = false;
            StartCoroutine(ArenaEncounterRoutine());
        }
    }

    private IEnumerator ArenaEncounterRoutine()
    {
        // 1. Lock the camera to the arena center and turn on left/right walls
        if (cinemachineCamera != null && cameraLockPoint != null)
        {
            originalCameraTarget = cinemachineCamera.Follow;
            cinemachineCamera.Follow = cameraLockPoint;
        }

        if (leftLockWall != null) leftLockWall.enabled = true;
        if (rightLockWall != null) rightLockWall.enabled = true;

        Debug.Log("Battle Zone Locked! Defeat all enemies!");

        // 2. Loop through each wave
        for (int w = 0; w < waves.Length; w++)
        {
            EnemyWave currentWave = waves[w];
            Debug.Log($"Spawning {currentWave.waveName}...");

            for (int i = 0; i < currentWave.enemyCount; i++)
            {
                SpawnEnemy(currentWave.enemyPrefab, i);

                if (currentWave.spawnInterval > 0f)
                {
                    yield return new WaitForSeconds(currentWave.spawnInterval);
                }
            }

            // Wait here until every enemy in the current wave is defeated
            yield return new WaitUntil(() => aliveEnemies.Count == 0);

            if (w < waves.Length - 1)
            {
                yield return new WaitForSeconds(delayBetweenWaves);
            }
        }

        // 3. All waves cleared: unlock barriers and return camera to the player
        if (leftLockWall != null) leftLockWall.enabled = false;
        if (rightLockWall != null) rightLockWall.enabled = false;

        if (cinemachineCamera != null && originalCameraTarget != null)
        {
            cinemachineCamera.Follow = originalCameraTarget;
        }

        Debug.Log("All waves defeated! GO! >>");
    }

    private void SpawnEnemy(GameObject prefab, int index)
    {
        if (prefab == null || spawnPoints == null || spawnPoints.Length == 0) return;

        Transform spawnPoint = spawnPoints[index % spawnPoints.Length];
        GameObject enemyObj = Instantiate(prefab, spawnPoint.position, Quaternion.identity);

        if (enemyObj.TryGetComponent(out EnemyHurtbox hurtbox))
        {
            aliveEnemies.Add(hurtbox);
            hurtbox.OnEnemyDied += HandleEnemyDied;
        }
    }

    private void HandleEnemyDied(EnemyHurtbox defeatedEnemy)
    {
        defeatedEnemy.OnEnemyDied -= HandleEnemyDied;
        aliveEnemies.Remove(defeatedEnemy);
    }

    private void OnDrawGizmos()
    {
        BoxCollider col = GetComponent<BoxCollider>();
        if (col != null)
        {
            Gizmos.color = new Color(1f, 0.9f, 0f, 0.25f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(col.center, col.size);
            Gizmos.DrawWireCube(col.center, col.size);
        }
    }
}