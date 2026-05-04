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
            isSubscribedToGameState = false;
        }
    }

    void Start()
    {
        EnsureEventSystemExists();

        Time.timeScale = 1f;
        AudioListener.pause = false;
        isPaused = false;

        if (pauseGroup != null) HideCanvasGroupImmediate(pauseGroup);

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
        }
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            TogglePause();
        }
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
        // This part only runs when you are playing in the Unity Editor
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
        // This part runs in the actual built game
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
        if (destroyedTMP) destroyedTMP.text = count.ToString(); else if (destroyedText) destroyedText.text = count.ToString();
    }

    private void HandleHullStabilityChanged(float percent)
    {
        int val = Mathf.RoundToInt(percent * 100f);
        if (hullTMP) hullTMP.text = $"Hull: {val}%"; else if (hullText) hullText.text = $"Hull: {val}%";
    }

    private void HandleGameOver() { ShowPermanent("gameover"); PauseGame(); }

    private void HandleRoundCleared()
    {
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