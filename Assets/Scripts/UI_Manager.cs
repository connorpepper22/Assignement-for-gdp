using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Single manager to control multiple UI elements (text boxes, images, etc.)
/// Each element uses a CanvasGroup so the whole element hierarchy can be faded
/// and toggled for interaction/blocking. Elements can be configured as Timed
/// (fade in -> visible -> fade out) or Permanent (visible until hidden).
/// Also listens to Game_State.OnLivesChanged to update a Lives text field (supports Text or TextMeshProUGUI).
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

    // track subscription to avoid double-subscribe
    private bool isSubscribedToLives = false;

        void OnEnable()
    {
        // Subscribe to lives change event if Game_State exists
        if (Game_State.Instance != null)
        {
            Game_State.Instance.OnLivesChanged += HandleLivesChanged;
            // update immediately with current value in case it changed before UI started
            HandleLivesChanged(Game_State.Instance.Lives);
        }
    }

    void OnDisable()
    {
        if (Game_State.Instance != null)
            Game_State.Instance.OnLivesChanged -= HandleLivesChanged;
    }

    void Start()
    {
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

        // Try to locate lives text component if not assigned (safe on main thread)
        if (livesText == null && livesTMP == null && !string.IsNullOrEmpty(livesElementId))
            TryFindLivesText();
    }

    // Public API: start timed fade sequence by id
    public void StartSequence(string id)
    {
        var e = FindById(id);
        if (e == null || e.group == null) return;
        if (e.mode != DisplayMode.Timed) return;

        StopSequence(id);
        e.group.gameObject.SetActive(true);
        e.runningCoroutine = StartCoroutine(PlaySequence(e));
    }

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

    public void ShowPermanent(string id)
    {
        var e = FindById(id);
        if (e == null || e.group == null) return;

        StopSequence(id);
        e.mode = DisplayMode.Permanent;
        e.group.gameObject.SetActive(true);
        e.group.alpha = 1f;
        e.group.interactable = true;
        e.group.blocksRaycasts = true;
    }

    public void HidePermanent(string id)
    {
        var e = FindById(id);
        if (e == null || e.group == null) return;

        StopSequence(id);
        e.group.alpha = 0f;
        e.group.interactable = false;
        e.group.blocksRaycasts = false;
        e.group.gameObject.SetActive(false);
    }

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
}

