using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using LevelSelection;
using MainGame.UI.Unified;

namespace MainGame.UI.PauseMenu
{
    /// <summary>
    /// Manages the embedded Level Selection panel inside the Pause Menu prefab.
    /// Handles procedural map synchronization via OnArcReady, focusing current level,
    /// safe unscaled level loading (restoring Time.timeScale = 1), and returning to Main Pause.
    /// </summary>
    [DisallowMultipleComponent]
    public class PauseLevelSelectionController : MonoBehaviour
    {
        [Header("Containers")]
        [SerializeField] private CanvasGroup m_CanvasGroup;

        [Header("Sub-Systems")]
        [SerializeField] private LevelSelectionManager m_LevelSelectionManager;
        [SerializeField] private LevelSelectionScreenAnimator m_Animator;

        [Header("Back Button")]
        [SerializeField] private Button m_BackButton;

        private Action m_OnBackCallback;
        private bool m_IsOpen;
        private bool m_IsLoadingLevel;

        public bool IsOpen => m_IsOpen;

        private void Awake()
        {
            ResolveReferences();
        }

        public void ResolveReferences()
        {
            if (m_CanvasGroup == null) m_CanvasGroup = GetComponent<CanvasGroup>();
            if (m_LevelSelectionManager == null)
            {
                m_LevelSelectionManager = GetComponent<LevelSelectionManager>();
                if (m_LevelSelectionManager == null) m_LevelSelectionManager = GetComponentInChildren<LevelSelectionManager>(true);
            }

            if (m_Animator == null)
            {
                m_Animator = GetComponent<LevelSelectionScreenAnimator>();
                if (m_Animator == null) m_Animator = GetComponentInChildren<LevelSelectionScreenAnimator>(true);
            }

            if (m_BackButton == null)
            {
                Transform b = transform.Find("Holder/B Back") ?? transform.Find("B Back") ?? transform.Find("Back B");
                if (b != null) m_BackButton = b.GetComponent<Button>();
            }
        }

        private void OnEnable()
        {
            if (m_BackButton != null) m_BackButton.onClick.AddListener(HandleBackClicked);
        }

        private void OnDisable()
        {
            if (m_BackButton != null) m_BackButton.onClick.RemoveListener(HandleBackClicked);
        }

        /// <summary>
        /// Opens the embedded Level Selection panel, synchronizing entrance with the arc generation event.
        /// </summary>
        public void Open(Action onBackToMainPause, Action onOpened = null)
        {
            m_OnBackCallback = onBackToMainPause;
            m_IsOpen = true;
            m_IsLoadingLevel = false;

            gameObject.SetActive(true);

            if (m_CanvasGroup != null)
            {
                m_CanvasGroup.alpha = 1f;
                m_CanvasGroup.interactable = true;
                m_CanvasGroup.blocksRaycasts = true;
            }

            if (m_LevelSelectionManager != null && m_Animator != null)
            {
                m_LevelSelectionManager.ResetToCurrentUnlockedLevel();
                m_Animator.PrepareEntranceState();

                Action<List<LevelNodeUI>, List<UIPathSegment>, int> onReady = null;
                onReady = (nodes, segments, highestUnlocked) =>
                {
                    m_LevelSelectionManager.OnArcReady -= onReady;

                    Sprite arcSprite = m_LevelSelectionManager.GetCurrentArcSprite();
                    m_Animator.PlayMapEntrance(nodes, segments, highestUnlocked, () =>
                    {
                        m_LevelSelectionManager.FocusCurrentLevelNode();
                        HookNodeClicks(nodes);
                        onOpened?.Invoke();
                    }, arcSprite, focusTargetNodeIndex: LevelSelectionManager.FocusCurrentLevel);
                };

                m_LevelSelectionManager.OnArcReady += onReady;
                m_LevelSelectionManager.RequestArcData(-1, forceRegenerate: false);
            }
            else
            {
                if (m_LevelSelectionManager != null)
                {
                    m_LevelSelectionManager.InitializeAndFocusCurrentLevel();
                    HookNodeClicks(m_LevelSelectionManager.LevelNodes);
                }
                onOpened?.Invoke();
            }
        }

        private void Update()
        {
            if (!m_IsOpen || m_IsLoadingLevel) return;

            // Check for cancel / back input
            bool backPressed = false;
            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                backPressed = UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame ||
                              UnityEngine.InputSystem.Keyboard.current.backspaceKey.wasPressedThisFrame;
            }
            if (UnityEngine.InputSystem.Gamepad.current != null)
            {
                backPressed |= UnityEngine.InputSystem.Gamepad.current.bButton.wasPressedThisFrame;
            }

#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace)) backPressed = true;
#endif

            if (backPressed)
            {
                HandleBackClicked();
                return;
            }

