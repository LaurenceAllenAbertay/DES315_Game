using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Animator))]
public class Brute_AnimController : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Agent velocity must exceed this to trigger the moving animation")]
    public float moveThreshold = 0.1f;

    private Animator animator;
    private NavMeshAgent agent;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        agent = GetComponentInParent<NavMeshAgent>();
    }

    private void Update()
    {
        bool isMoving = agent.velocity.sqrMagnitude > moveThreshold * moveThreshold;
        animator.SetBool("IsMoving", isMoving);
    }

    public void TriggerAttack()
    {
        animator.SetTrigger("Attack");
    }
}