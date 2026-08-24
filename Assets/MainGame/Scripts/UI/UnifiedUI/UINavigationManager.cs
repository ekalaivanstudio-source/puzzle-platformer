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
        private InputAction m_NavigateAction;

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
                    m_NavigateAction = uiMap.FindAction("Navigate", throwIfNotFound: false);
                }
            }
            Debug.Log("[UINavigationManager] Awake completed. Cancel action found: " + (m_CancelAction != null) + ", Navigate action found: " + (m_NavigateAction != null));
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

        private GameObject m_LastLoggedSelection = null;
        private void Update()
        {
            if (EventSystem.current != null)
            {
                GameObject currentSel = EventSystem.current.currentSelectedGameObject;
                if (currentSel != m_LastLoggedSelection)
                {
                    m_LastLoggedSelection = currentSel;
                    Debug.Log($"[UINavigationManager Focus Tracker] Selected GameObject changed to: {(currentSel != null ? currentSel.name : "NULL")}");
                }

                if (currentSel == null)
                {
                    bool hasNavigationInput = false;
                    if (m_NavigateAction != null && m_NavigateAction.triggered)
                    {
                        hasNavigationInput = true;
                    }
                    else
                    {
                        hasNavigationInput = Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow) ||
                                               Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow) ||
                                               Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.S) ||
                                               Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.D) ||
                                               Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.1f || Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.1f;
                    }

                    if (hasNavigationInput)
                    {
                        if (m_ScreenHistory.Count > 0)
                        {
                            UIScreen currentScreen = m_ScreenHistory.Peek();
                            if (currentScreen != null)
                            {
                                RestoreSelectedElement(currentScreen.DefaultSelectedObject);
                            }
                        }
                    }
                }
            }
        }

        // Tracks the GameObject that had selection focus on the previous screen
        private readonly Stack<GameObject> m_SelectionHistory = new Stack<GameObject>();

        /// <summary>
        /// Pushes a new screen onto the history stack and opens it.
        /// </summary>
        /// <param name="newScreen">The screen to show.</param>
        public void PushScreen(UIScreen newScreen)
        {
            if (newScreen == null) return;

            // Failsafe: If the screen history contains destroyed objects (due to scene changes), clear it
            while (m_ScreenHistory.Count > 0 && m_ScreenHistory.Peek() == null)
            {
                m_ScreenHistory.Pop();
                if (m_SelectionHistory.Count > 0) m_SelectionHistory.Pop();
            }

            Debug.Log($"[UINavigationManager] Pushing screen: {newScreen.gameObject.name}");

            // Remember what was selected on the current screen before pushing the new one
            if (m_ScreenHistory.Count > 0)
            {
                GameObject lastSelected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
                m_SelectionHistory.Push(lastSelected);
                
                UIScreen currentTop = m_ScreenHistory.Peek();
                if (currentTop != null)
                {
                    currentTop.Close();
                }
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
            // Clean up any destroyed screen references from the stack
            while (m_ScreenHistory.Count > 0 && m_ScreenHistory.Peek() == null)
            {
                m_ScreenHistory.Pop();
                if (m_SelectionHistory.Count > 0) m_SelectionHistory.Pop();
            }

            if (m_ScreenHistory.Count <= 1)
            {
                Debug.Log("[UINavigationManager] Cannot pop the base screen.");
                return;
            }

            UIScreen poppedScreen = m_ScreenHistory.Pop();
            if (poppedScreen != null)
            {
                poppedScreen.Close();
            }
            Debug.Log($"[UINavigationManager] Popped screen: {(poppedScreen != null ? poppedScreen.gameObject.name : "NullScreen")}");

            // Clean up subsequent references if destroyed
            while (m_ScreenHistory.Count > 0 && m_ScreenHistory.Peek() == null)
            {
                m_ScreenHistory.Pop();
                if (m_SelectionHistory.Count > 0) m_SelectionHistory.Pop();
            }

            if (m_ScreenHistory.Count > 0)
            {
                UIScreen previousScreen = m_ScreenHistory.Peek();
                if (previousScreen != null)
                {
                    previousScreen.Open();
                }

                // Retrieve and restore the specific button that opened this submenu
                GameObject previousSelection = null;
                if (m_SelectionHistory.Count > 0)
                {
                    previousSelection = m_SelectionHistory.Pop();
                }

                // Fallback to the screen's default selected object if nothing was stored
                if (previousSelection == null || !previousSelection.activeInHierarchy)
                {
                    previousSelection = previousScreen != null ? previousScreen.DefaultSelectedObject : null;
                }

                RestoreSelectedElement(previousSelection);
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
