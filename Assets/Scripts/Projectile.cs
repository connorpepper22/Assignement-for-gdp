using UnityEngine;

// [DisallowMultipleComponent] ensures a bullet can't have two Projectile scripts,
// which would cause it to deal double damage when it hits something!
[DisallowMultipleComponent]
public class Projectile : MonoBehaviour
{
    // [Tooltip] explains these variables in the Unity Inspector.
    [Tooltip("Seconds before this projectile is automatically destroyed.")]
    public float lifetime = 5f;

    [Tooltip("Time after spawn during which collisions are ignored (arming time).")]
    // 'Arming Time' is a classic game design trick. 
    // When a bullet spawns inside a gun barrel, it might accidentally touch the gun's own collider instantly.
    // By giving it a 0.05-second "ghost" period, it has time to safely exit the barrel before it can hit anything.
    public float armingTime = 0.05f;

    public int damage = 1;

    // We store the exact time the bullet was created so we can calculate its Arming Time later.
    private float spawnTime;

    void Start()
    {
        // Time.time gives us the total seconds since the game started.
        spawnTime = Time.time;

        // This is a built-in Unity shortcut! 
        // By adding a number after the object, it acts like a self-destruct timer.
        // If the bullet misses everything and flies into the sky, it will quietly delete itself after 5 seconds to save memory.
        Destroy(gameObject, lifetime);
    }

    // OnCollisionEnter is called automatically by Unity's physics engine 
    // the exact frame this bullet physically crashes into another object.
    void OnCollisionEnter(Collision collision)
    {
        // 1. Arming time safety check
        // "Is the current game time less than my spawn time + 0.05 seconds?"
        // If yes, ignore the collision entirely.
        if (Time.time < spawnTime + armingTime) return;

        // collision.collider is the specific physical shape we just hit.
        var other = collision.collider;

        // If the thing we hit somehow doesn't exist anymore, just destroy the bullet and stop.
        if (other == null)
        {
            Destroy(gameObject);
            return;
        }

        // 2. THE BULLETPROOF CHECK
        // Why GetComponentInParent instead of just GetComponent?
        // In 3D games, a tank might have colliders on its wheels, its turret, and its barrel.
        // Instead of putting a Health script on every single piece, we put ONE Health script on the parent object.
        // This command tells Unity: "Look at the piece I hit, and climb up the hierarchy until you find an EnemyHealth script!"
        EnemyHealth enemy = other.GetComponentInParent<EnemyHealth>();

        if (enemy != null)
        {
            // 1st: Grab the exact 3D coordinate where the bullet touched the metal.
            // GetContact(0).point gives us the very first mathematical point of impact.
            Vector3 impactPoint = collision.GetContact(0).point;

            // Tell our global Game_State exactly where the hit happened so it can play a "hitmarker" sound there!
            if (Game_State.Instance != null) Game_State.Instance.NotifyEnemyHit(impactPoint);

            // 2nd: Apply damage AFTER the UI and sounds are safely triggered.
            enemy.TakeDamage(damage);
        }
        else
        {
            // 3. If it wasn't an enemy, was it the Player?
            // (This allows the exact same bullet prefab to be used by enemies to shoot you!)
            PlayerHealth player = other.GetComponentInParent<PlayerHealth>();
            if (player != null)
            {
                player.TakeDamage(damage);
            }
        }

        // 4. Destroy the bullet after it hits anything (a wall, a floor, an enemy, etc.)
        Destroy(gameObject);
    }
}