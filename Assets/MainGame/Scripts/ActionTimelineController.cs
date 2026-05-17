using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages a single action row in the timeline UI (e.g., the Jump row or Left row).
/// Auto-attaches <see cref="ToggleSound"/> to each beat toggle and exposes the
/// active sequence and pitch values to <see cref="ActionManager"/>.
/// </summary>
public class ActionTimelineController : MonoBehaviour
{
    [Tooltip("The action type this row controls.")]
    [SerializeField] private ActionTypeEnum m_ActionType;

    [Tooltip("UI Toggle components representing each beat slot in this row.")]
    [SerializeField] private Toggle[] m_Toggles;

    /// <summary>The action type this row controls (Left, Right, Jump, Interact).</summary>
    public ActionTypeEnum ActionType => m_ActionType;

    /// <summary>Audio clip assigned to this row by ActionManager. Played on each beat tick.</summary>
    public AudioClip BeatIndex { get; set; }

    private float[] m_Pitches;

    private void Awake()
    {
        if (m_Toggles == null || m_Toggles.Length == 0)
        {
            Debug.LogError($"[ActionTimelineController] No toggles assigned on '{gameObject.name}'.", this);
            return;
        }

        m_Pitches = new float[m_Toggles.Length];

        for (int i = 0; i < m_Toggles.Length; i++)
        {
            if (m_Toggles[i] == null)
            {
                Debug.LogWarning($"[ActionTimelineController] Toggle at index {i} is null on '{gameObject.name}'.", this);
                continue;
            }

            // Attach ToggleSound at runtime and link it back to this controller
            ToggleSound ts = m_Toggles[i].gameObject.AddComponent<ToggleSound>();
            ts.controller = this;
        }

        RandomizePitches();
    }

    /// <summary>Returns a bool array indicating which beats are currently toggled on.</summary>
    public bool[] GetActionSequence()
    {
        if (m_Toggles == null) return System.Array.Empty<bool>();

        bool[] sequence = new bool[m_Toggles.Length];
        for (int i = 0; i < m_Toggles.Length; i++)
        {
            sequence[i] = m_Toggles[i] != null && m_Toggles[i].isOn;
        }
        return sequence;
    }

    /// <summary>Returns the randomized pitch values per beat slot.</summary>
    public float[] GetPitchofSequence() => m_Pitches ?? System.Array.Empty<float>();

    /// <summary>Turns all toggles off and re-randomizes beat pitches.</summary>
    public void OnActionReset()
    {
        if (m_Toggles == null) return;

        foreach (Toggle toggle in m_Toggles)
        {
            if (toggle != null)
                toggle.isOn = false;
        }

        RandomizePitches();
    }

    // Generates a new random pitch per slot and syncs them to ToggleSound components
    private void RandomizePitches()
    {
        if (m_Pitches == null) return;

        for (int i = 0; i < m_Pitches.Length; i++)
        {
            m_Pitches[i] = Random.Range(-3f, 3f);
        }

        for (int i = 0; i < m_Toggles.Length; i++)
        {
            if (m_Toggles[i] == null) continue;

            ToggleSound ts = m_Toggles[i].GetComponent<ToggleSound>();
            if (ts != null)
                ts.pitch = m_Pitches[i];
        }
    }
}
