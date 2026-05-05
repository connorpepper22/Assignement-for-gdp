using System.Collections;
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
    private GameObject activeEnemyGroup;

    void Start()
    {
        if (Game_State.Instance != null)
        {
            Game_State.Instance.OnRoundChanged += StartNewRound;
        }
    }

    void OnDestroy()
    {
        if (Game_State.Instance != null)
        {
            Game_State.Instance.OnRoundChanged -= StartNewRound;
        }
    }

    private void StartNewRound(int roundNumber)
    {
        if (currentRoundIndex == roundNumber - 1 && activeEnemyGroup != null)
        {
            return;
        }

        currentRoundIndex = roundNumber - 1;
        StartCoroutine(RespawnBoardRoutine());
    }

    public Transform GetCurrentSpawnPoint()
    {
        if (currentRoundIndex >= 0 && currentRoundIndex < rounds.Length)
        {
            return rounds[currentRoundIndex].playerSpawnPoint;
        }
        return null;
    }

    private IEnumerator RespawnBoardRoutine()
    {
        // Wait for the physics engine to finish the current frame
        yield return new WaitForFixedUpdate();

        if (currentRoundIndex >= 0 && currentRoundIndex < rounds.Length)
        {
            RoundSetup currentRound = rounds[currentRoundIndex];

            if (player != null && currentRound.playerSpawnPoint != null)
            {
                Rigidbody pRb = player.GetComponent<Rigidbody>();

                // Grab the controller so we can stop input "bleed" from the keyboard
                MonoBehaviour controller = player.GetComponent("Player_Controller") as MonoBehaviour;

                // Step A: Turn off input and physics
                if (controller != null) controller.enabled = false;
                if (pRb != null) pRb.isKinematic = true;

                player.gameObject.SetActive(false);

                // Step B: Move Transform & wipe Rigidbody momentum
                player.position = currentRound.playerSpawnPoint.position;
                player.rotation = currentRound.playerSpawnPoint.rotation;

                if (pRb != null)
                {
                    pRb.position = currentRound.playerSpawnPoint.position;
                    pRb.rotation = currentRound.playerSpawnPoint.rotation;
                    pRb.linearVelocity = Vector3.zero;
                    pRb.angularVelocity = Vector3.zero;
                }
                Debug.Log($"[GEO-TRACKER] Player is at {player.position}. Spawn Point '{currentRound.playerSpawnPoint.name}' is at {currentRound.playerSpawnPoint.position}.");
                Physics.SyncTransforms();

                // Step C: Turn the tank back on visually
                player.gameObject.SetActive(true);

                // Step D: Wait ONE MORE frame for the tank to settle into its new home
                yield return new WaitForFixedUpdate();

                // Step E: Give control and physics back to the player
                if (pRb != null) pRb.isKinematic = false;
                if (controller != null) controller.enabled = true;

                // Snap the camera safely to the new location
                CameraFollow cam = Camera.main.GetComponent<CameraFollow>();
                if (cam != null)
                {
                    cam.SnapToTarget();
                }
            }
            else // <--- THE CRITICAL ALARM
            {
                Debug.LogError($"[CRITICAL ALARM] Teleport aborted! Does Player exist? {player != null}. Does Round {currentRoundIndex + 1} Spawn Point exist? {currentRound.playerSpawnPoint != null}");
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

                Game_State.Instance.RegisterEnemies(enemyCount);
            }
        }
    }
}
