using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace MainGame.UI.PauseMenu
{
    /// <summary>
    /// Manages controller and keyboard navigation for the Pause Menu system.
    /// Ensures EventSystem self-provisioning, selection memory, safe fallbacks,
    /// and clean loop navigation across Reset, Level, and Exit buttons.
    /// </summary>
    [DisallowMultipleComponent]
    public class PauseNavigationController : MonoBehaviour
    {
        [Header("Main Menu Buttons")]
        [SerializeField] private Button m_ResetButton;
        [SerializeField] private Button m_LevelsButton;
        [SerializeField] private Button m_ExitButton;

        [Header("Input Asset")]
        [SerializeField] private InputActionAsset m_InputActionAsset;

        private GameObject m_LastFocusedButton;
        private Coroutine m_PunchRoutine;

        public Button ResetButton => m_ResetButton;
        public Button LevelsButton => m_LevelsButton;
        public Button ExitButton => m_ExitButton;

        public void Initialize(Button resetBtn, Button levelsBtn, Button exitBtn, InputActionAsset inputAsset)
        {
            if (resetBtn != null) m_ResetButton = resetBtn;
            if (levelsBtn != null) m_LevelsButton = levelsBtn;
            if (exitBtn != null) m_ExitButton = exitBtn;
            if (inputAsset != null) m_InputActionAsset = inputAsset;

            EnsureEventSystem();
            BuildVerticalNavigationLoop();
        }

        /// <summary>
        /// Ensures a valid EventSystem with InputSystemUIInputModule exists in the scene.
        /// If one already exists, reuses it. If none is found, creates one safely.
        /// </summary>
        public void EnsureEventSystem()
        {
            if (EventSystem.current == null)
            {
                EventSystem existing = Object.FindAnyObjectByType<EventSystem>();
                if (existing != null)
                {
                    EventSystem.current = existing;
                }
                else
                {
                    GameObject esGo = new GameObject("[EventSystem-AutoCreated]");
                    EventSystem es = esGo.AddComponent<EventSystem>();
                    InputSystemUIInputModule module = esGo.AddComponent<InputSystemUIInputModule>();

                    if (m_InputActionAsset != null)
                    {
                        module.actionsAsset = m_InputActionAsset;
                    }
                }
            }

            if (EventSystem.current != null)
            {
                EventSystem.current.sendNavigationEvents = true;
                InputSystemUIInputModule module = EventSystem.current.GetComponent<InputSystemUIInputModule>();
                if (module != null && module.actionsAsset == null && m_InputActionAsset != null)
                {
                    module.actionsAsset = m_InputActionAsset;
                }
            }
        }

        private void Update()
        {
            if (PauseMenuController.Instance == null || PauseMenuController.Instance.CurrentState != PauseState.MainPause)
            {
                return;
            }

            if (m_ResetButton == null || m_LevelsButton == null || m_ExitButton == null) return;

            bool upPressed = false;
            bool downPressed = false;
            bool submitPressed = false;

            if (Keyboard.current != null)
            {
                upPressed = Keyboard.current.upArrowKey.wasPressedThisFrame || Keyboard.current.wKey.wasPressedThisFrame;
                downPressed = Keyboard.current.downArrowKey.wasPressedThisFrame || Keyboard.current.sKey.wasPressedThisFrame;
                submitPressed = Keyboard.current.enterKey.wasPressedThisFrame ||
                                Keyboard.current.numpadEnterKey.wasPressedThisFrame ||
                                Keyboard.current.spaceKey.wasPressedThisFrame;
            }

            if (Gamepad.current != null)
            {
                upPressed |= Gamepad.current.dpad.up.wasPressedThisFrame || Gamepad.current.leftStick.up.wasPressedThisFrame;
                downPressed |= Gamepad.current.dpad.down.wasPressedThisFrame || Gamepad.current.leftStick.down.wasPressedThisFrame;
                submitPressed |= Gamepad.current.buttonSouth.wasPressedThisFrame;
            }

#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) upPressed = true;
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) downPressed = true;
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space)) submitPressed = true;
#endif

            GameObject cur = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;

            // If nothing is selected, any navigation or submit key immediately focuses the default/last button
            if (cur == null || !cur.activeInHierarchy || (cur != m_ResetButton.gameObject && cur != m_LevelsButton.gameObject && cur != m_ExitButton.gameObject))
            {
                if (upPressed || downPressed || submitPressed)
                {
                    RestoreLastSelection();
                    return;
                }
            }

            if (upPressed)
            {
                if (cur == m_ResetButton.gameObject) SetSelected(m_ExitButton.gameObject);
                else if (cur == m_LevelsButton.gameObject) SetSelected(m_ResetButton.gameObject);
                else if (cur == m_ExitButton.gameObject) SetSelected(m_LevelsButton.gameObject);
                else RestoreLastSelection();
            }
            else if (downPressed)
            {
                if (cur == m_ResetButton.gameObject) SetSelected(m_LevelsButton.gameObject);
                else if (cur == m_LevelsButton.gameObject) SetSelected(m_ExitButton.gameObject);
                else if (cur == m_ExitButton.gameObject) SetSelected(m_ResetButton.gameObject);
                else RestoreLastSelection();
            }
            else if (submitPressed)
            {
                if (cur == m_ResetButton.gameObject) m_ResetButton.onClick.Invoke();
                else if (cur == m_LevelsButton.gameObject) m_LevelsButton.onClick.Invoke();
                else if (cur == m_ExitButton.gameObject) m_ExitButton.onClick.Invoke();
            }
        }

        /// <summary>
        /// Sets up strict vertical loop navigation: Reset <-> Level <-> Exit.
        /// </summary>
        public void BuildVerticalNavigationLoop()
        {
            if (m_ResetButton == null || m_LevelsButton == null || m_ExitButton == null) return;

            Navigation resetNav = m_ResetButton.navigation;
            resetNav.mode = Navigation.Mode.Explicit;
            resetNav.selectOnDown = m_LevelsButton;
            resetNav.selectOnUp = m_ExitButton; // Loop to bottom
            m_ResetButton.navigation = resetNav;

            Navigation levelNav = m_LevelsButton.navigation;
            levelNav.mode = Navigation.Mode.Explicit;
            levelNav.selectOnUp = m_ResetButton;
            levelNav.selectOnDown = m_ExitButton;
            m_LevelsButton.navigation = levelNav;

            Navigation exitNav = m_ExitButton.navigation;
            exitNav.mode = Navigation.Mode.Explicit;
            exitNav.selectOnUp = m_LevelsButton;
            exitNav.selectOnDown = m_ResetButton; // Loop to top
            m_ExitButton.navigation = exitNav;
        }

        /// <summary>
        /// Selects the default button (RESET) and plays landing punch feedback.
        /// </summary>
        public void FocusDefaultButton()
        {
            GameObject target = m_ResetButton != null ? m_ResetButton.gameObject : null;
            SetSelected(target);
            m_LastFocusedButton = target;
        }

        /// <summary>
        /// Remembers which button was focused when transitioning away from MainPause.
        /// </summary>
        public void StoreCurrentSelection()
        {
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
            {
                m_LastFocusedButton = EventSystem.current.currentSelectedGameObject;
            }
        }

        /// <summary>
        /// Restores focus to the button that opened the active sub-panel (Reset, Level, or Exit).
        /// </summary>
        public void RestoreLastSelection()
        {
            GameObject target = m_LastFocusedButton;
            if (target == null || !target.activeInHierarchy)
            {
                target = m_ResetButton != null ? m_ResetButton.gameObject : null;
            }

            SetSelected(target);
        }

        /// <summary>
        /// Sets the active EventSystem selection safely and triggers a controller focus pulse.
        /// </summary>
        public void SetSelected(GameObject target)
        {
            EnsureEventSystem();
            if (EventSystem.current == null) return;

            EventSystem.current.SetSelectedGameObject(null);
            if (target != null && target.activeInHierarchy)
            {
                EventSystem.current.SetSelectedGameObject(target);
                PlayFocusPunch(target.transform);
            }
        }

        public void ClearSelection()
        {
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        private void PlayFocusPunch(Transform target)
        {
            if (target == null) return;
            if (m_PunchRoutine != null)
            {
                StopCoroutine(m_PunchRoutine);
            }
            m_PunchRoutine = StartCoroutine(FocusPunchRoutine(target));
        }

        private IEnumerator FocusPunchRoutine(Transform target)
        {
            Vector3 restScale = Vector3.one;
            Vector3 punchScale = restScale * 1.05f;
            float duration = 0.10f;
            float elapsed = 0f;

            while (elapsed < duration && target != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = 1f - Mathf.Pow(1f - t, 2f);
                target.localScale = Vector3.Lerp(punchScale, restScale, ease);
                yield return null;
            }

            if (target != null)
            {
                target.localScale = restScale;
            }
            m_PunchRoutine = null;
        }
    }
}
