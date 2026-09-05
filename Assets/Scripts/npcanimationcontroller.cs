using UnityEngine;
using UnityEngine.AI;

public class NPCAnimationController : MonoBehaviour
{
    private NavMeshAgent agent;
    private Animator animator;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        if (agent != null && animator != null)
        {
            // Get the current physical movement speed on the NavMesh
            float currentSpeed = agent.velocity.magnitude;

            // Feed the speed value directly to the Animator Controller parameter
            animator.SetFloat("Speed", currentSpeed);
        }
    }
}