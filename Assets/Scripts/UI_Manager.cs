using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class UI_Manager : MonoBehaviour
{
    public enum DisplayMode { Timed, Permanent }

    [System.Serializable]
    public class UIElement
    {
        public string id;
        public CanvasGroup group;
        public DisplayMode mode = DisplayMode.Timed;
        public float fadeInDuration = 1f;
        public float visibleDuration = 10f;
        public float fadeOutDuration = 1f;
        public bool autoPlay = false;

        [System.NonSerialized] public Coroutine runningCoroutine;
    }

    [Header("UI Elements List")]
    public List<UIElement> elements = new List<UIElement>();

    [Header("Round Display Settings")]
    public TextMeshProUGUI roundTMP;
    public CanvasGroup roundGroup;
    public float roundFadeInDuration = 0.5f;
    public float roundVisibleDuration = 2.0f;
    public float roundFadeOutDuration = 1.0f;
    public float roundPopScale = 1.5f; // Starts at 150% size

    [Header("Lives UI")]
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

    private bool isSubscribedToGameState = false;
    private bool isPaused = false;

    // THE FIX: A flag to lock out other UI states if the game is over
    private bool isGameOver = false;

    private Coroutine roundCoroutine;

    void OnEnable()
    {
        TrySubscribeToGameState();
    }

    void OnDisable()
    {
        if (isSubscribedToGameState && Game_State.Instance != null)
        {
            Game_State.Instance.OnLivesChanged -= HandleLivesChanged;
            Game_State.Instance.OnTanksDestroyedChanged -= HandleTanksDestroyedChanged;
            Game_State.Instance.OnLivesDepleted -= HandleGameOver;
            Game_State.Instance.OnHullStabilityChanged -= HandleHullStabilityChanged;
            Game_State.Instance.OnRoundCleared -= HandleRoundCleared;
            Game_State.Instance.OnRoundChanged -= HandleRoundChanged;
            Game_State.Instance.OnArmorPickedUp -= HandleArmorPickedUp;
            Game_State.Instance.OnPlayerDamaged -= HandlePlayerDamaged; // NEW
            isSubscribedToGameState = false;
        }
    }

    void Start()
    {
        EnsureEventSystemExists();

        Time.timeScale = 1f;
        AudioListener.pause = false;
        isPaused = false;
        isGameOver = false; // Reset the game over state when the scene loads

        if (pauseGroup != null) HideCanvasGroupImmediate(pauseGroup);
        if (roundGroup != null) HideCanvasGroupImmediate(roundGroup);

        foreach (var e in elements)
        {
            if (e == null || e.group == null) continue;
            e.group.gameObject.SetActive(true);

            if (e.mode == DisplayMode.Permanent)
            {
                e.group.alpha = 1f;
                e.group.interactable = true;
                e.group.blocksRaycasts = true;
            }
            else
            {
                if (e.autoPlay) StartSequence(e.id);
                else HideCanvasGroupImmediate(e.group);
            }
        }

        if (livesText == null && livesTMP == null) TryFindLivesText();
        if (destroyedText == null && destroyedTMP == null) TryFindDestroyedText();

        TrySubscribeToGameState();

        if (isSubscribedToGameState && Game_State.Instance != null)
        {
            HandleLivesChanged(Game_State.Instance.Lives);
            HandleTanksDestroyedChanged(Game_State.Instance.TanksDestroyed);
            HandleHullStabilityChanged(Game_State.Instance.HullStability);

            // Show Round 1 immediately on start
            HandleRoundChanged(Game_State.Instance.CurrentRound);
        }
    }

    void Update()
    {
        // Don't allow the player to pause/unpause if the game is over!
        if (!isGameOver && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            TogglePause();
        }
    }

    // --- Round Display Logic ---

    private void HandleRoundChanged(int roundNumber)
    {
        if (roundCoroutine != null) StopCoroutine(roundCoroutine);
        roundCoroutine = StartCoroutine(FadeRoundSequence(roundNumber));
    }

    private IEnumerator FadeRoundSequence(int roundNumber)
    {
        if (roundGroup == null || roundTMP == null) yield break;

        roundGroup.gameObject.SetActive(true);
        roundTMP.text = "ROUND " + roundNumber;

        // Reset state
        roundGroup.alpha = 0f;
        roundTMP.transform.localScale = Vector3.one * roundPopScale;

        // Fade In + Shrink (The Pop-In)
        float t = 0;
        while (t < roundFadeInDuration)
        {
            t += Time.deltaTime;
            float progress = t / roundFadeInDuration;
            roundGroup.alpha = progress;
            roundTMP.transform.localScale = Vector3.Lerp(Vector3.one * roundPopScale, Vector3.one, progress);
            yield return null;
        }
        roundGroup.alpha = 1f;
        roundTMP.transform.localScale = Vector3.one;

        yield return new WaitForSeconds(roundVisibleDuration);

        // Fade Out
        t = 0;
        while (t < roundFadeOutDuration)
        {
            t += Time.deltaTime;
            roundGroup.alpha = 1f - (t / roundFadeOutDuration);
            yield return null;
        }

        roundGroup.alpha = 0f;
        roundGroup.gameObject.SetActive(false);
        roundCoroutine = null;
    }

    // --- Core UI Logic ---

    public void StartSequence(string id)
    {
        var e = FindById(id);
        if (e == null || e.group == null || e.mode != DisplayMode.Timed) return;
        StopSequence(id);
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
        if (e == null)
        {
            Debug.LogError($"<color=red>[UI Manager]</color> Could not find UI Element with ID: '{id}'.");
            return;
        }

        if (e.group == null)
        {
            Debug.LogError($"<color=red>[UI Manager]</color> Element '{id}' found, but CanvasGroup is missing!");
            return;
        }

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

    // --- Pause/Scene Logic ---

    public void TogglePause() { if (isPaused) ResumeGame(); else PauseGame(); }

    public void PauseGame()
    {
        isPaused = true;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        ShowPermanent(pauseElementId);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void ResumeGame()
    {
        isPaused = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        HidePermanent(pauseElementId);
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    public void RestartLevel() { Time.timeScale = 1f; SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); }
    public void QuitGame()
    {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // --- Event Handlers ---

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
        int val = Mathf.RoundToInt(percent * 100f);
        if (hullTMP) hullTMP.text = $"Hull: {val}%"; else if (hullText) hullText.text = $"Hull: {val}%";
    }

    private void HandleGameOver()
    {
        // Prevent multiple calls just in case
        if (isGameOver) return;

        isGameOver = true;

        // Hide the round clear screen if it somehow popped up first
        HidePermanent("round_clear");

        ShowPermanent("gameover");

        // Pause the game manually here instead of calling PauseGame() so the Pause menu doesn't pop up
        Time.timeScale = 0f;
        AudioListener.pause = true;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void HandleRoundCleared()
    {
        // THE FIX: If the player died permanently, DO NOT show the Round Clear screen!
        if (isGameOver) return;

        Debug.Log("<color=cyan>[UI Manager]</color> Round Clear Event Received!");
        ShowPermanent("round_clear");
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void OnContinueClicked()
    {
        HidePermanent("round_clear");
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        if (Game_State.Instance != null) Game_State.Instance.AdvanceRound();
    }

    private void HandleArmorPickedUp()
    {
        StartSequence("armor_popup");
    }

    // NEW: Handle the damage flash event
    private void HandlePlayerDamaged()
    {
        StartSequence("damage_flash");
    }

    // --- Helpers ---

    private UIElement FindById(string id) => elements.Find(x => x.id == id);

    private void HideCanvasGroupImmediate(CanvasGroup g)
    {
        g.alpha = 0f; g.interactable = false; g.blocksRaycasts = false; g.gameObject.SetActive(false);
    }

    private void ShowCanvasGroupImmediate(CanvasGroup g)
    {
        g.gameObject.SetActive(true); g.alpha = 1f; g.interactable = true; g.blocksRaycasts = true;
    }

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
            Game_State.Instance.OnPlayerDamaged += HandlePlayerDamaged; // NEW
            isSubscribedToGameState = true;
        }
    }

    private void TryFindLivesText() { var e = FindById(livesElementId); if (e != null) { livesTMP = e.group.GetComponentInChildren<TextMeshProUGUI>(); livesText = e.group.GetComponentInChildren<Text>(); } }
    private void TryFindDestroyedText() { var e = FindById(destroyedElementId); if (e != null) { destroyedTMP = e.group.GetComponentInChildren<TextMeshProUGUI>(); destroyedText = e.group.GetComponentInChildren<Text>(); } }

    private void EnsureEventSystemExists()
    {
        if (EventSystem.current != null) return;
        var esGO = new GameObject("EventSystem");
        esGO.AddComponent<EventSystem>();
        esGO.AddComponent<StandaloneInputModule>();
    }
}