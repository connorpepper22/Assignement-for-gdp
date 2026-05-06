using UnityEngine;
using UnityEngine.SceneManagement; // CRITICAL: We need this library to load and transition between different levels (Scenes).
using System.Collections; // CRITICAL: We need this library to use "Coroutines" (timers).

// Note: There is a slight typo in your class name ("Manger" instead of "Manager"). 
// That's totally fine, just make sure the file name in Unity is exactly "Main_Menu_Manger.cs"!
public class Main_Menu_Manger : MonoBehaviour
{
    [Header("UI Panels")]
    // GameObjects can hold entire UI Canvas groups (like a folder full of buttons and text).
    // We drag our different menu screens into these slots in the Inspector.
    public GameObject mainMenuPanel;
    public GameObject controlsPanel;

    [Header("Loading Settings")]
    public GameObject loadingPanel; // Drag your "Loading..." UI screen here
    public float delayBeforeLoading = 2.5f; // How many seconds to wait on the loading screen

    void Start()
    {
        // --- 1. SETUP THE SCREENS ---
        // When the game boots up, we want to make absolutely sure the player is looking at the Main Menu, 
        // and that the Controls and Loading screens are hidden.
        // SetActive(true) turns an object ON. SetActive(false) turns it OFF.
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (controlsPanel != null) controlsPanel.SetActive(false);
        if (loadingPanel != null) loadingPanel.SetActive(false);

        // --- 2. UNLOCK THE MOUSE ---
        // If the player just died or finished a level where the mouse was locked to the center of the screen 
        // (like an FPS game), we MUST unlock it here so they can actually click the menu buttons!
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    // --- BUTTON FUNCTIONS ---
    // These must be 'public' so the Unity UI Buttons can find them and click them!

    // The Play button will call this function and pass in the exact name of the level you want to load (e.g., "Level_01").
    public void loadScene(string sceneName)
    {
        // Instead of loading instantly (which can be jarring), we start a "Coroutine".
        // Think of a Coroutine as a mini-program that runs in the background and is allowed to pause itself.
        StartCoroutine(LoadingRoutine(sceneName));
    }

    // IEnumerator is the special return type required for Coroutines.
    private IEnumerator LoadingRoutine(string sceneName)
    {
        // 1. Hide the Main Menu and show the Loading Screen!
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (loadingPanel != null) loadingPanel.SetActive(true);

        // 2. PAUSE THE CODE!
        // "yield return new WaitForSeconds" tells Unity: "Stop reading this script right here. 
        // Go render the game, let the player look at the loading screen, and come back in 2.5 seconds."
        yield return new WaitForSeconds(delayBeforeLoading);

        // 3. Time's up! Actually load the level now.
        // This command destroys the current scene and loads the new one into memory.
        SceneManager.LoadScene(sceneName);
    }

    public void ShowControls()
    {
        // Switch screens by turning one off and the other on.
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (controlsPanel != null) controlsPanel.SetActive(true);
    }

    public void HideControls()
    {
        // The "Back" button calls this to reverse the process.
        if (controlsPanel != null) controlsPanel.SetActive(false);
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
    }

    public void quitGame()
    {
        // This will print to the console so we know the button is working while testing in the Unity Editor.
        Debug.Log("Game Quitting...");

        // This actually closes the game window! 
        // (Note: This command does NOTHING while you are testing inside the Unity Editor, it only works in a built .exe game).
        Application.Quit();
    }
}