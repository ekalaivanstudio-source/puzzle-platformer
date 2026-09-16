using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using MainGame.UI.Feedback;
using MainGame.UI.Unified;

namespace MainGame.UI.PauseMenu
{
    /// <summary>
    /// Master controller for the self-contained, drop-in Pause Menu system.
    /// Orchestrates state machine, animation transitions, universal confirmations,
    /// embedded level selection, and guaranteed timeScale / input restoration.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    public class PauseMenuController : MonoBehaviour
    {
        public static PauseMenuController Instance { get; private set; }

        public static bool IsPaused => Instance != null && Instance.CurrentState != PauseState.Gameplay;

        public static event Action OnResetRequested;
        public static event Action OnPauseOpened;
        public static event Action OnPauseClosed;

        #region Inspector Fields

        [Header("State")]
        [SerializeField] private PauseState m_CurrentState = PauseState.Gameplay;

        [Header("Scene Configuration")]
        [SerializeField] private string m_HomeScreenName = "HomeScreen";

        [Header("Internal References")]
        [SerializeField] private RectTransform m_PausePanel;
        [SerializeField] private CanvasGroup m_DarkOverlay;
        [SerializeField] private Button m_ResetButton;
        [SerializeField] private Button m_LevelsButton;
        [SerializeField] private Button m_ExitButton;

        [Header("Sub-Controllers")]
        [SerializeField] private PauseAudioController m_Audio;
        [SerializeField] private PauseNavigationController m_Navigation;
        [SerializeField] private PauseConfirmationController m_Confirmation;
        [SerializeField] private PauseLevelSelectionController m_LevelSelection;
        [SerializeField] private PauseInputController m_Input;
        [SerializeField] private PauseMenuAnimator m_Animator;

        [Header("Title Sprites")]
        [SerializeField] private Sprite m_ResetTitleSprite;
        [SerializeField] private Sprite m_LevelsTitleSprite;
        [SerializeField] private Sprite m_ExitTitleSprite;

        [Header("Input Asset")]
        [SerializeField] private InputActionAsset m_InputActionAsset;

        [Header("Reset Event")]
        [Tooltip("UnityEvent invoked when level reset is confirmed.")]
        [SerializeField] private UnityEvent m_OnResetConfirmed;

        #endregion

        public PauseState CurrentState => m_CurrentState;
        public Button ResetButton => m_ResetButton;
        public Button LevelsButton => m_LevelsButton;
        public Button ExitButton => m_ExitButton;

        #region Unity Lifecycle

        private void Awake()
        {
            // 1. Home Screen protection: If placed into HomeScreen, stay completely inactive
            if (SceneManager.GetActiveScene().name == m_HomeScreenName)
            {
                gameObject.SetActive(false);
                return;
            }

            // 2. Singleton enforcement
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            ResolveInternalReferences();
            ValidateSetup();
            InitializeSubControllers();

            // 3. Start hidden in Gameplay state
            SetUIVisible(false);
            m_CurrentState = PauseState.Gameplay;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Start()
        {
            // Safety check: ensure timeScale is normal on level start
            if (Time.timeScale == 0f && m_CurrentState == PauseState.Gameplay)
            {
                Time.timeScale = 1f;
            }
        }

        #endregion

        #region Initialization & Discovery

        public void ResolveInternalReferences()
        {
            if (m_PausePanel == null)
            {
                Transform p = transform.Find("Pause screen/Pause panel") ?? transform.Find("Pause panel");
                if (p != null) m_PausePanel = p as RectTransform;
            }

            if (m_DarkOverlay == null)
            {
                Transform screen = transform.Find("Pause screen");
                if (screen != null) m_DarkOverlay = screen.GetComponent<CanvasGroup>();
                if (m_DarkOverlay == null) m_DarkOverlay = GetComponent<CanvasGroup>();
            }

            if (m_PausePanel != null)
            {
                if (m_ResetButton == null)
                {
                    Transform r = m_PausePanel.Find("Reset");
                    if (r != null) m_ResetButton = r.GetComponent<Button>();
                }
                if (m_LevelsButton == null)
                {
                    Transform l = m_PausePanel.Find("Level");
                    if (l != null) m_LevelsButton = l.GetComponent<Button>();
                }
                if (m_ExitButton == null)
                {
                    Transform e = m_PausePanel.Find("Exit");
                    if (e != null) m_ExitButton = e.GetComponent<Button>();
                }
            }

            if (m_Audio == null) m_Audio = GetComponent<PauseAudioController>() ?? gameObject.AddComponent<PauseAudioController>();
            if (m_Navigation == null) m_Navigation = GetComponent<PauseNavigationController>() ?? gameObject.AddComponent<PauseNavigationController>();
            if (m_Input == null) m_Input = GetComponent<PauseInputController>() ?? gameObject.AddComponent<PauseInputController>();

            if (m_Confirmation == null)
            {
                m_Confirmation = GetComponentInChildren<PauseConfirmationController>(true);
                if (m_Confirmation == null)
                {
                    Transform c = transform.Find("Pause screen/ConfirmationPopup") ?? transform.Find("ConfirmationPopup");
                    if (c != null) m_Confirmation = c.gameObject.AddComponent<PauseConfirmationController>();
                }
            }

            if (m_LevelSelection == null)
            {
                m_LevelSelection = GetComponentInChildren<PauseLevelSelectionController>(true);
                if (m_LevelSelection == null)
                {
                    Transform ls = transform.Find("Pause screen/Level Selection") ?? transform.Find("Level Selection");
                    if (ls != null) m_LevelSelection = ls.gameObject.AddComponent<PauseLevelSelectionController>();
                }
            }

            if (m_Animator == null)
            {
                m_Animator = GetComponentInChildren<PauseMenuAnimator>(true);
            }
        }

        private void ValidateSetup()
        {
            if (m_ResetButton == null) Debug.LogError("[PauseMenuController] RESET button not found in hierarchy!", this);
            if (m_LevelsButton == null) Debug.LogError("[PauseMenuController] LEVEL button not found in hierarchy!", this);
            if (m_ExitButton == null) Debug.LogError("[PauseMenuController] EXIT button not found in hierarchy!", this);
            if (m_Confirmation == null) Debug.LogError("[PauseMenuController] Confirmation panel not found!", this);
            if (m_LevelSelection == null) Debug.LogWarning("[PauseMenuController] Level Selection panel not found!", this);
        }

        private void InitializeSubControllers()
        {
            if (m_Navigation != null)
            {
                m_Navigation.Initialize(m_ResetButton, m_LevelsButton, m_ExitButton, m_InputActionAsset);
            }

            if (m_Input != null)
            {
                m_Input.Initialize(this, m_InputActionAsset);
            }

            if (m_ResetButton != null) m_ResetButton.onClick.AddListener(OnResetButtonClicked);
            if (m_LevelsButton != null) m_LevelsButton.onClick.AddListener(OnLevelsButtonClicked);
            if (m_ExitButton != null) m_ExitButton.onClick.AddListener(OnExitButtonClicked);
        }

        #endregion

        #region Public Interface

        public static void Toggle()
        {
            if (Instance == null)
            {
                Instance = FindAnyObjectByType<PauseMenuController>(FindObjectsInactive.Include);
            }

            if (Instance == null) return;

            if (Instance.m_CurrentState == PauseState.Gameplay)
            {
                Instance.OpenPauseMenu();
            }
            else if (Instance.m_CurrentState == PauseState.MainPause)
            {
                Instance.ResumeGameplay();
            }
        }

        /// <summary>
        /// Opens the Pause Menu, freezes gameplay, and plays the cinematic entrance.
        /// </summary>
        public void OpenPauseMenu()
        {
            if (m_CurrentState != PauseState.Gameplay) return;

            m_CurrentState = PauseState.Opening;

            // Freeze gameplay
            Time.timeScale = 0f;
            DeviceInputProvider.Instance?.SetEnabled(false);

            SetUIVisible(true);
            if (m_Confirmation != null && m_Confirmation.gameObject.activeSelf) m_Confirmation.gameObject.SetActive(false);
            if (m_LevelSelection != null && m_LevelSelection.gameObject.activeSelf) m_LevelSelection.gameObject.SetActive(false);

            if (m_Audio != null) m_Audio.PlayPauseOpen();

            if (m_Animator != null)
            {
                m_Animator.PlayEntrance(() =>
                {
                    m_CurrentState = PauseState.MainPause;
                    m_Navigation?.FocusDefaultButton();
                    OnPauseOpened?.Invoke();
                });
            }
            else
            {
                m_CurrentState = PauseState.MainPause;
                m_Navigation?.FocusDefaultButton();
                OnPauseOpened?.Invoke();
            }
        }

        /// <summary>
        /// Resumes gameplay with a clean exit animation.
        /// </summary>
        public void ResumeGameplay()
        {
            if (m_CurrentState != PauseState.MainPause) return;

            m_CurrentState = PauseState.Closing;
            m_Navigation?.ClearSelection();

            if (m_Audio != null) m_Audio.PlayPauseClose();

            if (m_Animator != null)
            {
                m_Animator.PlayExit(() =>
                {
                    FinishResume();
                });
            }
            else
            {
                FinishResume();
            }
        }

        private void FinishResume()
        {
            SetUIVisible(false);
            Time.timeScale = 1f;
            DeviceInputProvider.Instance?.SetEnabled(true);
            m_CurrentState = PauseState.Gameplay;
            OnPauseClosed?.Invoke();
        }

        #endregion

        #region Button Click Handlers

        private void OnResetButtonClicked()
        {
            if (m_CurrentState != PauseState.MainPause) return;

            TriggerButtonPunch(m_ResetButton, () =>
            {
                m_Navigation?.StoreCurrentSelection();
                m_CurrentState = PauseState.Confirmation;

                ConfirmationRequest request = new ConfirmationRequest
                {
                    TitleSprite = m_ResetTitleSprite,
                    OnConfirm = ExecuteResetConfirmed,
                    OnCancel = CancelConfirmation
                };

                m_Confirmation.Show(request);
            });
        }

        private void OnLevelsButtonClicked()
        {
            if (m_CurrentState != PauseState.MainPause) return;

            TriggerButtonPunch(m_LevelsButton, () =>
            {
                m_Navigation?.StoreCurrentSelection();
                m_CurrentState = PauseState.Confirmation;

                ConfirmationRequest request = new ConfirmationRequest
                {
                    TitleSprite = m_LevelsTitleSprite,
                    OnConfirm = OpenEmbeddedLevelSelection,
                    OnCancel = CancelConfirmation
                };

                m_Confirmation.Show(request);
            });
        }

        private void OnExitButtonClicked()
        {
            if (m_CurrentState != PauseState.MainPause) return;

            TriggerButtonPunch(m_ExitButton, () =>
            {
                m_Navigation?.StoreCurrentSelection();
                m_CurrentState = PauseState.Confirmation;

                ConfirmationRequest request = new ConfirmationRequest
                {
                    TitleSprite = m_ExitTitleSprite,
                    OnConfirm = ExecuteExitConfirmed,
                    OnCancel = CancelConfirmation
                };

                m_Confirmation.Show(request);
            });
        }

        #endregion

        #region Action Executions

        private void ExecuteResetConfirmed()
        {
            Debug.Log("[PauseMenuController] Reset confirmed. Restarting level...");

            Time.timeScale = 1f;
            DeviceInputProvider.Instance?.SetEnabled(true);
            SetUIVisible(false);
            m_CurrentState = PauseState.Gameplay;

            OnResetRequested?.Invoke();
            if (m_OnResetConfirmed != null && m_OnResetConfirmed.GetPersistentEventCount() > 0)
            {
                m_OnResetConfirmed.Invoke();
            }
            else if (GameManager.Instance != null)
            {
                GameManager.Instance.RestartLevel();
            }
            else
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }
        }

        private void OpenEmbeddedLevelSelection()
        {
            Debug.Log("[PauseMenuController] Levels confirmed. Opening embedded Level Selection...");

            if (m_LevelSelection == null)
            {
                // Fallback if level selection panel was omitted: load HomeScreen with autoOpenLevelSelection flag
                Time.timeScale = 1f;
                DeviceInputProvider.Instance?.SetEnabled(true);
                PauseMenuScreen.AutoOpenLevelSelection = true;
                SceneManager.LoadScene(m_HomeScreenName);
                return;
            }

            m_CurrentState = PauseState.LevelSelection;

            // Hide main pause panel while level selection is open
            if (m_PausePanel != null) m_PausePanel.gameObject.SetActive(false);

            if (m_Audio != null) m_Audio.PlayLevelSelectionOpen();

            m_LevelSelection.Open(onBackToMainPause: BackFromLevelSelection);
        }

        public void BackFromLevelSelection()
        {
            if (m_CurrentState != PauseState.LevelSelection) return;

            if (m_LevelSelection != null && m_LevelSelection.IsOpen)
            {
                m_LevelSelection.HandleBack();
                return;
            }

            m_CurrentState = PauseState.MainPause;

            if (m_PausePanel != null) m_PausePanel.gameObject.SetActive(true);

            m_Navigation?.RestoreLastSelection();
        }

        private void ExecuteExitConfirmed()
        {
            Debug.Log("[PauseMenuController] Exit confirmed. Loading Home Screen...");

            Time.timeScale = 1f;
            DeviceInputProvider.Instance?.SetEnabled(true);
            SetUIVisible(false);
            m_CurrentState = PauseState.Gameplay;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.GoToMainMenu();
            }
            else
            {
                SceneManager.LoadScene(m_HomeScreenName);
            }
        }

