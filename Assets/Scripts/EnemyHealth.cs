using UnityEngine; // Required to use Unity-specific tools and components.

// [DisallowMultipleComponent] prevents us from accidentally attaching TWO health scripts 
// to the same enemy, which would cause them to take double damage or die twice!
[DisallowMultipleComponent]
public class EnemyHealth : MonoBehaviour
{
    // [Header] creates a nice bold title in the Unity Inspector to keep things organized.
    [Header("Health Settings")]

    // [Tooltip] adds a helpful description when you hover over 'maxHealth' in the Inspector.
    [Tooltip("The starting health of this enemy.")]

    // 'public' means we can change this number in the Unity Editor without changing the code.
    // E.g., You can make a "Heavy Tank" with 10 health, and a "Light Tank" with 2 health using the exact same script!
    public int maxHealth = 3;

    // 'private' means this variable is hidden from the Inspector and protected from other scripts.
    // We keep currentHealth private so another script can't accidentally set it to 999 or -50.
    // They MUST use our TakeDamage() function to change it.
    private int currentHealth;

    [Header("Death Effects (Optional)")]
    // GameObjects can hold Prefabs (pre-built objects saved in your project folders).
    // Here, we can drag an explosion particle effect prefab into this slot.
    public GameObject deathVFX;

    // AudioClip holds an audio file (like a .wav or .mp3) for the explosion sound.
    public AudioClip deathSound;

    // [Range] turns the float number into a sliding bar in the Inspector from 0 (mute) to 1 (max volume).
    [Range(0f, 1f)] public float volume = 1f;

    // Start runs exactly once when this enemy first spawns into the game.
    void Start()
    {
        // Initialize health when the enemy spawns so they always start with full HP.
        currentHealth = maxHealth;
    }

    // --- TAKING DAMAGE ---
    // 'public' is VERY important here! Because it's public, the Projectile.cs script 
    // is allowed to "talk" to this script and trigger this specific block of code.
    // (int damageAmount) is a parameter—the bullet tells us exactly how much damage it deals.
    public void TakeDamage(int damageAmount)
    {
        // Subtract the damage from our current health.
        currentHealth -= damageAmount;

        // Debug.Log prints a message to the Unity Console. 
        // The '$' allows us to easily inject variables directly into the text using {curly braces}.
        Debug.Log($"[EnemyHealth] {gameObject.name} took {damageAmount} damage! Current Health: {currentHealth}");

        // Check if the enemy has run out of health
        if (currentHealth <= 0)
        {
            Die(); // Trigger the death sequence!
        }
    }

    // --- DYING ---
    // This is 'private' because we don't want other scripts forcing the enemy to die instantly.
    // They have to earn it by reducing the health to zero!
    private void Die()
    {
        // 1. Play Death Sound
        // We check "if (deathSound != null)" to ensure the game doesn't crash if we forgot to assign an audio clip.
        if (deathSound != null)
        {
            // PlayClipAtPoint spawns an invisible, temporary audio player exactly where the enemy died.
            // This is perfect for 3D sounds, so explosions far away sound quieter than ones up close.
            AudioSource.PlayClipAtPoint(deathSound, transform.position, volume);
        }

        // 2. Play Death VFX (Visual Effects)
        if (deathVFX != null)
        {
            // Instantiate is Unity's command to "Spawn" an object.
            // We spawn the explosion (deathVFX), at the enemy's exact position, with zero rotation (Quaternion.identity).
            GameObject vfx = Instantiate(deathVFX, transform.position, Quaternion.identity);

            // Destroy the explosion object after 2 seconds so it doesn't clutter up the game's memory forever.
            Destroy(vfx, 2f);
        }

        // 3. Update the score / Game State!
        // We check if the Game_State script exists in the level.
        // If it does, we trigger its 'EnemyDestroyed' function so it can track our score or spawn the next round.
        if (Game_State.Instance != null)
        {
            Game_State.Instance.EnemyDestroyed();
        }

        // Finally, destroy this enemy GameObject entirely. 
        // (gameObject with a lowercase 'g' refers to the object this specific script is attached to).
        Destroy(gameObject);
    }
}