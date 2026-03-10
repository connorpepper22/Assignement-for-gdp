using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class Tank_Movement : MonoBehaviour
{
    // Movement speed in units per second
    public float speed = 3f;

    // Rotation speed in degrees per second
    public float rotationSpeed = 120f;

    // Cached Rigidbody (the tank main body)
    private Rigidbody rb;

    // Input Action for movement
    private InputAction moveAction;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = false;

        // Keep the tank upright and allow positional movement
        rb.constraints &= ~RigidbodyConstraints.FreezePositionX;
        rb.constraints &= ~RigidbodyConstraints.FreezePositionY;
        rb.constraints &= ~RigidbodyConstraints.FreezePositionZ;
        rb.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // Build a simple Move action: WASD / arrows / gamepad left stick
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

    void OnEnable()
    {
        moveAction?.Enable();
    }

    void OnDisable()
    {
        moveAction?.Disable();
    }

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

        Vector3 forward = transform.forward;
        Vector3 movement = forward * (move * speed * Time.fixedDeltaTime);
        rb.MovePosition(rb.position + movement);

        float turnAngle = turn * rotationSpeed * Time.fixedDeltaTime;
        if (Mathf.Abs(turnAngle) > 0.0001f)
        {
            Quaternion turnRotation = Quaternion.Euler(0f, turnAngle, 0f);
            rb.MoveRotation(rb.rotation * turnRotation);
        }
    }

    // Read movement from the Input Action (returns normalized Vector2)
    private Vector2 ReadInput()
    {
        if (moveAction == null) return Vector2.zero;
        Vector2 v = moveAction.ReadValue<Vector2>();
        if (v.sqrMagnitude > 1f) v = v.normalized;
        return v;
    }
}
