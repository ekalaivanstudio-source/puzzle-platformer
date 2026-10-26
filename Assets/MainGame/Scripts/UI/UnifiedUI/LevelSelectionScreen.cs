using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using LevelSelection;

namespace MainGame.UI.Unified
{
    /// <summary>
    /// Unified Level Selection Screen controller for the paginated grid design.
    /// Synchronizes screen transitions, back button handling, and EventSystem focus with GridLevelGenerator.
    /// </summary>
    [DisallowMultipleComponent]
    public class LevelSelectionScreen : UIScreen
    {
        #region Inspector Fields

        [Header("Controls")]
        [SerializeField] private Button m_BackButton;

        [Header("References")]
        [Tooltip("The generator managing paginated level buttons in the grid.")]
        [SerializeField] private GridLevelGenerator m_GridLevelGenerator;

        [Header("Animator Reference")]
        [SerializeField] private LevelSelectionScreenAnimator m_Animator;

        [Header("Legacy Fallback (Optional)")]
        [Tooltip("Legacy coordinator reference for backwards compatibility.")]
        [SerializeField] private LevelSelectionManager m_LevelSelectionManager;

        #endregion

        #region Public Properties

        public override GameObject DefaultSelectedObject
        {
            get
            {
                if (m_GridLevelGenerator != null)
                {
                    GridLevelButton targetBtn = m_GridLevelGenerator.GetFocusedOrFirstButton();
                    if (targetBtn != null) return targetBtn.gameObject;
                }

                if (m_LevelSelectionManager != null)
                {
                    GameObject legacyTarget = m_LevelSelectionManager.GetCurrentUnlockedLevelNodeObject();
                    if (legacyTarget != null) return legacyTarget;
                }

                return m_BackButton != null ? m_BackButton.gameObject : base.DefaultSelectedObject;
            }
        }

        #endregion

        #region Unity Lifecycle

        protected override void Awake()
        {
            base.Awake();
            AutoWireComponents();
        }

        private void OnEnable()
        {
            if (m_BackButton != null)
            {
                m_BackButton.onClick.RemoveListener(HandleBackClicked);
                m_BackButton.onClick.AddListener(HandleBackClicked);
            }
        }

        private void OnDisable()
        {
            if (m_BackButton != null)
            {
                m_BackButton.onClick.RemoveListener(HandleBackClicked);
            }
        }

        private void Update()
        {
            if (!gameObject.activeInHierarchy) return;

            bool backPressed = false;

            // 1. New Input System Gamepad B (buttonEast)
            if (Gamepad.current != null && (Gamepad.current.buttonEast.wasPressedThisFrame || Gamepad.current.bButton.wasPressedThisFrame))
            {
                backPressed = true;
            }

            // 2. New Input System Keyboard Escape
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                backPressed = true;
            }

            // 3. Legacy Input Manager fallback
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton1))
            {
                backPressed = true;
            }

            if (backPressed)
            {
                HandleBackClicked();
            }
        }

        #endregion

        #region Auto-Wiring

        private void AutoWireComponents()
        {
            if (m_GridLevelGenerator == null)
            {
                m_GridLevelGenerator = GetComponent<GridLevelGenerator>() ?? GetComponentInChildren<GridLevelGenerator>(true);
            }

            if (m_Animator == null)
            {
                m_Animator = GetComponent<LevelSelectionScreenAnimator>() ?? GetComponentInChildren<LevelSelectionScreenAnimator>(true);
            }

            if (m_BackButton == null)
            {
                Transform backTrans = transform.Find("Holder/B Back") ?? transform.Find("B Back");
                if (backTrans != null)
                {
                    m_BackButton = backTrans.GetComponent<Button>();
                }
                else
                {
                    var buttons = GetComponentsInChildren<Button>(true);
                    foreach (var b in buttons)
                    {
                        if (b.name.Equals("B Back", StringComparison.OrdinalIgnoreCase) ||
                            b.name.Equals("Back", StringComparison.OrdinalIgnoreCase))
                        {
                            m_BackButton = b;
                            break;
                        }
                    }
                }
            }
        }

        #endregion

        #region Screen Transitions

        public override void PlayEnterTransition(Action onComplete)
        {
            base.Open();

            // Clear any remembered selection so we always focus the current unlocked level on panel open
            if (UINavigationManager.Instance != null)
            {
                UINavigationManager.Instance.ClearSelectionMemory(this);
            }

            // Refresh or navigate generator to highest unlocked level
            if (m_GridLevelGenerator != null)
            {
                int highest = ModernLevelSelection.SaveManager.GetHighestUnlocked();
                m_GridLevelGenerator.JumpToLevel(highest, selectInEventSystem: false);
            }

            if (m_Animator != null)
            {
                m_Animator.PlayEntrance(() =>
                {
                    FocusCurrentSelection();
                    onComplete?.Invoke();
                });
            }
            else
            {
                FocusCurrentSelection();
                onComplete?.Invoke();
            }
        }

        public override void PlayExitTransition(Action onComplete)
        {
            if (UINavigationManager.Instance != null)
            {
                UINavigationManager.Instance.ClearSelectionMemory(this);
            }

            if (m_Animator != null)
            {
                m_Animator.PlayExit(() =>
                {
                    Close();
                    onComplete?.Invoke();
                });
            }
            else
            {
                Close();
                onComplete?.Invoke();
            }
        }

        private void FocusCurrentSelection()
        {
            GameObject target = DefaultSelectedObject;
            if (target != null && UnityEngine.EventSystems.EventSystem.current != null)
            {
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(target);
            }
        }

        #endregion

        #region Navigation & Back Button

        private void HandleBackClicked()
        {
            if (UINavigationManager.Instance != null && UINavigationManager.Instance.IsTransitioning)
            {
                return;
            }

            if (m_BackButton != null)
            {
                UIAnimatedButton animBtn = m_BackButton.GetComponent<UIAnimatedButton>();
                if (animBtn != null)
                {
                    animBtn.PlayConfirmPunch(() =>
                    {
                        if (UINavigationManager.Instance != null)
                        {
                            UINavigationManager.Instance.PopScreen();
                        }
                    });
                    return;
                }
            }

            AudioManager.Instance?.PlayButton();
            if (UINavigationManager.Instance != null)
            {
                UINavigationManager.Instance.PopScreen();
            }
        }

        #endregion
    }
}
