using UnityEngine;

/// <summary>
/// Abstraction over any system that provides a beat-action sequence for
/// <see cref="PlayerController"/> to execute.
/// Implement this on any input mode adapter (mouse toggle grid, keyboard sequence, etc.).
/// PlayerController only references this interface — it is fully decoupled from
/// how the sequence was built.
/// </summary>
public interface ISequenceSource
{
    /// <summary>Total number of beats to execute this turn.</summary>
    int SequenceLength { get; }

    /// <summary>
    /// Returns the action queued at the given beat index.
    /// Returns null if the index is out of range or that beat has no action.
    /// </summary>
    ActionTypeEnum? GetActionAt(int index);

    /// <summary>
    /// Returns the audio clip mapped to the given action type for beat feedback.
    /// Returns null if no clip is configured.
    /// </summary>
    AudioClip GetClipForAction(ActionTypeEnum action);

    /// <summary>True when there are actions available and execution can begin.</summary>
    bool CanExecute { get; }
}
