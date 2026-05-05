using System;
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

    [Header("Collision pause")]
    [Tooltip("When colliding with the player the agent will pause. Resume chasing when player is this far away or more.")]
    public float resumeDistanceAfterCollision = 5f;

    [Header("Collision response (tuning)")]
    [Tooltip("Multiplier applied to the player's velocity immediately after collision (0..1). Lower = less knockback.")]
    [Range(0f, 1f)]
    public float collisionDampFactor = 0.25f;
    [Tooltip("Clamp upward (Y) velocity on the player after collision to prevent launching.")]
    public float maxCollisionUpwardVelocity = 2f;
    [Tooltip("Clamp total player speed after collision.")]
    public float maxCollisionSpeed = 6f;

    [Header("Pitch/Slope Settings")]
    [Tooltip("How fast the enemy tilts to match the ground.")]
    public float pitchAdjustmentSpeed = 5f;
    [Tooltip("How far down to look for the ground.")]
    public float groundCheckDistance = 1.5f;

    [Header("Misc")]
    [Tooltip("How close to a patrol point before moving to the next")]
    public float waypointTolerance = 0.5f;

    // runtime
    private State state = State.Idle;
    private int patrolIndex = 0;
    private float sqrDetectionRadius;
    private float sqrLoseSightDistance;

    // paused-on-collision flag
    private bool pausedByCollision = false;

    // cached components
    private Rigidbody rb;
    private Component sync; // optional sync component (toggled via reflection)

    void Awake()
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();

        // Allow the Rigidbody to Pitch (X rotation) but freeze Roll (Z rotation) to stop tipping
        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints.FreezeRotationZ;
        }

        // get the sync component by name (optional, may not exist)
        sync = GetComponent("NavMeshAgentRigidbodySync") as Component;

        sqrDetectionRadius = detectionRadius * detectionRadius;
        sqrLoseSightDistance = loseSightDistance * loseSightDistance;
    }

    void Start()
    {
        // Try to find the player on boot (might fail if player is currently respawning/hidden)
        TryFindPlayer();

        // Ensure agent is on NavMesh at start (best-effort)
        EnsureAgentOnNavMesh();

        // ensure sync is enabled initially
        SetSyncEnabled(true);

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
        // NEW: If we don't have a target (e.g. they were respawning when we booted up), keep looking!
        if (playerTransform == null)
        {
            TryFindPlayer();

            // If the player is STILL hidden, don't run the rest of the logic this frame
            if (playerTransform == null) return;
        }

        // If collision-paused, check if we should resume
        if (pausedByCollision && playerTransform != null)
        {
            float sqrDistToPlayer = (playerTransform.position - transform.position).sqrMagnitude;
            if (sqrDistToPlayer >= resumeDistanceAfterCollision * resumeDistanceAfterCollision)
            {
                // resume chasing immediately when player is far enough
                pausedByCollision = false;
                SetPausedState(false);
                BeginChase();
            }
            else
            {
                // keep paused; skip state updates
                return;
            }
        }

        // Always check for player presence and distance (unless pausedByCollision handled above)
        if (playerTransform != null)
        {
            var toPlayer = playerTransform.position - transform.position;
            float sqrDist = toPlayer.sqrMagnitude;

            if (sqrDist <= sqrDetectionRadius)
            {
                // Spot player: start chase if not currently chasing
                if (state != State.Chase && !pausedByCollision)
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

    // --- NEW: LATE UPDATE FOR PITCH ---
    // We use LateUpdate because the NavMeshAgent calculates its steering in Update.
    // This allows us to overwrite the Agent's "flat" rotation right before the frame is rendered!
    void LateUpdate()
    {
        ApplySlopeAlignment();
    }

    private void ApplySlopeAlignment()
    {
        RaycastHit hit;
        // Shoot a ray from slightly above the center of the tank downward
        if (Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out hit, groundCheckDistance))
        {
            Vector3 groundNormal = hit.normal;

            // Get the forward direction the NavMeshAgent is trying to face
            Vector3 currentForward = transform.forward;

            // Project it onto the slope
            Vector3 forwardOnSlope = Vector3.ProjectOnPlane(currentForward, groundNormal);

            if (forwardOnSlope != Vector3.zero)
            {
                // Calculate target rotation keeping the Yaw but adding Pitch
                Quaternion slopeRotation = Quaternion.LookRotation(forwardOnSlope, groundNormal);

                // Smoothly Slerp the transform's rotation to match the hill
                transform.rotation = Quaternion.Slerp(transform.rotation, slopeRotation, Time.deltaTime * pitchAdjustmentSpeed);
            }
        }
    }

    // NEW: A dedicated helper method to safely look for the player
    private void TryFindPlayer()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
        {
            playerTransform = p.transform;
            Debug.Log("[EnemyAI] Target Acquired!", gameObject);
        }
    }

    private void BeginChase()
    {
        // Try to ensure agent is on navmesh before chasing
        EnsureAgentOnNavMesh();

        // Ensure agent is enabled (it may have been disabled while paused)
        if (agent != null) agent.enabled = true;

        state = State.Chase;
        agent.isStopped = false;
        agent.speed = chaseSpeed;

        if (playerTransform != null)
            agent.SetDestination(playerTransform.position);
    }

    private void UpdateChase()
    {
        if (playerTransform == null)
        {
            BecomeDefaultState();
            return;
        }

        // If agent can't path, attempt to resample and set destination again
        if (!agent.isOnNavMesh || !agent.hasPath || agent.pathStatus != NavMeshPathStatus.PathComplete)
        {
            Debug.LogWarning($"[EnemyAI] Agent cannot path (isOnNavMesh={agent.isOnNavMesh}, hasPath={agent.hasPath}, pathStatus={agent.pathStatus}). Trying to sample position and retry.", gameObject);
            if (EnsureAgentOnNavMesh())
            {
                agent.SetDestination(playerTransform.position);
            }
            else
            {
                // if still no navmesh, do nothing this frame
                return;
            }
        }
        else
        {
            // Regular chase update
            agent.SetDestination(playerTransform.position);
        }
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

    // Ensures the agent is on the NavMesh. Returns true if agent is on NavMesh after the call.
    private bool EnsureAgentOnNavMesh()
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (agent == null) return false;

        if (agent.isOnNavMesh) return true;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(transform.position, out hit, 2f, agent.areaMask))
        {
            agent.Warp(hit.position);
            agent.nextPosition = transform.position;
            return true;
        }

        return false;
    }

    // --- Collision handling: pause on contact with player, resume when player is far enough ---
    void OnCollisionEnter(Collision collision)
    {
        if (collision.collider != null && collision.collider.CompareTag("Player"))
        {
            // Pause movement on contact
            pausedByCollision = true;

            // SAFETY CHECK: Only give NavMesh commands if the agent is fully awake and on the floor!
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();

                // zero velocity if available (softly supported across versions)
#if UNITY_2019_1_OR_NEWER
                agent.velocity = Vector3.zero;
#endif
            }

            // disable agent to be extra sure it won't move transform
            if (agent != null) agent.enabled = false;

            // Disable the sync component so FixedUpdate won't force movement (if present)
            SetSyncEnabled(false);

            // DAMP player rigidbody (so player doesn't fly off)
            Rigidbody playerRb = collision.collider.attachedRigidbody;
            if (playerRb == null) playerRb = collision.collider.GetComponentInParent<Rigidbody>();
            if (playerRb != null)
            {
                // Damp existing velocity and clamp
                Vector3 newVel = playerRb.linearVelocity * collisionDampFactor;
                float mag = newVel.magnitude;
                if (mag > maxCollisionSpeed) newVel = newVel.normalized * maxCollisionSpeed;

                if (newVel.y > maxCollisionUpwardVelocity)
                    newVel.y = maxCollisionUpwardVelocity;

                playerRb.linearVelocity = newVel;
                playerRb.angularVelocity *= collisionDampFactor;
            }
        }
    }

    // Reflection helper: safely set enableSync on optional sync component (if present)
    private void SetSyncEnabled(bool enabled)
    {
        if (sync == null) sync = GetComponent("NavMeshAgentRigidbodySync") as Component;
        if (sync == null) return;
        try
        {
            var t = sync.GetType();
            var f = t.GetField("enableSync");
            if (f != null)
            {
                f.SetValue(sync, enabled);
                return;
            }
            var p = t.GetProperty("enableSync");
            if (p != null && p.CanWrite)
            {
                p.SetValue(sync, enabled, null);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[EnemyAI] Failed to set enableSync on sync component: {ex.Message}", gameObject);
        }
    }

    // Restore agent and sync when unpausing
    private void SetPausedState(bool paused)
    {
        if (!paused)
        {
            // re-enable sync and agent
            SetSyncEnabled(true);
            if (agent != null)
            {
                agent.enabled = true;
                agent.isStopped = false;
            }
        }
        else
        {
            // handled in OnCollisionEnter
            pausedByCollision = true;
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, loseSightDistance);
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, resumeDistanceAfterCollision);
    }
#endif
}