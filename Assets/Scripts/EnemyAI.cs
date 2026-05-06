using System;
using UnityEngine;
using UnityEngine.AI; // We need this library to use Unity's built-in pathfinding system (NavMesh).

/// <summary>
/// Simple NavMesh-based enemy AI: spots the player within a configurable radius and chases them.
/// </summary>
[DisallowMultipleComponent] // Prevents accidentally attaching two AI brains to one tank!
public class EnemyAI : MonoBehaviour
{
    // --- THE STATE MACHINE ---
    // A Finite State Machine (FSM). 'enums' are custom lists of states. 
    // This tells the script the enemy can only ever be doing exactly ONE of these three things at a time.
    public enum State { Idle, Patrol, Chase }

    [Header("References")]
    // The NavMeshAgent is the "driver" of the AI. It calculates paths around obstacles automatically.
    public NavMeshAgent agent;
    public Transform playerTransform; // Who are we hunting?

    [Header("Behavior")]
    public bool usePatrol = false;
    // Arrays (indicated by []) let us store a list of multiple points, rather than just one.
    public Transform[] patrolPoints;
    public float patrolSpeed = 2f;
    public float chaseSpeed = 4f;

    [Header("Detection")]
    public float detectionRadius = 12f;
    public float loseSightDistance = 18f;

    [Header("Collision pause")]
    public float resumeDistanceAfterCollision = 5f;

    [Header("Collision response (tuning)")]
    // [Range] turns this variable into a nice slider in the Inspector.
    [Range(0f, 1f)]
    public float collisionDampFactor = 0.25f;
    public float maxCollisionUpwardVelocity = 2f;
    public float maxCollisionSpeed = 6f;

    [Header("Pitch/Slope Settings")]
    public float pitchAdjustmentSpeed = 5f;
    public float groundCheckDistance = 1.5f;

    [Header("Misc")]
    public float waypointTolerance = 0.5f; // How close is "close enough" to a patrol point?

    // --- RUNTIME VARIABLES ---
    // Private variables handle the background math while the game runs.
    private State state = State.Idle;
    private int patrolIndex = 0;

    // Performance trick: Checking exact distances uses square roots, which are slow.
    // We pre-calculate the "squared" distances to do faster math!
    private float sqrDetectionRadius;
    private float sqrLoseSightDistance;
    private bool pausedByCollision = false;

    private Rigidbody rb;
    private Component sync;

    // Awake runs before Start. We use it to grab all the components we need before the game fully begins.
    void Awake()
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();

        if (rb != null)
        {
            // Stop the tank from barrel-rolling on its side (Z axis)
            rb.constraints = RigidbodyConstraints.FreezeRotationZ;

            // --- THE ANTI-FLIP FIX EXPLAINED ---
            // In Unity, every object has a "Center of Mass" (the balancing point of its weight).
            // By default, it is perfectly in the center (0,0,0). When two heavy tanks crash, 
            // hitting above the center of mass acts like a lever, causing them to flip over.
            // By moving the center of mass down to -0.5f on the Y-Axis, we force all the "weight" 
            // down into the tank's treads. This makes it bottom-heavy (like a Weeble-Wobble toy).
            // Now, when rammed, the tank slides backward instead of violently flipping onto its roof!
            rb.centerOfMass = new Vector3(0f, -0.5f, 0f);
        }

