using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class Player_Controller : MonoBehaviour
{
    [Header("Movement Settings")]
    public float speed = 3f;
    public float rotationSpeed = 120f;

    [Header("Pitch/Slope Settings")]
    [Tooltip("How fast the tank tilts to match the ground.")]
    public float pitchAdjustmentSpeed = 5f;
    [Tooltip("How far down to look for the ground.")]
    public float groundCheckDistance = 1.5f;

    [Header("Audio")]
    private AudioSource engineAudio;

    private Rigidbody rb;
    private InputAction moveAction;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        engineAudio = GetComponent<AudioSource>(); // Grab the audio source here!

        rb.isKinematic = false;

        // --- UPDATE: UNFREEZE X ROTATION ---
        // We keep FreezeRotationZ (Roll) so the tank doesn't tip sideways.
        // We unfreeze FreezeRotationX so the tank can pitch up/down.
        rb.constraints = RigidbodyConstraints.FreezeRotationZ;

        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        moveAction = new InputAction("Move", InputActionType.Value);
        moveAction.AddCompositeBinding("2DVector")
            .With("up", "<Keyboard>/w")
            .With("down", "<Keyboard>/s")
            .With("left", "<Keyboard>/a")
            .With("right", "<Keyboard>/d");
        moveAction.AddCompositeBinding("2DVector")
            .With("up", "<Keyboard>/upArrow")
            .With("down", "<Keyboard>/downArrow")
            .With("left", "<Keyboard>/leftArrow")
            .With("right", "<Keyboard>/rightArrow");
        moveAction.AddBinding("<Gamepad>/leftStick");
    }

    void OnEnable() => moveAction?.Enable();
    void OnDisable() => moveAction?.Disable();
    void OnDestroy()
    {
        moveAction?.Disable();
        moveAction?.Dispose();
    }

    void FixedUpdate()
    {
        if (rb == null) return;

        Vector2 input = ReadInput();
        float move = input.y;
        float turn = input.x;

        if (Mathf.Abs(move) > 0.001f || Mathf.Abs(turn) > 0.001f)
            rb.WakeUp();

        // 1. Handle Movement (Position)
        Vector3 movement = transform.forward * (move * speed * Time.fixedDeltaTime);
        rb.MovePosition(rb.position + movement);

        // 2. Handle Rotation (Yaw + Pitch)
        ApplyRotation(turn);

    }

    void Update()
    {
        if (engineAudio != null && rb != null)
        {
            float speed = rb.linearVelocity.magnitude;

            // Pitch logic
            engineAudio.pitch = 1f + (speed * 0.05f);

            // NEW: Volume fade logic. 
            // If we are moving, smoothly raise volume to 0.5f. If stopped, fade down to 0.1f.
            float targetVolume = speed > 0.1f ? 0.5f : 0.1f;
            engineAudio.volume = Mathf.Lerp(engineAudio.volume, targetVolume, Time.deltaTime * 2f);
        }
    }

    private void ApplyRotation(float turnInput)
    {
        // Calculate the turn (Yaw)
        float turnAngle = turnInput * rotationSpeed * Time.fixedDeltaTime;
        Quaternion turnRotation = Quaternion.Euler(0f, turnAngle, 0f);

        // Calculate the Slope Alignment (Pitch)
        Quaternion slopeRotation = CalculateSlopeRotation();

        // Combine current rotation with the turn, then smooth it toward the slope tilt
        Quaternion finalRotation = Quaternion.Slerp(rb.rotation * turnRotation, slopeRotation, Time.fixedDeltaTime * pitchAdjustmentSpeed);

        rb.MoveRotation(finalRotation);
    }

    private Quaternion CalculateSlopeRotation()
    {
        RaycastHit hit;
        // Shoot a ray from slightly above the center of the tank downward
        if (Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out hit, groundCheckDistance))
        {
            // Project the current forward direction onto the plane we just hit
            Vector3 groundNormal = hit.normal;
            Vector3 forwardOnSlope = Vector3.ProjectOnPlane(transform.forward, groundNormal);

            // Create a rotation that looks forward along the slope while keeping the ground normal as 'Up'
            return Quaternion.LookRotation(forwardOnSlope, groundNormal);
        }

        // If in mid-air, try to keep the rotation flat
        return Quaternion.LookRotation(transform.forward, Vector3.up);
    }

    private Vector2 ReadInput()
    {
        if (moveAction == null) return Vector2.zero;
        Vector2 v = moveAction.ReadValue<Vector2>();
        if (v.sqrMagnitude > 1f) v = v.normalized;
        return v;
    }
}