using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerHurtbox : MonoBehaviour
{
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private SpriteRenderer playerSprite;
    [SerializeField] private Color hitFlashColor = new Color(1f, 0.3f, 0.3f, 1f);
    [SerializeField] private float hitFlashDuration = 0.12f;

    // UI Event
    public event System.Action<float> OnHealthChanged;

    private int currentHealth;
    private Rigidbody rb;
    private Color originalColor;
    private Coroutine flashCoroutine;

    private void Awake()
    {
        currentHealth = maxHealth;
        rb = GetComponent<Rigidbody>();

        if (playerSprite != null)
        {
            originalColor = playerSprite.color;
        }
    }

    private void Start()
    {
        // Tell the UI to set to 100% on start
        OnHealthChanged?.Invoke((float)currentHealth / maxHealth);
    }

    public void TakeDamage(int damage, Vector3 knockbackVelocity)
    {
        currentHealth = Mathf.Max(0, currentHealth - damage);
        rb.linearVelocity = new Vector3(knockbackVelocity.x, rb.linearVelocity.y + knockbackVelocity.y, knockbackVelocity.z);

        // Tell the UI the new health percentage
        OnHealthChanged?.Invoke((float)currentHealth / maxHealth);

        if (playerSprite != null)
        {
            if (flashCoroutine != null) StopCoroutine(flashCoroutine);
            flashCoroutine = StartCoroutine(FlashRoutine());
        }
    }

    private IEnumerator FlashRoutine()
    {
        playerSprite.color = hitFlashColor;
        yield return new WaitForSeconds(hitFlashDuration);
        playerSprite.color = originalColor;
    }
}