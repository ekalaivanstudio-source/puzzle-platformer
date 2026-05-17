using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Aggregates all <see cref="ActionTimelineController"/> rows and provides a per-beat
/// snapshot to <see cref="PlayerController"/>.
/// Acts as the data bridge between the UI timeline and the gameplay execution layer.
/// </summary>
public class ActionManager : MonoBehaviour
{
    [Tooltip("All action row controllers in order (Left, Right, Jump, Interact).")]
    [SerializeField] private ActionTimelineController[] m_Controllers;

    private void Awake()
    {
        if (m_Controllers == null || m_Controllers.Length == 0)
            Debug.LogError("[ActionManager] No ActionTimelineControllers assigned.", this);
    }

    /// <summary>
    /// Returns an <see cref="ActionState"/> array for the given beat index.
    /// One entry per action row — includes type, isActive flag, beat clip, and pitch.
    /// Returns an empty array if controllers are not assigned or the index is out of range.
    /// </summary>
    /// <param name="index">The zero-based beat index to query.</param>
    public ActionState[] GetActionsOfIndex(int index)
    {
        if (m_Controllers == null || m_Controllers.Length == 0)
            return System.Array.Empty<ActionState>();

        ActionState[] states = new ActionState[m_Controllers.Length];

        for (int i = 0; i < m_Controllers.Length; i++)
        {
            ActionTimelineController controller = m_Controllers[i];

            if (controller == null)
            {
                Debug.LogWarning($"[ActionManager] Controller at index {i} is null — skipping.", this);
                continue;
            }

            bool[]  sequence = controller.GetActionSequence();
            float[] pitches  = controller.GetPitchofSequence();

            states[i] = new ActionState
            {
                type      = controller.ActionType,
                isActive  = index < sequence.Length && sequence[index],
                BeatIndex = controller.BeatIndex,
                Pitch     = index < pitches.Length ? pitches[index] : 0f
            };
        }

        return states;
    }

    /// <summary>
    /// Resets all timeline rows and distributes unique beat tunes randomly across them.
    /// Clips are drawn without replacement so no two rows share the same tune.
    /// </summary>
    /// <param name="beatTunes">Pool of audio clips to distribute.</param>
    public void OnActionReset(List<AudioClip> beatTunes)
    {
        if (m_Controllers == null) return;

        // Work on a copy so the original list is not modified
        List<AudioClip> available = new List<AudioClip>(beatTunes);

        foreach (ActionTimelineController controller in m_Controllers)
        {
            if (controller == null) continue;

            if (available.Count > 0)
            {
                int pick = Random.Range(0, available.Count);
                controller.BeatIndex = available[pick];
                available.RemoveAt(pick);
            }

            controller.OnActionReset();
        }
    }
}
