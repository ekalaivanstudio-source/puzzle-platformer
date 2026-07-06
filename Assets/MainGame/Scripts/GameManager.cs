using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Singleton coordinator for all major game systems.
/// Routes play start, turn end, key collection, win/lose states, and level loading.
/// All systems are accessed via their own singletons — no serialized references needed.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    /// <summary>True once the player has collected the key this turn.</summary>
    public bool IsKeyCollected { get; private set; }

    /// <summary>Fired at the end of every turn so interactables can reset themselves.</summary>
    public static event System.Action OnTurnReset;
    public static event System.Action OnKeyReset;

    /// <summary>
    /// Fired only when the player explicitly restarts the level (UI Restart button or R key).
    /// Bricks, redirectors, and other persistent objects should listen to this — NOT OnTurnReset.
    /// </summary>
    public static event System.Action OnFullReset;

    /// <summary>Fired when execution begins so interactables can activate during a run.</summary>
    public static event System.Action OnExecutionStarted;

    // Prevents duplicate GameWin/GameOver calls within the same execution turn.
    private bool m_IsGameOver;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ─── Turn Flow ────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by <see cref="UIManager"/> when the Play button is pressed, or by
    /// <see cref="DeviceInputProvider"/> when Submit (Enter/Start) is pressed.
    /// Validates the sequence, blocks input, then starts execution.
    /// </summary>
    [ContextMenu("Start Play")]
    public void OnPlayClicked()
    {
        if (SequenceManager.Instance == null || !SequenceManager.Instance.CanExecute)
        {
            Debug.Log("[GameManager] Cannot start — sequence is empty or not ready.");
            return;
        }

        m_IsGameOver = false;
        DeviceInputProvider.Instance?.SetEnabled(false);
        OnExecutionStarted?.Invoke();
        PlayerController.Instance?.OnGamePlayStart();
    }

    /// <summary>
    /// Called by <see cref="PlayerController"/> after all beats finish executing.
    /// Clears the sequence, restores input, and fires the turn-reset event.
    /// </summary>
    public void PlayEnded()
    {
        IsKeyCollected = false;
        StopExecution();
        OnKeyReset?.Invoke();
    }

    public void StopExecution()
    {
        SequenceManager.Instance?.OnTurnEnded();
        DeviceInputProvider.Instance?.SetEnabled(true);
        OnTurnReset?.Invoke();
    }

    /// <summary>
    /// Resets the level in place — WITHOUT reloading the scene — so per-scene state
    /// (e.g. the doctor's failure streak) survives. Fires the full reset chain and
    /// clears the queued sequence. The caller is responsible for repositioning the
    /// player and restoring input. Used by the death/restart flow.
    /// </summary>
    public void SoftResetLevel()
    {
        IsKeyCollected = false;
        OnFullReset?.Invoke();   // bricks, redirectors → initial state
        OnTurnReset?.Invoke();   // movable bricks, lock points
        OnKeyReset?.Invoke();    // placeable key + key slot
        SequenceManager.Instance?.OnTurnEnded();
    }

    // ─── Game Events ─────────────────────────────────────────────────────────

    /// <summary>Marks the key as collected. Called by <see cref="Key"/> on interaction.</summary>
    public void KeyCollected() { IsKeyCollected = true; }

    /// <summary>Triggers the win state. Ignored if already in a game-over state this turn.</summary>
    public void GameWin()
    {
        if (m_IsGameOver) return;
        m_IsGameOver = true;
    }

    /// <summary>Triggers the lose state. Ignored if already in a game-over state this turn.</summary>
    public void GameOver()
    {
        if (m_IsGameOver) return;
        m_IsGameOver = true;
    }

    // ─── Scene Management ────────────────────────────────────────────────────

    /// <summary>Reloads the currently active scene.</summary>
    public void ReloadLevel()
    {
        OnFullReset?.Invoke();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>
    /// Called by the Restart UI button. Fires <see cref="OnFullReset"/> then reloads
    /// the scene — use this instead of ReloadLevel() so both paths share the event.
    /// </summary>
    public void RestartLevel() => ReloadLevel();

    /// <summary>Loads the next scene by build index, looping back to 0 after the last level.</summary>
    public void LoadNextLevel()
    {
        int next = SceneManager.GetActiveScene().buildIndex + 1;
        if (next >= SceneManager.sceneCountInBuildSettings) next = 0;
        SceneManager.LoadScene(next);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            GoToMainMenu();
        }
    }

    public void GoToMainMenu()
    {
        SceneManager.LoadScene(0);
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
