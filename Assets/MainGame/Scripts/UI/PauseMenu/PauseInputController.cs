using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MainGame.UI.PauseMenu
{
    /// <summary>
    /// Dedicated semantic input controller for the Pause Menu system.
    /// Listens to Pause and Cancel actions via PlayerInputAction asset,
    /// routes input strictly based on PauseState, and manages action map ownership.
    /// </summary>
    [DisallowMultipleComponent]
    public class PauseInputController : MonoBehaviour
    {
        [Header("Input Asset")]
        [SerializeField] private InputActionAsset m_InputActionAsset;

        private InputAction m_PlayerPauseAction;
        private InputAction m_UIPauseAction;
        private InputAction m_UICancelAction;

        private PauseMenuController m_Controller;
        private bool m_IsListening;

        public void Initialize(PauseMenuController controller, InputActionAsset inputAsset)
        {
            m_Controller = controller;
            if (inputAsset != null) m_InputActionAsset = inputAsset;

            ResolveInputActions();
        }

        private void ResolveInputActions()
        {
            if (m_InputActionAsset == null)
            {
                // Fallback: try resolving from DeviceInputProvider
                if (DeviceInputProvider.Instance != null && DeviceInputProvider.Instance.InputActionAsset != null)
                {
                    m_InputActionAsset = DeviceInputProvider.Instance.InputActionAsset;
                }
            }

            if (m_InputActionAsset == null) return;

            InputActionMap playerMap = m_InputActionAsset.FindActionMap("Player", throwIfNotFound: false);
            if (playerMap != null)
            {
                m_PlayerPauseAction = playerMap.FindAction("Pause", throwIfNotFound: false);
            }

            InputActionMap uiMap = m_InputActionAsset.FindActionMap("UI", throwIfNotFound: false);
            if (uiMap != null)
            {
                m_UIPauseAction = uiMap.FindAction("Pause", throwIfNotFound: false);
                m_UICancelAction = uiMap.FindAction("Cancel", throwIfNotFound: false);
            }
        }

        public void SetListening(bool listening)
        {
            if (m_IsListening == listening) return;
            m_IsListening = listening;

            if (m_PlayerPauseAction != null)
            {
                if (listening) { m_PlayerPauseAction.performed += OnPausePerformed; m_PlayerPauseAction.Enable(); }
                else { m_PlayerPauseAction.performed -= OnPausePerformed; }
            }

            if (m_UIPauseAction != null)
            {
                if (listening) { m_UIPauseAction.performed += OnPausePerformed; m_UIPauseAction.Enable(); }
                else { m_UIPauseAction.performed -= OnPausePerformed; }
            }

            if (m_UICancelAction != null)
            {
                if (listening) { m_UICancelAction.performed += OnCancelPerformed; m_UICancelAction.Enable(); }
                else { m_UICancelAction.performed -= OnCancelPerformed; }
            }
        }

        private void OnEnable()
        {
            ResolveInputActions();
            SetListening(true);
        }

        private void OnDisable()
        {
            SetListening(false);
        }

        private void Update()
        {
            // Fallback keyboard/gamepad polling if InputActionAsset is unassigned in a bare scene
            if (m_PlayerPauseAction == null && m_UIPauseAction == null)
            {
                bool escapePressed = false;
                if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                {
                    escapePressed = true;
                }
                if (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame)
                {
                    escapePressed = true;
                }

#if ENABLE_LEGACY_INPUT_MANAGER
                if (Input.GetKeyDown(KeyCode.Escape)) escapePressed = true;
#endif

                if (escapePressed)
                {
                    HandlePauseInput();
                }
            }
        }

        private void OnPausePerformed(InputAction.CallbackContext context)
        {
            HandlePauseInput();
        }

        private void OnCancelPerformed(InputAction.CallbackContext context)
        {
            HandleCancelInput();
        }

        private void HandlePauseInput()
        {
            if (m_Controller == null) return;

            switch (m_Controller.CurrentState)
            {
                case PauseState.Gameplay:
                    m_Controller.OpenPauseMenu();
                    break;

                case PauseState.MainPause:
                    m_Controller.ResumeGameplay();
                    break;

                case PauseState.Confirmation:
                    m_Controller.CancelConfirmation();
                    break;

                case PauseState.LevelSelection:
                    m_Controller.BackFromLevelSelection();
                    break;

                case PauseState.Opening:
                case PauseState.Closing:
                    // Ignore pause requests during transitions to avoid glitching
                    break;
            }
        }

        private void HandleCancelInput()
        {
            if (m_Controller == null) return;

            switch (m_Controller.CurrentState)
            {
                case PauseState.MainPause:
                    m_Controller.ResumeGameplay();
                    break;

                case PauseState.Confirmation:
                    m_Controller.CancelConfirmation();
                    break;

                case PauseState.LevelSelection:
                    m_Controller.BackFromLevelSelection();
                    break;

                case PauseState.Gameplay:
                case PauseState.Opening:
                case PauseState.Closing:
                    break;
            }
        }
    }
}
