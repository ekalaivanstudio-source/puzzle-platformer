using UnityEngine;

namespace TutorialSystem
{
    /// <summary>
    /// A bouncing arrow that points at the current target every frame.
    ///
    /// It lives on the Screen Space Overlay tutorial canvas. Each frame it asks
    /// <see cref="TutorialScreenPositioner"/> for the target's screen position, converts that into a
    /// local point on the canvas, sits a fixed offset away, rotates to point back at the target, and
    /// bounces along that direction. Because it re-reads the screen position every frame, it follows
    /// moving targets and works for UI or world objects under any camera/canvas setup for free.
    ///
    /// Visibility is controlled via a <see cref="CanvasGroup"/> (alpha), never by deactivating this
    /// GameObject — deactivating would stop <see cref="LateUpdate"/> and the arrow could never
    /// reappear (e.g. after a world target comes back in front of the camera).
    ///
    /// Set up by <see cref="TutorialManager"/> via <see cref="Initialize"/>; driven by
    /// <see cref="Follow"/> / <see cref="Hide"/>.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class TutorialArrowController : MonoBehaviour
    {
        [Header("Placement")]
        [Tooltip("If ON, the arrow's position you set in the editor (its anchoredPosition relative " +
                 "to the canvas center) is captured at startup and used as the follow offset — so " +
                 "wherever you drag the Arrow in the scene becomes its offset from EVERY target, and " +
                 "it keeps that same offset as it moves to the next step. If OFF, the explicit " +
                 "Offset field below is used instead.")]
        [SerializeField] private bool m_UseAuthoredPositionAsOffset = true;

        [Tooltip("Offset (canvas px) from the target center to the arrow's resting position. Used " +
                 "only when 'Use Authored Position As Offset' is OFF. Default sits the arrow above " +
                 "the target; the arrow auto-rotates to point at it.")]
        [SerializeField] private Vector2 m_Offset = new Vector2(0f, 110f);

        [Tooltip("Degrees to add after auto-aiming. Use this if your arrow sprite doesn't point " +
                 "'up' (0 = sprite points up the +Y axis toward the target).")]
        [SerializeField] private float m_SpriteAngleOffset = 0f;

        [Header("Bounce Animation")]
        [Tooltip("How far (canvas px) the arrow bounces toward/away from the target.")]
        [SerializeField] private float m_BounceAmplitude = 18f;

        [Tooltip("Bounces per second.")]
        [SerializeField] private float m_BounceSpeed = 2.2f;

        private RectTransform m_Rect;
        private RectTransform m_CanvasRect;
        private CanvasGroup m_Group;
        private Camera m_WorldCamera;
        private TutorialTarget m_Target;
        private float m_Time;

        /// <summary>Wires the canvas + world camera. Called once by the manager at setup.</summary>
        public void Initialize(RectTransform canvasRect, Camera worldCamera)
        {
            m_Rect = (RectTransform)transform;
            m_CanvasRect = canvasRect;
            m_WorldCamera = worldCamera;
            m_Group = GetComponent<CanvasGroup>();
            if (m_Group == null) m_Group = gameObject.AddComponent<CanvasGroup>();
            m_Group.blocksRaycasts = false; // the arrow never eats input.

            // Capture where the designer placed the arrow and use that as the offset from every
            // target. This is read once here so it survives the per-frame repositioning below.
            if (m_UseAuthoredPositionAsOffset) m_Offset = m_Rect.anchoredPosition;

            Hide();
        }

        /// <summary>Starts following <paramref name="target"/>. Pass null to hide.</summary>
        public void Follow(TutorialTarget target)
        {
            m_Target = target;
            m_Time = 0f;
            SetVisible(target != null);
        }

        /// <summary>Stops drawing the arrow.</summary>
        public void Hide()
        {
            m_Target = null;
            SetVisible(false);
        }

        private void SetVisible(bool visible)
        {
            if (m_Group != null) m_Group.alpha = visible ? 1f : 0f;
        }

        private void LateUpdate()
        {
            // LateUpdate so the target's transform/layout for this frame is already final.
            if (m_Target == null || m_CanvasRect == null) return;

            // Hide (but keep running) when a world target is behind the camera.
            if (!TutorialScreenPositioner.IsInFront(m_Target, m_WorldCamera)) { SetVisible(false); return; }
            SetVisible(true);

            Vector2 screen = TutorialScreenPositioner.GetScreenPoint(m_Target, m_WorldCamera);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    m_CanvasRect, screen, null, out Vector2 targetLocal))
                return;

            // Direction from the arrow's resting spot back toward the target = -offset.
            Vector2 dir = (-m_Offset).normalized;
            if (dir == Vector2.zero) dir = Vector2.down;

            m_Time += Time.unscaledDeltaTime; // unscaled so tutorials animate even when paused.
            float bounce = Mathf.Sin(m_Time * m_BounceSpeed * Mathf.PI * 2f) * m_BounceAmplitude;

            m_Rect.anchoredPosition = targetLocal + m_Offset + dir * bounce;

            // Aim: sprite's "up" should point along dir.
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f + m_SpriteAngleOffset;
            m_Rect.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
    }
}
