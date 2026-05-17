using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Input provider and sequence source for mouse/toggle UI mode.
/// Wraps the existing <see cref="ActionManager"/> + <see cref="ActionTimelineController"/> system
/// and adapts it to <see cref="ISequenceSource"/> so <see cref="PlayerController"/> can
/// execute it through the unified pipeline alongside keyboard/gamepad mode.
///
/// On Play: reads the current toggle grid and bakes it to a flat beat sequence.
/// On Clear: resets all toggles and re-randomizes beat tunes (same as the original clear).
/// </summary>
public class MouseInputProvider : MonoBehaviour, IInputProvider, ISequenceSource
{
    [SerializeField] private ActionManager m_ActionManager;

    [Tooltip("How many beat slots exist in the toggle grid.")]
    [SerializeField] private int m_MaxBeats = 6;

    [Tooltip("Pool of audio clips distributed to timeline rows on reset/clear.")]
    [SerializeField] private List<AudioClip> m_BeatTunes = new List<AudioClip>();

    [Tooltip("Per-action audio clips for beat playback during execution.")]
    [SerializeField] private ActionAudioEntry[] m_ActionAudioMap;

    private readonly List<ActionTypeEnum> m_BakedSequence = new List<ActionTypeEnum>();

    // ─── IInputProvider ──────────────────────────────────────────────────────

    public bool IsEnabled { get; private set; } = true;

    /// <summary>Enables or disables mouse mode. Called by InputModeManager on mode switch.</summary>
    public void SetEnabled(bool enabled) => IsEnabled = enabled;

    // ─── ISequenceSource ─────────────────────────────────────────────────────

    public int SequenceLength => m_BakedSequence.Count;
    public bool CanExecute => IsEnabled && m_BakedSequence.Count > 0;

    public ActionTypeEnum? GetActionAt(int index)
    {
        if (index < 0 || index >= m_BakedSequence.Count) return null;
        return m_BakedSequence[index];
    }

    public AudioClip GetClipForAction(ActionTypeEnum action)
    {
        if (m_ActionAudioMap == null) return null;
        foreach (ActionAudioEntry entry in m_ActionAudioMap)
            if (entry.actionType == action) return entry.clip;
        return null;
    }

    // ─── Public API ──────────────────────────────────────────────────────────

    /// <summary>
    /// Reads the current toggle grid and converts it to a flat beat sequence.
    /// Call this just before PlayerController.OnGamePlayStart() in mouse mode.
    /// Each beat takes the first active action found across all rows (one action per beat).
    /// </summary>
    public void BakeSequence()
    {
        m_BakedSequence.Clear();

        if (m_ActionManager == null)
        {
            Debug.LogError("[MouseInputProvider] ActionManager is not assigned.", this);
            return;
        }

        for (int beat = 0; beat < m_MaxBeats; beat++)
        {
            ActionState[] states = m_ActionManager.GetActionsOfIndex(beat);
            if (states == null) continue;

            // Take the first active action at this beat (one action per beat slot)
            foreach (ActionState state in states)
            {
                if (state.isActive)
                {
                    m_BakedSequence.Add(state.type);
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Resets all toggles in the grid and re-randomizes beat tunes.
    /// Equivalent to the original UIManager.OnClearClicked() behavior.
    /// </summary>
    public void ResetToggles()
    {
        if (m_ActionManager != null)
            m_ActionManager.OnActionReset(new List<AudioClip>(m_BeatTunes));

        m_BakedSequence.Clear();
    }

    /// <summary>Clears the baked sequence after a turn ends (does not reset the toggle UI).</summary>
    public void OnTurnEnded() => m_BakedSequence.Clear();
}
