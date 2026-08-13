using UnityEngine;
using UnityEngine.UI;

namespace MainGame.UI.Unified
{
    /// <summary>
    /// Unified Main Menu Screen controller managing the HomeScreenPanel New UI actions.
    /// </summary>
    public class MainMenuScreen : UIScreen
    {
        [Header("Main Menu Buttons")]
        [SerializeField] private Button m_ContinueButton;
        [SerializeField] private Button m_NewGameButton;
        [SerializeField] private Button m_CollectButton;
        [SerializeField] private Button m_OptionsButton;
        [SerializeField] private Button m_CreditsButton;
        [SerializeField] private Button m_ExitButton;

        [Header("Screens Mapping")]
        [SerializeField] private UIScreen m_LevelSelectionScreen;
        [SerializeField] private UIScreen m_CollectionScreen;
        [SerializeField] private UIScreen m_OptionsScreen;
        [SerializeField] private UIScreen m_CreditsScreen;
        [SerializeField] private UIScreen m_ConfirmationPopupScreen;

        /// <summary>
        /// Dynamically selects the Continue button if it is active and enabled, otherwise falls back to New Game.
        /// </summary>
        public override GameObject DefaultSelectedObject
        {
            get
            {
                if (m_ContinueButton != null && m_ContinueButton.gameObject.activeInHierarchy && m_ContinueButton.interactable)
                {
                    return m_ContinueButton.gameObject;
                }
                return m_NewGameButton != null ? m_NewGameButton.gameObject : m_DefaultSelectedObject;
            }
        }

        private void OnEnable()
        {
            if (m_ContinueButton != null) m_ContinueButton.onClick.AddListener(HandleContinueClicked);
            if (m_NewGameButton != null) m_NewGameButton.onClick.AddListener(HandleNewGameClicked);
            if (m_CollectButton != null) m_CollectButton.onClick.AddListener(HandleCollectClicked);
            if (m_OptionsButton != null) m_OptionsButton.onClick.AddListener(HandleOptionsClicked);
            if (m_CreditsButton != null) m_CreditsButton.onClick.AddListener(HandleCreditsClicked);
            if (m_ExitButton != null) m_ExitButton.onClick.AddListener(HandleExitClicked);
        }

        private void OnDisable()
        {
            if (m_ContinueButton != null) m_ContinueButton.onClick.RemoveListener(HandleContinueClicked);
            if (m_NewGameButton != null) m_NewGameButton.onClick.RemoveListener(HandleNewGameClicked);
            if (m_CollectButton != null) m_CollectButton.onClick.RemoveListener(HandleCollectClicked);
            if (m_OptionsButton != null) m_OptionsButton.onClick.RemoveListener(HandleOptionsClicked);
            if (m_CreditsButton != null) m_CreditsButton.onClick.RemoveListener(HandleCreditsClicked);
            if (m_ExitButton != null) m_ExitButton.onClick.RemoveListener(HandleExitClicked);
        }

        private void HandleContinueClicked()
        {
            AudioManager.Instance?.PlayButton();
            // Opens Level Selection screen so players can choose where to continue
            if (m_LevelSelectionScreen != null && UINavigationManager.Instance != null)
            {
                UINavigationManager.Instance.PushScreen(m_LevelSelectionScreen);
            }
        }

        private void HandleNewGameClicked()
        {
            AudioManager.Instance?.PlayButton();
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnPlayClicked();
            }
            else
            {
                Debug.LogWarning("[MainMenuScreen] GameManager instance not found, unable to play.");
            }
        }

        private void HandleCollectClicked()
        {
            AudioManager.Instance?.PlayButton();
            if (m_CollectionScreen != null && UINavigationManager.Instance != null)
            {
                UINavigationManager.Instance.PushScreen(m_CollectionScreen);
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

        private void HandleCreditsClicked()
        {
            AudioManager.Instance?.PlayButton();
            if (m_CreditsScreen != null && UINavigationManager.Instance != null)
            {
                UINavigationManager.Instance.PushScreen(m_CreditsScreen);
            }
        }

        private void HandleExitClicked()
        {
            AudioManager.Instance?.PlayButton();
            if (m_ConfirmationPopupScreen != null && UINavigationManager.Instance != null)
            {
                UINavigationManager.Instance.PushScreen(m_ConfirmationPopupScreen);
            }
            else
            {
                Debug.Log("[MainMenuScreen] ConfirmationPopupScreen not assigned, quitting application directly...");
                Application.Quit();
            }
        }
    }
}
