using System.Collections;
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
        [SerializeField] private ConfirmationPopupScreen m_ConfirmationPopupScreen;

        [Header("Confirmation Visuals")]
        [Tooltip("Sprite showing 'EXIT?'")]
        [SerializeField] private Sprite m_ExitTitleSprite;

        [Header("Scene Loading")]
        [Tooltip("Build index of the first playable level, loaded when starting a new game.")]
        [SerializeField] private int m_FirstLevelBuildIndex = 1;

        private UIVerticalNavigationLinker m_NavigationLinker;

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

        protected override void Awake()
        {
            base.Awake();

            m_NavigationLinker = GetComponent<UIVerticalNavigationLinker>();
            if (m_NavigationLinker == null)
            {
                m_NavigationLinker = GetComponentInChildren<UIVerticalNavigationLinker>(true);
            }
        }

        public override void Open()
        {
            base.Open();
            RefreshContinueButtonState();
        }

        private void RefreshContinueButtonState()
        {
            if (m_ContinueButton == null) return;

            bool hasSave = ModernLevelSelection.SaveManager.HasSaveData();
            m_ContinueButton.gameObject.SetActive(hasSave);
            m_ContinueButton.interactable = hasSave;

            // Showing/hiding a row changes the vertical chain, so relink it.
            if (m_NavigationLinker != null)
            {
                m_NavigationLinker.RefreshNavigationLinks();
            }
        }

        private void Start()
        {
            RefreshContinueButtonState();

            // If returning from the pause menu, jump straight back into Level Selection. Deferred by a
            // frame so this runs after UINavigationManager.Start has pushed the initial screen, otherwise
            // the main menu would land on top of the level selection screen.
            if (PauseMenuScreen.AutoOpenLevelSelection)
            {
                PauseMenuScreen.AutoOpenLevelSelection = false; // Reset flag
                StartCoroutine(OpenLevelSelectionNextFrame());
            }
        }

        private IEnumerator OpenLevelSelectionNextFrame()
        {
            yield return null;

            if (m_LevelSelectionScreen != null && UINavigationManager.Instance != null)
            {
                UINavigationManager.Instance.PushScreen(m_LevelSelectionScreen);
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
            PushScreen(m_LevelSelectionScreen);
        }

        private void HandleNewGameClicked()
        {
            AudioManager.Instance?.PlayButton();
            ModernLevelSelection.SaveManager.ResetProgress();
            Collectables.CollectableSaveSystem.ResetAll();

            // The intro cutscene loads the level itself once it finishes. It declines when the home
            // screen has no cutscene built, in which case we go straight in as before.
            if (!IntroCutsceneScreen.TryPlay(m_FirstLevelBuildIndex))
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(m_FirstLevelBuildIndex);
            }
        }

        private void HandleCollectClicked()
        {
            AudioManager.Instance?.PlayButton();
            PushScreen(m_CollectionScreen);
        }

        private void HandleOptionsClicked()
        {
            AudioManager.Instance?.PlayButton();
            PushScreen(m_OptionsScreen);
        }

        private void HandleCreditsClicked()
        {
            AudioManager.Instance?.PlayButton();
            PushScreen(m_CreditsScreen);
        }

        private void HandleExitClicked()
        {
            AudioManager.Instance?.PlayButton();

            if (m_ConfirmationPopupScreen == null || UINavigationManager.Instance == null)
            {
                Debug.LogWarning("[MainMenuScreen] ConfirmationPopupScreen not assigned, quitting application directly.");
                QuitApplication();
                return;
            }

            m_ConfirmationPopupScreen.SetupAction(QuitApplication, m_ExitTitleSprite);
            UINavigationManager.Instance.PushScreen(m_ConfirmationPopupScreen);
        }

        private static void QuitApplication()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private static void PushScreen(UIScreen screen)
        {
            if (screen != null && UINavigationManager.Instance != null)
            {
                UINavigationManager.Instance.PushScreen(screen);
            }
        }
    }
}
