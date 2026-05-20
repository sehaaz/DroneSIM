using UnityEngine;

public class PauseMenu : MonoBehaviour
{
    [Header("UI Panels")]
    [Tooltip("The main Panel containing the buttons (Continue, Options, Exit).")]
    public GameObject pauseMenuUI;
    [Tooltip("The Panel containing the Options settings. Should be initially hidden.")]
    public GameObject optionsMenuUI;

    [Header("Input")]
    [Tooltip("Legacy Key to toggle Pause (e.g. Escape).")]
    public KeyCode pauseKey = KeyCode.Escape;

    [Header("State")]
    public bool isPaused = false;

    private void Start()
    {
        // Ensure menu is closed at start
        if (pauseMenuUI != null) pauseMenuUI.SetActive(false);
        if (optionsMenuUI != null) optionsMenuUI.SetActive(false);
        Resume();
    }

    private void OnEnable()
    {
    }

    private void OnDisable()
    {
    }

    private void Update()
    {
        // Legacy Input Handling
        bool togglePause = false;
        if (Input.GetKeyDown(pauseKey) || Input.GetKeyDown(KeyCode.JoystickButton7)) // Button 7 is usually Start/Menu
        {
            togglePause = true;
        }

        if (togglePause)
        {
            if (isPaused)
            {
                // If options is open, close options first, return to pause menu? 
                // Or just Resume game? Standard behavior: Resume Game.
                Resume();
            }
            else
            {
                Pause();
            }
        }
    }

    public void Resume()
    {
        if (pauseMenuUI != null) pauseMenuUI.SetActive(false);
        if (optionsMenuUI != null) optionsMenuUI.SetActive(false);

        Time.timeScale = 1f;
        isPaused = false;

        // Lock Cursor if needed for game? Drone game usually doesn't need cursor lock unless it's FPS style style
        // But usually menus unlock cursor.
        Cursor.lockState = CursorLockMode.None; // Or Locked if game needs it?
        Cursor.visible = false; // Hide cursor for gameplay
    }

    public void Pause()
    {
        if (pauseMenuUI != null) pauseMenuUI.SetActive(true);
        // Options should remain closed when first pausing
        if (optionsMenuUI != null) optionsMenuUI.SetActive(false);

        Time.timeScale = 0f;
        isPaused = true;

        // Show Cursor
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // --- Button Bindings ---

    public void OnClick_Continue()
    {
        Resume();
    }

    public void OnClick_Options()
    {
        // Hide Pause Menu, Show Options Menu
        if (pauseMenuUI != null) pauseMenuUI.SetActive(false);
        if (optionsMenuUI != null)
        {
            optionsMenuUI.SetActive(true);

            // Refresh Options UI values from scripts
            OptionsMenu opts = optionsMenuUI.GetComponent<OptionsMenu>();
            if (opts != null) opts.RefreshUI();
        }
    }

    public void OnClick_Exit()
    {
        Debug.Log("[PauseMenu] Quitting Application...");
        Application.Quit();
    }

    /// <summary>
    /// Back button from Options Menu
    /// </summary>
    public void OnClick_BackFromOptions()
    {
        if (optionsMenuUI != null) optionsMenuUI.SetActive(false);
        if (pauseMenuUI != null) pauseMenuUI.SetActive(true);
    }
}
