using UnityEngine;

public class EnemyShooter : MonoBehaviour
{
    public Transform muzzle;
    public GameObject projectilePrefab;

    [Tooltip("How fast the enemy bullet flies")]
    public float projectileSpeed = 15f;

    [Tooltip("Seconds between enemy shots")]
    public float fireRate = 2f;

    [Header("VFX & Audio (Optional)")]
    public ParticleSystem muzzleParticle;
    public AudioClip fireClip;
    [Range(0f, 1f)] public float fireVolume = 1f;

    private float nextFireTime;

    void Start()
    {
        // Add a small random delay so if you spawn 5 enemies, they don't all fire on the exact same frame!
        nextFireTime = Time.time + Random.Range(1f, 3f);
    }

    void Update()
    {
        // Simple AI timer: If enough time has passed, shoot!
        if (Time.time >= nextFireTime)
        {
            Fire();
        }
    }

    private void Fire()
    {
        if (projectilePrefab == null || muzzle == null) return;

        // 1. Spawn Bullet
        var go = Instantiate(projectilePrefab, muzzle.position, muzzle.rotation);

        // 2. Audio & VFX
        if (fireClip != null) AudioSource.PlayClipAtPoint(fireClip, muzzle.position, fireVolume);
        if (muzzleParticle != null) muzzleParticle.Play();

        // 3. Set Velocity
        var rb = go.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.linearVelocity = muzzle.forward * projectileSpeed;
        }

        // 4. Reset Timer
        nextFireTime = Time.time + fireRate;
    }
}