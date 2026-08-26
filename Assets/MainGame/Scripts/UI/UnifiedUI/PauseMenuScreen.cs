using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Events;

namespace MainGame.UI.Unified
{
    /// <summary>
    /// Unified Pause Menu Screen controller.
    /// Manages Reset, Levels, and Exit buttons, routing through a single ConfirmationPopupScreen with distinct sprites.
    /// Supports a UnityEvent for Reset, loading HomeScreen, and deep-linking into Level Selection.
    /// </summary>
    public class PauseMenuScreen : UIScreen
    {
        [Header("Menu Buttons")]
        [SerializeField] private Button m_ResetButton;
        [SerializeField] private Button m_LevelsButton;
        [SerializeField] private Button m_ExitButton;

        [Header("Screens Mapping")]
        [SerializeField] private ConfirmationPopupScreen m_ConfirmationPopupScreen;

        [Header("Title Sprites")]
        [Tooltip("Sprite showing 'RESET?'")]
        [SerializeField] private Sprite m_ResetTitleSprite;
        [Tooltip("Sprite showing 'LEVELS?' (or 'LEVEL?')")]
        [SerializeField] private Sprite m_LevelsTitleSprite;
        [Tooltip("Sprite showing 'EXIT?'")]
        [SerializeField] private Sprite m_ExitTitleSprite;

        [Header("Scene Loading Configurations")]
        [SerializeField] private string m_LevelsSceneName = "HomeScreen"; // Scene name for main menu/levels select

        [Header("Reset Event")]
        [Tooltip("UnityEvent invoked when the player confirms level reset.")]
        [SerializeField] private UnityEvent m_OnResetConfirmed;

        // Static flag used to tell the HomeScreen scene to auto-open the Level Selection screen on load
        public static bool AutoOpenLevelSelection { get; set; } = false;

        private void OnEnable()
        {
            if (m_ResetButton != null) m_ResetButton.onClick.AddListener(HandleResetClicked);
            if (m_LevelsButton != null) m_LevelsButton.onClick.AddListener(HandleLevelsClicked);
            if (m_ExitButton != null) m_ExitButton.onClick.AddListener(HandleExitClicked);
        }

        private void OnDisable()
        {
            if (m_ResetButton != null) m_ResetButton.onClick.RemoveListener(HandleResetClicked);
            if (m_LevelsButton != null) m_LevelsButton.onClick.RemoveListener(HandleLevelsClicked);
            if (m_ExitButton != null) m_ExitButton.onClick.RemoveListener(HandleExitClicked);
        }

        private void HandleResetClicked()
        {
            RequestConfirmation(m_ResetTitleSprite, () =>
            {
                Debug.Log("[PauseMenuScreen] Reset confirmed. Invoking event...");
                m_OnResetConfirmed?.Invoke();
            });
        }

        private void HandleLevelsClicked()
        {
            RequestConfirmation(m_LevelsTitleSprite, () =>
            {
                Debug.Log("[PauseMenuScreen] Levels confirmed. Redirecting to Level Selection screen...");
                LoadLevelsScene(autoOpenLevelSelection: true);
            });
        }

        private void HandleExitClicked()
        {
            RequestConfirmation(m_ExitTitleSprite, () =>
            {
                Debug.Log("[PauseMenuScreen] Exit confirmed. Loading main menu...");
                LoadLevelsScene(autoOpenLevelSelection: false);
            });
        }

        /// <summary>
        /// Shows the shared confirmation popup with the given title, running <paramref name="onConfirmed"/>
        /// only if the player accepts. When no popup is wired up the action runs immediately, so a missing
        /// inspector reference degrades to the old un-confirmed behaviour rather than a dead button.
        /// </summary>
        private void RequestConfirmation(Sprite titleSprite, Action onConfirmed)
        {
            AudioManager.Instance?.PlayButton();

            if (m_ConfirmationPopupScreen == null || UINavigationManager.Instance == null)
            {
                onConfirmed?.Invoke();
                return;
            }

            m_ConfirmationPopupScreen.SetupAction(onConfirmed, titleSprite);
            UINavigationManager.Instance.PushScreen(m_ConfirmationPopupScreen);
        }

        private void LoadLevelsScene(bool autoOpenLevelSelection)
        {
            AutoOpenLevelSelection = autoOpenLevelSelection;
            SceneManager.LoadScene(m_LevelsSceneName);
        }
    }
}
