using System.Collections;
using UnityEngine;

public class RoundManager : MonoBehaviour
{
    [Header("Player Settings")]
    public Transform player;

    // --- CUSTOM DATA CLASSES ---
    // [System.Serializable] is a magic tag. 
    // Normally, Unity only shows simple variables (like ints or floats) in the Inspector.
    // By adding this tag above a custom class, we force Unity to draw a nice, neat box in the Inspector 
    // so we can fill out the Player Spawn Point and Enemy Prefab for EACH round!
    [System.Serializable]
    public class RoundSetup
    {
        public Transform playerSpawnPoint;

        [Tooltip("Drag the PREFAB of your enemy group here.")]
        public GameObject enemyGroupPrefab;
    }

    [Header("Round Configurations")]
    // An array ([]) of our custom class. This creates a list in the Inspector where we can add Round 1, Round 2, Round 3, etc.
    public RoundSetup[] rounds;

    // Keep track of where we are. Computers start counting at 0, so Round 1 is actually index 0!
    private int currentRoundIndex = 0;

    // A handle to the current wave of enemies so we can delete them when the round ends.
    private GameObject activeEnemyGroup;

    void Start()
    {
        // --- EVENT SUBSCRIPTION ---
        // We tune into the Game_State's radio broadcast. 
        // Whenever the Game_State shouts "OnRoundChanged!", this script will automatically run 'StartNewRound'.
        if (Game_State.Instance != null)
        {
            Game_State.Instance.OnRoundChanged += StartNewRound;
        }
    }

    // CRITICAL RULE FOR EVENTS: If you subscribe to an event (+), you MUST unsubscribe (-) when this object is destroyed.
    // Otherwise, the Game_State will try to talk to a destroyed RoundManager, causing a massive memory crash!
    void OnDestroy()
    {
        if (Game_State.Instance != null)
        {
            Game_State.Instance.OnRoundChanged -= StartNewRound;
        }
    }

    private void StartNewRound(int roundNumber)
    {
        // Safety check: Don't restart the round if we are already playing it!
        if (currentRoundIndex == roundNumber - 1 && activeEnemyGroup != null)
        {
            return;
        }

        currentRoundIndex = roundNumber - 1;

        // Start the teleportation sequence!
        StartCoroutine(RespawnBoardRoutine());
    }

    // A helper function so other scripts (like PlayerHealth) can ask where the spawn point is.
    public Transform GetCurrentSpawnPoint()
    {
        if (currentRoundIndex >= 0 && currentRoundIndex < rounds.Length)
        {
            return rounds[currentRoundIndex].playerSpawnPoint;
        }
        return null;
    }

    // --- THE PHYSICS TELEPORT SEQUENCE ---
    // Teleporting objects with Rigidbodies is dangerous. If you just change their position, 
    // the physics engine might think they moved at the speed of light and violently crash them into walls!
    // This Coroutine carefully puts the physics engine to sleep, moves the tank, and wakes it back up.
    private IEnumerator RespawnBoardRoutine()
    {
        // Wait for the physics engine to finish whatever math it is currently doing this frame.
        yield return new WaitForFixedUpdate();

        if (currentRoundIndex >= 0 && currentRoundIndex < rounds.Length)
        {
            RoundSetup currentRound = rounds[currentRoundIndex];

            if (player != null && currentRound.playerSpawnPoint != null)
            {
                Rigidbody pRb = player.GetComponent<Rigidbody>();

                // Grab the controller so we can stop input "bleed" from the keyboard
                MonoBehaviour controller = player.GetComponent("Player_Controller") as MonoBehaviour;

                // --- STEP A: Turn off input and physics ---
                if (controller != null) controller.enabled = false;
                if (pRb != null) pRb.isKinematic = true; // Make the tank ignore gravity/collisions temporarily

                // Turn the tank invisible while we move it
                player.gameObject.SetActive(false);

                // --- STEP B: Move Transform & wipe Rigidbody momentum ---
                player.position = currentRound.playerSpawnPoint.position;
                player.rotation = currentRound.playerSpawnPoint.rotation;

                if (pRb != null)
                {
                    pRb.position = currentRound.playerSpawnPoint.position;
                    pRb.rotation = currentRound.playerSpawnPoint.rotation;
                    pRb.linearVelocity = Vector3.zero; // Stop all forward momentum
                    pRb.angularVelocity = Vector3.zero; // Stop all spinning momentum
                }

                Debug.Log($"[GEO-TRACKER] Player is at {player.position}. Spawn Point '{currentRound.playerSpawnPoint.name}' is at {currentRound.playerSpawnPoint.position}.");

                // CRITICAL COMMAND: This forces Unity to instantly update the physics world to match the new coordinates.
                Physics.SyncTransforms();

                // --- STEP C: Turn the tank back on visually ---
                player.gameObject.SetActive(true);

                // --- STEP D: Wait ONE MORE frame for the tank to settle into its new home ---
                yield return new WaitForFixedUpdate();

                // --- STEP E: Give control and physics back to the player ---
                if (pRb != null) pRb.isKinematic = false;
                if (controller != null) controller.enabled = true;

                // --- STEP F: Snap the camera safely to the new location ---
                // If we don't do this, the camera will violently rubber-band across the map to catch up to the newly teleported tank.
                CameraFollow cam = Camera.main.GetComponent<CameraFollow>();
                if (cam != null)
                {
                    cam.SnapToTarget();
                }
            }
            else // <--- THE CRITICAL ALARM
            {
                // If something goes wrong, print a big red error so we know exactly why the teleport failed.
                Debug.LogError($"[CRITICAL ALARM] Teleport aborted! Does Player exist? {player != null}. Does Round {currentRoundIndex + 1} Spawn Point exist? {currentRound.playerSpawnPoint != null}");
            }

            // --- 2. ENEMY CLEANUP ---
            // If there are leftover enemies from the previous round (e.g., the player died and restarted the round), delete them.
            if (activeEnemyGroup != null)
            {
                Destroy(activeEnemyGroup);
            }

            // --- 3. ENEMY SPAWN ---
            // Spawn a brand new, fresh batch of enemies from the RoundSetup Prefab
            if (currentRound.enemyGroupPrefab != null)
            {
                activeEnemyGroup = Instantiate(currentRound.enemyGroupPrefab);

                int enemyCount = 0;

                // 'foreach' loops through every single child object inside the enemy group we just spawned.
                foreach (Transform child in activeEnemyGroup.transform)
                {
                    // Count how many valid enemies are in the group
                    if (child.CompareTag("Enemy") && child.gameObject.activeSelf)
                    {
                        enemyCount++;
                    }
                }

                // Tell the Game_State exactly how many enemies the player needs to destroy to win!
                Game_State.Instance.RegisterEnemies(enemyCount);
            }
        }
    }
}