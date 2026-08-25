using UnityEngine;

namespace MainGame.UI.Unified
{
    /// <summary>
    /// Base class for all unified UI screens. Handles default selection and active status.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class UIScreen : MonoBehaviour
    {
        [Header("UIScreen Configuration")]
        [Tooltip("The selectable UI element that gets focused when this screen opens.")]
        [SerializeField] protected GameObject m_DefaultSelectedObject;

        private CanvasGroup m_CanvasGroup;

        /// <summary>
        /// Gets the default selectable GameObject for this screen.
        /// </summary>
        public virtual GameObject DefaultSelectedObject => m_DefaultSelectedObject;

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

        private void SetCanvasGroupInteractive(bool active)
        {
            if (m_CanvasGroup != null)
            {
                m_CanvasGroup.alpha = active ? 1f : 0f;
                m_CanvasGroup.interactable = active;
                m_CanvasGroup.blocksRaycasts = active;
            }
        }
    }
}
