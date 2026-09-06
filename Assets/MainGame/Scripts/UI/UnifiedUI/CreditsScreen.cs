using System;
using UnityEngine;
using UnityEngine.UI;

namespace MainGame.UI.Unified
{
    /// <summary>
    /// Unified Credits Screen controller with calm cinematic transitions.
    /// </summary>
    [DisallowMultipleComponent]
    public class CreditsScreen : UIScreen
    {
        [Header("Controls")]
        [SerializeField] private Button m_BackButton;

        [Header("Animator Reference")]
        [SerializeField] private CreditsScreenAnimator m_Animator;

        public override GameObject DefaultSelectedObject =>
            m_BackButton != null ? m_BackButton.gameObject : base.DefaultSelectedObject;

        protected override void Awake()
        {
            base.Awake();

            if (m_Animator == null)
            {
                m_Animator = GetComponent<CreditsScreenAnimator>();
            }
            if (m_Animator == null)
            {
                m_Animator = GetComponentInChildren<CreditsScreenAnimator>(true);
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
            if (m_BackButton != null) m_BackButton.onClick.AddListener(HandleBackClicked);
        }

        private void OnDisable()
        {
            if (m_BackButton != null) m_BackButton.onClick.RemoveListener(HandleBackClicked);
        }

        private void HandleBackClicked()
        {
            if (UINavigationManager.Instance != null && UINavigationManager.Instance.IsTransitioning)
            {
                return;
            }

            if (m_BackButton != null)
            {
                UIAnimatedButton animBtn = m_BackButton.GetComponent<UIAnimatedButton>();
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
