using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

/// <summary>
/// Single manager to control multiple UI elements (text boxes, images, etc.)
/// Each element uses a CanvasGroup so the whole element hierarchy can be faded
/// and toggled for interaction/blocking. Elements can be configured as Timed
/// (fade in -> visible -> fade out) or Permanent (visible until hidden).
/// Also listens to Game_State events to update Lives UI and other counters.
/// </summary>
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

        // runtime only
        [System.NonSerialized] public Coroutine runningCoroutine;
    }

    // Inspector list
    public List<UIElement> elements = new List<UIElement>();

    [Header("Lives UI (optional)")]
    [Tooltip("Direct reference to a UnityEngine.UI.Text (assign in Inspector if possible)")]
    public Text livesText;
    [Tooltip("Direct reference to a TextMeshProUGUI (assign if you use TMP)")]
    public TextMeshProUGUI livesTMP;
    [Tooltip("If direct Text/TMP not assigned, manager will search inside the element with this id")]
    public string livesElementId = "lives";

    [Header("Tanks destroyed UI (optional)")]
    [Tooltip("Direct reference to a UnityEngine.UI.Text for destroyed count")]
    public Text destroyedText;
    [Tooltip("Direct reference to a TextMeshProUGUI for destroyed count")]
    public TextMeshProUGUI destroyedTMP;
    [Tooltip("If direct destroyed Text/TMP not assigned, manager will search inside the element with this id")]
    public string destroyedElementId = "destroyed";

    [Header("Pause Settings")]
    [Tooltip("Optional: direct CanvasGroup for the pause panel. Assign this first to ensure it starts hidden.")]
    public CanvasGroup pauseGroup;
    [Tooltip("ID of the CanvasGroup element that represents the pause menu/overlay (fallback if pauseGroup not assigned)")]
    public string pauseElementId = "pause";

    // track subscription to avoid double-subscribe
    private bool isSubscribedToGameState = false;

    // paused state
    private bool isPaused = false;

    void OnEnable()
    {
        // If Game_State already exists subscribe immediately
        TrySubscribeToGameState();
    }

    void OnDisable()
    {
        // Unsubscribe if subscribed
        if (isSubscribedToGameState && Game_State.Instance != null)
        {
            Game_State.Instance.OnLivesChanged -= HandleLivesChanged;
            Game_State.Instance.OnTanksDestroyedChanged -= HandleTanksDestroyedChanged;
            isSubscribedToGameState = false;
        }
    }

    void Start()
    {
        // Ensure EventSystem and GraphicRaycaster present so UI can receive clicks
        EnsureEventSystemExists();

        // Ensure normal timescale at start
        Time.timeScale = 1f;
        AudioListener.pause = false;
        isPaused = false;

        // Immediately hide the pauseGroup if assigned so it never appears briefly.
        if (pauseGroup != null)
        {
            HideCanvasGroupImmediate(pauseGroup);
        }

        // Initialize elements: ensure permanent items visible and timed items either autoplay or hidden
        for (int i = 0; i < elements.Count; i++)
        {
            var e = elements[i];
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
                if (e.autoPlay)
                    StartSequence(e.id);
                else
                {
                    e.group.alpha = 0f;
                    e.group.interactable = false;
                    e.group.blocksRaycasts = false;
                }
            }
        }

        // If pauseGroup not assigned, ensure pause element is hidden at start by id (fallback)
        if (pauseGroup == null && !string.IsNullOrEmpty(pauseElementId))
        {
            var pauseElem = FindById(pauseElementId);
            if (pauseElem != null && pauseElem.group != null)
            {
                HideCanvasGroupImmediate(pauseElem.group);
            }
        }

        // Try to locate UI text components if not assigned (safe on main thread)
        if (livesText == null && livesTMP == null && !string.IsNullOrEmpty(livesElementId))
            TryFindLivesText();

        if (destroyedText == null && destroyedTMP == null && !string.IsNullOrEmpty(destroyedElementId))
            TryFindDestroyedText();

        // Ensure subscription & immediate UI update (in case Game_State initialized after OnEnable)
        TrySubscribeToGameState();
        if (isSubscribedToGameState && Game_State.Instance != null)
        {
            HandleLivesChanged(Game_State.Instance.Lives);
            HandleTanksDestroyedChanged(Game_State.Instance.TanksDestroyed);
        }
    }

    void Update()
    {
        // Toggle pause on ESC. Support new Input System if present, fallback to old Input.
        bool escPressed = false;
        if (Keyboard.current != null)
            escPressed = Keyboard.current.escapeKey.wasPressedThisFrame;
        else
            escPressed = Input.GetKeyDown(KeyCode.Escape);

        if (escPressed)
            TogglePause();
    }

    // Helper to hide a CanvasGroup immediately (no coroutine)
    private void HideCanvasGroupImmediate(CanvasGroup g)
    {
        if (g == null) return;
        g.alpha = 0f;
        g.interactable = false;
        g.blocksRaycasts = false;
        g.gameObject.SetActive(false);
    }

    private void ShowCanvasGroupImmediate(CanvasGroup g)
    {
        if (g == null) return;

        // Make sure this Canvas has a GraphicRaycaster
        var canvas = g.GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            var gr = canvas.GetComponent<GraphicRaycaster>();
            if (gr == null) canvas.gameObject.AddComponent<GraphicRaycaster>();

            // ensure panel is on top so no other UI blocks it
            canvas.transform.SetAsLastSibling();
        }
        else
        {
            // if no parent canvas, bring group to top of its sibling order
            g.transform.SetAsLastSibling();
        }

        g.gameObject.SetActive(true);
        g.alpha = 1f;
        g.interactable = true;
        g.blocksRaycasts = true;
    }

    // Public API: start timed fade sequence by id
    public void StartSequence(string id)
    {
        var e = FindById(id);
        if (e == null || e.group == null) return;
        if (e.mode != DisplayMode.Timed) return;

        // Stop any running sequence for this element first.
        StopSequence(id);

        // Ensure the GameObject is active (so alpha changes are visible).
        e.group.gameObject.SetActive(true);

        // Start coroutine and store reference to allow cancellation.
        e.runningCoroutine = StartCoroutine(PlaySequence(e));
    }

    // Public API: stop a running timed sequence for an element (if any).
    public void StopSequence(string id)
    {
        var e = FindById(id);
        if (e == null) return;

        if (e.runningCoroutine != null)
        {
            StopCoroutine(e.runningCoroutine);
            e.runningCoroutine = null;
        }
    }

    // Public API: show an element permanently (cancels any running timed sequence).
    // Also switches the element's mode to Permanent so future StartSequence calls will be ignored.
    public void ShowPermanent(string id)
    {
        // Prefer direct pauseGroup handling if caller uses ShowPermanent(pauseElementId)
        if (!string.IsNullOrEmpty(id) && pauseGroup != null && id == pauseElementId)
        {
            ShowCanvasGroupImmediate(pauseGroup);
            return;
        }

        var e = FindById(id);
        if (e == null || e.group == null) return;

        StopSequence(id);
        e.mode = DisplayMode.Permanent;
        e.group.gameObject.SetActive(true);
        e.group.alpha = 1f;
        e.group.interactable = true;
        e.group.blocksRaycasts = true;
    }

    // Public API: hide an element that was permanent or timed.
    // Cancels any running sequence and disables the GameObject for cleanliness.
    public void HidePermanent(string id)
    {
        // Prefer direct pauseGroup handling if caller uses HidePermanent(pauseElementId)
        if (!string.IsNullOrEmpty(id) && pauseGroup != null && id == pauseElementId)
        {
            HideCanvasGroupImmediate(pauseGroup);
            return;
        }

        var e = FindById(id);
        if (e == null || e.group == null) return;

        StopSequence(id);
        e.group.alpha = 0f;
        e.group.interactable = false;
        e.group.blocksRaycasts = false;
        e.group.gameObject.SetActive(false);
    }

    // ---- Pause / Resume API ----

    // Pause the game and show pause UI
    public void PauseGame()
    {
        if (isPaused) return;
        isPaused = true;

        Time.timeScale = 0f;
        AudioListener.pause = true;

        if (pauseGroup != null)
            ShowCanvasGroupImmediate(pauseGroup);
        else if (!string.IsNullOrEmpty(pauseElementId))
            ShowPermanent(pauseElementId);

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    // Resume the game and hide pause UI
    public void ResumeGame()
    {
        if (!isPaused) return;
        isPaused = false;

        Time.timeScale = 1f;
        AudioListener.pause = false;

        if (pauseGroup != null)
            HideCanvasGroupImmediate(pauseGroup);
        else if (!string.IsNullOrEmpty(pauseElementId))
            HidePermanent(pauseElementId);

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    // Toggle pause (can be called from ESC or UI)
    public void TogglePause()
    {
        if (isPaused) ResumeGame();
        else PauseGame();
    }

    // Restart current level (resets timescale first)
    public void RestartLevel()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // Quit application (stops play mode in editor)
    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // Helper: find element by id. Returns null if not found.
    private UIElement FindById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        for (int i = 0; i < elements.Count; i++)
            if (elements[i] != null && elements[i].id == id) return elements[i];
        return null;
    }

    private IEnumerator PlaySequence(UIElement e)
    {
        var g = e.group;
        g.alpha = 0f;
        g.interactable = false;
        g.blocksRaycasts = false;

        if (e.fadeInDuration > 0f)
        {
            float t = 0f;
            while (t < e.fadeInDuration)
            {
                t += Time.deltaTime;
                g.alpha = Mathf.Clamp01(t / e.fadeInDuration);
                yield return null;
            }
        }
        g.alpha = 1f;
        g.interactable = true;
        g.blocksRaycasts = true;

        if (e.visibleDuration > 0f)
            yield return new WaitForSeconds(e.visibleDuration);

        g.interactable = false;
        g.blocksRaycasts = false;
        if (e.fadeOutDuration > 0f)
        {
            float t = 0f;
            while (t < e.fadeOutDuration)
            {
                t += Time.deltaTime;
                g.alpha = Mathf.Clamp01(1f - (t / e.fadeOutDuration));
                yield return null;
            }
        }
        g.alpha = 0f;
        e.runningCoroutine = null;
    }

    // ---- Lives UI integration ----

    // Called when Game_State raises OnLivesChanged
    private void HandleLivesChanged(int newLives)
    {
        // Ensure we have a Text/TMP reference, attempt to find if missing
        if (livesText == null && livesTMP == null)
            TryFindLivesText();

        if (livesTMP != null)
            livesTMP.text = newLives.ToString();
        else if (livesText != null)
            livesText.text = newLives.ToString();
    }

    // ---- Tanks destroyed UI integration ----

    // Called when Game_State raises OnTanksDestroyedChanged
    private void HandleTanksDestroyedChanged(int newCount)
    {
        // Ensure we have a Text/TMP reference, attempt to find if missing
        if (destroyedText == null && destroyedTMP == null)
            TryFindDestroyedText();

        if (destroyedTMP != null)
            destroyedTMP.text = newCount.ToString();
        else if (destroyedText != null)
            destroyedText.text = newCount.ToString();
    }

    // Search the element with livesElementId for a Text or TextMeshProUGUI child (main-thread only)
    private void TryFindLivesText()
    {
        if (string.IsNullOrEmpty(livesElementId)) return;

        var e = FindById(livesElementId);
        if (e == null || e.group == null) return;

        // Prefer TextMeshProUGUI if present
        var tmp = e.group.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmp != null)
        {
            livesTMP = tmp;
            return;
        }

        var txt = e.group.GetComponentInChildren<Text>(true);
        if (txt != null)
            livesText = txt;
    }

    // Search the element with destroyedElementId for a Text or TextMeshProUGUI child (main-thread only)
    private void TryFindDestroyedText()
    {
        if (string.IsNullOrEmpty(destroyedElementId)) return;

        var e = FindById(destroyedElementId);
        if (e == null || e.group == null) return;

        // Prefer TextMeshProUGUI if present
        var tmp = e.group.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmp != null)
        {
            destroyedTMP = tmp;
            return;
        }

        var txt = e.group.GetComponentInChildren<Text>(true);
        if (txt != null)
            destroyedText = txt;
    }

    // Try to subscribe to Game_State events if the singleton exists
    private void TrySubscribeToGameState()
    {
        if (!isSubscribedToGameState && Game_State.Instance != null)
        {
            Game_State.Instance.OnLivesChanged += HandleLivesChanged;
            Game_State.Instance.OnTanksDestroyedChanged += HandleTanksDestroyedChanged;
            isSubscribedToGameState = true;
        }
    }

    // Ensure an EventSystem exists so UI buttons receive input
    private void EnsureEventSystemExists()
    {
        if (EventSystem.current != null) return;
        if (FindObjectOfType<EventSystem>() != null) return;

        var esGO = new GameObject("EventSystem");
        esGO.transform.SetAsLastSibling();
        esGO.AddComponent<EventSystem>();

        // Try to find InputSystemUIInputModule type in loaded assemblies
        System.Type inputSystemModuleType = null;
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            inputSystemModuleType = asm.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule");
            if (inputSystemModuleType != null) break;
        }

        if (inputSystemModuleType != null)
        {
            // Add InputSystemUIInputModule if available (new Input System)
            esGO.AddComponent(inputSystemModuleType);
            Debug.Log("[UI_Manager] Created EventSystem with InputSystemUIInputModule");
        }
        else
        {
            // Fallback to legacy StandaloneInputModule
            esGO.AddComponent<StandaloneInputModule>();
            Debug.Log("[UI_Manager] Created EventSystem with StandaloneInputModule (legacy). Consider switching Player Settings to 'Both' or installing the Input System UI Module.");
        }
    }
}

