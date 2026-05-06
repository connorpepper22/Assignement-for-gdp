using UnityEngine;

[DisallowMultipleComponent]
public class EnemyAim : MonoBehaviour
{
    [Header("Targeting")]
    [Tooltip("The object to aim at. If empty, it will auto-find the Player tag.")]
    // The target is usually the Player. We leave it public so we can drag the player in, 
    // but the script is smart enough to find the player on its own if we forget!
    public Transform target;
    public string targetTag = "Player";

    [Header("Aiming Parts")]
    // We separate the Turret and the Barrel so they can move independently.
    [Tooltip("The part of the tank that rotates left/right (Y axis)")]
    public Transform turretYaw;

    [Tooltip("The gun barrel that aims up/down (X axis)")]
    public Transform barrelPitch;

    [Header("Aiming Speeds")]
    // How fast the enemy tracks the player. If you want a "sniper" enemy, make these slow 
    // so the player has time to dodge!
    public float yawSpeed = 5f;
    public float pitchSpeed = 5f;

    [Header("Barrel Limits (X-Axis)")]
    // If we don't clamp (limit) the pitch, the enemy could point its gun straight down 
    // and shoot itself through its own hull!
    public float minPitch = -10f; // Look down limit
    public float maxPitch = 45f;  // Look up limit

    void Start()
    {
        // Auto-find the player at the start of the game
        // If 'target' is blank, the computer will search the entire level for an object tagged "Player".
        if (target == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag(targetTag);
            if (p != null) target = p.transform;
        }
    }

    void Update()
    {
        // Safety Check: If the player died, was destroyed, or hidden (inactive), STOP running the aiming code.
        if (target == null || !target.gameObject.activeInHierarchy) return;

        // Call our two custom aiming functions every single frame.
        AimTurret();
        AimBarrel();
    }

    // --- 1. YAW (Spinning Left and Right) ---
    private void AimTurret()
    {
        if (turretYaw == null) return;

        // 3D Math trick: To find the direction FROM point A TO point B, you do (B - A).
        Vector3 directionToTarget = target.position - turretYaw.position;

        // CRITICAL: We force the Y (up/down) direction to 0. 
        // We only want the turret base to spin like a lazy susan, we NEVER want it to tilt into the ground.
        directionToTarget.y = 0;

        // sqrMagnitude measures the length of the direction line. 
        // We check if it's > 0.001f to ensure the player isn't standing perfectly dead-center inside the turret.
        if (directionToTarget.sqrMagnitude > 0.001f)
        {
            // Quaternion.LookRotation tells Unity: "Calculate the exact angle required to look down this invisible line."
            Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);

            // Slerp (Spherical Linear Interpolation) smoothly turns the turret over time based on our 'yawSpeed'.
            turretYaw.rotation = Quaternion.Slerp(turretYaw.rotation, targetRotation, yawSpeed * Time.deltaTime);
        }
    }

    // --- 2. PITCH (Tilting the gun Up and Down) ---
    private void AimBarrel()
    {
        if (barrelPitch == null) return;

        // InverseTransformPoint converts "World Space" coordinates into "Local Space" coordinates.
        // Basically, it translates the player's world position into a coordinate that is purely relative to the turret's current facing direction.
        Vector3 localTargetPos = turretYaw.InverseTransformPoint(target.position);

        // Trigonometry time! Atan2 (Arctangent) calculates the exact angle of a triangle.
        // We are using the Y (height) and Z (distance forward) of the local target position to figure out how much to tilt the gun.
        // We multiply by Rad2Deg to convert the raw math (Radians) into readable Degrees (like 45°).
        float pitchAngle = -Mathf.Atan2(localTargetPos.y, localTargetPos.z) * Mathf.Rad2Deg;

        // Clamp stops the angle from exceeding our minimum or maximum values.
        pitchAngle = Mathf.Clamp(pitchAngle, minPitch, maxPitch);

        // Create a new rotation applying our angle ONLY to the X-Axis (Pitch).
        Quaternion targetRotation = Quaternion.Euler(pitchAngle, 0f, 0f);

        // Use localRotation because we want the barrel to rotate relative to the turret it is attached to, not the world itself.
        barrelPitch.localRotation = Quaternion.Slerp(barrelPitch.localRotation, targetRotation, pitchSpeed * Time.deltaTime);
    }
}