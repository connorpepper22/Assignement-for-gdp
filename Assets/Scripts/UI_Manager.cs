using System.Collections;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class UI_Manager : MonoBehaviour
{
    // The CanvasGroup controlling the text box (will use the component on this GameObject by default)
    public CanvasGroup boxGroup;

    // Durations (seconds)
    public float fadeInDuration = 1f;
    public float visibleDuration = 10f;
    public float fadeOutDuration = 1f;

    // Start the sequence automatically on Start
    public bool autoPlay = true;

    private Coroutine sequenceCoroutine;

    void Awake()
    {
        if (boxGroup == null)
            boxGroup = GetComponent<CanvasGroup>();
    }

    void Start()
    {
        if (autoPlay)
            StartSequence();
    }

    public void StartSequence()
    {
        if (sequenceCoroutine != null)
            StopCoroutine(sequenceCoroutine);

        // Ensure object is active and visible to start
        if (boxGroup != null)
        {
            boxGroup.gameObject.SetActive(true);
            sequenceCoroutine = StartCoroutine(PlaySequence());
        }
    }

    public void StopSequence()
    {
        if (sequenceCoroutine != null)
        {
            StopCoroutine(sequenceCoroutine);
            sequenceCoroutine = null;
        }
    }

    private IEnumerator PlaySequence()
    {
        // Ensure initial state
        boxGroup.alpha = 0f;
        boxGroup.interactable = false;
        boxGroup.blocksRaycasts = false;

        // Fade in
        if (fadeInDuration > 0f)
        {
            float t = 0f;
            while (t < fadeInDuration)
            {
                t += Time.deltaTime;
                boxGroup.alpha = Mathf.Clamp01(t / fadeInDuration);
                yield return null;
            }
        }
        boxGroup.alpha = 1f;
        boxGroup.interactable = true;
        boxGroup.blocksRaycasts = true;

        // Visible period
        if (visibleDuration > 0f)
            yield return new WaitForSeconds(visibleDuration);

        // Fade out
        boxGroup.interactable = false;
        boxGroup.blocksRaycasts = false;
        if (fadeOutDuration > 0f)
        {
            float t = 0f;
            while (t < fadeOutDuration)
            {
                t += Time.deltaTime;
                boxGroup.alpha = Mathf.Clamp01(1f - (t / fadeOutDuration));
                yield return null;
            }
        }
        boxGroup.alpha = 0f;

        sequenceCoroutine = null;
    }
}
