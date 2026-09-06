using UnityEngine;
using UnityEngine.UI;
using System;

namespace MainGame.UI.Unified
{
    /// <summary>
    /// Unified Confirmation / Action Popup controller with physical pop entrance,
    /// opposing button slides, safe focus, and dynamic callback action.
    /// </summary>
    [DisallowMultipleComponent]
    public class ConfirmationPopupScreen : UIScreen
    {
        [Header("Controls")]
        [SerializeField] private Button m_ConfirmButton;
        [SerializeField] private Button m_CancelButton;

        [Header("Header Visuals")]
        [Tooltip("The Image component that displays the action title (e.g., 'EXIT?', 'RESET?', 'LEVELS?').")]
        [SerializeField] private Image m_TitleImage;

        [Header("Animator Reference")]
        [SerializeField] private ConfirmationPopupAnimator m_Animator;

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

            if (m_Animator == null)
            {
                m_Animator = GetComponent<ConfirmationPopupAnimator>();
            }
            if (m_Animator == null)
            {
                m_Animator = GetComponentInChildren<ConfirmationPopupAnimator>(true);
            }
        }

        public override void PlayEnterTransition(Action onComplete)
        {
            Open();

            if (m_Animator != null)
            {
                m_Animator.PlayEntrance(onComplete);
            }
            else
            {
                onComplete?.Invoke();
            }
        }

        public override void PlayExitTransition(Action onComplete)
        {
            if (m_Animator != null)
            {
                m_Animator.PlayExit(() =>
                {
                    Close();
                    onComplete?.Invoke();
                });
            }
            else
            {
                Close();
                onComplete?.Invoke();
            }
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
            if (UINavigationManager.Instance != null && UINavigationManager.Instance.IsTransitioning)
            {
                return;
            }

            Action confirmed = m_OnConfirmCallback;
            m_OnConfirmCallback = null;

            if (m_ConfirmButton != null)
            {
                UIAnimatedButton animBtn = m_ConfirmButton.GetComponent<UIAnimatedButton>();
                if (animBtn != null)
                {
                    animBtn.PlayConfirmPunch(() =>
                    {
                        if (UINavigationManager.Instance != null)
                        {
                            UINavigationManager.Instance.PopScreen();
                        }
                        confirmed?.Invoke();
                    });
                    return;
                }
            }

            AudioManager.Instance?.PlayButton();
            if (UINavigationManager.Instance != null)
            {
                UINavigationManager.Instance.PopScreen();
            }
            confirmed?.Invoke();
        }

        private void HandleCancelClicked()
        {
            if (UINavigationManager.Instance != null && UINavigationManager.Instance.IsTransitioning)
            {
                return;
            }

            m_OnConfirmCallback = null;

            if (m_CancelButton != null)
            {
                UIAnimatedButton animBtn = m_CancelButton.GetComponent<UIAnimatedButton>();
                if (animBtn != null)
                {
                    animBtn.PlayConfirmPunch(() =>
                    {
                        if (UINavigationManager.Instance != null)
                        {
                            UINavigationManager.Instance.PopScreen();
                        }
                    });
                    return;
                }
            }

            AudioManager.Instance?.PlayButton();
            if (UINavigationManager.Instance != null)
            {
                UINavigationManager.Instance.PopScreen();
            }
        }
    }
}
