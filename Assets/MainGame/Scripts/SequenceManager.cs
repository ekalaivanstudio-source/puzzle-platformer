using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton. Manages the player's command queue for keyboard/gamepad input.
/// The player builds up actions via <see cref="DeviceInputProvider"/> one key press at a time,
/// then presses Submit (Enter/Start) to execute the full sequence.
/// </summary>
public class SequenceManager : MonoBehaviour, ISequenceSource
{
    public static SequenceManager Instance { get; private set; }

    // Driven by the level's LevelConfig (via LevelContext) at runtime. Kept serialized but
    // hidden so any existing per-scene value survives as a fallback when no config is set.
    [HideInInspector, SerializeField] private int m_MaxSequenceLength = 6;

    [HideInInspector, SerializeField] private bool m_RequireFullSequence = false;


    private readonly List<ActionTypeEnum> m_Sequence = new List<ActionTypeEnum>();

    // Optional correct sequence â€” when set, CanExecute also requires an exact match.
    private ActionTypeEnum[] m_CorrectSequence;

    /// <summary>Read-only view of the current queued command sequence.</summary>
    public IReadOnlyList<ActionTypeEnum> Sequence => m_Sequence;

    // â”€â”€â”€ ISequenceSource â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public int SequenceLength => m_Sequence.Count;
    public bool CanExecute => m_RequireFullSequence
        ? (IsFull && IsSequenceCorrect())
        : !IsEmpty;

    public ActionTypeEnum? GetActionAt(int index)
    {
        if (index < 0 || index >= m_Sequence.Count) return null;
        return m_Sequence[index];
    }

    // â”€â”€â”€ Properties â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>Maximum allowed number of queued actions.</summary>
    public int MaxLength => m_MaxSequenceLength;

    /// <summary>Overrides the maximum sequence length at runtime (e.g. driven by a level's correct sequence).</summary>
    public void SetMaxLength(int length) { m_MaxSequenceLength = Mathf.Max(1, length); }

    /// <summary>
    /// Registers the correct sequence for this level. When set, <see cref="CanExecute"/> will
    /// return false unless the queued sequence exactly matches. Pass null to disable the check.
    /// </summary>
    public void SetCorrectSequence(ActionTypeEnum[] sequence) { m_CorrectSequence = sequence; }

    /// <summary>True when the queue has reached its maximum length.</summary>
    public bool IsFull => m_Sequence.Count >= m_MaxSequenceLength;

    /// <summary>True when no actions have been queued.</summary>
    public bool IsEmpty => m_Sequence.Count == 0;

    // Returns true when no correct sequence is registered, or when the current
    // sequence exactly matches the registered correct sequence.
    // Slots set to ActionTypeEnum.Any are wildcards and match any queued action.
    private bool IsSequenceCorrect()
    {
        if (m_CorrectSequence == null || m_CorrectSequence.Length == 0) return true;
        if (m_Sequence.Count != m_CorrectSequence.Length) return false;
        for (int i = 0; i < m_CorrectSequence.Length; i++)
        {
            if (m_CorrectSequence[i] == ActionTypeEnum.Any) continue;  // wildcard
            if (m_Sequence[i] != m_CorrectSequence[i]) return false;
        }
        return true;
    }

    /// <summary>Fired whenever the sequence is modified (add, remove, or clear).</summary>
    public event Action OnSequenceChanged;

    // â”€â”€â”€ Lifecycle â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        ApplyConfig();
    }

    /// <summary>Copies the sequence settings from the level's config, if one is present.</summary>
    private void ApplyConfig()
    {
        LevelConfig cfg = LevelContext.Instance != null ? LevelContext.Instance.Config : null;
        if (cfg == null) return;

        m_MaxSequenceLength = Mathf.Max(1, cfg.sequence.maxSequenceLength);
        m_RequireFullSequence = cfg.sequence.requireFullSequence;
    }

    private void OnValidate()
    {
        if (m_MaxSequenceLength <= 0) m_MaxSequenceLength = 1;
    }

    // â”€â”€â”€ Public API â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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

    /// <summary>Clears the queue at turn end. Called by <see cref="GameManager.PlayEnded"/>.</summary>
    public void OnTurnEnded() => ClearSequence();

}

