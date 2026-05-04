using UnityEngine;

[DisallowMultipleComponent]
public class EnemyAim : MonoBehaviour
{
    [Header("Targeting")]
    [Tooltip("The object to aim at. If empty, it will auto-find the Player tag.")]
    public Transform target;
    public string targetTag = "Player";

    [Header("Aiming Parts")]
    [Tooltip("The part of the tank that rotates left/right (Y axis)")]
    public Transform turretYaw;
    [Tooltip("The gun barrel that aims up/down (X axis)")]
    public Transform barrelPitch;

    [Header("Aiming Speeds")]
    public float yawSpeed = 5f;
    public float pitchSpeed = 5f;

    [Header("Barrel Limits (X-Axis)")]
    public float minPitch = -10f; // Look down limit
    public float maxPitch = 45f;  // Look up limit

    void Start()
    {
        // Auto-find the player at the start of the game
        if (target == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag(targetTag);
            if (p != null) target = p.transform;
        }
    }

    void Update()
    {
        // If the player is dead or missing, stop aiming
        if (target == null || !target.gameObject.activeInHierarchy) return;

        AimTurret();
        AimBarrel();
    }

    private void AimTurret()
    {
        if (turretYaw == null) return;

        // Find the direction to the player, but ignore height (so the turret stays flat)
        Vector3 directionToTarget = target.position - turretYaw.position;
        directionToTarget.y = 0;

        if (directionToTarget.sqrMagnitude > 0.001f)
        {
            // Smoothly rotate the turret on the Y-Axis to face the player
            Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
            turretYaw.rotation = Quaternion.Slerp(turretYaw.rotation, targetRotation, yawSpeed * Time.deltaTime);
        }
    }

    private void AimBarrel()
    {
        if (barrelPitch == null) return;

        // Figure out where the player is relative to the turret's current facing direction
        Vector3 localTargetPos = turretYaw.InverseTransformPoint(target.position);

        // Calculate the angle needed to look up/down at the player
        float pitchAngle = -Mathf.Atan2(localTargetPos.y, localTargetPos.z) * Mathf.Rad2Deg;

        // Clamp the angle so the barrel doesn't clip through the tank hull
        pitchAngle = Mathf.Clamp(pitchAngle, minPitch, maxPitch);

        // Smoothly rotate the barrel locally on the X-Axis
        Quaternion targetRotation = Quaternion.Euler(pitchAngle, 0f, 0f);
        barrelPitch.localRotation = Quaternion.Slerp(barrelPitch.localRotation, targetRotation, pitchSpeed * Time.deltaTime);
    }
}