            // Selection recovery: if selection is lost, refocus current node on any navigation key
            if (UnityEngine.EventSystems.EventSystem.current != null &&
                (UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject == null ||
                 !UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject.activeInHierarchy))
            {
                bool navPressed = false;
                if (UnityEngine.InputSystem.Keyboard.current != null)
                {
                    navPressed = UnityEngine.InputSystem.Keyboard.current.aKey.wasPressedThisFrame ||
                                 UnityEngine.InputSystem.Keyboard.current.dKey.wasPressedThisFrame ||
                                 UnityEngine.InputSystem.Keyboard.current.leftArrowKey.wasPressedThisFrame ||
                                 UnityEngine.InputSystem.Keyboard.current.rightArrowKey.wasPressedThisFrame ||
                                 UnityEngine.InputSystem.Keyboard.current.wKey.wasPressedThisFrame ||
                                 UnityEngine.InputSystem.Keyboard.current.sKey.wasPressedThisFrame ||
                                 UnityEngine.InputSystem.Keyboard.current.upArrowKey.wasPressedThisFrame ||
                                 UnityEngine.InputSystem.Keyboard.current.downArrowKey.wasPressedThisFrame;
                }
                if (UnityEngine.InputSystem.Gamepad.current != null)
                {
                    navPressed |= UnityEngine.InputSystem.Gamepad.current.dpad.left.wasPressedThisFrame ||
                                  UnityEngine.InputSystem.Gamepad.current.dpad.right.wasPressedThisFrame ||
                                  UnityEngine.InputSystem.Gamepad.current.leftStick.left.wasPressedThisFrame ||
                                  UnityEngine.InputSystem.Gamepad.current.leftStick.right.wasPressedThisFrame;
                }

#if ENABLE_LEGACY_INPUT_MANAGER
                if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow) ||
                    Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.D)) navPressed = true;
#endif

                if (navPressed && m_LevelSelectionManager != null)
                {
                    m_LevelSelectionManager.FocusCurrentLevelNode();
                }
            }
        }

        /// <summary>
        /// Intercepts clicks on level nodes so we can guarantee Time.timeScale = 1 before loading scene.
        /// </summary>
        private void HookNodeClicks(List<LevelNodeUI> nodes)
        {
            if (nodes == null) return;
            for (int i = 0; i < nodes.Count; i++)
            {
                LevelNodeUI node = nodes[i];
                if (node == null) continue;

                Button btn = node.GetComponent<Button>() ?? node.GetComponentInChildren<Button>(true);
                if (btn != null)
                {
                    btn.onClick.AddListener(() =>
                    {
                        m_IsLoadingLevel = true;
                        Time.timeScale = 1f;
                        DeviceInputProvider.Instance?.SetEnabled(true);
                    });
                }
            }
        }

        public void Close(Action onClosed = null)
        {
            m_IsOpen = false;

            if (UnityEngine.EventSystems.EventSystem.current != null)
            {
                GameObject cur = UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject;
                if (cur != null && cur.transform.IsChildOf(transform))
                {
                    UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
                }
            }

            if (m_LevelSelectionManager != null)
            {
                m_LevelSelectionManager.CancelActiveTransition();
            }

            if (m_CanvasGroup != null)
            {
                m_CanvasGroup.interactable = false;
                m_CanvasGroup.blocksRaycasts = false;
            }

            if (m_Animator != null)
            {
                var nodes = m_LevelSelectionManager != null ? m_LevelSelectionManager.LevelNodes : null;
                var segments = m_LevelSelectionManager != null ? m_LevelSelectionManager.PathSegments : null;

                m_Animator.PlayMapExit(nodes, segments, () =>
                {
                    if (m_CanvasGroup != null)
                    {
                        m_CanvasGroup.alpha = 0f;
                        m_CanvasGroup.interactable = false;
                        m_CanvasGroup.blocksRaycasts = false;
                    }
                    gameObject.SetActive(false);
                    onClosed?.Invoke();
                });
            }
            else
            {
                if (m_CanvasGroup != null)
                {
                    m_CanvasGroup.alpha = 0f;
                    m_CanvasGroup.interactable = false;
                    m_CanvasGroup.blocksRaycasts = false;
                }
                gameObject.SetActive(false);
                onClosed?.Invoke();
            }
        }

        public void HandleBack()
        {
            HandleBackClicked();
        }

        private void HandleBackClicked()
        {
            if (!m_IsOpen || m_IsLoadingLevel) return;

            UIAnimatedButton animBtn = m_BackButton != null ? m_BackButton.GetComponent<UIAnimatedButton>() : null;
            if (animBtn != null)
            {
                animBtn.PlayConfirmPunch(() =>
                {
                    ExecuteBack();
                });
            }
            else
            {
                ExecuteBack();
            }
        }

        private void ExecuteBack()
        {
            Action backCallback = m_OnBackCallback;
            m_OnBackCallback = null;

            Close(() =>
            {
                backCallback?.Invoke();
            });
        }
    }
}
