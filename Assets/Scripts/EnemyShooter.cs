using UnityEngine;

// This script makes the enemy shoot projectiles at a set interval.
public class EnemyShooter : MonoBehaviour
{
    // The 'muzzle' is usually an empty GameObject placed right at the tip of the enemy's gun barrel.
    // We use its Transform to know exactly WHERE and in what DIRECTION to spawn the bullet.
    public Transform muzzle;

    // A 'Prefab' is a pre-made object saved in your project folders (like a blueprint for a bullet).
    // We drag that blueprint here so the script knows WHAT to spawn.
    public GameObject projectilePrefab;

    [Tooltip("How fast the enemy bullet flies")]
    public float projectileSpeed = 15f;

    [Tooltip("Seconds between enemy shots")]
    public float fireRate = 2f; // This is our cooldown time.

    [Header("VFX & Audio (Optional)")]
    // Particle systems are great for muzzle flashes!
    public ParticleSystem muzzleParticle;
    public AudioClip fireClip;
    [Range(0f, 1f)] public float fireVolume = 1f;

    // 'nextFireTime' tracks the exact moment in the future when the enemy is allowed to shoot again.
    private float nextFireTime;

    void Start()
    {
        // Time.time is a built-in Unity variable that tracks the total seconds the game has been running.
        // When the enemy spawns, we give it a random initial delay (between 1 and 3 seconds).

        // DESIGN TRICK: If you spawn a wave of 5 enemies at the exact same time, this random 
        // delay stops them from shooting in perfect, robotic synchronization!
        nextFireTime = Time.time + Random.Range(1f, 3f);
    }

    void Update()
    {
        // Simple Cooldown Timer: 
        // "Is the current game time greater than or equal to my scheduled next shot?"
        if (Time.time >= nextFireTime)
        {
            Fire();
        }
    }

    // 'private' because only this specific enemy decides when it gets to fire.
    private void Fire()
    {
        // Safety check! If we forgot to assign a bullet blueprint or a muzzle in the Inspector, 
        // stop here to avoid crashing the game.
        if (projectilePrefab == null || muzzle == null) return;

        // --- 1. SPAWN THE BULLET ---
        // Instantiate means "Spawn". We spawn the prefab at the muzzle's exact position and rotation.
        // 'var go' (short for GameObject) creates a temporary handle to the freshly spawned bullet so we can mess with it.
        var go = Instantiate(projectilePrefab, muzzle.position, muzzle.rotation);

        // --- 2. PLAY AUDIO & VISUALS ---
        // PlayClipAtPoint places the sound in the 3D world, so it gets quieter as you move away from it.
        if (fireClip != null) AudioSource.PlayClipAtPoint(fireClip, muzzle.position, fireVolume);

        // Trigger the muzzle flash!
        if (muzzleParticle != null) muzzleParticle.Play();

        // --- 3. LAUNCH THE BULLET ---
        // We need to access the Rigidbody (the physics engine component) of the spawned bullet.
        var rb = go.GetComponent<Rigidbody>();
        if (rb != null)
        {
            // ContinuousDynamic is a very important physics setting for bullets. 
            // Normally, Unity updates physics in frames. Fast-moving objects effectively teleport between frames, 
            // which means a fast bullet can accidentally "phase" through a thin wall!
            // This setting forces Unity to carefully draw a line between the frames so the bullet never clips through walls.
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            // linearVelocity applies raw speed directly forward from the muzzle.
            rb.linearVelocity = muzzle.forward * projectileSpeed;
        }

        // --- 4. RESET THE TIMER ---
        // Schedule the next shot! (Current time + our 2-second fire rate).
        nextFireTime = Time.time + fireRate;
    }
}