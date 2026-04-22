using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Simple NavMesh-based enemy AI: spots the player within a configurable radius and chases them.
/// - Attach to the enemy GameObject that has a NavMeshAgent.
/// - Assign the player by tag "Player" or set PlayerTransform in the inspector.
/// </summary>
[DisallowMultipleComponent]
public class EnemyAI : MonoBehaviour
{
    public enum State { Idle, Patrol, Chase }

    [Header("References")]
    public NavMeshAgent agent;               // (optional) will GetComponent if null
    [Tooltip("Optional: assign the player transform. If null the script will find the GameObject tagged 'Player'")]
    public Transform playerTransform;

    [Header("Behavior")]
    [Tooltip("If true the enemy will patrol between the provided patrolPoints")]
    public bool usePatrol = false;
    public Transform[] patrolPoints;
    public float patrolSpeed = 2f;
    public float chaseSpeed = 4f;

    [Header("Detection")]
    [Tooltip("Distance (units) at which the enemy spots the player and starts chasing")]
    public float detectionRadius = 12f;

    [Tooltip("Distance at which the enemy gives up chase and returns to patrol/idle")]
    public float loseSightDistance = 18f;

    [Header("Misc")]
    [Tooltip("How close to a patrol point before moving to the next")]
    public float waypointTolerance = 0.5f;

    // runtime
    private State state = State.Idle;
    private int patrolIndex = 0;
    private float sqrDetectionRadius;
    private float sqrLoseSightDistance;

    void Awake()
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        sqrDetectionRadius = detectionRadius * detectionRadius;
        sqrLoseSightDistance = loseSightDistance * loseSightDistance;
    }

    void Start()
    {
        if (playerTransform == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerTransform = p.transform;
        }

        if (usePatrol && patrolPoints != null && patrolPoints.Length > 0)
        {
            state = State.Patrol;
            patrolIndex = 0;
            agent.isStopped = false;
            agent.speed = patrolSpeed;
            agent.SetDestination(patrolPoints[patrolIndex].position);
        }
        else
        {
            state = State.Idle;
            agent.isStopped = true;
        }
    }

    void Update()
    {
        // Always check for player presence and distance
        if (playerTransform != null)
        {
            var toPlayer = playerTransform.position - transform.position;
            float sqrDist = toPlayer.sqrMagnitude;

            if (sqrDist <= sqrDetectionRadius)
            {
                // Spot player: start chase
                if (state != State.Chase)
                    BeginChase();
            }
            else if (state == State.Chase && sqrDist > sqrLoseSightDistance)
            {
                // Lost player: return to default behaviour
                BecomeDefaultState();
            }
        }

        // State updates
        switch (state)
        {
            case State.Idle:
                // nothing to do
                break;

            case State.Patrol:
                UpdatePatrol();
                break;

            case State.Chase:
                UpdateChase();
                break;
        }
    }

    private void BeginChase()
    {
        state = State.Chase;
        agent.isStopped = false;
        agent.speed = chaseSpeed;
    }

    private void UpdateChase()
    {
        if (playerTransform == null)
        {
            BecomeDefaultState();
            return;
        }

        agent.SetDestination(playerTransform.position);
    }

    private void UpdatePatrol()
    {
        if (patrolPoints == null || patrolPoints.Length == 0) return;
        if (agent.pathPending) return;

        if (agent.remainingDistance <= waypointTolerance)
        {
            patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
            agent.SetDestination(patrolPoints[patrolIndex].position);
        }
    }

    private void BecomeDefaultState()
    {
        if (usePatrol && patrolPoints != null && patrolPoints.Length > 0)
        {
            state = State.Patrol;
            agent.isStopped = false;
            agent.speed = patrolSpeed;
            agent.SetDestination(patrolPoints[patrolIndex].position);
        }
        else
        {
            state = State.Idle;
            agent.isStopped = true;
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, loseSightDistance);
    }
#endif
}