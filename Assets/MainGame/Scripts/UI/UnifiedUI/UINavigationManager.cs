using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace MainGame.UI.Unified
{
    /// <summary>
    /// Manages screen transitions, history stack, and default active UI focus.
    /// Does not handle custom input devices or prompts; delegates completely.
    /// </summary>
    public class UINavigationManager : MonoBehaviour
    {
        public static UINavigationManager Instance { get; private set; }

        [Header("Starting Settings")]
        [Tooltip("The screen that will open immediately on startup.")]
        [SerializeField] private UIScreen m_InitialScreen;

        [Header("Global Input Settings")]
        [Tooltip("Optional reference to Input Action Asset to listen to global UI events (e.g. Cancel).")]
        [SerializeField] private InputActionAsset m_UIInputActionAsset;

        private readonly Stack<UIScreen> m_ScreenHistory = new Stack<UIScreen>();
        private InputAction m_CancelAction;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (m_UIInputActionAsset != null)
            {
                // Enable the UI map globally so the EventSystem UI module receives navigation events
                InputActionMap uiMap = m_UIInputActionAsset.FindActionMap("UI", throwIfNotFound: false);
                if (uiMap != null)
                {
                    uiMap.Enable();
                    m_CancelAction = uiMap.FindAction("Cancel", throwIfNotFound: false);
                }
            }
            Debug.Log("[UINavigationManager] Awake completed. Cancel action found: " + (m_CancelAction != null));
        }

        private void OnEnable()
        {
            if (m_CancelAction != null)
            {
                m_CancelAction.performed += OnCancelPerformed;
            }
        }

        private void OnDisable()
        {
            if (m_CancelAction != null)
            {
                m_CancelAction.performed -= OnCancelPerformed;
            }
        }

        private void Start()
        {
            if (m_InitialScreen != null)
            {
                PushScreen(m_InitialScreen);
            }
        }

        /// <summary>
        /// Pushes a new screen onto the history stack and opens it.
        /// </summary>
        /// <param name="newScreen">The screen to show.</param>
        public void PushScreen(UIScreen newScreen)
        {
            if (newScreen == null) return;

            Debug.Log($"[UINavigationManager] Pushing screen: {newScreen.gameObject.name}");

            // Deactivate the current screen in stack if any
            if (m_ScreenHistory.Count > 0)
            {
                m_ScreenHistory.Peek().Close();
            }

            m_ScreenHistory.Push(newScreen);
            newScreen.Open();
            RestoreSelectedElement(newScreen.DefaultSelectedObject);
        }

        /// <summary>
        /// Pops the current screen and returns to the previous screen in the stack.
        /// </summary>
        public void PopScreen()
        {
            if (m_ScreenHistory.Count <= 1)
            {
                Debug.Log("[UINavigationManager] Cannot pop the base screen.");
                return;
            }

            UIScreen poppedScreen = m_ScreenHistory.Pop();
            poppedScreen.Close();
            Debug.Log($"[UINavigationManager] Popped screen: {poppedScreen.gameObject.name}");

            if (m_ScreenHistory.Count > 0)
            {
                UIScreen previousScreen = m_ScreenHistory.Peek();
                previousScreen.Open();
                RestoreSelectedElement(previousScreen.DefaultSelectedObject);
            }
        }

        /// <summary>
        /// Restores focus to the specified selectable UI element.
        /// </summary>
        public void RestoreSelectedElement(GameObject defaultSelectable)
        {
            if (EventSystem.current == null)
            {
                Debug.LogWarning("[UINavigationManager] EventSystem.current is null! Focus cannot be restored.");
                return;
            }

            Debug.Log($"[UINavigationManager] Restoring selection focus to: {(defaultSelectable != null ? defaultSelectable.name : "None")}");
            EventSystem.current.SetSelectedGameObject(null);
            if (defaultSelectable != null)
            {
                EventSystem.current.SetSelectedGameObject(defaultSelectable);
            }
        }

        private void OnCancelPerformed(InputAction.CallbackContext context)
        {
            Debug.Log("[UINavigationManager] Global Cancel input detected.");
            PopScreen();
        }
    }
}
