using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class EnemyHurtbox : MonoBehaviour
{
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private float hitstunDuration = 0.45f;
    [SerializeField] private SpriteRenderer enemySprite;
    [SerializeField] private Color hitFlashColor = Color.red;
    [SerializeField] private float hitFlashDuration = 0.1f;

    public bool IsInHitstun => hitstunTimer > 0f;
    public event System.Action OnHitInterrupted;
    public event System.Action<EnemyHurtbox> OnEnemyDied;

    private int currentHealth;
    private float hitstunTimer;
    private Rigidbody rb;
    private Color originalColor;
    private Coroutine flashCoroutine;

    private void Awake()
    {
        currentHealth = maxHealth;
        rb = GetComponent<Rigidbody>();

        if (enemySprite != null)
        {
            originalColor = enemySprite.color;
        }
    }

    private void Update()
    {
        if (hitstunTimer > 0f)
        {
            hitstunTimer -= Time.deltaTime;
        }
    }

    public void TakeDamage(int damage, Vector3 knockbackVelocity)
    {
        currentHealth -= damage;
        hitstunTimer = hitstunDuration;

        // Interrupt any active enemy windup/attack
        OnHitInterrupted?.Invoke();

        // Apply 3D knockback using Unity 6 linearVelocity
        rb.linearVelocity = knockbackVelocity;

        if (enemySprite != null)
        {
            if (flashCoroutine != null) StopCoroutine(flashCoroutine);
            flashCoroutine = StartCoroutine(FlashSprite());
        }

        Debug.Log($"{gameObject.name} took {damage} damage! Remaining HP: {currentHealth}");

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public void ResetSpriteColor()
    {
        if (enemySprite != null)
        {
            enemySprite.color = originalColor;
        }
    }

    private IEnumerator FlashSprite()
    {
        enemySprite.color = hitFlashColor;
        yield return new WaitForSeconds(hitFlashDuration);
        enemySprite.color = originalColor;
    }

    private void Die()
    {

        OnEnemyDied?.Invoke(this);
        Destroy(gameObject);
    }
}