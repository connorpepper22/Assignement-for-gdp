using UnityEngine;
using UnityEngine.InputSystem; // Required for the new modern input system

// [RequireComponent] is a safety net for game designers! 
// It tells Unity: "This script WILL break if there is no Rigidbody attached."
// If you drop this script onto a 3D model, Unity will automatically add a Rigidbody for you.
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

    // We cache the Rigidbody so we can easily talk to the physics engine.
    private Rigidbody rb;

    // 'InputAction' is a flexible container that can hold button presses from keyboards, mice, or Xbox/PS controllers.
    private InputAction moveAction;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        engineAudio = GetComponent<AudioSource>(); // Grab the audio source to make vroom-vroom noises!

        // isKinematic = false means we want gravity and physics to affect the tank.
        rb.isKinematic = false;

        // --- CONSTRAINTS ---
        // Constraints act like superglue for physics. 
        // We freeze the Z-axis (Roll) so the tank can't tip over sideways like a flipped turtle.
        // We leave the X-axis (Pitch) unfrozen so the tank can aim its nose up/down hills.
        rb.constraints = RigidbodyConstraints.FreezeRotationZ;

        // --- NEW: THE ANTI-FLIP FIX ---
        // Every object in Unity has a "Center of Mass" (the balancing point of its weight).
        // By default, it is perfectly in the center (0,0,0). When tanks crash at high speeds, 
        // hitting above the center of mass causes them to flip.
        // By moving it down to -0.5 on the Y-Axis, we make the tank incredibly bottom-heavy 
        // (like a Weeble-Wobble toy), making it almost impossible to flip over in a crash!
        rb.centerOfMass = new Vector3(0f, -0.5f, 0f);

        // Interpolation makes the camera follow look buttery smooth instead of jittery.
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        // Continuous collision stops fast-moving objects from falling through the floor.
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // --- SETTING UP CONTROLS THROUGH CODE ---
        // We create an action that returns a 'Value' (specifically, an X and Y coordinate, like a joystick).
        moveAction = new InputAction("Move", InputActionType.Value);

        // A "Composite Binding" groups 4 buttons together to act like one single joystick.
        moveAction.AddCompositeBinding("2DVector")
            .With("up", "<Keyboard>/w")
            .With("down", "<Keyboard>/s")
            .With("left", "<Keyboard>/a")
            .With("right", "<Keyboard>/d");

        // We add the arrow keys to the exact same action...
        moveAction.AddCompositeBinding("2DVector")
            .With("up", "<Keyboard>/upArrow")
            .With("down", "<Keyboard>/downArrow")
            .With("left", "<Keyboard>/leftArrow")
            .With("right", "<Keyboard>/rightArrow");

        // ...And we also add the Gamepad's Left Stick! Now all 3 control methods work instantly.
        moveAction.AddBinding("<Gamepad>/leftStick");
    }

    // Safety rule for the new input system: You must enable/disable the controls when the object turns on/off.
    void OnEnable() => moveAction?.Enable();
    void OnDisable() => moveAction?.Disable();
    void OnDestroy()
    {
        moveAction?.Disable();
        moveAction?.Dispose(); // Clean up memory when the tank is destroyed
    }

    // --- CRITICAL DESIGN RULE: FixedUpdate vs Update ---
    // Update() runs as fast as your computer can draw frames (maybe 60fps, maybe 144fps).
    // FixedUpdate() runs exactly 50 times a second, no matter what. 
    // ALL Physics math (moving Rigidbodies) MUST happen in FixedUpdate, otherwise players with faster computers will drive faster!
    void FixedUpdate()
    {
        if (rb == null) return;

        // Read our keyboard/gamepad inputs
        Vector2 input = ReadInput();
        float move = input.y; // W/S or Joystick Up/Down
        float turn = input.x; // A/D or Joystick Left/Right

        // 'WakeUp' forces the physics engine to pay attention to the tank if it had "fallen asleep" to save performance.
        if (Mathf.Abs(move) > 0.001f || Mathf.Abs(turn) > 0.001f)
            rb.WakeUp();

        // 1. Handle Movement (Position)
        // transform.forward is the direction the tank's nose is pointing.
        // Time.fixedDeltaTime ensures the speed is perfectly consistent regardless of computer lag.
        Vector3 movement = transform.forward * (move * speed * Time.fixedDeltaTime);

        // MovePosition safely pushes the tank through the physics world.
        rb.MovePosition(rb.position + movement);

        // 2. Handle Rotation (Yaw + Pitch)
        ApplyRotation(turn);
    }

    // Standard Update is great for things that don't push physical objects (like Audio and UI).
    void Update()
    {
        if (engineAudio != null && rb != null)
        {
            // magnitude gives us the exact overall speed of the tank in a single number.
            float currentSpeed = rb.linearVelocity.magnitude;

            // Pitch logic: As the tank drives faster, the engine sound gets higher pitched!
            engineAudio.pitch = 1f + (currentSpeed * 0.05f);

            // Volume fade logic: Mathf.Lerp is a smoothing function. 
            // It gradually fades the audio up to 50% (0.5f) when driving, and fades it down to 10% (0.1f) when idling.
            float targetVolume = currentSpeed > 0.1f ? 0.5f : 0.1f;
            engineAudio.volume = Mathf.Lerp(engineAudio.volume, targetVolume, Time.deltaTime * 2f);
        }
    }

    private void ApplyRotation(float turnInput)
    {
        // 1. Calculate the turn (Yaw - spinning left/right)
        float turnAngle = turnInput * rotationSpeed * Time.fixedDeltaTime;
        Quaternion turnRotation = Quaternion.Euler(0f, turnAngle, 0f);

        // 2. Calculate the Slope Alignment (Pitch - aiming up/down hills)
        Quaternion slopeRotation = CalculateSlopeRotation();

        // Combine our steering wheel turn with the angle of the hill we are parked on.
        // Slerp blends these rotations together so it doesn't instantly snap.
        Quaternion finalRotation = Quaternion.Slerp(rb.rotation * turnRotation, slopeRotation, Time.fixedDeltaTime * pitchAdjustmentSpeed);

        rb.MoveRotation(finalRotation);
    }

    private Quaternion CalculateSlopeRotation()
    {
        RaycastHit hit;
        // Shoot a raycast (invisible laser) straight down to find the floor
        if (Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out hit, groundCheckDistance))
        {
            // ProjectOnPlane takes the tank's forward direction and squashes it flat against the angle of the hill.
            Vector3 groundNormal = hit.normal;
            Vector3 forwardOnSlope = Vector3.ProjectOnPlane(transform.forward, groundNormal);

            // Create a rotation that looks forward along the slope while keeping the ground normal as 'Up'
            return Quaternion.LookRotation(forwardOnSlope, groundNormal);
        }

        // If the raycast misses (we are flying through the air!), try to keep the tank flat to the horizon.
        return Quaternion.LookRotation(transform.forward, Vector3.up);
    }

    private Vector2 ReadInput()
    {
        if (moveAction == null) return Vector2.zero;
        Vector2 v = moveAction.ReadValue<Vector2>();

        // This is a "diagonal fix". If you press 'W' and 'D' at the same time, math makes you move 40% faster than just pressing 'W'.
        // .normalized limits the absolute maximum input to 1.0, so driving diagonally isn't faster than driving straight!
        if (v.sqrMagnitude > 1f) v = v.normalized;

        return v;
    }
}