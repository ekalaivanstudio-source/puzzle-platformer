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
        SequenceManager.Instance?.OnTurnEnded();
        DeviceInputProvider.Instance?.SetEnabled(true);
        OnTurnReset?.Invoke();
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
    public void ReloadLevel() =>
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

    /// <summary>Loads the next scene by build index, looping back to 0 after the last level.</summary>
    public void LoadNextLevel()
    {
        int next = SceneManager.GetActiveScene().buildIndex + 1;
        if (next >= SceneManager.sceneCountInBuildSettings) next = 0;
        SceneManager.LoadScene(next);
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
