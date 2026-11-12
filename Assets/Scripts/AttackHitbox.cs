using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class AttackHitbox : MonoBehaviour
{
    private BoxCollider hitboxCollider;
    private readonly HashSet<EnemyHurtbox> hitTargetsThisSwing = new HashSet<EnemyHurtbox>();

    private Vector3 initialLocalPos;
    private Vector3 initialColliderCenter;

    private int currentDamage = 10;
    private Vector3 currentKnockback = Vector3.zero;
    private float currentHitstop = 0f;
    private float currentShakeForce = 0f;
    private Coroutine hitstopCoroutine;

    private void Awake()
    {
        hitboxCollider = GetComponent<BoxCollider>();
        hitboxCollider.isTrigger = true;
        hitboxCollider.enabled = false;

        initialLocalPos = transform.localPosition;
        initialColliderCenter = hitboxCollider.center;
    }

    public void ActivateHitbox(int damage, Vector3 localKnockback, bool facingRight, float hitstopDuration = 0f, float shakeForce = 0f)
    {
        hitTargetsThisSwing.Clear();
        currentDamage = damage;
        currentHitstop = hitstopDuration;
        currentShakeForce = shakeForce;

        float facingDirection = facingRight ? 1f : -1f;

        // Mirror position/center on X without using negative scale on the BoxCollider
        transform.localPosition = new Vector3(
            Mathf.Abs(initialLocalPos.x) * facingDirection,
            initialLocalPos.y,
            initialLocalPos.z
        );
        hitboxCollider.center = new Vector3(
            Mathf.Abs(initialColliderCenter.x) * facingDirection,
            initialColliderCenter.y,
            initialColliderCenter.z
        );

        currentKnockback = new Vector3(
            localKnockback.x * facingDirection,
            localKnockback.y,
            localKnockback.z
        );

        hitboxCollider.enabled = true;
    }

    public void DeactivateHitbox()
    {
        hitboxCollider.enabled = false;
        hitTargetsThisSwing.Clear();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent(out EnemyHurtbox hurtbox))
        {
            if (hitTargetsThisSwing.Add(hurtbox))
            {
                hurtbox.TakeDamage(currentDamage, currentKnockback);

                // Trigger camera shake and hitstop on the first enemy hit by this swing
                if (hitTargetsThisSwing.Count == 1)
                {
                    if (HitImpactManager.Instance != null)
                    {
                        HitImpactManager.Instance.TriggerImpact(currentHitstop, currentShakeForce);
                    }
                    else if (currentHitstop > 0f)
                    {
                        if (hitstopCoroutine != null) StopCoroutine(hitstopCoroutine);
                        hitstopCoroutine = StartCoroutine(HitstopRoutine(currentHitstop));
                    }
                }
            }
        }
    }

    private IEnumerator HitstopRoutine(float duration)
    {
        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = 1f;
        hitstopCoroutine = null;
    }

    private void OnDisable()
    {
        if (hitstopCoroutine != null)
        {
            Time.timeScale = 1f;
            hitstopCoroutine = null;
        }
    }

    private void OnDrawGizmos()
    {
        BoxCollider col = GetComponent<BoxCollider>();
        if (col == null) return;

        Gizmos.color = col.enabled ? new Color(1f, 0f, 0f, 0.5f) : new Color(1f, 0.5f, 0f, 0.2f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(col.center, col.size);
        Gizmos.DrawWireCube(col.center, col.size);
    }
}