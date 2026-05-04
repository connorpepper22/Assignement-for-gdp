using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerHealth : MonoBehaviour
{
    [Header("Health Settings")]
    public int maxHealth = 4;
    private int currentHealth;

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
        currentHealth = maxHealth; UpdateHealthUI();
        rb = GetComponent<Rigidbody>();
        controller = GetComponent<Player_Controller>();

        // Grab all child colliders and renderers (the tank body, turret, etc.)
        colliders = GetComponentsInChildren<Collider>();
        renderers = GetComponentsInChildren<Renderer>();
    }

    public void TakeDamage(int damageAmount)
    {
        if (currentHealth <= 0) return; // Already dead, ignore extra bullets

        currentHealth -= damageAmount; UpdateHealthUI();
        Debug.Log($"[PlayerHealth] Player took {damageAmount} damage! Current Health: {currentHealth}");

        if (currentHealth <= 0)
        {
            Die();
        }
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

        // Tell the Game_State we lost a life. 
        // THIS triggers the RoundManager to teleport the player to the correct Area!
        if (Game_State.Instance != null)
        {
            Game_State.Instance.LoseLife(1);

            // Stop here if we got a Game Over
            if (Game_State.Instance.Lives <= 0)
            {
                Debug.Log("GAME OVER!");
                yield break;
            }
        }

        // Kill any leftover physics momentum from the explosion/death
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Heal up
        currentHealth = maxHealth;
        UpdateHealthUI();

        // "Show" the player again
        SetPlayerActive(true);

        // Snap the camera so it doesn't fly across the map during the teleport
        CameraFollow cam = Camera.main.GetComponent<CameraFollow>();
        if (cam != null)
        {
            cam.SnapToTarget();
        }

        Debug.Log("[PlayerHealth] Player Respawn Sequence Completed!");
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
            // Calculate percentage (e.g., 3 / 4 = 0.75f)
            float healthPercent = (float)currentHealth / maxHealth;
            Game_State.Instance.UpdateHullStability(healthPercent);
        }
    }
}