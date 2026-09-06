using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MainGame.UI.Unified
{
    /// <summary>
    /// Unified Main Menu Screen controller managing the HomeScreenPanel New UI actions,
    /// button confirmation punches, and physical cinematic transitions.
    /// </summary>
    [DisallowMultipleComponent]
    public class MainMenuScreen : UIScreen
    {
        [Header("Main Menu Buttons")]
        [SerializeField] private Button m_ContinueButton;
        [SerializeField] private Button m_NewGameButton;
        [SerializeField] private Button m_CollectButton;
        [SerializeField] private Button m_OptionsButton;
        [SerializeField] private Button m_CreditsButton;
        [SerializeField] private Button m_ExitButton;

        public Button ContinueButton => m_ContinueButton;
        public Button NewGameButton => m_NewGameButton;
        public Button CollectButton => m_CollectButton;
        public Button OptionsButton => m_OptionsButton;
        public Button CreditsButton => m_CreditsButton;
        public Button ExitButton => m_ExitButton;

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

        [Header("Animator Reference")]
        [SerializeField] private HomeScreenAnimator m_HomeScreenAnimator;

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

            if (m_HomeScreenAnimator == null)
            {
                m_HomeScreenAnimator = GetComponent<HomeScreenAnimator>();
            }
            if (m_HomeScreenAnimator == null)
            {
                m_HomeScreenAnimator = GetComponentInChildren<HomeScreenAnimator>(true);
            }
        }

        public override void Open()
        {
            base.Open();
            RefreshContinueButtonState();
        }

        public override void PlayEnterTransition(Action onComplete)
        {
            if (m_HomeScreenAnimator != null)
            {
                m_HomeScreenAnimator.PrepareEntranceState();
            }

            Open();

            if (m_HomeScreenAnimator != null)
            {
                m_HomeScreenAnimator.PlayEntrance(onComplete);
            }
            else
            {
                onComplete?.Invoke();
            }
        }

        public override void PlayExitTransition(Action onComplete)
        {
            if (m_HomeScreenAnimator != null)
            {
                m_HomeScreenAnimator.PlayExit(() =>
                {
                    SetCanvasGroupInteractive(false);
                    onComplete?.Invoke();
                });
            }
            else
            {
                Close();
                onComplete?.Invoke();
            }
        }

        private void RefreshContinueButtonState()
        {
            if (m_ContinueButton == null) return;

            bool hasSave = ModernLevelSelection.SaveManager.HasSaveData();
            m_ContinueButton.gameObject.SetActive(hasSave);
            m_ContinueButton.interactable = hasSave;

            if (m_NavigationLinker != null)
            {
                m_NavigationLinker.RefreshNavigationLinks();
            }
        }

        private void Start()
        {
            RefreshContinueButtonState();

            if (PauseMenuScreen.AutoOpenLevelSelection)
            {
                PauseMenuScreen.AutoOpenLevelSelection = false;
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

        private void TriggerWithPunch(Button button, Action callback)
        {
            if (UINavigationManager.Instance != null && UINavigationManager.Instance.IsTransitioning)
            {
                return;
            }

            if (button != null)
            {
                UIAnimatedButton animBtn = button.GetComponent<UIAnimatedButton>();
                if (animBtn != null)
                {
                    animBtn.PlayConfirmPunch(callback);
                    return;
                }
            }

            callback?.Invoke();
        }

        private void HandleContinueClicked()
        {
            TriggerWithPunch(m_ContinueButton, () =>
            {
                PushScreen(m_LevelSelectionScreen);
            });
        }

        private void HandleNewGameClicked()
        {
            TriggerWithPunch(m_NewGameButton, () =>
            {
                ModernLevelSelection.SaveManager.ResetProgress();
                Collectables.RobotCollectionService.ResetAll();

                if (!IntroCutsceneScreen.TryPlay(m_FirstLevelBuildIndex))
                {
                    UnityEngine.SceneManagement.SceneManager.LoadScene(m_FirstLevelBuildIndex);
                }
            });
        }

        private void HandleCollectClicked()
        {
            TriggerWithPunch(m_CollectButton, () =>
            {
                PushScreen(m_CollectionScreen);
            });
        }

        private void HandleOptionsClicked()
        {
            TriggerWithPunch(m_OptionsButton, () =>
            {
                PushScreen(m_OptionsScreen);
            });
        }

        private void HandleCreditsClicked()
        {
            TriggerWithPunch(m_CreditsButton, () =>
            {
                PushScreen(m_CreditsScreen);
            });
        }

        private void HandleExitClicked()
        {
            TriggerWithPunch(m_ExitButton, () =>
            {
                if (m_ConfirmationPopupScreen == null || UINavigationManager.Instance == null)
                {
                    Debug.LogWarning("[MainMenuScreen] ConfirmationPopupScreen not assigned, quitting application directly.");
                    QuitApplication();
                    return;
                }

                m_ConfirmationPopupScreen.SetupAction(QuitApplication, m_ExitTitleSprite);
                UINavigationManager.Instance.PushScreen(m_ConfirmationPopupScreen);
            });
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
