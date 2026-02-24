using UnityEngine;
using UnityEngine.AI;

public class AnimationStateController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private PlayerAbilityManager abilityHandler;

    [Header("Crouch Settings")]
    [Tooltip("How quickly the player blends into/out of crouch")]
    [SerializeField] private float crouchBlendSpeed = 3f;

    private NavMeshAgent agent;

    //Animator parameter hashes for optimization
    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int CrouchBlendHash = Animator.StringToHash("CrouchBlend");
    private static readonly int CastHash = Animator.StringToHash("Cast");
    private static readonly int AbilitySlotHash = Animator.StringToHash("AbilitySlot");
    private static readonly int IsCastingHash = Animator.StringToHash("IsCasting");


    private float currentCrouchBlend = 0f;
    private bool isCasting = false;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();
        if (playerController == null)
            playerController = GetComponent<PlayerController>();
        if (abilityHandler == null)
            abilityHandler = GetComponent<PlayerAbilityManager>();

        agent = GetComponent<NavMeshAgent>();
    }

    private void OnEnable()
    {
        if (abilityHandler != null)
            abilityHandler.OnAbilityCast += HandleAbilityCast;
    }

    private void OnDisable()
    {
        if (abilityHandler != null)
            abilityHandler.OnAbilityCast -= HandleAbilityCast;
    }

    private void Update()
    {
        HandleMovementAnimations();
        HandleCrouchAnimations();
        HandleCastingState();
    }

    private void HandleMovementAnimations()
    {
        // Don't update movement animations while casting
        if (isCasting) return;

        bool isMoving = playerController != null && playerController.IsMoving;
        animator.SetBool(IsMovingHash, isMoving);
    }

    private void HandleCrouchAnimations()
    {
        // IsInLight is false when in shadow, so we invert it for crouch
        bool shouldCrouch = playerController != null && !playerController.IsInLight;

        float targetCrouch = shouldCrouch ? 1f : 0f;

        // Smoothly blend towards the target crouch value
        currentCrouchBlend = Mathf.Lerp(currentCrouchBlend, targetCrouch, Time.deltaTime * crouchBlendSpeed);

        animator.SetFloat(CrouchBlendHash, currentCrouchBlend);
    }

    private void HandleAbilityCast(int slotIndex)
    {
        // Stop movement when casting
        if (agent != null)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }

        animator.SetBool(IsMovingHash, false);

        // Set which ability slot (0, 1, or 2) so the Animator can pick the right animation
        animator.SetInteger(AbilitySlotHash, slotIndex);
        animator.SetTrigger(CastHash);

        isCasting = true;
    }

    private void HandleCastingState()
    {
        if (!isCasting) return;

        // Check if the Animator has finished the cast state and returned to idle
        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        bool inCastState = stateInfo.IsTag("Cast"); // tag your cast states with "Cast" in the Animator

        if (!inCastState)
        {
            isCasting = false;
            animator.SetBool(IsCastingHash, false);

            // Re-enable the agent after casting is done
            if (agent != null)
                agent.isStopped = false;
        }
    }
}