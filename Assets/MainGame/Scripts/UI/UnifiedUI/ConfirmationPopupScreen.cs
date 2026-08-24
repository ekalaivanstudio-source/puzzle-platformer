using UnityEngine;
using UnityEngine.UI;
using System;

namespace MainGame.UI.Unified
{
    /// <summary>
    /// Unified Confirmation / Action Popup controller.
    /// Supports executing a dynamic callback action when confirmed.
    /// </summary>
    public class ConfirmationPopupScreen : UIScreen
    {
        [Header("Controls")]
        [SerializeField] private Button m_ConfirmButton;
        [SerializeField] private Button m_CancelButton;

        [Header("Header Visuals")]
        [Tooltip("The Image component that displays the action title (e.g., 'EXIT?', 'RESET?', 'LEVELS?').")]
        [SerializeField] private Image m_TitleImage;

        private Action m_OnConfirmCallback;

        private void OnEnable()
        {
            if (m_ConfirmButton != null)
            {
                m_ConfirmButton.onClick.AddListener(HandleConfirmClicked);
                m_ConfirmButton.navigation = new Navigation { mode = Navigation.Mode.None };
            }
            if (m_CancelButton != null)
            {
                m_CancelButton.onClick.AddListener(HandleCancelClicked);
                m_CancelButton.navigation = new Navigation { mode = Navigation.Mode.None };
            }
        }

        private void OnDisable()
        {
            if (m_ConfirmButton != null) m_ConfirmButton.onClick.RemoveListener(HandleConfirmClicked);
            if (m_CancelButton != null) m_CancelButton.onClick.RemoveListener(HandleCancelClicked);
        }

        private void Update()
        {
            // Submit key triggers Confirm
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
            {
                HandleConfirmClicked();
            }
            // Cancel key triggers Cancel
            else if (Input.GetKeyDown(KeyCode.Escape))
            {
                HandleCancelClicked();
            }

            // Gamepad triggers
            if (UnityEngine.InputSystem.Gamepad.current != null)
            {
                if (UnityEngine.InputSystem.Gamepad.current.buttonSouth.wasPressedThisFrame)
                {
                    HandleConfirmClicked();
                }
                else if (UnityEngine.InputSystem.Gamepad.current.buttonEast.wasPressedThisFrame)
                {
                    HandleCancelClicked();
                }
            }
        }

        /// <summary>
        /// Configure the confirmation popup with a callback to execute on confirmation and a custom title sprite.
        /// </summary>
        public void SetupAction(Action onConfirm, Sprite titleSprite)
        {
            m_OnConfirmCallback = onConfirm;

            if (m_TitleImage != null && titleSprite != null)
            {
                m_TitleImage.sprite = titleSprite;
            }
        }

        private void HandleConfirmClicked()
        {
            AudioManager.Instance?.PlayButton();
            Debug.Log("[ConfirmationPopupScreen] Action confirmed.");
            
            // Pop popup off the screen stack
            if (UINavigationManager.Instance != null)
            {
                UINavigationManager.Instance.PopScreen();
            }

            // Execute action
            m_OnConfirmCallback?.Invoke();
            m_OnConfirmCallback = null;
        }

        private void HandleCancelClicked()
        {
            AudioManager.Instance?.PlayButton();
            m_OnConfirmCallback = null;

            if (UINavigationManager.Instance != null)
            {
                UINavigationManager.Instance.PopScreen();
            }
        }
    }
}
