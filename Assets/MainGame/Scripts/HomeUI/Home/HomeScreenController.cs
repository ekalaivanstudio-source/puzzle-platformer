using UnityEngine;
using UnityEngine.UI;

namespace HomeUI
{
    /// <summary>
    /// Wires the four Home Screen buttons (Play, Collections, Settings, Quit) to navigation
    /// actions. It is deliberately thin: it translates clicks into <see cref="ScreenManager"/>
    /// calls and a confirmation popup, and owns no game state itself.
    ///
    /// Panel ids are data (Inspector strings), not hard-coded constants, so renaming/adding
    /// screens never requires editing this script.
    /// </summary>
    public class HomeScreenController : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private Button m_PlayButton;
        [SerializeField] private Button m_CollectionsButton;
        [SerializeField] private Button m_SettingsButton;
        [SerializeField] private Button m_QuitButton;

        [Header("Navigation")]
        [SerializeField] private ScreenManager m_ScreenManager;

        [Tooltip("Panel id (as set on the UIPanel) opened by Play — the Level Selection screen.")]
        [SerializeField] private string m_LevelSelectionPanelId = "LevelSelection";
        [SerializeField] private string m_CollectionsPanelId = "Collections";
        [SerializeField] private string m_SettingsPanelId = "Settings";

        [Header("Quit")]
        [Tooltip("Reusable confirmation popup shown before quitting.")]
        [SerializeField] private ConfirmationPopup m_ConfirmationPopup;
        [SerializeField] private string m_QuitTitle = "Quit Game";
        [TextArea] [SerializeField] private string m_QuitMessage = "Are you sure you want to quit the game?";

        private void Awake()
        {
            if (m_ScreenManager == null) m_ScreenManager = FindFirstObjectByType<ScreenManager>();

            Wire(m_PlayButton, OnPlay);
            Wire(m_CollectionsButton, OnCollections);
            Wire(m_SettingsButton, OnSettings);
            Wire(m_QuitButton, OnQuit);
        }

        private static void Wire(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;
            button.onClick.AddListener(action);
        }

        private void OnPlay()
        {
            AudioManager.Instance?.PlayButton();
            m_ScreenManager.Show(m_LevelSelectionPanelId);
        }

        private void OnCollections()
        {
            AudioManager.Instance?.PlayButton();
            m_ScreenManager.Show(m_CollectionsPanelId);
        }

        private void OnSettings()
        {
            AudioManager.Instance?.PlayButton();
            m_ScreenManager.Show(m_SettingsPanelId);
        }

        private void OnQuit()
        {
            AudioManager.Instance?.PlayButton();

            if (m_ConfirmationPopup != null)
                m_ConfirmationPopup.Show(m_QuitTitle, m_QuitMessage, onYes: QuitApplication);
            else
                QuitApplication();
        }

        /// <summary>Quits the build; stops Play mode in the editor so the flow is testable.</summary>
        private void QuitApplication()
        {
            Debug.Log("[HomeScreenController] Quitting application.");
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}
