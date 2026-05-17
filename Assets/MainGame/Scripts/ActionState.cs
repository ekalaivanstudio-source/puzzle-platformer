using UnityEngine;

/// <summary>
/// Snapshot of a single action row at a specific beat index.
/// Produced by <see cref="ActionManager"/> and consumed by <see cref="PlayerController"/> each tick.
/// </summary>
public struct ActionState
{
    /// <summary>Which action type this row controls (Left, Right, Jump, Interact).</summary>
    public ActionTypeEnum type;

    /// <summary>Whether this action is toggled on for the current beat.</summary>
    public bool isActive;

    /// <summary>Audio clip assigned to this timeline row for beat feedback.</summary>
    public AudioClip BeatIndex;

    /// <summary>Randomized pitch applied when playing the beat sound.</summary>
    public float Pitch;
}
