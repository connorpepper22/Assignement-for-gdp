using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Rotates a turret (yaw) and an optional barrel (pitch) using the new Input System.
/// Attach to the turret root (the part that should yaw). Assign the barrel Transform
/// if you want independent elevation (pitch) control.
/// </summary>
[DisallowMultipleComponent]
public class TurretController : MonoBehaviour
{
    [Header("Targets")]
    [Tooltip("Transform that yaws (usually the turret root). Defaults to this GameObject.")]
    public Transform turretYaw;

    [Tooltip("Optional: world-space pivot used for yaw rotation. If set, the turret will RotateAround this pivot.")]
    public Transform yawPivot;

    [Tooltip("Optional pivot offset in local space of the turretYaw (used when yawPivot is not assigned).")]
    public Vector3 pivotOffset = Vector3.zero;

    [Tooltip("Transform that pitches up/down (usually the gun barrel). Optional.")]
    public Transform barrelPitch;

    [Header("Sensitivity & speed")]
    [Tooltip("Multiplier applied to raw input delta")]
    public float sensitivity = 0.15f;

    [Tooltip("Multiplier for yaw rotation speed")]
    public float yawSpeed = 1f;

    [Tooltip("Multiplier for pitch rotation speed")]
    public float pitchSpeed = 1f;

    [Header("Yaw limits (degrees)")]
    [Tooltip("Maximum absolute yaw from the initial turret orientation (degrees).")]
    [Range(0f, 180f)]
    public float maxYawAngle = 90f;

    [Header("Pitch limits (degrees)")]
    public float minPitch = -5f;   // look down limit (local X)
    public float maxPitch = 45f;   // look up limit (local X)

    [Header("Options")]
    [Tooltip("Invert vertical mouse input")]
    public bool invertY = false;

    [Tooltip("Should the cursor be locked & hidden on Start?")]
    public bool lockCursorOnStart = true;

    [Tooltip("Seconds to ignore initial mouse delta after Start (consumes any accumulated delta).")]
    public float initialAimIgnoreSeconds = 0.05f;

    // Input action (mouse delta + gamepad right stick)
    private InputAction aimAction;

    // Current pitch in degrees (local X)
    private float currentPitch = 0f;

    // Yaw tracking: 0 = initial orientation at Start. Positive = clockwise local Y (degrees)
    private float currentYaw = 0f;
    private float initialYaw = 0f;

    // Time until aiming input is ignored (prevent jump from initial cursor position)
    private float ignoreAimUntil = 0f;

    void Awake()
    {
        if (turretYaw == null) turretYaw = transform;

        // Build a simple Aim action: mouse delta & gamepad right stick
        aimAction = new InputAction("Aim", InputActionType.Value);
        // Mouse delta (pointer)
        aimAction.AddBinding("<Mouse>/delta");
        // Touch/stylus pointer delta
        aimAction.AddBinding("<Pointer>/delta");
        // Gamepad right stick
        aimAction.AddBinding("<Gamepad>/rightStick");
    }

    void OnEnable()
    {
        aimAction?.Enable();
    }

    void OnDisable()
    {
        aimAction?.Disable();
    }

    void Start()
    {
        // Lock cursor if desired
        if (lockCursorOnStart)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // Initialize currentPitch from barrel after all Awake() runs (safer)
        if (barrelPitch != null)
        {
            float raw = NormalizeAngle(barrelPitch.localEulerAngles.x);
            currentPitch = Mathf.Clamp(raw, minPitch, maxPitch);
            Vector3 e = barrelPitch.localEulerAngles;
            e.x = currentPitch;
            barrelPitch.localEulerAngles = e;
        }

        // Initialize yaw tracking relative to turretYaw's starting orientation
        initialYaw = NormalizeAngle(turretYaw.eulerAngles.y);
        currentYaw = 0f; // at Start we're at initial orientation

        // Consume any initial mouse/gamepad delta so starting cursor placement doesn't cause a jump.
        aimAction?.ReadValue<Vector2>();
        ignoreAimUntil = Time.time + Mathf.Max(0f, initialAimIgnoreSeconds);
    }

    void Update()
    {
        // Ignore aiming for a short window after Start to avoid an initial jump
        if (Time.time < ignoreAimUntil) return;

        // Read input (could be mouse or gamepad)
        if (aimAction == null) return;

        Vector2 delta = aimAction.ReadValue<Vector2>();

        // For mouse the delta is in pixels/frame, for sticks it's -1..1. Multiply by sensitivity and Time.deltaTime.
        float yawDelta = delta.x * sensitivity * yawSpeed * Time.deltaTime;
        float pitchDelta = delta.y * sensitivity * pitchSpeed * Time.deltaTime;

        // Compute candidate yaw, clamp, and apply only the allowed delta
        if (Mathf.Abs(yawDelta) > 0.0001f)
        {
            float desiredYaw = currentYaw + yawDelta;
            float clampedYaw = Mathf.Clamp(desiredYaw, -Mathf.Abs(maxYawAngle), Mathf.Abs(maxYawAngle));
            float deltaToApply = clampedYaw - currentYaw;

            if (Mathf.Abs(deltaToApply) > 0.00001f)
            {
                // Apply rotation around chosen pivot or local Y
                if (yawPivot != null)
                {
                    turretYaw.RotateAround(yawPivot.position, turretYaw.up, deltaToApply);
                }
                else if (pivotOffset != Vector3.zero)
                {
                    Vector3 pivotWorld = turretYaw.TransformPoint(pivotOffset);
                    turretYaw.RotateAround(pivotWorld, turretYaw.up, deltaToApply);
                }
                else
                {
                    turretYaw.Rotate(0f, deltaToApply, 0f, Space.Self);
                }

                // Update tracked yaw
                currentYaw = clampedYaw;
            }
        }

        // Apply pitch (barrel local X). Invert Y option.
        if (barrelPitch != null && Mathf.Abs(pitchDelta) > 0.0001f)
        {
            float sign = invertY ? 1f : -1f;
            currentPitch += pitchDelta * sign;

            // clamp
            currentPitch = Mathf.Clamp(currentPitch, minPitch, maxPitch);

            // apply as local rotation on X, preserve Y/Z
            Vector3 e = barrelPitch.localEulerAngles;
            e.x = currentPitch;
            barrelPitch.localEulerAngles = e;
        }
    }

    // Utility: convert 0..360 to -180..180 for easier clamping/setting
    private static float NormalizeAngle(float a)
    {
        a = Mathf.Repeat(a + 180f, 360f) - 180f;
        return a;
    }
}
