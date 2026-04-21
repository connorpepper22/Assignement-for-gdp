using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Handles firing projectiles from a muzzle transform using the new Input System.
/// Attach to the turret (or gun) GameObject. Assign a `projectilePrefab` (must have Rigidbody + Collider)
/// and the `muzzle` transform (tip of the barrel).
/// </summary>
[DisallowMultipleComponent]
public class GunController : MonoBehaviour
{
    [Tooltip("Point where projectiles are spawned")]
    public Transform muzzle;

    [Tooltip("Projectile prefab (must contain a Rigidbody and Collider and ideally the Projectile script)")]
    public GameObject projectilePrefab;

    [Tooltip("Impulse applied to projectile Rigidbody")]
    public float fireForce = 800f;

    [Tooltip("Minimum time between shots (seconds)")]
    public float fireRate = 0.25f;

    // Input action for fire (left mouse / gamepad trigger)
    private InputAction fireAction;

    private float nextFireTime;

    void Awake()
    {
        // Build a simple Fire action: left mouse button and gamepad right trigger
        fireAction = new InputAction("Fire", InputActionType.Button);
        fireAction.AddBinding("<Mouse>/leftButton");
        fireAction.AddBinding("<Gamepad>/rightTrigger");
        fireAction.performed += _ => TryFire();
    }

    void OnEnable()
    {
        fireAction?.Enable();
    }

    void OnDisable()
    {
        fireAction?.Disable();
    }

    private void TryFire()
    {
        if (Time.time < nextFireTime) return;
        if (projectilePrefab == null || muzzle == null) return;

        // Spawn projectile
        var go = Instantiate(projectilePrefab, muzzle.position, muzzle.rotation);
        var rb = go.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.AddForce(muzzle.forward * fireForce);
        }

        nextFireTime = Time.time + fireRate;
    }
}