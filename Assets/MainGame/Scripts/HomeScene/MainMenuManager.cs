using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// MainMenuManager.cs
/// Attach to a GameObject in your Main Menu scene (e.g., "MenuManager").
///
/// SCENE BUILD ORDER (File → Build Settings):
///   Index 0 → Main Menu
///   Index 1 → Level 1
///   Index 2 → Level 2
///   Index 3 → Level 3
///   Index 4 → Level 4
///   Index 5 → Level 5
///
/// SETUP:
///   1. Assign all 5 level buttons into the levelButtons array in order.
///   2. Optionally add a CanvasGroup component to each button for the lock fade effect.
///   3. No Play button needed — player selects a level directly from the menu.
/// </summary>
public class MainMenuManager : MonoBehaviour
{
    [Header("Level Buttons (assign Level 1 to Level 5 in order)")]
    [SerializeField] private Button[] levelButtons;

    // PlayerPrefs key — must match the one in LevelManager
    public const string UNLOCKED_LEVEL_KEY = "UnlockedLevel";

    private void Start()
    {
        // First launch default = 1, meaning only Level 1 is playable
        int unlockedLevel = PlayerPrefs.GetInt(UNLOCKED_LEVEL_KEY, 1);
        SetupLevelButtons(unlockedLevel);
    }

    /// <summary>
    /// Sets each button as interactable or locked based on saved progress.
    /// </summary>
    private void SetupLevelButtons(int unlockedLevel)
    {
        for (int i = 0; i < levelButtons.Length; i++)
        {
            int levelNumber = i + 1; // index 0 = Level 1, index 4 = Level 5
            bool isUnlocked = levelNumber <= unlockedLevel;

            // Only unlocked levels can be clicked
            levelButtons[i].interactable = isUnlocked;

            // Visual feedback: fade locked buttons (needs CanvasGroup on each button)
            CanvasGroup cg = levelButtons[i].GetComponent<CanvasGroup>();
            if (cg != null)
                cg.alpha = isUnlocked ? 1f : 0.4f;

            // Wire up click — capture levelNumber to avoid closure issue
            int capturedLevel = levelNumber;
            levelButtons[i].onClick.RemoveAllListeners();
            levelButtons[i].onClick.AddListener(() => LoadLevel(capturedLevel));
        }
    }

    /// <summary>
    /// Loads a level scene. Scene index equals the level number
    /// because index 0 is reserved for the Main Menu.
    /// </summary>
    private void LoadLevel(int levelNumber)
    {
        SceneManager.LoadScene(levelNumber);
    }

    /// <summary>
    /// Hook this up to a Quit button if your menu has one.
    /// </summary>
    public void QuitGame()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}