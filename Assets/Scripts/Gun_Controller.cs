using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem; // Required to use Unity's modern Input System

/// <summary>
/// Handles firing projectiles from a muzzle transform using the new Input System.
/// Attach to the turret (or gun) GameObject.
/// </summary>
[DisallowMultipleComponent] // Prevents accidentally attaching two guns to one slot!
public class GunController : MonoBehaviour
{
    [Header("Core Gun Settings")]
    [Tooltip("Point where projectiles are spawned")]
    public Transform muzzle;

    [Tooltip("Projectile prefab (must contain a Rigidbody and Collider and ideally the Projectile script)")]
    // The "blueprint" of the bullet we are going to shoot.
    public GameObject projectilePrefab;

    [Tooltip("Initial speed applied to projectile (meters/second).")]
    public float spawnProjectileSpeed = 30f;

    [Tooltip("Minimum time between shots (seconds)")]
    public float fireRate = 0.25f; // This means we can shoot 4 times per second (1.0 / 0.25)

    [Tooltip("If > 0, overrides the projectile prefab's lifetime when spawned.")]
    public float spawnProjectileLifetime = 0f;

    [Tooltip("Small forward offset to avoid spawning inside the gun/tank collider")]
    // Extremely important! If the bullet spawns exactly inside the barrel, the physics engine
    // might think the tank shot ITSELF and apply damage/knockback to the player instantly.
    public float spawnOffset = 0.5f;

    [Header("Audio (optional)")]
    public AudioClip fireClip;

    // Sometimes we want to use a specific speaker (AudioSource) attached to the tank, 
    // rather than spawning a temporary 3D sound in the world.
    public AudioSource audioSource;
    [Range(0f, 1f)] public float fireVolume = 1f;

    [Header("Muzzle VFX (optional)")]
    // A Particle System already attached to the barrel (like smoke/sparks).
    public ParticleSystem muzzleParticle;

    // A standalone explosion prefab we can spawn at the barrel every time we shoot.
    public GameObject muzzleVFXPrefab;
    public float muzzleVFXLifetime = 2f;

    // --- INPUT SYSTEM VARIABLES ---
    // Instead of using the old Input.GetKeyDown(), we create a flexible 'Action'.
    // This allows us to easily bind Mouse, Keyboard, and Gamepad controllers all to the exact same button!
    private InputAction fireAction;

    // The cooldown timer to stop the player from firing 1000 bullets a second by clicking really fast.
    private float nextFireTime;

    void Awake()
    {
        // --- 1. SETUP THE CONTROLS ---
        // We define a new action called "Fire" that acts like a button press.
        fireAction = new InputAction("Fire", InputActionType.Button);

        // We bind the Left Mouse Button to this action.
        fireAction.AddBinding("<Mouse>/leftButton");

        // We also bind the Xbox/PlayStation Right Trigger to this exact same action!
        fireAction.AddBinding("<Gamepad>/rightTrigger");

        // This is an "Event Subscription" (like we used in Game_State).
        // It says: "Whenever this action is performed, trigger the TryFire() function."
        // The '_ =>' is just shorthand C# syntax for passing an event along.
        fireAction.performed += _ => TryFire();
    }

    // OnEnable and OnDisable are critical for the New Input System.
    // If the player dies or the game is paused, we MUST disable the input so they can't shoot while dead!
    void OnEnable()
    {
        fireAction?.Enable();
    }

    void OnDisable()
    {
        fireAction?.Disable();
    }

    // 'private' because only the Input System should trigger this.
    private void TryFire()
    {
        // --- 2. COOLDOWN & SAFETY CHECKS ---
        if (Time.time < nextFireTime) return; // Gun is still reloading, stop here!
        if (projectilePrefab == null || muzzle == null) return; // Missing pieces, stop here!

        // --- 3. SPAWNING THE BULLET ---
        // Calculate the exact safe spawn point (Muzzle Position + Muzzle Forward Direction * Offset Distance)
        Vector3 spawnPos = muzzle.position + muzzle.forward * spawnOffset;

        // 'go' is our newly spawned bullet.
        var go = Instantiate(projectilePrefab, spawnPos, muzzle.rotation);

        // We can reach into the newly spawned bullet and change its rules dynamically!
        var proj = go.GetComponent<Projectile>();
        if (proj != null && spawnProjectileLifetime > 0f)
        {
            // E.g., if we picked up a "Long Range" powerup, we could increase the bullet's lifespan here.
            proj.lifetime = spawnProjectileLifetime;
        }

        // --- 4. PLAY AUDIO ---
        if (fireClip != null)
        {
            if (audioSource != null)
            {
                // PlayOneShot plays the sound through the tank's speaker without interrupting engine noises.
                audioSource.PlayOneShot(fireClip, fireVolume);
            }
            else
            {
                // Fallback: Just spawn a floating sound in the world.
                AudioSource.PlayClipAtPoint(fireClip, muzzle.position, fireVolume);
            }
        }

        // --- 5. PLAY VISUALS ---
        if (muzzleParticle != null) muzzleParticle.Play(); // Play attached sparks

        if (muzzleVFXPrefab != null)
        {
            // Spawn a temporary muzzle flash prefab
            GameObject vfx = Instantiate(muzzleVFXPrefab, muzzle.position, muzzle.rotation);
            if (muzzleVFXLifetime > 0f) Destroy(vfx, muzzleVFXLifetime); // Clean it up
        }

        // --- 6. LAUNCH THE BULLET ---
        // Grab the physics engine (Rigidbody) of the bullet we just spawned.
        var rb = go.GetComponent<Rigidbody>();
        if (rb != null)
        {
            // Again, ContinuousDynamic prevents fast-moving bullets from clipping through walls.
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            // Apply raw velocity (speed + direction).
            rb.linearVelocity = muzzle.forward * spawnProjectileSpeed;
        }

        // --- 7. RESET THE COOLDOWN ---
        nextFireTime = Time.time + fireRate;
    }
}