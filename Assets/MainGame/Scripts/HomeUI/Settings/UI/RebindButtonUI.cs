using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HomeUI
{
    /// <summary>
    /// One rebindable control row. Shows the action's current binding and, when clicked, listens
    /// for the next key/button the player presses and assigns it. Works for both keyboard and
    /// controller bindings — which one is captured depends on what the player presses.
    ///
    /// Add one of these per binding you want players to remap (Jump, Move Left, …). The action
    /// name + binding index are data, so designers configure rows in the Inspector, not in code.
    /// </summary>
    public class RebindButtonUI : MonoBehaviour
    {
        [SerializeField] private SettingsManager m_Settings;

        [Header("Binding")]
        [Tooltip("Action to rebind, e.g. \"Player/Jump\" or just \"Jump\".")]
        [SerializeField] private string m_ActionName;

        [Tooltip("Index of the binding within the action (0 for simple single-control actions, " +
                 "or a specific composite part).")]
        [SerializeField] private int m_BindingIndex = 0;

        [Header("References")]
        [SerializeField] private Button m_Button;
        [SerializeField] private TextMeshProUGUI m_BindingLabel;
        [SerializeField] private TextMeshProUGUI m_ActionLabel;
        [SerializeField] private string m_DisplayName;

        private InputManager Input => m_Settings != null && m_Settings.Input != null
            ? m_Settings.Input
            : (SettingsManager.Instance != null ? SettingsManager.Instance.Input : null);

        private void Awake()
        {
            if (m_Button == null) m_Button = GetComponent<Button>();
            if (m_Button != null) m_Button.onClick.AddListener(BeginRebind);
            if (m_ActionLabel != null && !string.IsNullOrEmpty(m_DisplayName))
                m_ActionLabel.text = m_DisplayName;
        }

        private void OnEnable() => Refresh();

        /// <summary>Updates the label to the current binding (e.g. after a rebind or reset).</summary>
        public void Refresh()
        {
            if (m_BindingLabel == null || Input == null) return;
            m_BindingLabel.text = Input.GetBindingDisplayString(m_ActionName, m_BindingIndex);
        }

        private void BeginRebind()
        {
            if (Input == null) return;

            AudioManager.Instance?.PlayButton();
            if (m_BindingLabel != null) m_BindingLabel.text = "Press a key…";
            if (m_Button != null) m_Button.interactable = false;

            Input.StartInteractiveRebind(m_ActionName, m_BindingIndex, onComplete: () =>
            {
                if (m_Button != null) m_Button.interactable = true;
                Refresh();
            });
        }
    }
}
