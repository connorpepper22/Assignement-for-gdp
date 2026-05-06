using UnityEngine;
using UnityEngine.InputSystem; // We need this for mouse and gamepad aiming!

/// <summary>
/// Rotates a turret (yaw) and an optional barrel (pitch) using the new Input System.
/// </summary>
[DisallowMultipleComponent]
public class TurretController : MonoBehaviour
{
    [Header("Targets")]
    [Tooltip("Transform that yaws (usually the turret root). Defaults to this GameObject.")]
    // The main base of the turret that spins like a record player.
    public Transform turretYaw;

    [Tooltip("Optional: world-space pivot used for yaw rotation. If set, the turret will RotateAround this pivot.")]
    // Sometimes a turret doesn't spin perfectly in the center. A pivot lets us define exactly where the "hinge" is.
    public Transform yawPivot;

    [Tooltip("Optional pivot offset in local space of the turretYaw (used when yawPivot is not assigned).")]
    public Vector3 pivotOffset = Vector3.zero;

    [Tooltip("Transform that pitches up/down (usually the gun barrel). Optional.")]
    // The gun barrel itself. We keep this separate from the turret base so it can aim up and down independently!
    public Transform barrelPitch;

    [Header("Sensitivity & speed")]
    // Sensitivity is how much the game multiplies your mouse movement.
    // If this is too high, the turret will violently spin with a tiny mouse twitch.
    public float sensitivity = 0.15f;
    public float yawSpeed = 1f;
    public float pitchSpeed = 1f;

    [Header("Yaw limits (degrees)")]
    [Tooltip("Maximum absolute yaw from the initial turret orientation (degrees).")]
    // [Range] makes a nice slider in the Inspector. This limits how far left/right the turret can spin.
    [Range(0f, 180f)]
    public float maxYawAngle = 90f;

    [Header("Pitch limits (degrees)")]
    // We limit the barrel so the tank can't shoot its own roof!
    public float minPitch = -5f;   // look down limit (local X)
    public float maxPitch = 45f;   // look up limit (local X)

    [Header("Options")]
    [Tooltip("Invert vertical mouse input")]
    // Classic "flight simulator" controls: checking this means pushing the mouse up makes the gun look down.
    public bool invertY = false;

    [Tooltip("Should the cursor be locked & hidden on Start?")]
    // In 3D shooter games, we want to hide the mouse cursor so it doesn't drag off the screen.
    public bool lockCursorOnStart = true;

    [Tooltip("Seconds to ignore initial mouse delta after Start (consumes any accumulated delta).")]
    public float initialAimIgnoreSeconds = 0.05f;

    // --- INTERNAL VARIABLES ---
    private InputAction aimAction;
    private float currentPitch = 0f;
    private float currentYaw = 0f;
    private float initialYaw = 0f;
    private float ignoreAimUntil = 0f;

    void Awake()
    {
        // If we forgot to assign the turret base, just assume it's the object this script is attached to.
        if (turretYaw == null) turretYaw = transform;

        // --- SET UP THE NEW INPUT SYSTEM ---
        // 'Value' means we are reading a continuous number (like mouse movement), not just a true/false button press.
        aimAction = new InputAction("Aim", InputActionType.Value);

        // "delta" is the amount the mouse moved since the last frame.
        aimAction.AddBinding("<Mouse>/delta");
        aimAction.AddBinding("<Pointer>/delta"); // For touch screens/styluses
        aimAction.AddBinding("<Gamepad>/rightStick"); // For Xbox/PS controllers
    }

    // Enable and Disable the inputs safely so players can't aim while dead or paused.
    void OnEnable() { aimAction?.Enable(); }
    void OnDisable() { aimAction?.Disable(); }

    void Start()
    {
        // --- HIDE THE MOUSE ---
        if (lockCursorOnStart)
        {
            Cursor.lockState = CursorLockMode.Locked; // Locks mouse to the dead-center of the screen.
            Cursor.visible = false; // Makes it invisible.
        }

        // --- CALIBRATE THE BARREL ---
        if (barrelPitch != null)
        {
            // We figure out where the barrel is currently pointing when the game starts, 
            // and use that as our "zero" starting point.
            float raw = NormalizeAngle(barrelPitch.localEulerAngles.x);
            currentPitch = Mathf.Clamp(raw, minPitch, maxPitch);

            Vector3 e = barrelPitch.localEulerAngles;
            e.x = currentPitch;
            barrelPitch.localEulerAngles = e;
        }

        // Calibrate the turret base.
        initialYaw = NormalizeAngle(turretYaw.eulerAngles.y);
        currentYaw = 0f;

        // Sometimes when the game first loads, the mouse "jerks" into the locked position. 
        // We ignore input for 0.05 seconds to stop the tank turret from violently snapping on frame 1.
        aimAction?.ReadValue<Vector2>();
        ignoreAimUntil = Time.time + Mathf.Max(0f, initialAimIgnoreSeconds);
    }

    void Update()
    {
        if (Time.time < ignoreAimUntil) return; // Still in our 0.05-second safety window? Stop here.
        if (aimAction == null) return;

        // Read the mouse/joystick movement! This gives us an X (left/right) and Y (up/down) number.
        Vector2 delta = aimAction.ReadValue<Vector2>();

        // Multiply the raw input by our sensitivity and Time.deltaTime (to keep rotation speed perfectly consistent, even if the game lags).
        float yawDelta = delta.x * sensitivity * yawSpeed * Time.deltaTime;
        float pitchDelta = delta.y * sensitivity * pitchSpeed * Time.deltaTime;

        // --- 1. SPIN THE TURRET (YAW) ---
        // Mathf.Abs turns negative numbers positive. So if yawDelta is anything other than 0, we move!
        if (Mathf.Abs(yawDelta) > 0.0001f)
        {
            // Calculate where we WANT to be, and clamp it so we don't spin past our limits.
            float desiredYaw = currentYaw + yawDelta;
            float clampedYaw = Mathf.Clamp(desiredYaw, -Mathf.Abs(maxYawAngle), Mathf.Abs(maxYawAngle));
            float deltaToApply = clampedYaw - currentYaw;

            if (Mathf.Abs(deltaToApply) > 0.00001f)
            {
                // RotateAround spins an object around a specific point in space (like a planet orbiting the sun).
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
                    // Standard rotation if we don't have a custom pivot point.
                    turretYaw.Rotate(0f, deltaToApply, 0f, Space.Self);
                }

                currentYaw = clampedYaw; // Save our new position
            }
        }

        // --- 2. TILT THE BARREL (PITCH) ---
        if (barrelPitch != null && Mathf.Abs(pitchDelta) > 0.0001f)
        {
            // If invertY is true, multiply by 1. If false, multiply by -1 to flip the direction!
            float sign = invertY ? 1f : -1f;
            currentPitch += pitchDelta * sign;

            // Stop the barrel from breaking through the tank roof.
            currentPitch = Mathf.Clamp(currentPitch, minPitch, maxPitch);

            // Apply the new tilt angle to the barrel's local X axis.
            Vector3 e = barrelPitch.localEulerAngles;
            e.x = currentPitch;
            barrelPitch.localEulerAngles = e;
        }
    }

    // --- MATH UTILITY ---
    // Unity sometimes reads angles like 350 degrees when it really means -10 degrees.
    // This math function forces all angles into a clean -180 to 180 range so our Clamps work perfectly!
    private static float NormalizeAngle(float a)
    {
        a = Mathf.Repeat(a + 180f, 360f) - 180f;
        return a;
    }
}