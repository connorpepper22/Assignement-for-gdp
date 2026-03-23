using System.Collections;
using System.Collections.Generic;
using UnityEngine;


/// Single manager to control multiple UI elements (text boxes, images, etc.)
/// Each element uses a CanvasGroup so the whole element hierarchy can be faded
/// and toggled for interaction/blocking. Elements can be configured as Timed
/// (fade in -> visible -> fade out) or Permanent (visible until hidden).

public class UI_Manager : MonoBehaviour
{
    
    /// Mode for how an element should be displayed.
    /// Timed: runs a fade-in -> visible -> fade-out sequence.
    /// Permanent: shown immediately and stays visible until hidden.
    
    public enum DisplayMode { Timed, Permanent }

    [System.Serializable]
    public class UIElement
    {
        // Unique identifier used to reference this element from code (StartSequence/ShowPermanent/etc.)
        public string id;

        // The CanvasGroup controlling visibility and interactivity of the element.
        // Assigning the GameObject that contains my UI 
        public CanvasGroup group;

        // Per-element timing (only used for Timed mode)
        public DisplayMode mode = DisplayMode.Timed;
        public float fadeInDuration = 1f;
        public float visibleDuration = 10f;
        public float fadeOutDuration = 1f;

        // If true and mode == Timed, the sequence will start automatically at Start().
        public bool autoPlay = false;

        // Runtime only: store coroutine so we can stop it later if needed.
        [System.NonSerialized] public Coroutine runningCoroutine;
    }

    // Inspector list
    public List<UIElement> elements = new List<UIElement>();

    void Start()
    {
        // Initialize elements: ensure permanent items are visible immediately and timed items
        // are either started (autoPlay) or left hidden until triggered.
        for (int i = 0; i < elements.Count; i++)
        {
            var e = elements[i];
            if (e == null || e.group == null) continue; // guard against misconfigured entries

            // Keep the element GameObject active so CanvasGroup can control alpha.
            // (We don't destroy/deactivate here; manager controls alpha and raycast/interaction.)
            e.group.gameObject.SetActive(true);

            if (e.mode == DisplayMode.Permanent)
            {
                // For permanent mode make the element visible and interactive immediately.
                e.group.alpha = 1f;
                e.group.interactable = true;
                e.group.blocksRaycasts = true;
            }
            else // Timed mode
            {
                if (e.autoPlay)
                {
                    // Start the timed fade sequence automatically.
                    StartSequence(e.id);
                }
                else
                {
                    // Default hidden state for timed, non-autoplay elements.
                    e.group.alpha = 0f;
                    e.group.interactable = false;
                    e.group.blocksRaycasts = false;
                }
            }
        }
    }

    // Public API: start the timed fade sequence for the element with the given id.
    // If the element is Permanent, this call is ignored.
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
        var e = FindById(id);
        if (e == null || e.group == null) return;

        StopSequence(id);

        e.group.alpha = 0f;
        e.group.interactable = false;
        e.group.blocksRaycasts = false;
        e.group.gameObject.SetActive(false);
    }

    // Helper: find element by id. Returns null if not found.
    private UIElement FindById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        for (int i = 0; i < elements.Count; i++)
            if (elements[i] != null && elements[i].id == id) return elements[i];
        return null;
    }

    // Coroutine implementing fade-in -> visible -> fade-out for a single element.
    // Uses the element's configured durations and sets CanvasGroup.interactable/blocksRaycasts
    // appropriately so the UI element only receives input while fully visible.
    private IEnumerator PlaySequence(UIElement e)
    {
        var g = e.group;

        // Ensure initial hidden state
        g.alpha = 0f;
        g.interactable = false;
        g.blocksRaycasts = false;

        // Fade in over fadeInDuration seconds (smooth linear interpolation)
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

        // Fully visible state
        g.alpha = 1f;
        g.interactable = true;
        g.blocksRaycasts = true;

        // Stay visible for configured duration
        if (e.visibleDuration > 0f)
            yield return new WaitForSeconds(e.visibleDuration);

        // Prepare to fade out: disable interaction immediately so UI doesn't respond while fading
        g.interactable = false;
        g.blocksRaycasts = false;

        // Fade out over fadeOutDuration seconds
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

        // Ensure fully hidden at the end and clear running coroutine reference
        g.alpha = 0f;
        e.runningCoroutine = null;
    }
}

