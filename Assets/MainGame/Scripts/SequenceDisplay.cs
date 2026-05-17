using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays the current keyboard/gamepad command queue as a row of labeled slots.
/// Subscribes to <see cref="SequenceManager.OnSequenceChanged"/> and automatically
/// refreshes whenever the queue changes. Only relevant in Device (keyboard/gamepad) mode;
/// hide this panel when in Mouse mode from your settings UI.
/// </summary>
public class SequenceDisplay : MonoBehaviour
{
    [Tooltip("Root GameObjects for each slot. Count should match SequenceManager.MaxLength.")]
    [SerializeField] private GameObject[] m_Slots;

    [Tooltip("Text component inside each slot that shows the action icon or label.")]
    [SerializeField] private Text[] m_SlotLabels;

    [SerializeField] private SequenceManager m_SequenceManager;

    // Unicode icons for each action type displayed in the slots
    private static readonly Dictionary<ActionTypeEnum, string> k_Icons =
        new Dictionary<ActionTypeEnum, string>
        {
            { ActionTypeEnum.Left,     "◀" },
            { ActionTypeEnum.Right,    "▶" },
            { ActionTypeEnum.Jump,     "▲" },
            { ActionTypeEnum.Interact, "✦" }
        };

    private void Awake()
    {
        if (m_SequenceManager == null)
            Debug.LogError("[SequenceDisplay] SequenceManager is not assigned.", this);
    }

    private void OnEnable()
    {
        if (m_SequenceManager != null)
            m_SequenceManager.OnSequenceChanged += RefreshDisplay;
    }

    private void OnDisable()
    {
        if (m_SequenceManager != null)
            m_SequenceManager.OnSequenceChanged -= RefreshDisplay;
    }

    private void Start() => RefreshDisplay();

    /// <summary>
    /// Updates all slot visuals to reflect the current queued sequence.
    /// Filled slots show an action icon; empty slots show a placeholder dash.
    /// </summary>
    public void RefreshDisplay()
    {
        if (m_SequenceManager == null || m_Slots == null) return;

        IReadOnlyList<ActionTypeEnum> seq = m_SequenceManager.Sequence;

        for (int i = 0; i < m_Slots.Length; i++)
        {
            if (m_Slots[i] == null) continue;

            bool filled = i < seq.Count;

            if (m_SlotLabels != null && i < m_SlotLabels.Length && m_SlotLabels[i] != null)
            {
                m_SlotLabels[i].text = filled
                    ? (k_Icons.TryGetValue(seq[i], out string icon) ? icon : seq[i].ToString())
                    : "—";
            }
        }
    }
}
