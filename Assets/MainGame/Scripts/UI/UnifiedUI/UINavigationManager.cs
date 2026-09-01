using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

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

        [Header("Debugging")]
        [Tooltip("Log every screen push/pop and focus change. Leave off outside of UI debugging; the focus tracker is noisy.")]
        [SerializeField] private bool m_VerboseLogging;

        // Screen stack and the selection that was active on each screen below the top one.
        // Both stacks are pushed and popped together, so m_SelectionHistory always holds
        // exactly (m_ScreenHistory.Count - 1) entries.
        private readonly Stack<UIScreen> m_ScreenHistory = new Stack<UIScreen>();
        private readonly Stack<GameObject> m_SelectionHistory = new Stack<GameObject>();

        private InputAction m_CancelAction;
        private GameObject m_LastValidSelection;

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
                else
                {
                    Debug.LogWarning("[UINavigationManager] No 'UI' action map found on the assigned Input Action Asset.");
                }
            }
        }

        private void OnEnable()
        {
            if (m_CancelAction != null)
            {
                m_CancelAction.performed += OnCancelPerformed;
            }
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            if (m_CancelAction != null)
            {
                m_CancelAction.performed -= OnCancelPerformed;
            }
            SceneManager.sceneLoaded -= OnSceneLoaded;
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
            if (m_InitialScreen != null)
            {
                PushScreen(m_InitialScreen);
            }
        }

        /// <summary>
        /// The screen stack refers to objects owned by the previous scene, so drop it wholesale on load.
        /// Keeping stale entries desynchronised the screen and selection stacks.
        /// </summary>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Additive) return;

            m_ScreenHistory.Clear();
            m_SelectionHistory.Clear();
            m_LastValidSelection = null;
        }

        private void Update()
        {
            if (EventSystem.current == null) return;

            GameObject currentSel = EventSystem.current.currentSelectedGameObject;
            if (currentSel != null)
            {
                if (currentSel != m_LastValidSelection)
                {
                    m_LastValidSelection = currentSel;
                    Log($"Selection changed to: {currentSel.name}");
                }
                return;
            }

            // Selection was lost (a screen closed, a button was disabled, a click landed on empty space).
            // Restore the last thing that was focused so keyboard/controller input keeps working.
            // Only ever re-select an object that is still alive and active; never poke the current
            // screen's DefaultSelectedObject here, since screens may compute it lazily and expensively.
            if (m_LastValidSelection != null && m_LastValidSelection.activeInHierarchy)
            {
                EventSystem.current.SetSelectedGameObject(m_LastValidSelection);
                return;
            }

            m_LastValidSelection = null;

            UIScreen currentScreen = CurrentScreen;
            if (currentScreen != null)
            {
                GameObject fallback = currentScreen.DefaultSelectedObject;
                if (fallback != null && fallback.activeInHierarchy)
                {
                    EventSystem.current.SetSelectedGameObject(fallback);
                    m_LastValidSelection = fallback;
                }
            }
        }

        /// <summary>
        /// Gets the screen currently on top of the stack, or null when the stack is empty.
        /// </summary>
        public UIScreen CurrentScreen
        {
            get
            {
                PruneDestroyedScreens();
                return m_ScreenHistory.Count > 0 ? m_ScreenHistory.Peek() : null;
            }
        }

        /// <summary>
        /// Pushes a new screen onto the history stack and opens it.
        /// </summary>
        /// <param name="newScreen">The screen to show.</param>
        public void PushScreen(UIScreen newScreen)
        {
            if (newScreen == null) return;

            PruneDestroyedScreens();

            Log($"Pushing screen: {newScreen.gameObject.name}");

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
            PruneDestroyedScreens();

            if (m_ScreenHistory.Count <= 1)
            {
                Log("Cannot pop the base screen.");
                return;
            }

            UIScreen poppedScreen = m_ScreenHistory.Pop();
            if (poppedScreen != null)
            {
                poppedScreen.Close();
            }
            Log($"Popped screen: {(poppedScreen != null ? poppedScreen.gameObject.name : "NullScreen")}");

            // Retrieve the specific button that opened this submenu, keeping both stacks in step
            GameObject previousSelection = m_SelectionHistory.Count > 0 ? m_SelectionHistory.Pop() : null;

            PruneDestroyedScreens();
            if (m_ScreenHistory.Count == 0) return;

            UIScreen previousScreen = m_ScreenHistory.Peek();
            if (previousScreen != null)
            {
                previousScreen.Open();
            }

            // Fallback to the screen's default selected object if nothing usable was stored
            if (previousSelection == null || !previousSelection.activeInHierarchy)
            {
                previousSelection = previousScreen != null ? previousScreen.DefaultSelectedObject : null;
            }

            RestoreSelectedElement(previousSelection);
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

            Log($"Restoring selection focus to: {(defaultSelectable != null ? defaultSelectable.name : "None")}");
            EventSystem.current.SetSelectedGameObject(null);
            if (defaultSelectable != null)
            {
                EventSystem.current.SetSelectedGameObject(defaultSelectable);
                m_LastValidSelection = defaultSelectable;
            }
        }

        /// <summary>
        /// Drops screens destroyed by a scene change from the top of the stack,
        /// keeping the paired selection history aligned.
        /// </summary>
        private void PruneDestroyedScreens()
        {
            while (m_ScreenHistory.Count > 0 && m_ScreenHistory.Peek() == null)
            {
                m_ScreenHistory.Pop();
                if (m_ScreenHistory.Count > 0 && m_SelectionHistory.Count > 0)
                {
                    m_SelectionHistory.Pop();
                }
            }
        }

        private void OnCancelPerformed(InputAction.CallbackContext context)
        {
            Log("Global Cancel input detected.");
            PopScreen();
        }

        private void Log(string message)
        {
            if (m_VerboseLogging)
            {
                Debug.Log($"[UINavigationManager] {message}");
            }
        }
    }
}
