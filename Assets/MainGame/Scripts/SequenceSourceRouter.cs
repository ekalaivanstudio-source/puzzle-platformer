using UnityEngine;

/// <summary>
/// Routes all <see cref="ISequenceSource"/> reads to whichever input mode is currently active.
/// <see cref="PlayerController"/> holds a reference to this single component and never needs
/// to know which input mode the player chose — all reads are transparently delegated.
///
/// This is the single point of truth for sequence data during execution.
/// </summary>
public class SequenceSourceRouter : MonoBehaviour, ISequenceSource
{
    [SerializeField] private InputModeManager m_InputModeManager;
    [SerializeField] private MouseInputProvider m_MouseProvider;
    [SerializeField] private SequenceManager m_SequenceManager;  // used in Device mode

    private void Awake()
    {
        if (m_InputModeManager == null) Debug.LogError("[SequenceSourceRouter] InputModeManager is not assigned.", this);
        if (m_MouseProvider == null) Debug.LogError("[SequenceSourceRouter] MouseInputProvider is not assigned.", this);
        if (m_SequenceManager == null) Debug.LogError("[SequenceSourceRouter] SequenceManager is not assigned.", this);
    }

    // Returns the active source based on the current input mode
    private ISequenceSource ActiveSource
    {
        get
        {
            if (m_InputModeManager == null) return null;
            return m_InputModeManager.CurrentMode == InputModeManager.InputMode.Mouse
                ? (ISequenceSource)m_MouseProvider
                : m_SequenceManager;
        }
    }

    // ─── ISequenceSource delegation ──────────────────────────────────────────

    public int SequenceLength => ActiveSource?.SequenceLength ?? 0;
    public bool CanExecute => ActiveSource?.CanExecute ?? false;

    public ActionTypeEnum? GetActionAt(int index) => ActiveSource?.GetActionAt(index);
    public AudioClip GetClipForAction(ActionTypeEnum a) => ActiveSource?.GetClipForAction(a);

    // ─── Turn lifecycle ──────────────────────────────────────────────────────

    /// <summary>
    /// Prepares the active source just before execution starts.
    /// In Mouse mode: bakes the toggle grid to a flat sequence.
    /// In Device mode: the queue is already built via key presses (no-op).
    /// Called by <see cref="GameManager.OnPlayClicked"/> before starting the turn.
    /// </summary>
    public void PrepareForExecution()
    {
        if (m_InputModeManager?.CurrentMode == InputModeManager.InputMode.Mouse)
            m_MouseProvider?.BakeSequence();
    }

    /// <summary>
    /// Cleans up after a turn ends. Clears the baked/queued sequence.
    /// Called by <see cref="GameManager.PlayEnded"/>.
    /// </summary>
    public void OnTurnEnded()
    {
        if (m_InputModeManager?.CurrentMode == InputModeManager.InputMode.Mouse)
            m_MouseProvider?.OnTurnEnded();
        else
            m_SequenceManager?.OnTurnEnded();
    }

    /// <summary>
    /// Clears the current sequence based on the active mode.
    /// Mouse mode: resets toggles and re-randomizes beat tunes.
    /// Device mode: clears the key-press queue.
    /// Called by <see cref="UIManager.OnClearClicked"/> and the Clear key/button.
    /// </summary>
    public void ClearCurrentSequence()
    {
        if (m_InputModeManager?.CurrentMode == InputModeManager.InputMode.Mouse)
            m_MouseProvider?.ResetToggles();
        else
            m_SequenceManager?.ClearSequence();
    }
}
