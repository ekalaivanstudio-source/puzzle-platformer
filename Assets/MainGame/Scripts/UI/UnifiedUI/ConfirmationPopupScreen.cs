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

        /// <summary>
        /// Focus defaults to the safe option (Cancel / "NO") so a stray Submit never confirms a destructive action.
        /// </summary>
        public override GameObject DefaultSelectedObject =>
            m_CancelButton != null ? m_CancelButton.gameObject : base.DefaultSelectedObject;

        protected override void Awake()
        {
            base.Awake();
            BuildHorizontalLoopNavigation();
        }

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
        }

        private void OnDisable()
        {
            if (m_ConfirmButton != null) m_ConfirmButton.onClick.RemoveListener(HandleConfirmClicked);
            if (m_CancelButton != null) m_CancelButton.onClick.RemoveListener(HandleCancelClicked);
        }

        public override void Close()
        {
            base.Close();
            // Whoever closed us (including the global Cancel action popping the stack) cancels the pending action.
            m_OnConfirmCallback = null;
        }

        /// <summary>
        /// Sets up explicit horizontal loop navigation between the Yes and No buttons.
        /// </summary>
        private void BuildHorizontalLoopNavigation()
        {
            if (m_ConfirmButton == null || m_CancelButton == null) return;

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

            // Capture before popping: Close() clears the pending callback.
            Action confirmed = m_OnConfirmCallback;

            if (UINavigationManager.Instance != null)
            {
                UINavigationManager.Instance.PopScreen();
            }

            confirmed?.Invoke();
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
