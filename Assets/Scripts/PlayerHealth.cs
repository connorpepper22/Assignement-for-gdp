using UnityEngine;
using System.Collections;

/// <summary>
/// Full-featured Player Health system.
/// </summary>
[DisallowMultipleComponent]
public class PlayerHealth : MonoBehaviour
{
    [Header("Health & Armor Stats")]
    public int maxHealth = 10;
    public int armorBonus = 0;

    [Header("Respawn Invulnerability")]
    [Tooltip("Seconds of safety after respawning.")]
    public float invulnerabilityDuration = 1.5f;
    [Tooltip("How fast the player model flashes when respawning.")]
    public float flickerInterval = 0.1f;

    [Header("Visual & Audio Feedback")]
    public GameObject damageVFX;
    public GameObject deathVFX;
    public AudioClip damageSound;
    public AudioClip deathSound;
    public AudioClip healSound;

    [Header("Screen Shake")]
    public bool shakeCameraOnDamage = true;

    [Header("Debug Info")]
    [SerializeField] private int currentHealth;
    [SerializeField] private bool isInvulnerable = false;

    // We brought the renderers back so we can flash the tank!
    private MeshRenderer[] renderers;
    private Rigidbody rb;
    private MonoBehaviour controller;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        controller = GetComponent("Player_Controller") as MonoBehaviour;

        // Grab all the 3D models on the tank so we can flash them later
        renderers = GetComponentsInChildren<MeshRenderer>();

        currentHealth = maxHealth;
    }

    private void Start()
    {
        UpdateGameState();
    }

    public void TakeDamage(int damageAmount)
    {
        // Still ignore damage if dead or currently in our respawn I-frames
        if (currentHealth <= 0 || isInvulnerable) return;

        int damageAfterArmor = damageAmount;
        if (armorBonus > 0)
        {
            damageAfterArmor = Mathf.Max(1, damageAmount - 1);
        }

        currentHealth -= damageAfterArmor;

        if (damageSound != null) AudioSource.PlayClipAtPoint(damageSound, transform.position);
        if (damageVFX != null) Instantiate(damageVFX, transform.position, Quaternion.identity);

        if (Game_State.Instance != null)
        {
            Game_State.Instance.NotifyPlayerDamaged();
        }

        if (shakeCameraOnDamage)
        {
            CameraFollow cam = Camera.main?.GetComponent<CameraFollow>();
            if (cam != null) cam.TriggerShake();
        }

        if (currentHealth <= 0)
        {
            Die();
        }
        // REMOVED: We no longer trigger the Invulnerability Coroutine here when taking a hit!

        UpdateGameState();
    }

    public void Heal(int amount)
    {
        if (currentHealth >= maxHealth) return;
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        if (healSound != null) AudioSource.PlayClipAtPoint(healSound, transform.position);
        UpdateGameState();
    }

    public void AddArmor(int amount)
    {
        armorBonus += amount;
        if (Game_State.Instance != null) Game_State.Instance.NotifyArmorPickedUp();
    }

    public bool IsAtMaxHealth()
    {
        return currentHealth >= maxHealth;
    }

    private void Die()
    {
        currentHealth = 0;
        UpdateGameState();

        if (deathSound != null) AudioSource.PlayClipAtPoint(deathSound, transform.position);
        if (deathVFX != null) Instantiate(deathVFX, transform.position, Quaternion.identity);

        if (Game_State.Instance != null)
        {
            Game_State.Instance.LoseLife(1);

            if (Game_State.Instance.Lives > 0)
            {
                Respawn();
            }
            else
            {
                if (controller != null) controller.enabled = false;
                if (rb != null) rb.isKinematic = true;
            }
        }
    }

    private void Respawn()
    {
        currentHealth = maxHealth;
        armorBonus = 0;

        RoundManager rm = FindObjectOfType<RoundManager>();
        if (rm != null)
        {
            Transform spawn = rm.GetCurrentSpawnPoint();
            if (spawn != null)
            {
                transform.position = spawn.position;
                transform.rotation = spawn.rotation;

                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
            }
        }

        UpdateGameState();

        // This is now the ONLY place the invulnerability flashing is triggered!
        StartCoroutine(InvulnerabilityRoutine());
    }

    private void UpdateGameState()
    {
        if (Game_State.Instance != null)
        {
            float ratio = (float)currentHealth / maxHealth;
            Game_State.Instance.UpdateHullStability(ratio);
        }
    }

    // --- RESTORED FLASHING LOGIC ---
    private IEnumerator InvulnerabilityRoutine()
    {
        isInvulnerable = true;
        float timer = 0;

        while (timer < invulnerabilityDuration)
        {
            // Toggle all mesh renderers off and on
            foreach (var r in renderers)
            {
                if (r != null) r.enabled = !r.enabled;
            }

            yield return new WaitForSeconds(flickerInterval);
            timer += flickerInterval;
        }

        // Ensure all renderers are locked back ON when finished
        foreach (var r in renderers)
        {
            if (r != null) r.enabled = true;
        }

        isInvulnerable = false;
    }

    private void OnValidate()
    {
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
    }
}