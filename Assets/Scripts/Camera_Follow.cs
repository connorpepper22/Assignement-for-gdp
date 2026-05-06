using UnityEngine; // This tells the script to use Unity's built-in tools (like Vector3, Transform, and MonoBehaviour).

/// <summary>
/// Simple smooth follow camera using only `offset` (local-space) for position
/// and `lookOffset` (world-space) for the look target. 
/// </summary>
// [DisallowMultipleComponent] is a safety lock. It prevents you or a teammate from accidentally 
// putting two of these camera scripts on the exact same object, which would cause bugs and fighting logic.
[DisallowMultipleComponent]
public class CameraFollow : MonoBehaviour
{
    // [Header(...)] creates a neat, bold title in the Unity Inspector. Great for keeping your variables organized!
    [Header("Target")]

    // A 'Transform' is the component in Unity that holds Position, Rotation, and Scale. 
    // By making this 'public', we can drag our Player into this slot in the Unity Inspector so the camera knows who to chase.
    public Transform target;

    [Header("Follow")]
    // [Tooltip(...)] makes a helpful little text box appear when you hover over the variable in the Inspector.
    [Tooltip("Local-space offset from the target used to compute desired camera position")]

    // A Vector3 is a 3D coordinate (X, Y, Z). 
    // This offset tells the camera where to float relative to the player. 
    // For example: 0 on the X (centered), 5 units up on the Y, and -8 units behind on the Z.
    public Vector3 offset = new Vector3(0f, 5f, -8f);

    [Tooltip("World-space additional offset applied to the look target (fine control)")]
    // This is where the camera should point its "eyes". We add 2.5 to the Y axis so the camera looks slightly above the player's feet.
    public Vector3 lookOffset = new Vector3(0f, 2.5f, 0f);

    [Header("Smoothing")]
    [Tooltip("Smooth time for position smoothing")]
    // 'float' means a number with a decimal point. 
    // Lower numbers make the camera snap to the player faster. Higher numbers make it feel floaty, heavy, and cinematic.
    public float positionSmoothTime = 0.1f;

    [Tooltip("Rotation smoothing factor (0..1) where larger is faster)")]
    public float rotationSmoothTime = 0.08f;

    [Header("Camera Shake Settings")]
    [Tooltip("How violent the camera shake is.")]
    public float shakeMagnitude = 0.2f;
    [Tooltip("How long the camera shakes when hit.")]
    public float shakeDuration = 0.3f;

    // --- INTERNAL VARIABLES ---
    // 'private' means these variables are hidden from the Unity Inspector and other scripts. 
    // They are just temporary memory spaces used by the math formulas below.
    private float currentShakeTime = 0f;
    private Vector3 shakeOffset = Vector3.zero;
    private Vector3 velocity = Vector3.zero;

    // LateUpdate is a built-in Unity event, just like Update or Start.
    // CRITICAL DESIGN RULE: We use LateUpdate for cameras because it runs AFTER the player has finished moving in Update/FixedUpdate. 
    // If the camera and player both moved in standard Update, they might race each other and cause severe screen stuttering!
    void LateUpdate()
    {
        // If we haven't assigned a target, or if the player was destroyed, stop running the code so the game doesn't crash.
        if (target == null) return;

        // --- 1. SHAKE LOGIC ---
        // If our shake timer is active, generate a random bumpy offset.
        if (currentShakeTime > 0)
        {
            // Random.insideUnitSphere gives us a random 3D direction to jitter the camera
            shakeOffset = Random.insideUnitSphere * shakeMagnitude;

            // Count down the timer using Time.deltaTime (the time passed since the last frame)
            currentShakeTime -= Time.deltaTime;
        }
        else
        {
            // Timer is done, reset the shake offset to zero so the camera is perfectly still again
            shakeOffset = Vector3.zero;
        }

        // --- 2. POSITION (Moving the camera) ---
        // TransformPoint takes our 'offset' (which is local/relative to the player) 
        // and calculates the exact 3D world coordinate where the camera should currently hover.
        // We also add our 'shakeOffset' so the camera vibrates when we take damage!
        Vector3 desiredPosition = target.TransformPoint(offset) + shakeOffset;

        // Vector3.SmoothDamp is like a rubber band attaching the camera to the desired position. 
        // It smoothly and organically pulls the camera over.
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref velocity, positionSmoothTime);

        // --- 3. ROTATION (Looking at the target) ---
        // Find the exact 3D spot we want to look at (Player's position + our slight vertical offset).
        Vector3 lookPoint = target.position + lookOffset;

        // LookRotation calculates exactly how much to tilt the camera's 'head' to point perfectly at the lookPoint.
        Quaternion desiredRotation = Quaternion.LookRotation(lookPoint - transform.position);

        // Quaternion.Slerp (Spherical Linear Interpolation) smoothly blends the camera's current rotation 
        // toward the desired rotation over time. This stops the camera from snapping violently when the player turns.
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, Mathf.Clamp01(Time.deltaTime / rotationSmoothTime));
    }

    /// <summary>
    /// Instantly teleports the camera to its proper place behind the target.
    /// Call this immediately after respawning the player!
    /// </summary>
    // 'public' means other scripts (like your Respawn or Round Manager) can trigger this specific chunk of code.
    public void SnapToTarget()
    {
        // Safety check again!
        if (target == null) return;

        // Instantly jump position without the rubber-band smoothing
        transform.position = target.TransformPoint(offset);

        // Instantly snap rotation to look exactly at the target
        Vector3 lookPoint = target.position + lookOffset;
        transform.rotation = Quaternion.LookRotation(lookPoint - transform.position);

        // We must reset our helper 'velocity' to zero. Otherwise, the camera might accidentally 
        // carry over leftover momentum from before the teleport and fling itself away!
        velocity = Vector3.zero;
    }

    /// <summary>
    /// Triggers the camera shake effect. Called by PlayerHealth.cs when the tank takes damage!
    /// </summary>
    public void TriggerShake()
    {
        // Set the timer to our max duration so the shaking logic inside LateUpdate() turns on.
        currentShakeTime = shakeDuration;
    }
}