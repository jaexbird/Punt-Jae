using System.Collections;
using UnityEngine;
using Unity.Cinemachine; // Unity 6 / Cinemachine 3.x namespace

[RequireComponent(typeof(CinemachineImpulseSource))]
public class HitImpactManager : MonoBehaviour
{
    public static HitImpactManager Instance { get; private set; }

    private CinemachineImpulseSource impulseSource;
    private Coroutine hitstopCoroutine;
    private bool isFrozen;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        impulseSource = GetComponent<CinemachineImpulseSource>();

        // Crucial for Unity 6: Allows Cinemachine shakes to play while Time.timeScale is 0
        CinemachineImpulseManager.Instance.IgnoreTimeScale = true;
    }

    public void TriggerImpact(float hitstopDuration, float shakeForce)
    {
        // 1. Trigger Cinemachine camera shake
        if (impulseSource != null && shakeForce > 0f)
        {
            impulseSource.GenerateImpulseWithForce(shakeForce);
        }

        // 2. Trigger Hitstop freeze frames
        if (hitstopDuration > 0f)
        {
            if (hitstopCoroutine != null)
            {
                StopCoroutine(hitstopCoroutine);
            }
            hitstopCoroutine = StartCoroutine(HitstopRoutine(hitstopDuration));
        }
    }

    private IEnumerator HitstopRoutine(float duration)
    {
        isFrozen = true;
        Time.timeScale = 0f;

        // Wait in real-world time so the timer still counts down while timeScale is 0
        yield return new WaitForSecondsRealtime(duration);

        Time.timeScale = 1f;
        isFrozen = false;
        hitstopCoroutine = null;
    }

    private void OnDestroy()
    {
        // Safety reset if the scene reloads mid-hitstop
        if (isFrozen)
        {
            Time.timeScale = 1f;
        }
    }
}