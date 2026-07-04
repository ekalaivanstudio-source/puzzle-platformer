/*using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
// UnityEngine.InputSystem is available if you need direct input polling elsewhere.
// The UI Button click already works with the New Input System via
// the "Input System UI Input Module" on your EventSystem — no extra code needed here.

/// <summary>
/// LevelManager.cs
/// Attach this to a GameObject in EVERY level scene (e.g., "LevelManager").
///
/// SCENE BUILD ORDER (File → Build Settings):
///   Index 0 → Main Menu
///   Index 1 → Level 1
///   Index 2 → Level 2
///   Index 3 → Level 3
///   Index 4 → Level 4
///   Index 5 → Level 5
///
/// SETUP PER LEVEL SCENE:
///   1. Create an empty GameObject → name it "LevelManager" → attach this script.
///   2. Assign the "Level Complete" button to levelCompleteButton.
///   3. Assign the "Back to Menu" button to backToMenuButton.
///   4. Make sure your EventSystem uses "Input System UI Input Module" (New Input System).
///
/// BEHAVIOUR:
///   - Clicking "Level Complete" unlocks the next level and saves progress via PlayerPrefs.
///   - No automatic scene loading — player returns to Main Menu manually.
///   - Clicking "Back to Menu" loads Main Menu (index 0) at any time.
///   - Completing the final level (Level 5) still saves and returns to menu.
/// </summary>
public class LevelManager : MonoBehaviour
{
    [Header("Buttons")]
    [Tooltip("The button the player clicks when they finish the level.")]
    [SerializeField] private Button levelCompleteButton;

    [Tooltip("A button always visible in the level to return to the main menu.")]
    [SerializeField] private Button backToMenuButton;

    [Header("Optional UI")]
    [Tooltip("Assign a TextMeshPro label to show a message after level is complete.")]
    [SerializeField] private TMPro.TextMeshProUGUI completionMessageText;

    // Must match MainMenuManager.UNLOCKED_LEVEL_KEY
    private const string UNLOCKED_LEVEL_KEY = "UnlockedLevel";

    // Total levels — update this number as you add more levels
    private const int TOTAL_LEVELS = 5;

    // Current level derived from the scene's build index
    private int currentLevel;

    // Prevents the complete button from firing more than once
    private bool levelCompleted = false;

    private void Start()
    {
        // Scene index 1 = Level 1, index 2 = Level 2, etc.
        currentLevel = SceneManager.GetActiveScene().buildIndex;

        // Hide completion message at start
        if (completionMessageText != null)
            completionMessageText.gameObject.SetActive(false);

        // Wire up Level Complete button
        if (levelCompleteButton != null)
        {
            levelCompleteButton.onClick.RemoveAllListeners();
            levelCompleteButton.onClick.AddListener(OnLevelComplete);
        }
        else
        {
            Debug.LogWarning("[LevelManager] Level Complete button is not assigned!");
        }

        // Wire up Back to Menu button
        if (backToMenuButton != null)
        {
            backToMenuButton.onClick.RemoveAllListeners();
            backToMenuButton.onClick.AddListener(ReturnToMainMenu);
        }
    }

    /// <summary>
    /// Called when the player clicks the Level Complete button.
    /// Unlocks the next level and saves progress. No auto-load.
    /// </summary>
    public void OnLevelComplete()
    {
        // Guard: run only once per session
        if (levelCompleted) return;
        levelCompleted = true;

        AudioManager.Instance?.PlayLevelComplete();

        // Disable the button so it can't be clicked again
        if (levelCompleteButton != null)
            levelCompleteButton.interactable = false;

        // The next level index to unlock
        int nextLevelToUnlock = currentLevel + 1;

        // Read current saved progress
        int alreadyUnlocked = PlayerPrefs.GetInt(UNLOCKED_LEVEL_KEY, 1);

        // Only save if this is higher than what was already unlocked
        // (prevents replaying Level 1 from resetting progress)
        if (nextLevelToUnlock > alreadyUnlocked && nextLevelToUnlock <= TOTAL_LEVELS)
        {
            PlayerPrefs.SetInt(UNLOCKED_LEVEL_KEY, nextLevelToUnlock);
            PlayerPrefs.Save(); // Write to disk immediately
            Debug.Log($"[LevelManager] Level {currentLevel} complete. Unlocked up to Level {nextLevelToUnlock}.");
        }
        else if (currentLevel == TOTAL_LEVELS)
        {
            // Player completed the last level — nothing new to unlock
            Debug.Log("[LevelManager] Final level complete! All levels already unlocked.");
        }

        // Show optional completion message
        if (completionMessageText != null)
        {
            completionMessageText.gameObject.SetActive(true);

            if (currentLevel == TOTAL_LEVELS)
                completionMessageText.text = "You completed all levels!\nReturn to Menu.";
            else
                completionMessageText.text = $"Level {currentLevel} Complete!\nLevel {nextLevelToUnlock} Unlocked!";
        }

        // Player goes back to menu manually using the Back to Menu button — no auto-load
    }

    /// <summary>
    /// Loads the Main Menu scene (index 0).
    /// Called by the Back to Menu button, or after level completion.
    /// </summary>
    public void ReturnToMainMenu()
    {
        SceneManager.LoadScene(0);
    }

    /// <summary>
    /// Optional: Hook to a Restart button inside the level.
    /// </summary>
    public void RestartLevel()
    {
        levelCompleted = false;
        SceneManager.LoadScene(currentLevel);
    }
}*/