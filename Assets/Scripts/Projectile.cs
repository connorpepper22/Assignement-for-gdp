using UnityEngine;

/// <summary>
/// Simple projectile behaviour: destroys itself after lifetime and notifies Game_State on hits.
/// Attach to your projectile prefab (requires Rigidbody + Collider).
/// </summary>
[DisallowMultipleComponent]
public class Projectile : MonoBehaviour
{
    public float lifetime = 5f;
    public int damage = 1;

    // Optional tag check; if the hit object has this tag, it's counted as a destroyed tank.
    // Set to "Tank" or "Enemy" depending on your scene. Leave empty to skip.
    public string targetTag = "Tank";

    void Start()
    {
        Destroy(gameObject, lifetime);
    }

    void OnCollisionEnter(Collision collision)
    {
        var other = collision.collider;
        if (other == null)
        {
            Destroy(gameObject);
            return;
        }

        // If the hit object is a tank (by tag) notify Game_State and optionally apply behavior.
        if (!string.IsNullOrEmpty(targetTag) && other.CompareTag(targetTag))
        {
            // Example: increment destroyed counter. If you want to remove the tank or reduce lives,
            // do it here (call Game_State.Instance.LoseLife(...) or destroy the tank).
            if (Game_State.Instance != null)
            {
                Game_State.Instance.AddTanksDestroyed(1);
                // Optionally: also reduce player lives or damage the tank if appropriate.
                // Game_State.Instance.LoseLife(1);
            }
        }

        // Destroy projectile on any collision
        Destroy(gameObject);
    }
}