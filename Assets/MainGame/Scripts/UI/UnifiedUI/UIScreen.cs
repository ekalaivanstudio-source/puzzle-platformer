using System;
using UnityEngine;

namespace MainGame.UI.Unified
{
    /// <summary>
    /// Base class for all unified UI screens. Handles default selection, active status,
    /// and cinematic entrance/exit transition hooks.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class UIScreen : MonoBehaviour
    {
        [Header("UIScreen Configuration")]
        [Tooltip("The selectable UI element that gets focused when this screen opens.")]
        [SerializeField] protected GameObject m_DefaultSelectedObject;

        private CanvasGroup m_CanvasGroup;
        protected bool m_IsTransitioning;

        /// <summary>
        /// Gets the default selectable GameObject for this screen.
        /// </summary>
        public virtual GameObject DefaultSelectedObject => m_DefaultSelectedObject;

        /// <summary>
        /// True while the screen is currently executing an enter or exit animation.
        /// </summary>
        public bool IsTransitioning => m_IsTransitioning;

        public CanvasGroup CanvasGroup
        {
            get
            {
                if (m_CanvasGroup == null) m_CanvasGroup = GetComponent<CanvasGroup>();
                return m_CanvasGroup;
            }
        }

        protected virtual void Awake()
        {
            m_CanvasGroup = GetComponent<CanvasGroup>();
        }

        /// <summary>
        /// Called when the screen is pushed onto the navigation stack.
        /// </summary>
        public virtual void Open()
        {
            gameObject.SetActive(true);
            SetCanvasGroupInteractive(true);
        }

        /// <summary>
        /// Called when the screen is popped from the stack.
        /// </summary>
        public virtual void Close()
        {
            SetCanvasGroupInteractive(false);
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Plays the physical entrance transition sequence.
        /// Derived classes or attached animators override this to run multi-layered animations.
        /// </summary>
        public virtual void PlayEnterTransition(Action onComplete)
        {
            Open();
            onComplete?.Invoke();
        }

        /// <summary>
        /// Plays the physical exit transition sequence.
        /// Derived classes or attached animators override this to run multi-layered animations.
        /// </summary>
        public virtual void PlayExitTransition(Action onComplete)
        {
            Close();
            onComplete?.Invoke();
        }

        public void SetCanvasGroupInteractive(bool active)
        {
            if (CanvasGroup != null)
            {
                CanvasGroup.alpha = active ? 1f : 0f;
                CanvasGroup.interactable = active;
                CanvasGroup.blocksRaycasts = active;
            }
        }
    }
}
