using System;
using UnityEngine;

/// <summary>
/// Global game state for the match. Tracks player lives and tanks destroyed count.
/// Place this on a persistent GameObject (e.g., a "GameManager" or Canvas root).
/// </summary>
public class Game_State : MonoBehaviour
{
    // Singleton for easy access from other scripts
    public static Game_State Instance { get; private set; }

    [Header("Lives")]
    [Tooltip("Number of lives the player starts with.")]
    [SerializeField] private int startingLives = 3;

    // Current lives (read-only publicly)
    public int Lives { get; private set; }

    // Tanks destroyed counter (read-only publicly)
    public int TanksDestroyed { get; private set; }

    // Events:
    // - OnLivesChanged passes the new lives count
    // - OnLivesDepleted fires when lives reach zero
    // - OnTanksDestroyedChanged passes the new destroyed count
    public event Action<int> OnLivesChanged;
    public event Action OnLivesDepleted;
    public event Action<int> OnTanksDestroyedChanged;

    void Awake()
    {
        // Singleton setup (simple)
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        Lives = Mathf.Max(0, startingLives);
        TanksDestroyed = 0;
    }

    /// <summary>
    /// Subtract lives (clamped at zero). Triggers events.
    /// </summary>
    public void LoseLife(int amount = 1)
    {
        if (amount <= 0) return;

        Lives = Mathf.Max(0, Lives - amount);
        OnLivesChanged?.Invoke(Lives);

        if (Lives == 0)
            OnLivesDepleted?.Invoke();
    }

    /// <summary>
    /// Add lives. Triggers OnLivesChanged.
    /// </summary>
    public void AddLife(int amount = 1)
    {
        if (amount <= 0) return;

        Lives += amount;
        OnLivesChanged?.Invoke(Lives);
    }

    /// <summary>
    /// Set lives directly (clamped at zero). Triggers events.
    /// </summary>
    public void SetLives(int value)
    {
        Lives = Mathf.Max(0, value);
        OnLivesChanged?.Invoke(Lives);

        if (Lives == 0)
            OnLivesDepleted?.Invoke();
    }

    /// <summary>
    /// Reset lives to the configured starting value.
    /// </summary>
    public void ResetLives()
    {
        SetLives(startingLives);
    }

    /// <summary>
    /// Increment tanks destroyed counter and notify listeners.
    /// </summary>
    public void AddTanksDestroyed(int amount = 1)
    {
        if (amount <= 0) return;

        TanksDestroyed += amount;
        OnTanksDestroyedChanged?.Invoke(TanksDestroyed);
    }

    /// <summary>
    /// Set tanks destroyed directly (clamped at zero).
    /// </summary>
    public void SetTanksDestroyed(int value)
    {
        TanksDestroyed = Mathf.Max(0, value);
        OnTanksDestroyedChanged?.Invoke(TanksDestroyed);
    }

    /// <summary>
    /// Reset destroyed counter to zero.
    /// </summary>
    public void ResetTanksDestroyed()
    {
        SetTanksDestroyed(0);
    }
}
