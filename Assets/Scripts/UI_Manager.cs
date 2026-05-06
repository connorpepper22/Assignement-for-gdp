using System;
using System.Collections;
using System.Collections.Generic; // Required to use 'Lists', which are like Arrays but can grow and shrink in size dynamically!
using UnityEngine;
using UnityEngine.UI; // Required for classic Unity UI elements (like basic Text or Images)
using TMPro; // TextMeshPro: The modern, high-quality text system in Unity! Always use this over classic Text when possible.
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class UI_Manager : MonoBehaviour
{
    // --- CUSTOM CLASSES & ENUMS ---
    public enum DisplayMode { Timed, Permanent }

    // [System.Serializable] tells Unity to draw this custom class in the Inspector.
    // We created this class so we can easily add new pop-ups (like "Armor Up!" or "Damage!") 
    // and customize exactly how long they fade in, stay on screen, and fade out.
    [System.Serializable]
    public class UIElement
    {
        public string id; // A simple name we can use to find this UI element later, like "armor_popup"

        // A CanvasGroup is a magical UI component. It lets us change the 'Alpha' (transparency) 
        // of a whole folder of UI at once, making smooth fade-ins very easy!
        public CanvasGroup group;

        public DisplayMode mode = DisplayMode.Timed;
        public float fadeInDuration = 1f;
        public float visibleDuration = 10f;
        public float fadeOutDuration = 1f;
        public bool autoPlay = false; // Should this show up immediately when the game starts?

        // [System.NonSerialized] hides this from the Unity Inspector because it's purely for background math.
        // We use it to remember if a fade animation is currently playing, so we don't accidentally play two at once.
        [System.NonSerialized] public Coroutine runningCoroutine;
    }

    [Header("UI Elements List")]
    // A List is like an Array ([]), but you can easily Add() and Remove() items while the game is running.
    public List<UIElement> elements = new List<UIElement>();

    [Header("Round Display Settings")]
    public TextMeshProUGUI roundTMP;
    public CanvasGroup roundGroup;
    public float roundFadeInDuration = 0.5f;
    public float roundVisibleDuration = 2.0f;
    public float roundFadeOutDuration = 1.0f;
    public float roundPopScale = 1.5f; // Makes the text pop out big, then shrink down to normal size!

    [Header("Lives UI")]
    // We provide slots for both classic Text and modern TextMeshPro, just in case!
    public Text livesText;
    public TextMeshProUGUI livesTMP;
    public string livesElementId = "lives";

    [Header("Tanks Destroyed UI")]
    public Text destroyedText;
    public TextMeshProUGUI destroyedTMP;
    public string destroyedElementId = "destroyed";

    [Header("Hull Stability UI")]
    public Text hullText;
    public TextMeshProUGUI hullTMP;

    [Header("Pause Settings")]
    public CanvasGroup pauseGroup;
    public string pauseElementId = "pause";

    [Header("Audio Settings")]
    public AudioSource musicSource;
    public AudioClip victoryMusic;
    public AudioClip hitmarkerSound;
    [Range(0f, 1f)] public float hitmarkerVolume = 0.5f;

    // --- INTERNAL TRACKERS ---
    private bool isSubscribedToGameState = false;
    private bool isPaused = false;
    private bool isGameOver = false;
    private Coroutine roundCoroutine;

    // OnEnable/OnDisable is where we handle Event Subscriptions (+ and -)
    void OnEnable() { TrySubscribeToGameState(); }

    void OnDisable()
    {
        // If we are subscribed, we MUST unsubscribe when the UI is turned off or destroyed.
        // Otherwise, Game_State will try to send messages to a ghost UI, causing errors!
        if (isSubscribedToGameState && Game_State.Instance != null)
        {
            Game_State.Instance.OnLivesChanged -= HandleLivesChanged;
            Game_State.Instance.OnTanksDestroyedChanged -= HandleTanksDestroyedChanged;
            Game_State.Instance.OnLivesDepleted -= HandleGameOver;
            Game_State.Instance.OnHullStabilityChanged -= HandleHullStabilityChanged;
            Game_State.Instance.OnRoundCleared -= HandleRoundCleared;
            Game_State.Instance.OnRoundChanged -= HandleRoundChanged;
            Game_State.Instance.OnArmorPickedUp -= HandleArmorPickedUp;
            Game_State.Instance.OnPlayerDamaged -= HandlePlayerDamaged;
            Game_State.Instance.OnGameWon -= HandleGameWon;
            Game_State.Instance.OnEnemyHit -= HandleEnemyHit;
            isSubscribedToGameState = false;
        }
    }

    void Start()
    {
        EnsureEventSystemExists(); // UI buttons don't work without an EventSystem!

        // --- PAUSE & TIME RESET ---
        // Time.timeScale controls the speed of the whole game. 1f is 100% normal speed.
        // We set it to 1f on Start just in case the player restarted a game while paused.
        Time.timeScale = 1f;
        AudioListener.pause = false; // Unpause all sounds
        isPaused = false;
        isGameOver = false;

        // Hide menus that shouldn't be seen yet
        if (pauseGroup != null) HideCanvasGroupImmediate(pauseGroup);
        if (roundGroup != null) HideCanvasGroupImmediate(roundGroup);

        // Loop through all our custom UI popups and set them up correctly
        foreach (var e in elements)
        {
            if (e == null || e.group == null) continue;
            e.group.gameObject.SetActive(true);

            if (e.mode == DisplayMode.Permanent)
            {
                // Alpha = 1f means 100% visible (solid).
                e.group.alpha = 1f;
                // Interactable/BlocksRaycasts means the player's mouse can actually click buttons inside this group.
                e.group.interactable = true;
                e.group.blocksRaycasts = true;
            }
            else
            {
                if (e.autoPlay) StartSequence(e.id);
                else HideCanvasGroupImmediate(e.group);
            }
        }

        // Auto-find text components if we forgot to drag them in
        if (livesText == null && livesTMP == null) TryFindLivesText();
        if (destroyedText == null && destroyedTMP == null) TryFindDestroyedText();

        TrySubscribeToGameState();

        // On boot, ask the Game_State for the current scores so the UI isn't blank for the first few seconds!
        if (isSubscribedToGameState && Game_State.Instance != null)
        {
            HandleLivesChanged(Game_State.Instance.Lives);
            HandleTanksDestroyedChanged(Game_State.Instance.TanksDestroyed);
            HandleHullStabilityChanged(Game_State.Instance.HullStability);
            HandleRoundChanged(Game_State.Instance.CurrentRound);
        }
    }

    void Update()
    {
        // Listen for the Escape key to pause the game
        if (!isGameOver && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            TogglePause();
        }
    }

    // --- ROUND DISPLAY LOGIC (Fading UI) ---
    private void HandleRoundChanged(int roundNumber)
    {
        if (roundCoroutine != null) StopCoroutine(roundCoroutine);
        roundCoroutine = StartCoroutine(FadeRoundSequence(roundNumber));
    }

    // This Coroutine handles the smooth fading and scaling of the "ROUND 1" text.
    private IEnumerator FadeRoundSequence(int roundNumber)
    {
        if (roundGroup == null || roundTMP == null) yield break;

        roundGroup.gameObject.SetActive(true);
        roundTMP.text = "ROUND " + roundNumber;
        roundGroup.alpha = 0f; // Start invisible
        roundTMP.transform.localScale = Vector3.one * roundPopScale; // Start big!

        // Fade in loop
        float t = 0;
        while (t < roundFadeInDuration)
        {
            t += Time.deltaTime;
            float progress = t / roundFadeInDuration; // Calculate a percentage from 0.0 to 1.0

            roundGroup.alpha = progress; // Fade in
            // Shrink from the big scale down to normal (Vector3.one)
            roundTMP.transform.localScale = Vector3.Lerp(Vector3.one * roundPopScale, Vector3.one, progress);
            yield return null; // Wait until next frame
        }

        // Ensure we end exactly on perfect numbers
        roundGroup.alpha = 1f;
        roundTMP.transform.localScale = Vector3.one;

        // Wait on screen for a few seconds
        yield return new WaitForSeconds(roundVisibleDuration);

        // Fade out loop
        t = 0;
        while (t < roundFadeOutDuration)
        {
            t += Time.deltaTime;
            roundGroup.alpha = 1f - (t / roundFadeOutDuration); // Subtracting from 1 fades it out!
            yield return null;
        }

        roundGroup.alpha = 0f;
        roundGroup.gameObject.SetActive(false);
        roundCoroutine = null; // Clean up
    }

    // --- CORE UI ANIMATION SYSTEM ---
    public void StartSequence(string id)
    {
        var e = FindById(id);
        if (e == null || e.group == null || e.mode != DisplayMode.Timed) return;

        StopSequence(id); // If it's already playing, stop it so we can restart the animation cleanly
        e.group.gameObject.SetActive(true);
        e.runningCoroutine = StartCoroutine(PlaySequence(e));
    }

    public void StopSequence(string id)
    {
        var e = FindById(id);
        if (e != null && e.runningCoroutine != null)
        {
            StopCoroutine(e.runningCoroutine);
            e.runningCoroutine = null;
        }
    }

    public void ShowPermanent(string id)
    {
        var e = FindById(id);
        if (e == null || e.group == null) return;
        StopSequence(id);
        e.mode = DisplayMode.Permanent;
        ShowCanvasGroupImmediate(e.group);
    }

    public void HidePermanent(string id)
    {
        var e = FindById(id);
        if (e == null || e.group == null) return;
        StopSequence(id);
        HideCanvasGroupImmediate(e.group);
    }

    // --- PAUSE & SCENE LOGIC ---
    public void TogglePause() { if (isPaused) ResumeGame(); else PauseGame(); }

    public void PauseGame()
    {
        isPaused = true;
        // Setting Time.timeScale to 0 instantly freezes all Physics, Updates, and movements that multiply by Time.deltaTime!
        Time.timeScale = 0f;
        AudioListener.pause = true; // Freezes game audio

        ShowPermanent(pauseElementId); // Show pause menu

        Cursor.visible = true; // Give the player their mouse back so they can click 'Resume'
        Cursor.lockState = CursorLockMode.None;
    }

    public void ResumeGame()
    {
        isPaused = false;
        Time.timeScale = 1f; // Back to normal speed!
        AudioListener.pause = false;

        HidePermanent(pauseElementId);

        Cursor.visible = false; // Hide the mouse again
        Cursor.lockState = CursorLockMode.Locked;
    }

    public void RestartLevel()
    {
        Time.timeScale = 1f; // NEVER load a scene with timeScale 0, or the new scene will be frozen forever!
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void LoadMainMenu()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene("Main_Menu");
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        // Special code to stop the game inside the Unity Editor
        UnityEditor.EditorApplication.isPlaying = false;
#else
        // Normal code for the built/exported game
        Application.Quit();
#endif
    }

    // --- EVENT HANDLERS (Reacting to the Game_State) ---
    private void HandleLivesChanged(int lives)
    {
        if (livesTMP) livesTMP.text = lives.ToString(); else if (livesText) livesText.text = lives.ToString();
    }

    private void HandleTanksDestroyedChanged(int count)
    {
        if (destroyedTMP) destroyedTMP.text = count.ToString() + "/6";
        else if (destroyedText) destroyedText.text = count.ToString() + "/6";
    }

    private void HandleHullStabilityChanged(float percent)
    {
        // Convert the 0.0-1.0 percentage into a readable 0-100 number.
        int val = Mathf.RoundToInt(percent * 100f);
        if (hullTMP) hullTMP.text = $"Hull: {val}%"; else if (hullText) hullText.text = $"Hull: {val}%";
    }

    private void HandleGameOver()
    {
        if (isGameOver) return;
        isGameOver = true;
        HidePermanent("round_clear");
        ShowPermanent("gameover");

        Time.timeScale = 0f; // Freeze game
        AudioListener.pause = true;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void HandleRoundCleared()
    {
        if (isGameOver) return;
        ShowPermanent("round_clear");
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void HandleGameWon()
    {
        if (isGameOver) return;
        isGameOver = true;

        HidePermanent("round_clear");
        ShowPermanent("victory");

        // Swap out the background music for the victory song!
        if (musicSource != null && victoryMusic != null)
        {
            // ignoreListenerPause means this audio source will keep playing even though we paused all other game audio!
            musicSource.ignoreListenerPause = true;
            musicSource.clip = victoryMusic;
            musicSource.Play();
        }

        Time.timeScale = 0f;
        AudioListener.pause = true;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void OnContinueClicked()
    {
        HidePermanent("round_clear");
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        // Tell the Game_State we are ready to move on
        if (Game_State.Instance != null) Game_State.Instance.AdvanceRound();
    }

    // Trigger visual popups using our custom Sequence system
    private void HandleArmorPickedUp() { StartSequence("armor_popup"); }
    private void HandlePlayerDamaged() { StartSequence("damage_flash"); }

    // --- HITMARKER LOGIC ---
    private void HandleEnemyHit(Vector3 hitWorldPos)
    {
        // Instantly flash the Hitmarker UI
        StartSequence("hitmarker");

        // Play the hitmarker sound directly on the Main Camera so it is always loud, crisp, and clear to the player.
        if (hitmarkerSound != null && Camera.main != null)
        {
            AudioSource.PlayClipAtPoint(hitmarkerSound, Camera.main.transform.position, hitmarkerVolume);
        }
    }

    // --- HELPER FUNCTIONS ---
    // A quick way to search through our custom List for a specific ID.
    private UIElement FindById(string id) => elements.Find(x => x.id == id);

    // Alpha = 0 hides the UI. Interactable/BlocksRaycasts = false ensures hidden buttons can't be accidentally clicked!
    private void HideCanvasGroupImmediate(CanvasGroup g)
    {
        g.alpha = 0f; g.interactable = false; g.blocksRaycasts = false; g.gameObject.SetActive(false);
    }

    private void ShowCanvasGroupImmediate(CanvasGroup g)
    {
        g.gameObject.SetActive(true); g.alpha = 1f; g.interactable = true; g.blocksRaycasts = true;
    }

    // The Coroutine that handles the fade in/fade out for our custom UI list items (like the Hitmarker or Damage flash).
    private IEnumerator PlaySequence(UIElement e)
    {
        CanvasGroup g = e.group;
        float t = 0;
        while (t < e.fadeInDuration)
        {
            t += Time.deltaTime;
            if (g == null) yield break;
            g.alpha = t / e.fadeInDuration;
            yield return null;
        }
        if (g != null) g.alpha = 1f;

        yield return new WaitForSeconds(e.visibleDuration);

        t = 0;
        while (t < e.fadeOutDuration)
        {
            t += Time.deltaTime;
            if (g == null) yield break;
            g.alpha = 1f - (t / e.fadeOutDuration);
            yield return null;
        }

        if (g != null)
        {
            g.alpha = 0f;
            g.gameObject.SetActive(false);
        }
        e.runningCoroutine = null;
    }

    private void TrySubscribeToGameState()
    {
        if (!isSubscribedToGameState && Game_State.Instance != null)
        {
            Game_State.Instance.OnLivesChanged += HandleLivesChanged;
            Game_State.Instance.OnTanksDestroyedChanged += HandleTanksDestroyedChanged;
            Game_State.Instance.OnLivesDepleted += HandleGameOver;
            Game_State.Instance.OnHullStabilityChanged += HandleHullStabilityChanged;
            Game_State.Instance.OnRoundCleared += HandleRoundCleared;
            Game_State.Instance.OnRoundChanged += HandleRoundChanged;
            Game_State.Instance.OnArmorPickedUp += HandleArmorPickedUp;
            Game_State.Instance.OnPlayerDamaged += HandlePlayerDamaged;
            Game_State.Instance.OnGameWon += HandleGameWon;
            Game_State.Instance.OnEnemyHit += HandleEnemyHit;
            isSubscribedToGameState = true;
        }
    }

    private void TryFindLivesText() { var e = FindById(livesElementId); if (e != null) { livesTMP = e.group.GetComponentInChildren<TextMeshProUGUI>(); livesText = e.group.GetComponentInChildren<Text>(); } }
    private void TryFindDestroyedText() { var e = FindById(destroyedElementId); if (e != null) { destroyedTMP = e.group.GetComponentInChildren<TextMeshProUGUI>(); destroyedText = e.group.GetComponentInChildren<Text>(); } }

    // If you delete the EventSystem from the scene by accident, the UI buttons stop working. 
    // This function automatically creates a new one so your game never breaks!
    private void EnsureEventSystemExists()
    {
        if (EventSystem.current != null) return;
        var esGO = new GameObject("EventSystem");
        esGO.AddComponent<EventSystem>();
        esGO.AddComponent<StandaloneInputModule>();
    }
}