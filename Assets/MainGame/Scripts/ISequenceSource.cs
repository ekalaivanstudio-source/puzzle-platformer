using UnityEngine;

/// <summary>
/// Abstraction over any system that provides a beat-action sequence for
/// <see cref="PlayerController"/> to execute.
/// Currently implemented by <see cref="SequenceManager"/>.
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


    /// <summary>True when there are actions available and execution can begin.</summary>
    bool CanExecute { get; }
}
