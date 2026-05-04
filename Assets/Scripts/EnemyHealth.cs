using UnityEngine;

[DisallowMultipleComponent]
public class EnemyHealth : MonoBehaviour
{
    [Header("Health Settings")]
    [Tooltip("The starting health of this enemy.")]
    public int maxHealth = 3;

    // We keep currentHealth private so other scripts can't accidentally mess with it
    private int currentHealth;

    [Header("Death Effects (Optional)")]
    public GameObject deathVFX;
    public AudioClip deathSound;
    [Range(0f, 1f)] public float volume = 1f;

    void Start()
    {
        // Initialize health when the enemy spawns
        currentHealth = maxHealth;
    }

    // This is the public method our Projectile will call when it hits
    public void TakeDamage(int damageAmount)
    {
        currentHealth -= damageAmount;
        Debug.Log($"[EnemyHealth] {gameObject.name} took {damageAmount} damage! Current Health: {currentHealth}");

        // Check if the enemy has run out of health
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        // 1. Play Death Sound
        if (deathSound != null)
        {
            AudioSource.PlayClipAtPoint(deathSound, transform.position, volume);
        }

        // 2. Play Death VFX
        if (deathVFX != null)
        {
            GameObject vfx = Instantiate(deathVFX, transform.position, Quaternion.identity);
            Destroy(vfx, 2f); // Clean up VFX after 2 seconds
        }

        // 3. Update the score / Game_State!
        if (Game_State.Instance != null)
        {
            Game_State.Instance.EnemyDestroyed();
        }

        Destroy(gameObject);
    }
}