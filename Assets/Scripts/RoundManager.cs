using UnityEngine;

public class RoundManager : MonoBehaviour
{
    [Header("Player Settings")]
    public Transform player;

    [System.Serializable]
    public class RoundSetup
    {
        public Transform playerSpawnPoint;
        [Tooltip("Drag the PREFAB of your enemy group here.")]
        public GameObject enemyGroupPrefab;
    }

    [Header("Round Configurations")]
    public RoundSetup[] rounds;

    private int currentRoundIndex = 0;

    // Keeps track of the currently spawned enemies so we can delete them on death/round change
    private GameObject activeEnemyGroup;

    void Start()
    {
        if (Game_State.Instance != null)
        {
            Game_State.Instance.OnRoundChanged += StartNewRound;
            Game_State.Instance.OnLivesChanged += HandlePlayerDeath;

            // Note: We removed the manual StartNewRound(1) here because 
            // your Game_State automatically starts Round 1 on boot!
        }
    }

    void OnDestroy()
    {
        if (Game_State.Instance != null)
        {
            Game_State.Instance.OnRoundChanged -= StartNewRound;
            Game_State.Instance.OnLivesChanged -= HandlePlayerDeath;
        }
    }

    private void StartNewRound(int roundNumber)
    {
        // SAFETY LOCK: Prevent double-spawning if called twice on the same frame
        if (currentRoundIndex == roundNumber - 1 && activeEnemyGroup != null)
        {
            return;
        }

        currentRoundIndex = roundNumber - 1;
        RespawnPlayerAndEnemies();
    }

    private void HandlePlayerDeath(int remainingLives)
    {
        // If the player lost a life but the game isn't over yet
        if (remainingLives > 0)
        {
            Debug.Log("[RoundManager] Player died. Resetting the current round...");
            RespawnPlayerAndEnemies();
        }
    }

    // The master method to reset the board
    private void RespawnPlayerAndEnemies()
    {
        if (currentRoundIndex >= 0 && currentRoundIndex < rounds.Length)
        {
            RoundSetup currentRound = rounds[currentRoundIndex];

            // 1. Teleport the Player to the correct Area
            if (player != null && currentRound.playerSpawnPoint != null)
            {
                player.gameObject.SetActive(false);
                player.position = currentRound.playerSpawnPoint.position;
                player.rotation = currentRound.playerSpawnPoint.rotation;
                player.gameObject.SetActive(true);
            }

            // 2. Clean up the old, half-defeated enemies
            if (activeEnemyGroup != null)
            {
                Destroy(activeEnemyGroup);
            }

            // 3. Spawn a brand new, fresh batch of enemies from the Prefab
            if (currentRound.enemyGroupPrefab != null)
            {
                activeEnemyGroup = Instantiate(currentRound.enemyGroupPrefab);

                int enemyCount = 0;
                foreach (Transform child in activeEnemyGroup.transform)
                {
                    if (child.CompareTag("Enemy") && child.gameObject.activeSelf)
                    {
                        enemyCount++;
                    }
                }

                // Tell the game manager how many targets are in this new area
                Game_State.Instance.RegisterEnemies(enemyCount);
            }
        }
    }
}