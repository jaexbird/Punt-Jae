using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerHurtbox : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private int maxHealth = 100;

    [Header("Hit Flash Settings")]
    [SerializeField] private SpriteRenderer playerSprite;
    [ColorUsage(true, true)]
    [SerializeField] private Color hitFlashColor = new Color(1f, 0.3f, 0.3f, 1f);
    [SerializeField] private float hitFlashDuration = 0.12f;

    [Header("Invincibility (i-Frames)")]
    [Tooltip("Total time in seconds the player cannot be hit again after taking damage")]
    [SerializeField] private float invincibilityDuration = 0.8f;
    [Tooltip("How fast the sprite blinks on and off during i-frames")]
    [SerializeField] private float blinkInterval = 0.08f;

    // UI Event
    public event System.Action<float> OnHealthChanged;

    public bool IsInvincible => isInvincible;

    private int currentHealth;
    private bool isInvincible;
    private Rigidbody rb;
    private Coroutine damageRoutine;
    private MaterialPropertyBlock propBlock;

    private static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");
    private static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");

    private void Awake()
    {
        currentHealth = maxHealth;
        rb = GetComponent<Rigidbody>();
        propBlock = new MaterialPropertyBlock();

        SetFlashAmount(0f);
    }

    private void Start()
    {
        // Tell the UI to set to 100% on start
        OnHealthChanged?.Invoke((float)currentHealth / maxHealth);
    }

    public void TakeDamage(int damage, Vector3 knockbackVelocity)
    {
        // Ignore incoming attacks while invincible or already defeated
        if (isInvincible || currentHealth <= 0)
        {
            return;
        }

        currentHealth = Mathf.Max(0, currentHealth - damage);
        rb.linearVelocity = new Vector3(
            knockbackVelocity.x,
            rb.linearVelocity.y + knockbackVelocity.y,
            knockbackVelocity.z
        );

        // Tell the UI the new health percentage
        OnHealthChanged?.Invoke((float)currentHealth / maxHealth);

        if (playerSprite != null)
        {
            if (damageRoutine != null) StopCoroutine(damageRoutine);
            damageRoutine = StartCoroutine(DamageFeedbackRoutine());
        }

        Debug.Log($"Player took {damage} damage! Player HP: {currentHealth}/{maxHealth}");
    }

    private IEnumerator DamageFeedbackRoutine()
    {
        isInvincible = true;
        playerSprite.enabled = true;

        // 1. Solid shader hit flash on initial impact
        SetFlashAmount(1f);
        yield return new WaitForSeconds(hitFlashDuration);
        SetFlashAmount(0f);

        // 2. Rapid arcade blink for the remainder of the invincibility window
        float elapsed = hitFlashDuration;
        while (elapsed < invincibilityDuration)
        {
            playerSprite.enabled = !playerSprite.enabled;
            yield return new WaitForSeconds(blinkInterval);
            elapsed += blinkInterval;
        }

        // 3. Ensure sprite is visible and vulnerability is restored
        playerSprite.enabled = true;
        isInvincible = false;
        damageRoutine = null;
    }

    private void SetFlashAmount(float amount)
    {
        if (playerSprite == null) return;

        if (propBlock == null) propBlock = new MaterialPropertyBlock();

        playerSprite.GetPropertyBlock(propBlock);
        propBlock.SetColor(FlashColorId, hitFlashColor);
        propBlock.SetFloat(FlashAmountId, amount);
        playerSprite.SetPropertyBlock(propBlock);
    }

    private void OnDisable()
    {
        SetFlashAmount(0f);
        if (playerSprite != null)
        {
            playerSprite.enabled = true;
        }
        isInvincible = false;
    }
}