        sync = GetComponent("NavMeshAgentRigidbodySync") as Component;
        sqrDetectionRadius = detectionRadius * detectionRadius;
        sqrLoseSightDistance = loseSightDistance * loseSightDistance;
    }

    void Start()
    {
        TryFindPlayer();
        EnsureAgentOnNavMesh(); // Make sure the AI didn't spawn inside a wall or floating in the air.
        SetSyncEnabled(true);

        // Check our starting orders: should we stand still, or start walking a patrol route?
        if (usePatrol && patrolPoints != null && patrolPoints.Length > 0)
        {
            state = State.Patrol;
            patrolIndex = 0;
            agent.isStopped = false;
            agent.speed = patrolSpeed;
            // SetDestination tells the NavMesh system to calculate a path to that exact coordinate.
            agent.SetDestination(patrolPoints[patrolIndex].position);
        }
        else
        {
            state = State.Idle;
            agent.isStopped = true;
        }
    }

    // Update runs every single frame. This is the AI's "Brain".
    void Update()
    {
        // 1. Is the player missing? Try to find them!
        if (playerTransform == null)
        {
            TryFindPlayer();
            if (playerTransform == null) return; // If we still can't find them, give up for this frame.
        }

        // 2. Are we recovering from ramming the player?
        if (pausedByCollision && playerTransform != null)
        {
            float sqrDistToPlayer = (playerTransform.position - transform.position).sqrMagnitude;
            if (sqrDistToPlayer >= resumeDistanceAfterCollision * resumeDistanceAfterCollision)
            {
                // The player drove away, we can resume chasing!
                pausedByCollision = false;
                SetPausedState(false);
                BeginChase();
            }
            else
            {
                return; // Player is still too close, stay frozen.
            }
        }

        // 3. Radar System: Detect the player if they get too close.
        if (playerTransform != null)
        {
            var toPlayer = playerTransform.position - transform.position;
            float sqrDist = toPlayer.sqrMagnitude;

            if (sqrDist <= sqrDetectionRadius)
            {
                // Player stepped into our circle!
                if (state != State.Chase && !pausedByCollision) BeginChase();
            }
            else if (state == State.Chase && sqrDist > sqrLoseSightDistance)
            {
                // Player ran far enough away to escape!
                BecomeDefaultState();
            }
        }

        // 4. The State Machine "Switch". Runs ONLY the specific code for our current behavior.
        switch (state)
        {
            case State.Idle: break;
            case State.Patrol: UpdatePatrol(); break;
            case State.Chase: UpdateChase(); break;
        }
    }

    // LateUpdate runs right after Update. We use this to manually tilt the tank AFTER the AI chooses its path.
    void LateUpdate()
    {
        ApplySlopeAlignment();
    }

    private void ApplySlopeAlignment()
    {
        RaycastHit hit;
        // Physics.Raycast shoots an invisible laser straight down to find the floor.
        if (Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out hit, groundCheckDistance))
        {
            Vector3 groundNormal = hit.normal;
            Vector3 currentForward = transform.forward;

            // This squashes our forward direction flat against the angle of the hill.
            Vector3 forwardOnSlope = Vector3.ProjectOnPlane(currentForward, groundNormal);

            if (forwardOnSlope != Vector3.zero)
            {
                Quaternion slopeRotation = Quaternion.LookRotation(forwardOnSlope, groundNormal);
                transform.rotation = Quaternion.Slerp(transform.rotation, slopeRotation, Time.deltaTime * pitchAdjustmentSpeed);
            }
        }
    }

    private void TryFindPlayer()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
        {
            playerTransform = p.transform;
        }
    }

    private void BeginChase()
    {
        EnsureAgentOnNavMesh();
        if (agent != null) agent.enabled = true;

        state = State.Chase;
        agent.isStopped = false;
        agent.speed = chaseSpeed;

        if (playerTransform != null) agent.SetDestination(playerTransform.position);
    }

    private void UpdateChase()
    {
        if (playerTransform == null)
        {
            BecomeDefaultState();
            return;
        }

        // "pathStatus" checks if the AI is confused, stuck, or can't find a way to the player.
        if (!agent.isOnNavMesh || !agent.hasPath || agent.pathStatus != NavMeshPathStatus.PathComplete)
        {
            if (EnsureAgentOnNavMesh()) agent.SetDestination(playerTransform.position);
            else return;
        }
        else
        {
            // Every frame, update our path to where the player is currently standing.
            agent.SetDestination(playerTransform.position);
        }
    }

    private void UpdatePatrol()
    {
        if (patrolPoints == null || patrolPoints.Length == 0) return;
        if (agent.pathPending) return; // Wait if the computer is still doing pathfinding math.

        // "remainingDistance" tells us how close we are to our current waypoint.
        if (agent.remainingDistance <= waypointTolerance)
        {
            // The % (modulo) operator loops our index back to 0 when we reach the end of the array!
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

    // Safety net function: Forces the agent back onto the nearest valid, walkable ground.
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

    // Unity Physics Event: This automatically triggers whenever two objects physically crash into each other.
    void OnCollisionEnter(Collision collision)
    {
        // Did we hit the player?
        if (collision.collider != null && collision.collider.CompareTag("Player"))
        {
            pausedByCollision = true;

            // Stop the AI's engine
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();

#if UNITY_2019_1_OR_NEWER
                agent.velocity = Vector3.zero;
#endif
            }

            if (agent != null) agent.enabled = false;
            SetSyncEnabled(false);

            // --- PROTECT THE PLAYER FROM FLYING ---
            // When two 1500-mass objects collide, Unity generates massive kinetic energy.
            // We intercept that energy here before the player gets launched like a rocket.
            Rigidbody playerRb = collision.collider.attachedRigidbody;
            if (playerRb == null) playerRb = collision.collider.GetComponentInParent<Rigidbody>();

            if (playerRb != null)
            {
                // Read the player's post-crash velocity and apply our 'collisionDampFactor' (e.g., 0.25).
                // This instantly absorbs 75% of the crash energy!
                Vector3 newVel = playerRb.linearVelocity * collisionDampFactor;

                // If they are still moving too fast horizontally, cap their speed.
                float mag = newVel.magnitude;
                if (mag > maxCollisionSpeed) newVel = newVel.normalized * maxCollisionSpeed;

                // If the crash is trying to launch them upwards (Y-axis), force them back down.
                if (newVel.y > maxCollisionUpwardVelocity) newVel.y = maxCollisionUpwardVelocity;

                // Apply the new, safe velocity back to the player.
                playerRb.linearVelocity = newVel;
                playerRb.angularVelocity *= collisionDampFactor; // Kill any crazy spinning
            }

            // --- PROTECT THE ENEMY FROM FLYING EXPLAINED ---
            // The code above saved the player, but Newton's Third Law means the Enemy 
            // still absorbed half the crash! We need to do the exact same safety check for ourselves.
            if (rb != null)
            {
                // Grab the enemy's current speed/direction
                Vector3 myVel = rb.linearVelocity;

                // The Y-axis controls vertical height. If the physics engine says "launch this enemy 
                // upward at 50 meters per second", we step in and say "No, you are only allowed 
                // to bounce upward at 2 meters per second max."
                if (myVel.y > maxCollisionUpwardVelocity) myVel.y = maxCollisionUpwardVelocity;

                // Re-apply the safe, clamped velocity to the enemy tank.
                rb.linearVelocity = myVel;

                // Angular Velocity is "spin". By multiplying it by a fraction, we stop the 
                // tank from spinning like a top after a heavy impact.
                rb.angularVelocity *= collisionDampFactor;
            }
        }
    }

    // Advanced programming tool: "Reflection". This looks into a totally separate script to change a variable!
    private void SetSyncEnabled(bool enabled)
    {
        if (sync == null) sync = GetComponent("NavMeshAgentRigidbodySync") as Component;
        if (sync == null) return;
        try
        {
            var t = sync.GetType();
            var f = t.GetField("enableSync");
            if (f != null) { f.SetValue(sync, enabled); return; }
            var p = t.GetProperty("enableSync");
            if (p != null && p.CanWrite) { p.SetValue(sync, enabled, null); }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[EnemyAI] Failed to set enableSync on sync component: {ex.Message}", gameObject);
        }
    }

    private void SetPausedState(bool paused)
    {
        if (!paused)
        {
            SetSyncEnabled(true);
            if (agent != null)
            {
                agent.enabled = true;
                agent.isStopped = false;
            }
        }
        else
        {
            pausedByCollision = true;
        }
    }

    // This is a special command that ONLY runs in the Unity Editor. It will not compile into the final shipped game.
    // It draws those helpful colored wireframe spheres around the enemy so you can visually see the radar zones!
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