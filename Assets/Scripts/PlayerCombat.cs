using System.Collections;
using UnityEngine;

[System.Serializable]
public struct ComboAttackData
{
    public string animationTriggerName;
    public int damage;
    public Vector3 knockbackVelocity;
    public float startupTime;
    public float activeTime;
    public float recoveryTime;
    [Header("Impact Feel")]
    [Tooltip("Freeze duration in seconds (0.05 = ~3 frames, 0.14 = ~8 frames)")]
    public float hitstopDuration;
    [Tooltip("Cinemachine Impulse force multiplier")]
    public float shakeForce;
    [Header("Procedural Lunge Visuals")]
    public float lungeDistance;
    public float punchTiltAngle;
    public Vector3 punchScale;
}

[RequireComponent(typeof(BeatEmUpPlayerController))]
public class PlayerCombat : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform hitboxPivot;
    [SerializeField] private AttackHitbox attackHitbox;
    [SerializeField] private Animator animator;
    [SerializeField] private Transform visualsTransform;
    [SerializeField] private SpriteRenderer strikeEffectSprite;

    [Header("Combo Configuration")]
    [SerializeField]
    private ComboAttackData[] lightCombo = new ComboAttackData[3]
    {
        new ComboAttackData {
            animationTriggerName = "Attack1", damage = 10, knockbackVelocity = new Vector3(1.5f, 0f, 0f),
            startupTime = 0.06f, activeTime = 0.1f, recoveryTime = 0.18f, hitstopDuration = 0.05f, shakeForce = 0.25f,
            lungeDistance = 0.2f, punchTiltAngle = -8f, punchScale = new Vector3(1.15f, 0.9f, 1f)
        },
        new ComboAttackData {
            animationTriggerName = "Attack2", damage = 12, knockbackVelocity = new Vector3(2.0f, 0f, 0f),
            startupTime = 0.07f, activeTime = 0.1f, recoveryTime = 0.20f, hitstopDuration = 0.06f, shakeForce = 0.35f,
            lungeDistance = 0.28f, punchTiltAngle = 10f, punchScale = new Vector3(0.9f, 1.15f, 1f)
        },
        new ComboAttackData {
            animationTriggerName = "Attack3", damage = 25, knockbackVelocity = new Vector3(6.5f, 5.5f, 0f),
            startupTime = 0.10f, activeTime = 0.14f, recoveryTime = 0.30f, hitstopDuration = 0.14f, shakeForce = 1.0f,
            lungeDistance = 0.45f, punchTiltAngle = -18f, punchScale = new Vector3(1.3f, 0.8f, 1f)
        }
    };

    private BeatEmUpPlayerController playerController;
    private int currentComboIndex = 0;
    private bool canBufferInput = false;
    private bool inputBuffered = false;

    private Vector3 initialVisualsPos;
    private Quaternion initialVisualsRot;
    private Vector3 initialVisualsScale;
    private Vector3 initialStrikeEffectPos;

    private void Awake()
    {
        playerController = GetComponent<BeatEmUpPlayerController>();

        if (attackHitbox == null)
        {
            Debug.LogError("PlayerCombat requires an AttackHitbox reference.", this);
            enabled = false;
            return;
        }

        if (visualsTransform != null)
        {
            initialVisualsPos = visualsTransform.localPosition;
            initialVisualsRot = visualsTransform.localRotation;
            initialVisualsScale = visualsTransform.localScale;
        }

        if (strikeEffectSprite != null)
        {
            initialStrikeEffectPos = strikeEffectSprite.transform.localPosition;
            strikeEffectSprite.enabled = false;
        }
    }

    private void Update()
    {
        if (Input.GetButtonDown("Fire1") || Input.GetKeyDown(KeyCode.J))
        {
            HandleAttackInput();
        }
    }

    private void HandleAttackInput()
    {
        if (lightCombo == null || lightCombo.Length == 0)
        {
            return;
        }

        if (!playerController.IsAttacking)
        {
            currentComboIndex = 0;
            StartCoroutine(ExecuteAttackStep(currentComboIndex));
        }
        else if (canBufferInput && currentComboIndex < lightCombo.Length - 1)
        {
            inputBuffered = true;
        }
    }

    private IEnumerator ExecuteAttackStep(int stepIndex)
    {
        if (lightCombo == null || stepIndex < 0 || stepIndex >= lightCombo.Length)
        {
            playerController.IsAttacking = false;
            yield break;
        }

        playerController.IsAttacking = true;
        canBufferInput = false;
        inputBuffered = false;

        ComboAttackData attack = lightCombo[stepIndex];
        bool facingRight = playerController.FacingRight;
        float direction = facingRight ? 1f : -1f;

        if (animator != null && !string.IsNullOrEmpty(attack.animationTriggerName))
        {
            animator.SetTrigger(attack.animationTriggerName);
        }

        if (visualsTransform != null)
        {
            visualsTransform.localPosition = initialVisualsPos + new Vector3(-0.08f * direction, 0f, 0f);
            visualsTransform.localScale = new Vector3(
                initialVisualsScale.x * 0.9f,
                initialVisualsScale.y * 1.1f,
                initialVisualsScale.z);
        }

        yield return new WaitForSeconds(attack.startupTime);

        canBufferInput = true;

        if (visualsTransform != null)
        {
            visualsTransform.localPosition =
                initialVisualsPos + new Vector3(attack.lungeDistance * direction, 0f, 0f);
            visualsTransform.localRotation =
                initialVisualsRot * Quaternion.Euler(0f, 0f, attack.punchTiltAngle * direction);
            visualsTransform.localScale = Vector3.Scale(initialVisualsScale, attack.punchScale);
        }

        if (strikeEffectSprite != null)
        {
            strikeEffectSprite.transform.localPosition = new Vector3(
                Mathf.Abs(initialStrikeEffectPos.x) * direction,
                initialStrikeEffectPos.y,
                initialStrikeEffectPos.z);
            strikeEffectSprite.flipX = !facingRight;
            strikeEffectSprite.enabled = true;
        }

        attackHitbox.ActivateHitbox(
            attack.damage,
            attack.knockbackVelocity,
            facingRight,
            attack.hitstopDuration,
            attack.shakeForce);

        yield return new WaitForSeconds(attack.activeTime);

        attackHitbox.DeactivateHitbox();

        if (strikeEffectSprite != null)
        {
            strikeEffectSprite.enabled = false;
        }

        Vector3 recoveryStartPos = visualsTransform != null
            ? visualsTransform.localPosition
            : Vector3.zero;
        Quaternion recoveryStartRot = visualsTransform != null
            ? visualsTransform.localRotation
            : Quaternion.identity;
        Vector3 recoveryStartScale = visualsTransform != null
            ? visualsTransform.localScale
            : Vector3.one;

        float timer = 0f;
        while (timer < attack.recoveryTime)
        {
            if (visualsTransform != null)
            {
                float t = attack.recoveryTime > 0f ? timer / attack.recoveryTime : 1f;
                visualsTransform.localPosition = Vector3.Lerp(recoveryStartPos, initialVisualsPos, t);
                visualsTransform.localRotation = Quaternion.Slerp(recoveryStartRot, initialVisualsRot, t);
                visualsTransform.localScale = Vector3.Lerp(recoveryStartScale, initialVisualsScale, t);
            }

            if (inputBuffered && stepIndex < lightCombo.Length - 1)
            {
                ResetVisuals();
                currentComboIndex = stepIndex + 1;
                StartCoroutine(ExecuteAttackStep(currentComboIndex));
                yield break;
            }

            timer += Time.deltaTime;
            yield return null;
        }

        ResetVisuals();
        canBufferInput = false;
        playerController.IsAttacking = false;
        currentComboIndex = 0;
    }

    private void ResetVisuals()
    {
        if (visualsTransform != null)
        {
            visualsTransform.localPosition = initialVisualsPos;
            visualsTransform.localRotation = initialVisualsRot;
            visualsTransform.localScale = initialVisualsScale;
        }
    }
}