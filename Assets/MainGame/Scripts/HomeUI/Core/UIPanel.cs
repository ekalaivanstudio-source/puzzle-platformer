using UnityEngine;

namespace HomeUI
{
    /// <summary>
    /// Base class for every full-screen panel (Home, Settings, Collections, Level Select, Popup).
    ///
    /// A panel is never destroyed — it is shown/hidden by toggling its GameObject + its
    /// <see cref="CanvasGroup"/> (alpha + interactivity + raycasts). This keeps navigation cheap and
    /// stateful (a hidden panel keeps its scroll position, selection, etc.).
    ///
    /// Show/hide is intentionally INSTANT and side-effect-free here so navigation is 100% reliable.
    /// To add fades / slides / scene transitions, override <see cref="ApplyVisibility"/> in a
    /// subclass (or here) and animate toward the same end state — `m_TransitionDuration` is provided
    /// as the duration such an override would use.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class UIPanel : MonoBehaviour
    {
        [Tooltip("Stable id used by ScreenManager to address this panel.")]
        [SerializeField] private string m_PanelId;

        [Tooltip("Reserved for animated transitions added via an ApplyVisibility override. " +
                 "The default show/hide is instant and ignores this.")]
        [SerializeField] private float m_TransitionDuration = 0f;

        [Tooltip("If set, ScreenManager selects this object first when the panel opens " +
                 "(for keyboard/controller navigation).")]
        [SerializeField] private GameObject m_FirstSelected;

        private CanvasGroup m_Group;

        /// <summary>Stable id used for navigation. Defaults to the GameObject name if left blank.</summary>
        public string PanelId => string.IsNullOrEmpty(m_PanelId) ? name : m_PanelId;

        /// <summary>The object to focus first for gamepad/keyboard navigation (may be null).</summary>
        public GameObject FirstSelected => m_FirstSelected;

        /// <summary>Duration an animated <see cref="ApplyVisibility"/> override should use.</summary>
        protected float TransitionDuration => m_TransitionDuration;

        /// <summary>True while the panel is the active, interactive screen.</summary>
        public bool IsVisible { get; private set; }

        protected CanvasGroup Group
        {
            get
            {
                if (m_Group == null) m_Group = GetComponent<CanvasGroup>();
                return m_Group;
            }
        }

        protected virtual void Awake()
        {
            // Start hidden by default; ScreenManager decides what is shown.
            ApplyVisibility(false);
        }

        /// <summary>Shows the panel and raises <see cref="OnShow"/>.</summary>
        public void Show(bool instant = true)
        {
            IsVisible = true;
            ApplyVisibility(true);
            OnShow();
        }

        /// <summary>Hides the panel and raises <see cref="OnHide"/>.</summary>
        public void Hide(bool instant = true)
        {
            IsVisible = false;
            OnHide();
            ApplyVisibility(false);
        }

        /// <summary>Called right after the panel becomes visible. Override to refresh content.</summary>
        protected virtual void OnShow() { }

        /// <summary>Called right before the panel hides. Override to pause/cleanup.</summary>
        protected virtual void OnHide() { }

        /// <summary>
        /// Puts the panel into the shown or hidden end state. Default = instant, fully reliable.
        /// Override to animate the transition (fade/slide) toward this same end state.
        /// </summary>
        protected virtual void ApplyVisibility(bool visible)
        {
            if (visible)
            {
                gameObject.SetActive(true);
                Group.alpha = 1f;
                Group.interactable = true;
                Group.blocksRaycasts = true;
            }
            else
            {
                Group.alpha = 0f;
                Group.interactable = false;
                Group.blocksRaycasts = false;
                gameObject.SetActive(false);
            }
        }
    }
}