        public void CancelConfirmation()
        {
            if (m_CurrentState != PauseState.Confirmation) return;

            if (m_Confirmation != null && m_Confirmation.IsOpen)
            {
                m_Confirmation.HandleCancel();
                return;
            }

            m_CurrentState = PauseState.MainPause;
            m_Navigation?.RestoreLastSelection();
        }

        #endregion

        #region Helpers

        private void TriggerButtonPunch(Button button, Action callback)
        {
            if (button != null)
            {
                MainMenuButtonEnergyAnimator energy = button.GetComponent<MainMenuButtonEnergyAnimator>();
                if (energy != null)
                {
                    energy.PlayConfirmPunch(callback);
                    return;
                }

                UIAnimatedButton anim = button.GetComponent<UIAnimatedButton>();
                if (anim != null)
                {
                    anim.PlayConfirmPunch(callback);
                    return;
                }
            }

            callback?.Invoke();
        }

        private void SetUIVisible(bool visible)
        {
            Transform screen = transform.Find("Pause screen");
            if (screen != null) screen.gameObject.SetActive(visible);

            if (m_DarkOverlay != null)
            {
                m_DarkOverlay.alpha = visible ? 1f : 0f;
                m_DarkOverlay.interactable = visible;
                m_DarkOverlay.blocksRaycasts = visible;
            }

            if (m_PausePanel != null)
            {
                m_PausePanel.gameObject.SetActive(visible);
            }
        }

        #endregion
    }
}
