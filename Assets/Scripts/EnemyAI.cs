using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(EnemyHurtbox))]
public class EnemyAI : MonoBehaviour
{
    [Header("Movement & Z-Lane Tracking")]
    [SerializeField] private float moveSpeedX = 3.5f;
    [SerializeField] private float moveSpeedZ = 2.5f;
    [SerializeField] private float aggroRange = 14f;
    [Tooltip("Horizontal distance from the player where the enemy stops to punch")]
    [SerializeField] private float attackRangeX = 1.15f;
    [Tooltip("How closely aligned on the Z-axis the enemy must be to throw a punch")]
    [SerializeField] private float zLaneTolerance = 0.35f;
    [SerializeField] private float knockbackGroundDrag = 18f;

    [Header("Ground & Shadow Settings")]
    [SerializeField] private Transform groundCheckPoint;
    [SerializeField] private float groundCheckRadius = 0.2f;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private Transform shadowTransform;
    [SerializeField] private float shadowGroundOffset = 0.02f;

    [Header("Attack & Hitbox Settings")]
    [SerializeField] private int attackDamage = 10;
    [SerializeField] private Vector3 attackKnockback = new Vector3(3f, 0f, 0f);
    [SerializeField] private float windupTime = 0.35f;
    [SerializeField] private float activeTime = 0.12f;
    [SerializeField] private float recoveryTime = 0.4f;
    [SerializeField] private float attackCooldown = 1.1f;
    [SerializeField] private Vector3 hitboxOffset = new Vector3(0.8f, 0.8f, 0f);
    [SerializeField] private Vector3 hitboxSize = new Vector3(1.0f, 1.0f, 0.5f);
    [SerializeField] private LayerMask playerLayer;

    [Header("Visuals References")]
    [SerializeField] private Transform visualsTransform;
    [SerializeField] private SpriteRenderer enemySprite;
    [SerializeField] private Animator animator;

    private Rigidbody rb;
    private EnemyHurtbox hurtbox;
    private Transform playerTransform;

    private bool isGrounded;
    private bool isAttacking;
    private bool facingRight = false;
    private float cooldownTimer;
    private Vector2 desiredVelocityXZ;
    private Coroutine attackCoroutine;

    private Vector3 initialVisualsPos;
    private Vector3 initialVisualsScale;
    private Vector3 initialShadowScale;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        hurtbox = GetComponent<EnemyHurtbox>();

        if (visualsTransform != null)
        {
            initialVisualsPos = visualsTransform.localPosition;
            initialVisualsScale = visualsTransform.localScale;
        }

