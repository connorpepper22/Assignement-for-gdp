using UnityEngine;

public class Pickup : MonoBehaviour
{
    // --- ENUMS ---
    // 'enum' stands for Enumeration. It's a way to create a custom list of options.
    // By creating this enum, Unity will automatically turn the 'type' variable below 
    // into a clickable drop-down menu in the Inspector! 
    // This lets us use the exact same script for both Health and Armor pickups.
    public enum PickupType { HealthRepair, ArmorModule }
    public PickupType type;

    [Tooltip("How much health to restore or armor to add")]
    public int powerAmount = 1;

    [Header("Effects")]
    public GameObject pickupVFX;
    public AudioClip pickupSound;

    // Optional: Make the pickup spin in the air so it catches the player's eye
    public float spinSpeed = 100f;

    void Update()
    {
        // Simple visual flair so it looks like a classic video game pickup!
        // Vector3.up is shorthand for (0, 1, 0) — meaning it rotates on the Y axis.
        // Space.World ensures it always spins flat relative to the ground, even if it's placed on a tilted hill.
        transform.Rotate(Vector3.up * spinSpeed * Time.deltaTime, Space.World);
    }

    // --- TRIGGERS ---
    // OnTriggerEnter is different from OnCollisionEnter (which we used for bullets and ramming).
    // A "Trigger" is a collider with "Is Trigger" checked in the Inspector. 
    // It acts like a laser tripwire. You don't physically bump into it; you pass right through it, 
    // and it fires off this chunk of code when you do!
    void OnTriggerEnter(Collider other)
    {
        // 1. Did the object that touched us have the "Player" tag?
        if (other.CompareTag("Player"))
        {
            // 2. Try to grab the PlayerHealth script off the object that touched us.
            PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();

            // Make sure the script actually exists before we try to use it!
            if (playerHealth != null)
            {
                // 3. THE SWITCH STATEMENT
                // A 'switch' is basically a much cleaner version of writing a bunch of "if / else if" statements.
                // We check what 'type' this pickup was set to in the Unity Inspector.
                switch (type)
                {
                    case PickupType.HealthRepair: // If it's a Health Repair...

                        // Don't let the player pick it up (and waste it) if their health is already completely full!
                        if (playerHealth.IsAtMaxHealth()) return;

                        // Give them health!
                        playerHealth.Heal(powerAmount);
                        break;

                    case PickupType.ArmorModule: // If it's an Armor Module...

                        // Give them armor!
                        playerHealth.AddArmor(powerAmount);
                        break;
                }

                // 4. FEEDBACK (Audio and Visuals)
                // Play the sound right where the pickup was floating.
                if (pickupSound != null) AudioSource.PlayClipAtPoint(pickupSound, transform.position);

                // Spawn the sparkle/flash particle effect.
                if (pickupVFX != null) Instantiate(pickupVFX, transform.position, Quaternion.identity);

                // 5. DESTROY
                // We've given the player their reward, so delete this pickup from the game world.
                Destroy(gameObject);
            }
        }
    }
}