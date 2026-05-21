using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Singleton coordinator for all major game systems.
/// Routes play start, turn end, key collection, win/lose states, and level loading.
/// </summary>
public class GameManager : MonoBehaviour
{
    private static GameManager m_Instance;

    /// <summary>Global singleton access to the GameManager.</summary>
    public static GameManager Instance => m_Instance;

    [SerializeField] private PlayerController m_PlayerController;
    [SerializeField] private UIManager m_UIManager;
    [SerializeField] private SequenceSourceRouter m_SequenceSourceRouter;
    [SerializeField] private DeviceInputProvider m_DeviceInputProvider;

    /// <summary>True once the player has collected the key this turn.</summary>
    public bool IsKeyCollected { get; private set; }

    /// <summary>Fired at the end of every turn so interactables can reset themselves.</summary>
    public static event System.Action OnTurnReset;

    // Prevents duplicate GameWin/GameOver calls within the same execution turn
    private bool m_IsGameOver;

    private void Awake()
    {
        if (m_Instance != null && m_Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        m_Instance = this;

        if (m_PlayerController == null) Debug.LogError("[GameManager] PlayerController is not assigned.", this);
        if (m_UIManager == null) Debug.LogError("[GameManager] UIManager is not assigned.", this);
        if (m_SequenceSourceRouter == null) Debug.LogWarning("[GameManager] SequenceSourceRouter is not assigned.", this);
    }

    /// <summary>
    /// Called by <see cref="UIManager"/> when the Play button is pressed, or by
    /// <see cref="DeviceInputProvider"/> when Submit (Enter/Start) is pressed.
    /// Prepares the sequence, updates UI, then starts the execution turn.
    /// </summary>
    /// 
    [ContextMenu("Start Play")]
    public void OnPlayClicked()
    {
        if (m_SequenceSourceRouter != null && !m_SequenceSourceRouter.CanExecute)
        {
            Debug.Log("[GameManager] Cannot start — sequence is empty.");
            return;
        }

        // Mouse mode: bake toggle grid into a flat sequence.
        // Device mode: sequence already built via key presses, this is a no-op.
        m_SequenceSourceRouter?.PrepareForExecution();

        m_IsGameOver = false;
        m_DeviceInputProvider?.SetEnabled(false); // block keyboard/gamepad during execution
        m_UIManager?.HidePopup();
        m_UIManager?.LockUI();
        m_PlayerController?.OnGamePlayStart();
    }

    /// <summary>
    /// Called by <see cref="PlayerController"/> after all beats are executed.
    /// Clears the sequence, restores input, unlocks the UI, and re-opens the action panel.
    /// </summary>
    public void PlayEnded()
    {
        IsKeyCollected = false;
        m_SequenceSourceRouter?.OnTurnEnded();   // clear sequence for next round
        m_DeviceInputProvider?.SetEnabled(true); // restore keyboard/gamepad input
        m_UIManager?.UnlockUI();
        m_UIManager?.PopUp();
        OnTurnReset?.Invoke();
    }

    /// <summary>Marks the key as collected. Called by <see cref="Key"/> on interaction.</summary>
    public void KeyCollected()
    {
        IsKeyCollected = true;
    }

    /// <summary>
    /// Triggers the win screen. Ignored if already in a game-over state this turn
    /// to prevent duplicate calls from multiple trigger overlaps.
    /// </summary>
    public void GameWin()
    {
        if (m_IsGameOver) return;
        m_IsGameOver = true;
        m_UIManager?.GameOver(true);
    }

    /// <summary>
    /// Triggers the lose screen. Ignored if already in a game-over state this turn.
    /// </summary>
    public void GameOver()
    {
        if (m_IsGameOver) return;
        m_IsGameOver = true;
        m_UIManager?.GameOver(false);
    }

    /// <summary>Reloads the currently active scene.</summary>
    public void ReloadLevel()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>
    /// Loads the next scene by build index.
    /// Logs a warning if there is no next scene available.
    /// </summary>
    public void LoadNextLevel()
    {
        int nextIndex = SceneManager.GetActiveScene().buildIndex + 1;

        // Loop back to scene 0 when the last level is complete.
        if (nextIndex >= SceneManager.sceneCountInBuildSettings)
            nextIndex = 0;

        SceneManager.LoadScene(nextIndex);
    }

    public void QuitApplication()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