        if (shadowTransform != null)
        {
            initialShadowScale = shadowTransform.localScale;
        }
    }

    private void OnEnable()
    {
        hurtbox.OnHitInterrupted += HandleHitInterrupted;
    }

    private void OnDisable()
    {
        hurtbox.OnHitInterrupted -= HandleHitInterrupted;
    }

    private void Start()
    {
        BeatEmUpPlayerController player = FindFirstObjectByType<BeatEmUpPlayerController>();
        if (player != null)
        {
            playerTransform = player.transform;
        }
    }

    private void Update()
    {
        UpdateShadowPosition();

        if (cooldownTimer > 0f)
        {
            cooldownTimer -= Time.deltaTime;
        }

        // Do not calculate chase logic while stunned or mid-attack
        if (playerTransform == null || hurtbox.IsInHitstun || isAttacking)
        {
            desiredVelocityXZ = Vector2.zero;
            UpdateAnimator(0f);
            return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        if (distanceToPlayer > aggroRange)
        {
            desiredVelocityXZ = Vector2.zero;
            UpdateAnimator(0f);
            return;
        }

        // Always face the player horizontally
        float deltaXToPlayer = playerTransform.position.x - transform.position.x;
        if (Mathf.Abs(deltaXToPlayer) > 0.05f)
        {
            FlipSprite(deltaXToPlayer > 0f);
        }

        // Pick a target X slot just outside the player's body on the closer side, and match the player's Z-lane
        float sideSign = (transform.position.x < playerTransform.position.x) ? -1f : 1f;
        float targetX = playerTransform.position.x + (sideSign * attackRangeX);
        float targetZ = playerTransform.position.z;

        float diffX = targetX - transform.position.x;
        float diffZ = targetZ - transform.position.z;

        bool inXRange = Mathf.Abs(deltaXToPlayer) <= (attackRangeX + 0.2f);
        bool inZLane = Mathf.Abs(diffZ) <= zLaneTolerance;

        // If aligned in the player's Z-lane and within horizontal punch reach, attack
        if (inXRange && inZLane)
        {
            desiredVelocityXZ = Vector2.zero;
            UpdateAnimator(0f);

            if (cooldownTimer <= 0f && isGrounded)
            {
                attackCoroutine = StartCoroutine(AttackRoutine());
            }
        }
        else
        {
            // Move toward the target X slot and Z-lane
            float moveDirX = Mathf.Abs(diffX) > 0.1f ? Mathf.Sign(diffX) : 0f;
            float moveDirZ = Mathf.Abs(diffZ) > 0.08f ? Mathf.Sign(diffZ) : 0f;

            Vector2 moveDir = new Vector2(moveDirX, moveDirZ);
            if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();

            desiredVelocityXZ = new Vector2(moveDir.x * moveSpeedX, moveDir.y * moveSpeedZ);
            UpdateAnimator(moveDir.magnitude);
        }
    }

    private void FixedUpdate()
    {
        Vector3 checkPos = groundCheckPoint != null ? groundCheckPoint.position : transform.position;
        isGrounded = Physics.CheckSphere(checkPos, groundCheckRadius, groundLayer, QueryTriggerInteraction.Ignore);

        if (hurtbox.IsInHitstun)
        {
            // Smoothly brake knockback sliding once the enemy lands back on the ground
            if (isGrounded && rb.linearVelocity.y <= 0.1f)
            {
                float newX = Mathf.MoveTowards(rb.linearVelocity.x, 0f, knockbackGroundDrag * Time.fixedDeltaTime);
                float newZ = Mathf.MoveTowards(rb.linearVelocity.z, 0f, knockbackGroundDrag * Time.fixedDeltaTime);
                rb.linearVelocity = new Vector3(newX, rb.linearVelocity.y, newZ);
            }
            return;
        }

        if (isAttacking)
        {
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            return;
        }

        rb.linearVelocity = new Vector3(desiredVelocityXZ.x, rb.linearVelocity.y, desiredVelocityXZ.y);
    }

    private IEnumerator AttackRoutine()
    {
        isAttacking = true;
        float dir = facingRight ? 1f : -1f;

        // 1. Wind-up (pull back slightly so the player can react)
        if (visualsTransform != null)
        {
            visualsTransform.localPosition = initialVisualsPos + new Vector3(-0.12f * dir, 0f, 0f);
            visualsTransform.localScale = new Vector3(initialVisualsScale.x * 0.9f, initialVisualsScale.y * 1.1f, initialVisualsScale.z);
        }
        yield return new WaitForSeconds(windupTime);

        // 2. Active Lunge & 3D OverlapBox Hit Check
        if (visualsTransform != null)
        {
            visualsTransform.localPosition = initialVisualsPos + new Vector3(0.3f * dir, 0f, 0f);
            visualsTransform.localScale = new Vector3(initialVisualsScale.x * 1.2f, initialVisualsScale.y * 0.85f, initialVisualsScale.z);
        }

        CheckHitboxOverlap(dir);
        yield return new WaitForSeconds(activeTime);

        // 3. Recovery
        ResetVisuals();
        yield return new WaitForSeconds(recoveryTime);

        cooldownTimer = attackCooldown;
        isAttacking = false;
        attackCoroutine = null;
    }

    private void CheckHitboxOverlap(float dir)
    {
        Vector3 boxCenter = transform.position + new Vector3(hitboxOffset.x * dir, hitboxOffset.y, hitboxOffset.z);
        Collider[] hits = Physics.OverlapBox(boxCenter, hitboxSize * 0.5f, Quaternion.identity, playerLayer, QueryTriggerInteraction.Ignore);

        foreach (Collider hit in hits)
        {
            if (hit.TryGetComponent(out PlayerHurtbox playerHurtbox))
            {
                Vector3 finalKnockback = new Vector3(attackKnockback.x * dir, attackKnockback.y, attackKnockback.z);
                playerHurtbox.TakeDamage(attackDamage, finalKnockback);
                break;
            }
        }
    }

    private void HandleHitInterrupted()
    {
        if (attackCoroutine != null)
        {
            StopCoroutine(attackCoroutine);
            attackCoroutine = null;
        }
        ResetVisuals();
        isAttacking = false;
        cooldownTimer = attackCooldown * 0.5f;
    }

    private void ResetVisuals()
    {
        if (visualsTransform != null)
        {
            visualsTransform.localPosition = initialVisualsPos;
            visualsTransform.localScale = initialVisualsScale;
        }
    }

    private void FlipSprite(bool faceRight)
    {
        facingRight = faceRight;
        if (enemySprite != null)
        {
            enemySprite.flipX = !facingRight;
        }
    }

    private void UpdateAnimator(float speed)
    {
        if (animator != null)
        {
            animator.SetFloat(SpeedHash, speed);
        }
    }

    private void UpdateShadowPosition()
    {
        if (shadowTransform == null) return;

        Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 20f, groundLayer, QueryTriggerInteraction.Ignore))
        {
            shadowTransform.position = new Vector3(
                transform.position.x,
                hit.point.y + shadowGroundOffset,
                transform.position.z
            );

            float heightAboveGround = Mathf.Max(0f, transform.position.y - hit.point.y);
            float scaleFactor = Mathf.Lerp(1f, 0.5f, heightAboveGround / 4f);
            shadowTransform.localScale = initialShadowScale * scaleFactor;
        }
    }

    private void OnDrawGizmosSelected()
    {
        float dir = facingRight ? 1f : -1f;
        Vector3 boxCenter = transform.position + new Vector3(hitboxOffset.x * dir, hitboxOffset.y, hitboxOffset.z);

        Gizmos.color = isAttacking ? new Color(1f, 0f, 0f, 0.6f) : new Color(1f, 0.8f, 0f, 0.35f);
        Gizmos.DrawCube(boxCenter, hitboxSize);
        Gizmos.DrawWireCube(boxCenter, hitboxSize);
    }
}