using UnityEngine;

namespace SAT1Controller
{

public class SAT1Controller : MonoBehaviour
{
    [Header("Car Settings")]
    public float acceleration = 5000f;
    public float maxSpeed = 50f;
    public float turnSpeed = 3f; // Lowered for better control
    public float driftFactor = 0.9f;
    public float driftBoost = 1.2f;
    public float brakeForce = 8000f; // Stronger brake force

    [Header("Wheel Colliders")]
    public WheelCollider frontLeftWheel, frontRightWheel, rearLeftWheel, rearRightWheel;

    [Header("Wheel Mesh Transforms")]
    public Transform frontLeftTransform, frontRightTransform, rearLeftTransform, rearRightTransform;

    private Rigidbody rb;
    private bool isDrifting = false;
    private bool isBraking = false;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.mass = 1000f; // Heavy mass for stability
        rb.centerOfMass = new Vector3(0, -0.5f, 0); // Lower center of mass to prevent flipping
    }

    void FixedUpdate()
    {
        HandleAcceleration();
        HandleSteering();
        HandleDrifting();
        HandleBraking();
        AlignWithGround();
        ApplyDownforce();
        UpdateWheelTransforms();
    }

    void HandleAcceleration()
    {
        float moveInput = Input.GetAxis("Vertical");
        float speedMultiplier = isDrifting ? driftBoost : 1f;

        if (!isBraking && rb.linearVelocity.magnitude < maxSpeed)
        {
            rearLeftWheel.motorTorque = moveInput * acceleration * speedMultiplier;
            rearRightWheel.motorTorque = moveInput * acceleration * speedMultiplier;
        }
    }

    void HandleSteering()
    {
        float steerInput = Input.GetAxis("Horizontal");
        float maxSteerAngle = 30f;
        float steerAngle = steerInput * maxSteerAngle;

        frontLeftWheel.steerAngle = steerAngle;
        frontRightWheel.steerAngle = steerAngle;

        // Adding slight yaw rotation for better turning
        if (rb.linearVelocity.magnitude > 5f) 
        {
            rb.AddTorque(transform.up * steerInput * turnSpeed * rb.linearVelocity.magnitude);
        }
    }

    void HandleDrifting()
    {
        if (Input.GetKey(KeyCode.F))
        {
            isDrifting = true;
            rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, transform.forward * rb.linearVelocity.magnitude * driftFactor, Time.fixedDeltaTime * 5);
        }
        else
        {
            isDrifting = false;
        }
    }

    void HandleBraking()
    {
        if (Input.GetKey(KeyCode.Space))
        {
            isBraking = true;
            rearLeftWheel.brakeTorque = brakeForce;
            rearRightWheel.brakeTorque = brakeForce;
            frontLeftWheel.brakeTorque = brakeForce;
            frontRightWheel.brakeTorque = brakeForce;
        }
        else
        {
            isBraking = false;
            rearLeftWheel.brakeTorque = 0;
            rearRightWheel.brakeTorque = 0;
            frontLeftWheel.brakeTorque = 0;
            frontRightWheel.brakeTorque = 0;
        }
    }

    void AlignWithGround()
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position + Vector3.up * 0.5f, -Vector3.up, out hit, 1.5f))
        {
            Quaternion groundRotation = Quaternion.FromToRotation(transform.up, hit.normal) * transform.rotation;
            transform.rotation = Quaternion.Slerp(transform.rotation, groundRotation, Time.fixedDeltaTime * 5f);
        }
    }

    void ApplyDownforce()
    {
        rb.AddForce(-transform.up * 500f);
    }

    void UpdateWheelTransforms()
    {
        UpdateWheel(frontLeftWheel, frontLeftTransform);
        UpdateWheel(frontRightWheel, frontRightTransform);
        UpdateWheel(rearLeftWheel, rearLeftTransform);
        UpdateWheel(rearRightWheel, rearRightTransform);
    }

    void UpdateWheel(WheelCollider collider, Transform transform)
    {
        Vector3 position;
        Quaternion rotation;
        collider.GetWorldPose(out position, out rotation);
        transform.position = position;
        transform.rotation = rotation;
    }
}
}