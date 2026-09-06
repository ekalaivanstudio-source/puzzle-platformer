using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace MainGame.UI.Unified
{
    /// <summary>
    /// Manages screen transitions, history stack, default active UI focus,
    /// cinematic transition overlap, input locking, and selection memory.
    /// </summary>
    [DisallowMultipleComponent]
    public class UINavigationManager : MonoBehaviour
    {
        public static UINavigationManager Instance { get; private set; }

        [Header("Starting Settings")]
        [Tooltip("The screen that will open immediately on startup.")]
        [SerializeField] private UIScreen m_InitialScreen;

        [Header("Global Input Settings")]
        [Tooltip("Optional reference to Input Action Asset to listen to global UI events (e.g. Cancel).")]
        [SerializeField] private InputActionAsset m_UIInputActionAsset;

        [Header("Transition Timing")]
        [Tooltip("Transition overlap delay (seconds) between old screen exiting and new screen entering.")]
        [SerializeField] private float m_TransitionOverlapDelay = 0.08f;

        [Header("Audio Feedback Hooks (Optional)")]
        [SerializeField] private AudioClip m_NavigateSound;
        [SerializeField] private AudioClip m_ConfirmSound;
        [SerializeField] private AudioClip m_BackSound;
        [SerializeField] private AudioClip m_PanelOpenSound;
        [SerializeField] private AudioClip m_PanelCloseSound;
        [SerializeField] private AudioClip m_PopupOpenSound;

        [Header("Debugging")]
        [Tooltip("Log every screen push/pop and focus change.")]
        [SerializeField] private bool m_VerboseLogging;

        private readonly Stack<UIScreen> m_ScreenHistory = new Stack<UIScreen>();
        private readonly Stack<GameObject> m_SelectionHistory = new Stack<GameObject>();
        private readonly Dictionary<UIScreen, GameObject> m_SelectionMemory = new Dictionary<UIScreen, GameObject>();

        private InputAction m_CancelAction;
        private GameObject m_LastValidSelection;
        private bool m_IsTransitioning;
        private Coroutine m_TransitionCoroutine;

        public bool IsTransitioning => m_IsTransitioning;

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

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Additive) return;

            m_ScreenHistory.Clear();
            m_SelectionHistory.Clear();
            m_SelectionMemory.Clear();
            m_LastValidSelection = null;
            m_IsTransitioning = false;

            if (m_TransitionCoroutine != null)
            {
                StopCoroutine(m_TransitionCoroutine);
                m_TransitionCoroutine = null;
            }
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

                    // Selection sound hook
                    if (m_NavigateSound != null && AudioManager.Instance != null && !m_IsTransitioning)
                    {
                        AudioManager.Instance.PlayUi(m_NavigateSound);
                    }

                    // Remember selection for active screen
                    UIScreen activeScreen = CurrentScreen;
                    if (activeScreen != null)
                    {
                        m_SelectionMemory[activeScreen] = currentSel;
                    }
                }
                return;
            }

            // If transitioning, EventSystem selection is intentionally cleared
            if (m_IsTransitioning) return;

            // Selection lost fallback
            if (m_LastValidSelection != null && m_LastValidSelection.activeInHierarchy)
            {
                EventSystem.current.SetSelectedGameObject(m_LastValidSelection);
                return;
            }

            m_LastValidSelection = null;

            UIScreen currentScreen = CurrentScreen;
            if (currentScreen != null)
            {
                GameObject fallback = GetTargetSelectionForScreen(currentScreen);
                if (fallback != null && fallback.activeInHierarchy)
                {
                    EventSystem.current.SetSelectedGameObject(fallback);
                    m_LastValidSelection = fallback;
                }
            }
        }

        public UIScreen CurrentScreen
        {
            get
            {
                PruneDestroyedScreens();
                return m_ScreenHistory.Count > 0 ? m_ScreenHistory.Peek() : null;
            }
        }

        /// <summary>
        /// Pushes a new screen onto the history stack and opens it with physical transition overlap.
        /// </summary>
        public void PushScreen(UIScreen newScreen)
        {
            if (newScreen == null) return;
            if (m_IsTransitioning)
            {
                Log($"Ignoring PushScreen({newScreen.name}) while transitioning.");
                return;
            }

            PruneDestroyedScreens();
            Log($"Pushing screen: {newScreen.gameObject.name}");

            UIScreen currentTop = m_ScreenHistory.Count > 0 ? m_ScreenHistory.Peek() : null;
            if (currentTop != null)
            {
                GameObject lastSelected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
                m_SelectionHistory.Push(lastSelected);
                if (lastSelected != null)
                {
                    m_SelectionMemory[currentTop] = lastSelected;
                }
            }

            m_ScreenHistory.Push(newScreen);

            if (m_TransitionCoroutine != null)
            {
                StopCoroutine(m_TransitionCoroutine);
            }
            m_TransitionCoroutine = StartCoroutine(PushTransitionRoutine(currentTop, newScreen));
        }

        /// <summary>
        /// Pops the current screen and returns to the previous screen in the stack with reverse transition.
        /// </summary>
        public void PopScreen()
        {
            if (m_IsTransitioning)
            {
                Log("Ignoring PopScreen() while transitioning.");
                return;
            }

            PruneDestroyedScreens();

            if (m_ScreenHistory.Count <= 1)
            {
                Log("Cannot pop the base screen.");
                return;
            }

            UIScreen poppedScreen = m_ScreenHistory.Pop();
            GameObject previousSelection = m_SelectionHistory.Count > 0 ? m_SelectionHistory.Pop() : null;

            PruneDestroyedScreens();
            if (m_ScreenHistory.Count == 0) return;

            UIScreen previousScreen = m_ScreenHistory.Peek();
            Log($"Popped screen: {(poppedScreen != null ? poppedScreen.gameObject.name : "NullScreen")}, returning to: {previousScreen.name}");

            if (m_TransitionCoroutine != null)
            {
                StopCoroutine(m_TransitionCoroutine);
            }
            m_TransitionCoroutine = StartCoroutine(PopTransitionRoutine(poppedScreen, previousScreen, previousSelection));
        }

        private IEnumerator PushTransitionRoutine(UIScreen oldScreen, UIScreen newScreen)
        {
            m_IsTransitioning = true;

            // Strict controller input locking: disable navigation events and clear selection
            if (EventSystem.current != null)
            {
                EventSystem.current.sendNavigationEvents = false;
                EventSystem.current.SetSelectedGameObject(null);
            }

            // Audio hook
            if (newScreen.name.Contains("Confirmation") || newScreen.name.Contains("Popup"))
            {
                PlayAudio(m_PopupOpenSound);
            }
            else
            {
                PlayAudio(m_PanelOpenSound);
            }

            bool oldExitFinished = false;
            if (oldScreen != null)
            {
                oldScreen.PlayExitTransition(() => oldExitFinished = true);
            }
            else
            {
                oldExitFinished = true;
            }

            // Overlap timing: let old screen start moving before new screen lands
            if (oldScreen != null && m_TransitionOverlapDelay > 0f)
            {
                float overlapElapsed = 0f;
                while (overlapElapsed < m_TransitionOverlapDelay)
                {
                    overlapElapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            bool newEnterFinished = false;
            newScreen.PlayEnterTransition(() => newEnterFinished = true);

            while (!newEnterFinished || !oldExitFinished)
            {
                yield return null;
            }

            // Restore selection
            GameObject selectTarget = GetTargetSelectionForScreen(newScreen);
            RestoreSelectedElement(selectTarget);

            m_IsTransitioning = false;
            m_TransitionCoroutine = null;
        }

        private IEnumerator PopTransitionRoutine(UIScreen poppedScreen, UIScreen targetScreen, GameObject fallbackSelection)
        {
            m_IsTransitioning = true;

            // Strict controller input locking: disable navigation events and clear selection
            if (EventSystem.current != null)
            {
                EventSystem.current.sendNavigationEvents = false;
                EventSystem.current.SetSelectedGameObject(null);
            }

            // Audio hook
            PlayAudio(m_BackSound != null ? m_BackSound : m_PanelCloseSound);

            bool exitFinished = false;
            if (poppedScreen != null)
            {
                poppedScreen.PlayExitTransition(() => exitFinished = true);
            }
            else
            {
                exitFinished = true;
            }

            // Fast exit overlap for responsive feel
            float popOverlap = Mathf.Min(0.06f, m_TransitionOverlapDelay);
            if (poppedScreen != null && popOverlap > 0f)
            {
                float overlapElapsed = 0f;
                while (overlapElapsed < popOverlap)
                {
                    overlapElapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            bool enterFinished = false;
            if (targetScreen != null)
            {
                targetScreen.PlayEnterTransition(() => enterFinished = true);
            }
            else
            {
                enterFinished = true;
            }

            while (!enterFinished || !exitFinished)
            {
                yield return null;
            }

            // Retrieve selection: prefer remembered selection on targetScreen, else fallbackSelection
            GameObject targetSelection = null;
            if (targetScreen != null && m_SelectionMemory.TryGetValue(targetScreen, out GameObject remembered) && remembered != null && remembered.activeInHierarchy)
            {
                targetSelection = remembered;
            }
            else if (fallbackSelection != null && fallbackSelection.activeInHierarchy)
            {
                targetSelection = fallbackSelection;
            }
            else if (targetScreen != null)
            {
                targetSelection = targetScreen.DefaultSelectedObject;
            }

            RestoreSelectedElement(targetSelection);

            m_IsTransitioning = false;
            m_TransitionCoroutine = null;
        }

        private GameObject GetTargetSelectionForScreen(UIScreen screen)
        {
            if (screen == null) return null;

            if (m_SelectionMemory.TryGetValue(screen, out GameObject remembered) && remembered != null && remembered.activeInHierarchy)
            {
                return remembered;
            }

            return screen.DefaultSelectedObject;
        }

        public void RestoreSelectedElement(GameObject defaultSelectable)
        {
            if (EventSystem.current == null)
            {
                Debug.LogWarning("[UINavigationManager] EventSystem.current is null! Focus cannot be restored.");
                return;
            }

            // Re-enable navigation events now that flight has landed
            EventSystem.current.sendNavigationEvents = true;

            Log($"Restoring selection focus to: {(defaultSelectable != null ? defaultSelectable.name : "None")}");
            EventSystem.current.SetSelectedGameObject(null);
            if (defaultSelectable != null)
            {
                EventSystem.current.SetSelectedGameObject(defaultSelectable);
                m_LastValidSelection = defaultSelectable;

                // Controller landing overshoot on newly focused element
                StartCoroutine(PunchFocusElement(defaultSelectable.transform));
            }
        }

        private IEnumerator PunchFocusElement(Transform target)
        {
            if (target == null) yield break;
            Vector3 originalScale = target.localScale;
            Vector3 punchScale = originalScale * 1.08f;
            float duration = 0.12f;
            float elapsed = 0f;

            while (elapsed < duration && target != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = 1f - Mathf.Pow(1f - t, 2f);
                target.localScale = Vector3.Lerp(punchScale, originalScale, ease);
                yield return null;
            }

            if (target != null) target.localScale = originalScale;
        }

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
            if (m_IsTransitioning)
            {
                Log("Ignoring global Cancel input during transition.");
                return;
            }

            Log("Global Cancel input detected.");
            PopScreen();
        }

        private void PlayAudio(AudioClip clip)
        {
            if (clip != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayUi(clip);
            }
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
