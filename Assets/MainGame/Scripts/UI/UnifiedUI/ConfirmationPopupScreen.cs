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
            }
            if (m_CancelButton != null)
            {
                m_CancelButton.onClick.AddListener(HandleCancelClicked);
            }

            // Set up explicit horizontal loop navigation between Yes and No
            if (m_ConfirmButton != null && m_CancelButton != null)
            {
                Navigation confirmNav = m_ConfirmButton.navigation;
                confirmNav.mode = Navigation.Mode.Explicit;
                confirmNav.selectOnRight = m_CancelButton;
                confirmNav.selectOnLeft = m_CancelButton; // loop
                confirmNav.selectOnUp = null;
                confirmNav.selectOnDown = null;
                m_ConfirmButton.navigation = confirmNav;

                Navigation cancelNav = m_CancelButton.navigation;
                cancelNav.mode = Navigation.Mode.Explicit;
                cancelNav.selectOnLeft = m_ConfirmButton;
                cancelNav.selectOnRight = m_ConfirmButton; // loop
                cancelNav.selectOnUp = null;
                cancelNav.selectOnDown = null;
                m_CancelButton.navigation = cancelNav;
            }

            // Force focus to the NO (Cancel) button by default as a safety measure
            if (UINavigationManager.Instance != null && m_CancelButton != null)
            {
                UINavigationManager.Instance.RestoreSelectedElement(m_CancelButton.gameObject);
            }
        }

        private void OnDisable()
        {
            if (m_ConfirmButton != null) m_ConfirmButton.onClick.RemoveListener(HandleConfirmClicked);
            if (m_CancelButton != null) m_CancelButton.onClick.RemoveListener(HandleCancelClicked);
        }

        private void Update()
        {
            // Cancel key triggers Cancel (Submit key is handled natively by EventSystem based on selection)
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                HandleCancelClicked();
            }

            // Gamepad Cancel trigger
            if (UnityEngine.InputSystem.Gamepad.current != null)
            {
                if (UnityEngine.InputSystem.Gamepad.current.buttonEast.wasPressedThisFrame)
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
