using UnityEngine;

namespace MainGame.UI.CinematicEffects
{
    /// <summary>
    /// Follower script for dedicated outline rendering layers.
    /// Ensures the outline layer's RectTransform exactly tracks its target artwork
    /// in LateUpdate, preventing any lag, drift, or spatial detachment during physics or animations.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class UIOutlineFollower : MonoBehaviour
    {
        [Tooltip("Target RectTransform to track.")]
        [SerializeField] private RectTransform m_Target;

        private RectTransform m_RectTransform;

        public RectTransform Target
        {
            get => m_Target;
            set => m_Target = value;
        }

        private void Awake()
        {
            m_RectTransform = GetComponent<RectTransform>();
        }

        private void OnEnable()
        {
            SyncToTarget();
        }

        private void LateUpdate()
        {
            SyncToTarget();
        }

        public void SyncToTarget()
        {
            if (m_Target == null) return;
            if (m_RectTransform == null) m_RectTransform = GetComponent<RectTransform>();
            if (m_RectTransform == null) return;

            // If this is a direct child of the target, standard hierarchy already moves it
            if (transform.parent == m_Target) return;

            m_RectTransform.anchoredPosition = m_Target.anchoredPosition;
            m_RectTransform.localRotation = m_Target.localRotation;
            m_RectTransform.localScale = m_Target.localScale;
        }
    }
}
