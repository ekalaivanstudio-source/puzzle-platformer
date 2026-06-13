using UnityEngine;
using UnityEngine.UI;

namespace HomeUI
{
    /// <summary>
    /// Top-level Settings screen. Hosts the three category sections (Graphics / Audio / Controls)
    /// as switchable tabs, refreshes them from saved data whenever the screen opens, and provides
    /// Back + Reset-All. It is a thin conductor — the actual control binding lives in each category
    /// UI, so adding a category tab means adding a section + button, not editing logic here.
    /// </summary>
    public class SettingsPanelUI : UIPanel
    {
        [Header("Category Sections (root objects toggled per tab)")]
        [SerializeField] private GameObject[] m_Sections;
        [SerializeField] private Button[] m_TabButtons;

        [Header("Category UIs")]
        [SerializeField] private GraphicsSettingsUI m_Graphics;
        [SerializeField] private AudioSettingsUI m_Audio;
        [SerializeField] private ControlsSettingsUI m_Controls;

        [Header("Navigation")]
        [SerializeField] private Button m_BackButton;
        [SerializeField] private Button m_ResetAllButton;
        [SerializeField] private SettingsManager m_Settings;
        [SerializeField] private ConfirmationPopup m_ConfirmationPopup;

        protected override void Awake()
        {
            base.Awake();

            if (m_BackButton != null) m_BackButton.onClick.AddListener(OnBack);
            if (m_ResetAllButton != null) m_ResetAllButton.onClick.AddListener(OnResetAll);

            if (m_TabButtons != null)
            {
                for (int i = 0; i < m_TabButtons.Length; i++)
                {
                    int index = i; // capture
                    if (m_TabButtons[i] != null) m_TabButtons[i].onClick.AddListener(() => ShowCategory(index));
                }
            }
        }

        protected override void OnShow()
        {
            // Pull saved values into every control, then open the first tab.
            m_Graphics?.Refresh();
            m_Audio?.Refresh();
            m_Controls?.Refresh();
            ShowCategory(0);
        }

        /// <summary>Switches the visible category section.</summary>
        public void ShowCategory(int index)
        {
            AudioManager.Instance?.PlayButton();
            if (m_Sections == null) return;
            for (int i = 0; i < m_Sections.Length; i++)
                if (m_Sections[i] != null) m_Sections[i].SetActive(i == index);
        }

        private void OnBack()
        {
            AudioManager.Instance?.PlayButton();
            ScreenManager.Instance?.Back();
        }

        private void OnResetAll()
        {
            AudioManager.Instance?.PlayButton();
            SettingsManager settings = m_Settings != null ? m_Settings : SettingsManager.Instance;
            if (settings == null) return;

            if (m_ConfirmationPopup != null)
                m_ConfirmationPopup.Show("Reset Settings",
                    "Restore ALL settings to their defaults?",
                    onYes: () => { settings.ResetToDefaults(); OnShow(); });
            else
            {
                settings.ResetToDefaults();
                OnShow();
            }
        }
    }
}
