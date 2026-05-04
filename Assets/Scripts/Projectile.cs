using UnityEngine;

/// <summary>
/// Simple projectile behaviour: destroys itself after lifetime and notifies Game_State on hits.
/// Attach to your projectile prefab (requires Rigidbody + Collider).
/// </summary>
[DisallowMultipleComponent]
public class Projectile : MonoBehaviour
{ // <-- This opening brace was also missing!

    [Tooltip("Seconds before this projectile is automatically destroyed.")]
    public float lifetime = 5f;

    [Tooltip("Time after spawn during which collisions are ignored (arming time).")]
    public float armingTime = 0.05f;

    public int damage = 1;
    public string targetTag = "Tank";

    private float spawnTime;

    void Start()
    {
        spawnTime = Time.time;
        Debug.Log($"[Projectile] Spawned '{name}' lifetime={lifetime} armingTime={armingTime} at {spawnTime}", gameObject);
        Destroy(gameObject, lifetime);
    }

    void OnCollisionEnter(Collision collision)
    {
        // If still within arming time, ignore this collision (prevents immediate self-hit)
        if (Time.time < spawnTime + armingTime)
        {
            Debug.Log($"[Projectile] Ignoring collision during arming: {name} with {collision.collider.gameObject.name}", gameObject);
            return;
        }

        var other = collision.collider;
        if (other == null)
        {
            Debug.Log($"[Projectile] Collided with null at {Time.time}", gameObject);
            Destroy(gameObject);
            return;
        }

        // Log what we hit for debugging
        Debug.Log($"[Projectile] '{name}' collided with '{other.gameObject.name}' (tag='{other.gameObject.tag}') at {Time.time}", gameObject);

        if (!string.IsNullOrEmpty(targetTag) && other.CompareTag(targetTag))
        {
            // 1. Try to damage an Enemy
            EnemyHealth enemy = other.gameObject.GetComponentInParent<EnemyHealth>();
            if (enemy != null)
            {
                enemy.TakeDamage(damage);
            }

            // 2. Try to damage the Player
            PlayerHealth player = other.gameObject.GetComponentInParent<PlayerHealth>();
            if (player != null)
            {
                player.TakeDamage(damage);
            }
        }

        Destroy(gameObject);
    }
}