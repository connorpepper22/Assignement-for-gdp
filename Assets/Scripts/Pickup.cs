using UnityEngine;

public class Pickup : MonoBehaviour
{
    // Creates a dropdown in the Inspector to choose what this item does
    public enum PickupType { HealthRepair, ArmorModule }
    public PickupType type;

    [Tooltip("How much health to restore or armor to add")]
    public int powerAmount = 1;

    [Header("Effects")]
    public GameObject pickupVFX;
    public AudioClip pickupSound;

    // Optional: Make the pickup spin in the air
    public float spinSpeed = 100f;

    void Update()
    {
        // Simple visual flair so it looks like a classic video game pickup
        transform.Rotate(Vector3.up * spinSpeed * Time.deltaTime, Space.World);
    }

    void OnTriggerEnter(Collider other)
    {
        // Did the Player touch this?
        if (other.CompareTag("Player"))
        {
            PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();

            if (playerHealth != null)
            {
                // Check what type of pickup this is and apply the effect
                switch (type)
                {
                    case PickupType.HealthRepair:
                        // Don't let the player pick it up if they are already full!
                        if (playerHealth.IsAtMaxHealth()) return;

                        playerHealth.Heal(powerAmount);
                        break;

                    case PickupType.ArmorModule:
                        playerHealth.AddArmor(powerAmount);
                        break;
                }

                // Play the sound and visuals at this location
                if (pickupSound != null) AudioSource.PlayClipAtPoint(pickupSound, transform.position);
                if (pickupVFX != null) Instantiate(pickupVFX, transform.position, Quaternion.identity);

                // Destroy the pickup object
                Destroy(gameObject);
            }
        }
    }
}