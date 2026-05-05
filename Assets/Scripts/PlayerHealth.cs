using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerHealth : MonoBehaviour
{
    [Header("Health Settings")]
    public int maxHealth = 4;
    private int currentHealth;

    // NEW: Armor System
    [Header("Armor Settings")]
    public int currentArmor = 0;

    [Header("Respawn Settings")]
    public float respawnDelay = 2f;

    [Header("Damage/Death Effects")]
    public GameObject deathVFX;
    public AudioClip deathSound;
    [Range(0f, 1f)] public float volume = 1f;

    // Cache components to disable them during respawn
    private Rigidbody rb;
    private Player_Controller controller;
    private Collider[] colliders;
    private Renderer[] renderers;

    void Start()
    {
        currentHealth = maxHealth;
        currentArmor = 0; // Start with no extra armor
        UpdateHealthUI();

        rb = GetComponent<Rigidbody>();
        controller = GetComponent<Player_Controller>();

        // Grab all child colliders and renderers
        colliders = GetComponentsInChildren<Collider>();
        renderers = GetComponentsInChildren<Renderer>();
    }

    // UPDATE: Armor takes damage before Hull
    public void TakeDamage(int damageAmount)
    {
        if (currentHealth <= 0) return; // Already dead, ignore extra bullets

        // NEW: Tell the Game State we took a hit to trigger the UI red flash!
        if (Game_State.Instance != null) Game_State.Instance.NotifyPlayerDamaged();

        // 1. Armor takes the hit first
        if (currentArmor > 0)
        {
            // Figure out how much the armor can absorb
            int damageToArmor = Mathf.Min(currentArmor, damageAmount);
            currentArmor -= damageToArmor;
            damageAmount -= damageToArmor;

            Debug.Log($"[PlayerHealth] Armor absorbed {damageToArmor} damage! Armor remaining: {currentArmor}");
        }

        // 2. If there is still damage left over, hit the hull
        if (damageAmount > 0)
        {
            currentHealth -= damageAmount;
            Debug.Log($"[PlayerHealth] Hull took {damageAmount} damage! Health remaining: {currentHealth}");
        }

        UpdateHealthUI();

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    // NEW: Health Pickup Logic
    public void Heal(int healAmount)
    {
        currentHealth += healAmount;
        if (currentHealth > maxHealth)
        {
            currentHealth = maxHealth; // Prevent overhealing the hull
        }

        UpdateHealthUI();
        Debug.Log($"[PlayerHealth] Healed! Current Health: {currentHealth}");
    }

    // NEW: Armor Pickup Logic
    public void AddArmor(int armorAmount)
    {
        currentArmor += armorAmount;
        Debug.Log($"[PlayerHealth] Armor Module equipped! Current Armor: {currentArmor}");

        // NEW: Tell the Game State to trigger the UI!
        if (Game_State.Instance != null) Game_State.Instance.NotifyArmorPickedUp();
    }

    // NEW: Helper so we don't accidentally consume health packs when full
    public bool IsAtMaxHealth()
    {
        return currentHealth >= maxHealth;
    }

    private void Die()
    {
        // 1. Play Effects
        if (deathSound != null) AudioSource.PlayClipAtPoint(deathSound, transform.position, volume);
        if (deathVFX != null)
        {
            GameObject vfx = Instantiate(deathVFX, transform.position, Quaternion.identity);
            Destroy(vfx, 2f);
        }

        // 2. Hide the player immediately so they look destroyed
        SetPlayerActive(false);

        // 3. Start the delay/heal process
        StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        // Wait for the respawn delay so the player can watch the explosion
        yield return new WaitForSeconds(respawnDelay);

        // Heal up FIRST and strip away any broken armor
        currentHealth = maxHealth;
        currentArmor = 0;
        UpdateHealthUI();

        // Check lives
        if (Game_State.Instance != null)
        {
            Game_State.Instance.LoseLife(1);
            if (Game_State.Instance.Lives <= 0)
            {
                Debug.Log("GAME OVER!");
                yield break;
            }
        }

        // --- THE BULLETPROOF TELEPORT ---
        // Find the RoundManager and ask it for the safe zone coordinates
        RoundManager rm = FindObjectOfType<RoundManager>();
        if (rm != null)
        {
            Transform safeZone = rm.GetCurrentSpawnPoint();
            if (safeZone != null)
            {
                // 1. Move the raw transform
                transform.position = safeZone.position;
                transform.rotation = safeZone.rotation;

                // 2. Force the Rigidbody to perfectly match, and kill all momentum
                if (rb != null)
                {
                    rb.position = safeZone.position;
                    rb.rotation = safeZone.rotation;
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }

                // 3. FORCE Unity's physics engine to register the new location immediately!
                Physics.SyncTransforms();
            }
        }
        // --------------------------------

        // "Show" the player again ONLY AFTER the teleport is 100% finished
        SetPlayerActive(true);

        // Snap the camera safely to the new spawn location
        CameraFollow cam = Camera.main.GetComponent<CameraFollow>();
        if (cam != null)
        {
            cam.SnapToTarget();
        }
    }

    // Helper method to toggle the player's presence in the world
    private void SetPlayerActive(bool isActive)
    {
        if (controller != null) controller.enabled = isActive;
        foreach (var col in colliders) if (col != null) col.enabled = isActive;
        foreach (var ren in renderers) if (ren != null) ren.enabled = isActive;

        if (rb != null) rb.isKinematic = !isActive;
    }

    private void UpdateHealthUI()
    {
        if (Game_State.Instance != null)
        {
            float healthPercent = (float)currentHealth / maxHealth;
            Game_State.Instance.UpdateHullStability(healthPercent);
        }
    }
}