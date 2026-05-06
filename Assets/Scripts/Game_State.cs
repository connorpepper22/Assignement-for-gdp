using System; // We need this to use 'Action' (which is C#'s version of an Event).
using UnityEngine;

/// <summary>
/// Global game state for the match. Tracks player lives, score, and rounds.
/// </summary>
public class Game_State : MonoBehaviour
{
    // --- THE SINGLETON PATTERN ---
    // 'public static' means this variable belongs to the CLASS itself, not any specific object. 
    // It creates a globally accessible "Instance" of the Game State. 
    // Any script in your entire game can now type "Game_State.Instance" to instantly talk to this script 
    // without needing to drag-and-drop it in the Inspector or use GetComponent!
    public static Game_State Instance { get; private set; }

    [Header("Lives")]
    [SerializeField] private int startingLives = 3;

    // PROPERTIES: { get; private set; }
    // This is a safety lock. It means ANY script can 'get' (read) the number of lives, 
    // but ONLY this specific Game_State script is allowed to 'set' (change) it.
    public int Lives { get; private set; }
    public int TanksDestroyed { get; private set; }
    public float HullStability { get; private set; } = 1f;

    // --- THE OBSERVER PATTERN (EVENTS) ---
    // 'Action' is a radio broadcast. Instead of the Game_State individually tracking down the UI, 
    // the Audio Manager, and the Player to tell them what happened, it just shouts into a radio: "THE PLAYER LOST A LIFE!"
    // Any other script can "tune in" (subscribe) to this radio station and react however they want.
    public event Action<float> OnHullStabilityChanged;
    public event Action<int> OnLivesChanged;
    public event Action OnLivesDepleted;
    public event Action<int> OnTanksDestroyedChanged;

    public event Action OnArmorPickedUp;
    public event Action OnPlayerDamaged;

    // NEW: Hitmarker event carrying a 3D coordinate (Vector3)! 
    // This allows the UI to know exactly WHERE the bullet hit so it can play a sound there.
    public event Action<Vector3> OnEnemyHit;

    [Header("Round Settings")]
    public int totalRounds = 3;
    public int CurrentRound { get; private set; } = 1;
    public int EnemiesRemaining { get; private set; }

    public event Action OnRoundCleared;
    public event Action<int> OnRoundChanged;
    public event Action OnGameWon;

    void Awake()
    {
        // SINGLETON SETUP: 
        // "If a Game_State already exists in the world, and it's not me, destroy myself."
        // This guarantees there is only ever exactly ONE Game_State manager in the game at a time.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        // "I am the one and only Game_State manager."
        Instance = this;

        // Setup initial values
        Lives = Mathf.Max(0, startingLives);
        TanksDestroyed = 0;
    }

    // Called by the RoundManager to tell the Game_State how many enemies just spawned.
    public void RegisterEnemies(int count) { EnemiesRemaining = count; }

    // Called by EnemyHealth.cs when an enemy dies.
    public void EnemyDestroyed()
    {
        EnemiesRemaining--; // Subtract 1 from remaining enemies
        AddTanksDestroyed(1); // Add 1 to score

        // Check for round/game completion
        if (EnemiesRemaining <= 0)
        {
            if (CurrentRound >= totalRounds) OnGameWon?.Invoke(); // The '?' means "If anyone is listening, trigger this event"
            else OnRoundCleared?.Invoke();
        }
    }

    public void AdvanceRound()
    {
        if (CurrentRound < totalRounds)
        {
            CurrentRound++;
            // Broadcast that the round changed, and send the new round number over the radio.
            OnRoundChanged?.Invoke(CurrentRound);
        }
    }

    // These functions act as middle-men so other scripts can safely trigger events.
    public void NotifyArmorPickedUp() { OnArmorPickedUp?.Invoke(); }
    public void NotifyPlayerDamaged() { OnPlayerDamaged?.Invoke(); }
    public void NotifyEnemyHit(Vector3 hitPosition) { OnEnemyHit?.Invoke(hitPosition); }

    public void LoseLife(int amount = 1)
    {
        if (amount <= 0) return;

        // Mathf.Max guarantees our lives NEVER drop below 0 (no negative lives).
        Lives = Mathf.Max(0, Lives - amount);

        OnLivesChanged?.Invoke(Lives); // Tell the UI to update the text!
        if (Lives == 0) OnLivesDepleted?.Invoke(); // Tell the game to end!
    }

    public void AddLife(int amount = 1)
    {
        if (amount <= 0) return;
        Lives += amount;
        OnLivesChanged?.Invoke(Lives);
    }

    public void SetLives(int value)
    {
        Lives = Mathf.Max(0, value);
        OnLivesChanged?.Invoke(Lives);
        if (Lives == 0) OnLivesDepleted?.Invoke();
    }

    public void ResetLives() { SetLives(startingLives); }

    public void AddTanksDestroyed(int amount = 1)
    {
        if (amount <= 0) return;
        TanksDestroyed += amount;
        OnTanksDestroyedChanged?.Invoke(TanksDestroyed); // Tell UI to update score
    }

    public void SetTanksDestroyed(int value)
    {
        TanksDestroyed = Mathf.Max(0, value);
        OnTanksDestroyedChanged?.Invoke(TanksDestroyed);
    }

    public void ResetTanksDestroyed() { SetTanksDestroyed(0); }

    // Takes a percentage (0.0 to 1.0) and updates the player's health UI.
    public void UpdateHullStability(float percentage)
    {
        // Clamp01 forces the number to stay strictly between 0.0 and 1.0. 
        // If someone tries to pass in 1.5, it just snaps back to 1.0.
        HullStability = Mathf.Clamp01(percentage);
        OnHullStabilityChanged?.Invoke(HullStability);
    }
}