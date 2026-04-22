using System.Collections;
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

    [Tooltip("Initial speed applied to projectile (meters/second). Use this to control projectile velocity directly.")]
    public float spawnProjectileSpeed = 30f;

    [Tooltip("Minimum time between shots (seconds)")]
    public float fireRate = 0.25f;

    // Optional: lifetime to assign at spawn (overrides prefab value if > 0)
    [Tooltip("If > 0, overrides the projectile prefab's lifetime when spawned.")]
    public float spawnProjectileLifetime = 0f;

    [Tooltip("Small forward offset to avoid spawning inside the gun/tank collider")]
    public float spawnOffset = 0.5f;

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

        // Spawn projectile slightly forward to avoid immediate collisions with shooter
        Vector3 spawnPos = muzzle.position + muzzle.forward * spawnOffset;
        var go = Instantiate(projectilePrefab, spawnPos, muzzle.rotation);

        // Optionally override lifetime on the spawned projectile
        var proj = go.GetComponent<Projectile>();
        if (proj != null && spawnProjectileLifetime > 0f)
        {
            proj.lifetime = spawnProjectileLifetime;
        }

        // Debug spawn log
        if (proj != null)
            Debug.Log($"[GunController] Spawned projectile '{go.name}' lifetime={proj.lifetime} at time={Time.time}", go);
        else
            Debug.Log($"[GunController] Spawned projectile '{go.name}' (no Projectile component found)", go);

        // Prevent immediate collision between projectile and the shooter:
        // Gather colliders on the shooter's root (parent) or this object and ignore collisions.
        Collider[] ownerCols = null;
        var rootRb = GetComponentInParent<Rigidbody>();
        if (rootRb != null)
            ownerCols = rootRb.GetComponentsInChildren<Collider>();
        else
            ownerCols = GetComponentsInChildren<Collider>();

        var projCols = go.GetComponentsInChildren<Collider>();
        if (projCols.Length > 0 && ownerCols.Length > 0)
        {
            foreach (var pc in projCols)
                foreach (var oc in ownerCols)
                    if (pc != null && oc != null)
                        Physics.IgnoreCollision(pc, oc, true);

            // Re-enable collisions after a short delay so projectile can hit things later
            StartCoroutine(ReenableCollisions(projCols, ownerCols, 0.1f));
        }

        // Set projectile velocity directly (predictable, no huge impulses)
        var rb = go.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            // directly set velocity for consistent behaviour
            rb.linearVelocity = muzzle.forward * spawnProjectileSpeed;
        }

        nextFireTime = Time.time + fireRate;
    }

    private IEnumerator ReenableCollisions(Collider[] projCols, Collider[] ownerCols, float delay)
    {
        yield return new WaitForSeconds(delay);
        foreach (var pc in projCols)
            foreach (var oc in ownerCols)
                if (pc != null && oc != null)
                    Physics.IgnoreCollision(pc, oc, false);
    }
}