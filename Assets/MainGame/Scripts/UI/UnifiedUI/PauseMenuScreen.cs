using UnityEngine;
using UnityEngine.UI;

namespace MainGame.UI.Unified
{
    /// <summary>
    /// Unified Pause Menu Screen controller.
    /// </summary>
    public class PauseMenuScreen : UIScreen
    {
        [Header("Menu Buttons")]
        [SerializeField] private Button m_ResumeButton;
        [SerializeField] private Button m_OptionsButton;
        [SerializeField] private Button m_QuitButton;

        [Header("Screens Mapping")]
        [SerializeField] private UIScreen m_OptionsScreen;

        private void OnEnable()
        {
            if (m_ResumeButton != null) m_ResumeButton.onClick.AddListener(HandleResumeClicked);
            if (m_OptionsButton != null) m_OptionsButton.onClick.AddListener(HandleOptionsClicked);
            if (m_QuitButton != null) m_QuitButton.onClick.AddListener(HandleQuitClicked);
        }

        private void OnDisable()
        {
            if (m_ResumeButton != null) m_ResumeButton.onClick.RemoveListener(HandleResumeClicked);
            if (m_OptionsButton != null) m_OptionsButton.onClick.RemoveListener(HandleOptionsClicked);
            if (m_QuitButton != null) m_QuitButton.onClick.RemoveListener(HandleQuitClicked);
        }

        private void HandleResumeClicked()
        {
            AudioManager.Instance?.PlayButton();
            // Close Pause Menu via UINavigationManager if desired
            if (UINavigationManager.Instance != null)
            {
                UINavigationManager.Instance.PopScreen();
            }
        }

        private void HandleOptionsClicked()
        {
            AudioManager.Instance?.PlayButton();
            if (m_OptionsScreen != null && UINavigationManager.Instance != null)
            {
                UINavigationManager.Instance.PushScreen(m_OptionsScreen);
            }
        }

        private void HandleQuitClicked()
        {
            AudioManager.Instance?.PlayButton();
            Debug.Log("[PauseMenuScreen] Returning to main menu or quitting...");
            // Example logic: reload Main Menu scene or push Main Menu screen
            Application.Quit();
        }
    }
}
