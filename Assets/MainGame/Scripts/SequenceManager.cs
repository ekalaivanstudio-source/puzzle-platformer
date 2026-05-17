using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages the player's command queue for keyboard/gamepad input mode.
/// The player builds up actions via <see cref="DeviceInputProvider"/> one key press at a time,
/// then presses Submit (Enter/Start) to execute the full sequence.
/// Implements <see cref="ISequenceSource"/> so <see cref="PlayerController"/> reads from it directly.
/// </summary>
public class SequenceManager : MonoBehaviour, ISequenceSource
{
    [Tooltip("Maximum number of actions the player can queue per turn.")]
    [SerializeField] private int m_MaxSequenceLength = 6;

    [Tooltip("Per-action audio clips played as feedback when each action is added to the queue.")]
    [SerializeField] private ActionAudioEntry[] m_ActionAudioMap;

    private readonly List<ActionTypeEnum> m_Sequence = new List<ActionTypeEnum>();

    /// <summary>Read-only view of the current queued command sequence.</summary>
    public IReadOnlyList<ActionTypeEnum> Sequence => m_Sequence;

    // ─── ISequenceSource ─────────────────────────────────────────────────────

    public int SequenceLength => m_Sequence.Count;
    public bool CanExecute => !IsEmpty;

    public ActionTypeEnum? GetActionAt(int index)
    {
        if (index < 0 || index >= m_Sequence.Count) return null;
        return m_Sequence[index];
    }

    public AudioClip GetClipForAction(ActionTypeEnum action)
    {
        if (m_ActionAudioMap == null) return null;
        foreach (ActionAudioEntry entry in m_ActionAudioMap)
            if (entry.actionType == action) return entry.clip;
        return null;
    }

    // ─── Properties ──────────────────────────────────────────────────────────

    /// <summary>Maximum allowed number of queued actions.</summary>
    public int MaxLength => m_MaxSequenceLength;

    /// <summary>True when the queue has reached its maximum length.</summary>
    public bool IsFull => m_Sequence.Count >= m_MaxSequenceLength;

    /// <summary>True when no actions have been queued.</summary>
    public bool IsEmpty => m_Sequence.Count == 0;

    /// <summary>Fired whenever the sequence is modified (add, remove, or clear).</summary>
    public event Action OnSequenceChanged;

    // ─── Lifecycle ───────────────────────────────────────────────────────────

    private void OnValidate()
    {
        if (m_MaxSequenceLength <= 0) m_MaxSequenceLength = 1;
    }

    // ─── Public API ──────────────────────────────────────────────────────────

    /// <summary>
    /// Appends an action to the end of the queue.
    /// Returns false and logs a message if the queue is already full.
    /// </summary>
    public bool AddAction(ActionTypeEnum action)
    {
        if (IsFull)
        {
            Debug.Log($"[SequenceManager] Queue is full ({m_MaxSequenceLength} actions max).");
            return false;
        }

        m_Sequence.Add(action);
        PlayAddFeedback(action);
        OnSequenceChanged?.Invoke();
        return true;
    }

    /// <summary>Removes the last added action (undo last key press).</summary>
    public void RemoveLastAction()
    {
        if (IsEmpty) return;
        m_Sequence.RemoveAt(m_Sequence.Count - 1);
        OnSequenceChanged?.Invoke();
    }

    /// <summary>Clears all queued actions.</summary>
    public void ClearSequence()
    {
        if (IsEmpty) return;
        m_Sequence.Clear();
        OnSequenceChanged?.Invoke();
    }

    /// <summary>
    /// Clears the queue at turn end, readying it for the next input phase.
    /// Called by <see cref="SequenceSourceRouter.OnTurnEnded"/>.
    /// </summary>
    public void OnTurnEnded() => ClearSequence();

    // Plays a short audio cue as immediate feedback when an action is added
    private void PlayAddFeedback(ActionTypeEnum action)
    {
        AudioClip clip = GetClipForAction(action);
        if (clip != null)
            AudioManager.Instance?.PlayBeatTune(clip, UnityEngine.Random.Range(0.8f, 1.2f));
    }
}

/// <summary>
/// Maps a single <see cref="ActionTypeEnum"/> to an <see cref="AudioClip"/> for beat sound feedback.
/// Assign one entry per action type in the SequenceManager and MouseInputProvider Inspectors.
/// </summary>
[Serializable]
public struct ActionAudioEntry
{
    [Tooltip("The action type this clip is played for.")]
    public ActionTypeEnum actionType;

    [Tooltip("Clip played when this action is added to the sequence.")]
    public AudioClip clip;
}
