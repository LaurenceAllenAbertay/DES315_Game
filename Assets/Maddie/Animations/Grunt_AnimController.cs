using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
public class Grunt_AnimController : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Agent velocity must exceed this to trigger the moving animation")]
    public float moveThreshold = 0.3f;

    private Animator animator;
    private NavMeshAgent agent;

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        agent = GetComponent<NavMeshAgent>();
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
