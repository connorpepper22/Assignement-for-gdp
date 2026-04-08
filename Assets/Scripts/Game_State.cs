using System;
using UnityEngine;

/// Global game state for the match. Tracks player lives and exposes a simple API + events. 
/// Defaults to 3 lives and provides methods to add/remove/set/reset lives.
public class Game_State : MonoBehaviour
{
    // Singleton for easy access from other scripts
    public static Game_State Instance { get; private set; }

    [Header("Lives")]
    [Tooltip("Number of lives the player starts with." )]
    [SerializeField] private int startingLives = 3;

    // Current lives (read-only publicly)
    public int Lives { get; private set; }

    // Events:
    // - OnLivesChanged passes the new lives count
    // - OnLivesDepleted fires when lives reach zero
    public event Action<int> OnLivesChanged;
    public event Action OnLivesDepleted;

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
    }

   
    /// Subtract lives (clamped at zero). Triggers events.
    public void LoseLife(int amount = 1)
    {
        if (amount <= 0) return;

        Lives = Mathf.Max(0, Lives - amount);
        OnLivesChanged?.Invoke(Lives);

        if (Lives == 0)
            OnLivesDepleted?.Invoke();
    }

    /// Add lives. Triggers OnLivesChanged.
   
    public void AddLife(int amount = 1)
    {
        if (amount <= 0) return;

        Lives += amount;
        OnLivesChanged?.Invoke(Lives);
    }

    /// Set lives directly (clamped at zero). Triggers events.
   
    public void SetLives(int value)
    {
        Lives = Mathf.Max(0, value);
        OnLivesChanged?.Invoke(Lives);

        if (Lives == 0)
            OnLivesDepleted?.Invoke();
    }

    /// Reset lives to the configured starting value.
 
    public void ResetLives()
    {
        SetLives(startingLives);
    }
}
