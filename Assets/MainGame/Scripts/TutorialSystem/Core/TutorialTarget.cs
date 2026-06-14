using UnityEngine;

namespace TutorialSystem
{
    /// <summary>
    /// Marks a GameObject as something a tutorial step can point at, and registers it under a
    /// stable string id with the <see cref="TutorialTargetRegistry"/>.
    ///
    /// Works for BOTH:
    ///   • UI objects (anything with a <see cref="RectTransform"/> — buttons, cards, shop icons), and
    ///   • World objects (NPCs, buildings, collectibles — anything with a plain Transform).
    ///
    /// The tutorial step references this object by <see cref="TargetId"/> only, so the step asset
    /// never holds a hard scene reference. Add this component by hand, via the
    /// "GameObject ▸ Tutorial ▸ Convert To Tutorial Target" menu, or from the Tutorial Creator window.
    /// </summary>
    [DisallowMultipleComponent]
    public class TutorialTarget : MonoBehaviour
    {
        [Tooltip("Stable, unique id used by tutorial steps to find this object. " +
                 "Use the inspector button to generate / copy it.")]
        [SerializeField] private string m_TargetId = "";

        private RectTransform m_RectTransform;
        private Canvas m_Canvas;
        private bool m_Resolved;

        /// <summary>The id tutorial steps use to address this target.</summary>
        public string TargetId => m_TargetId;

        /// <summary>This target's transform (always valid).</summary>
        public Transform Transform => transform;

        /// <summary>
        /// The RectTransform if this is a UI object, otherwise null. Cached on first access.
        /// </summary>
        public RectTransform RectTransform
        {
            get { Resolve(); return m_RectTransform; }
        }

        /// <summary>The Canvas this UI target lives under (null for world objects).</summary>
        public Canvas Canvas
        {
            get { Resolve(); return m_Canvas; }
        }

        /// <summary>True if this target is a UI element (has a RectTransform under a Canvas).</summary>
        public bool IsUI
        {
            get { Resolve(); return m_RectTransform != null && m_Canvas != null; }
        }

        private void Resolve()
        {
            if (m_Resolved) return;
            m_RectTransform = transform as RectTransform;
            if (m_RectTransform != null)
                m_Canvas = GetComponentInParent<Canvas>();
            m_Resolved = true;
        }

        private void OnEnable()  => TutorialTargetRegistry.Register(this);
        private void OnDisable() => TutorialTargetRegistry.Unregister(this);

        /// <summary>
        /// Editor / tooling hook to set the id programmatically (used by the setup tools).
        /// Not intended for runtime gameplay use.
        /// </summary>
        public void SetTargetId(string id)
        {
            // Re-register under the new id if we're already live.
            bool wasRegistered = Application.isPlaying && isActiveAndEnabled;
            if (wasRegistered) TutorialTargetRegistry.Unregister(this);
            m_TargetId = id;
            if (wasRegistered) TutorialTargetRegistry.Register(this);
        }
    }
}
