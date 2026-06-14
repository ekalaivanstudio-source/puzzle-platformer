using System;
using UnityEngine;
using UnityEngine.UI;

namespace TutorialSystem
{
    /// <summary>
    /// Dims the screen and cuts a "spotlight" hole around the current target, plus an optional
    /// pulsing highlight ring.
    ///
    /// <b>How the spotlight works (no custom shader required):</b> instead of masking a hole out of a
    /// full-screen image (which needs a stencil shader and behaves differently per render pipeline),
    /// this draws FOUR opaque strips — top, bottom, left, right — that frame the target. The
    /// rectangular gap they leave IS the spotlight. The strips block input everywhere they cover;
    /// the gap lets taps fall through to the real target underneath (so WaitForButtonClick works).
    ///
    /// All four strips and the ring live under one full-screen "Overlay" RectTransform that this
    /// component is attached to. Sizes are recomputed every frame so the spotlight tracks moving or
    /// animating targets and stays correct under any camera/canvas mode.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class TutorialHighlightSystem : MonoBehaviour
    {
        /// <summary>How the highlight ring decides its width/height.</summary>
        public enum RingSizeMode
        {
            /// <summary>Ring = target size × multiplier + padding (per axis).</summary>
            MatchTargetSize = 0,
            /// <summary>Ring is always a fixed (width, height), ignoring the target size.</summary>
            FixedSize = 1,
        }

        [Header("Strips (the dim)")]
        [Tooltip("Top / Bottom / Left / Right dim strips. Order does not matter.")]
        [SerializeField] private Image m_Top;
        [SerializeField] private Image m_Bottom;
        [SerializeField] private Image m_Left;
        [SerializeField] private Image m_Right;

        [Tooltip("The four strips share this color (set alpha to control how dark the dim is).")]
        [SerializeField] private Color m_DimColor = new Color(0f, 0f, 0f, 0.72f);

        [Header("Highlight Ring")]
        [Tooltip("Image drawn on top of the target to draw the eye. Optional.")]
        [SerializeField] private Image m_Ring;

        [Tooltip("How the ring is sized:\n" +
                 "• MatchTargetSize — ring = target size × Multiplier + Padding (per axis).\n" +
                 "• FixedSize — ring is always Fixed Size, ignoring the target's size.")]
        [SerializeField] private RingSizeMode m_RingSizeMode = RingSizeMode.MatchTargetSize;

        [Tooltip("MatchTargetSize: multiplies the target's width/height. (1,1) = exactly the target " +
                 "size; (1.5, 1) = 50% wider, same height.")]
        [SerializeField] private Vector2 m_RingSizeMultiplier = Vector2.one;

        [Tooltip("MatchTargetSize: extra width (x) and height (y) in canvas px added on top of the " +
                 "scaled target size.")]
        [SerializeField] private Vector2 m_RingPadding = new Vector2(16f, 16f);

        [Tooltip("FixedSize: the exact ring size (width, height) in canvas px.")]
        [SerializeField] private Vector2 m_RingFixedSize = new Vector2(180f, 180f);

        [Header("Pulse Animation")]
        [Tooltip("Ring pulses between scale 1 and 1+this.")]
        [SerializeField] private float m_PulseAmplitude = 0.08f;

        [Tooltip("Ring pulses this many times per second.")]
        [SerializeField] private float m_PulseSpeed = 1.4f;

        private RectTransform m_Rect;
        private Camera m_WorldCamera;
        private TutorialTarget m_Target;
        private float m_Padding;
        private bool m_DimEnabled;
        private bool m_RingEnabled;
        private float m_Time;
        private Action m_OnTap;

        /// <summary>Wires the world camera and registers tap callbacks on the strips. Called once.</summary>
        public void Initialize(Camera worldCamera)
        {
            m_Rect = (RectTransform)transform;
            m_WorldCamera = worldCamera;

            foreach (Image strip in new[] { m_Top, m_Bottom, m_Left, m_Right })
            {
                if (strip == null) continue;
                strip.color = m_DimColor;
                strip.raycastTarget = true; // strips block input where they cover.
                TutorialClickCatcher catcher = strip.GetComponent<TutorialClickCatcher>();
                if (catcher == null) catcher = strip.gameObject.AddComponent<TutorialClickCatcher>();
                catcher.OnClicked = () => m_OnTap?.Invoke();
            }
            if (m_Ring != null) m_Ring.raycastTarget = false; // never eat the target's taps.

            HideAll();
        }

        /// <summary>
        /// Enables "tap the dim to continue". When <paramref name="enabled"/> is false the strips
        /// still block input but a tap does nothing (used for WaitForButtonClick / event steps).
        /// </summary>
        public void SetTapToContinue(bool enabled, Action onTap)
        {
            m_OnTap = enabled ? onTap : null;
        }

        /// <summary>
        /// Configures the highlight for a step.
        /// </summary>
        /// <param name="target">Target to spotlight, or null for a full-screen dim.</param>
        /// <param name="dim">Draw the dim strips at all.</param>
        /// <param name="ring">Draw the pulsing ring (ignored when target is null).</param>
        /// <param name="padding">Extra padding around the target (canvas px).</param>
        public void Show(TutorialTarget target, bool dim, bool ring, float padding)
        {
            m_Target = target;
            m_DimEnabled = dim;
            m_RingEnabled = ring && target != null;
            m_Padding = padding;
            m_Time = 0f;

            SetStripsActive(dim);
            if (m_Ring != null) m_Ring.gameObject.SetActive(m_RingEnabled);

            if (dim && target == null) ShowFullDim(); // dim everything, no hole.
        }

        /// <summary>Hides the dim and the ring.</summary>
        public void HideAll()
        {
            m_Target = null;
            m_DimEnabled = false;
            m_RingEnabled = false;
            SetStripsActive(false);
            if (m_Ring != null) m_Ring.gameObject.SetActive(false);
        }

        private void SetStripsActive(bool active)
        {
            if (m_Top != null) m_Top.gameObject.SetActive(active);
            if (m_Bottom != null) m_Bottom.gameObject.SetActive(active);
            if (m_Left != null) m_Left.gameObject.SetActive(active);
            if (m_Right != null) m_Right.gameObject.SetActive(active);
        }

        private void LateUpdate()
        {
            if (!m_DimEnabled && !m_RingEnabled) return;
            if (m_Target == null) return; // full-dim case is static, set once in Show().

            // World target behind the camera → just dim everything, no hole.
            if (!TutorialScreenPositioner.IsInFront(m_Target, m_WorldCamera))
            {
                if (m_DimEnabled) ShowFullDim();
                if (m_Ring != null) m_Ring.gameObject.SetActive(false);
                return;
            }
            if (m_RingEnabled && m_Ring != null && !m_Ring.gameObject.activeSelf)
                m_Ring.gameObject.SetActive(true);

            // Convert the target's screen-space box into local rect on this overlay.
            Rect screenRect = TutorialScreenPositioner.GetScreenRect(m_Target, m_WorldCamera);
            if (!ScreenToLocal(new Vector2(screenRect.xMin, screenRect.yMin), out Vector2 min) ||
                !ScreenToLocal(new Vector2(screenRect.xMax, screenRect.yMax), out Vector2 max))
                return;

            // The spotlight hole uses the step's padding; the ring sizes from the RAW target box so
            // its own size options aren't entangled with the spotlight padding.
            Vector2 holeMin = min - new Vector2(m_Padding, m_Padding);
            Vector2 holeMax = max + new Vector2(m_Padding, m_Padding);

            if (m_DimEnabled) LayoutStrips(holeMin, holeMax);
            if (m_RingEnabled) LayoutRing(min, max);
        }

        private bool ScreenToLocal(Vector2 screen, out Vector2 local) =>
            RectTransformUtility.ScreenPointToLocalPointInRectangle(m_Rect, screen, null, out local);

        /// <summary>Frames the hole [min,max] with the four strips.</summary>
        private void LayoutStrips(Vector2 min, Vector2 max)
        {
            float hw = m_Rect.rect.width * 0.5f;
            float hh = m_Rect.rect.height * 0.5f;

            // Clamp the hole inside the screen so strips never invert.
            min.x = Mathf.Clamp(min.x, -hw, hw); max.x = Mathf.Clamp(max.x, -hw, hw);
            min.y = Mathf.Clamp(min.y, -hh, hh); max.y = Mathf.Clamp(max.y, -hh, hh);

            SetStrip(m_Top,    -hw,  hw, max.y,  hh);   // above the hole
            SetStrip(m_Bottom, -hw,  hw, -hh,  min.y);  // below the hole
            SetStrip(m_Left,   -hw, min.x, min.y, max.y); // left of the hole
            SetStrip(m_Right,  max.x, hw, min.y, max.y);  // right of the hole
        }

        /// <summary>Positions a strip to cover the rectangle [left,right] x [bottom,top] in local space.</summary>
        private static void SetStrip(Image img, float left, float right, float bottom, float top)
        {
            if (img == null) return;
            var rt = (RectTransform)img.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            float w = Mathf.Max(0f, right - left);
            float h = Mathf.Max(0f, top - bottom);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2((left + right) * 0.5f, (bottom + top) * 0.5f);
        }

        private void LayoutRing(Vector2 min, Vector2 max)
        {
            var rt = (RectTransform)m_Ring.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            Vector2 targetSize = max - min; // raw target box (width, height) in canvas px
            Vector2 size = m_RingSizeMode == RingSizeMode.FixedSize
                ? m_RingFixedSize
                : Vector2.Scale(targetSize, m_RingSizeMultiplier) + m_RingPadding * 2f;

            rt.sizeDelta = size;
            rt.anchoredPosition = (min + max) * 0.5f;

            m_Time += Time.unscaledDeltaTime;
            float s = 1f + Mathf.Abs(Mathf.Sin(m_Time * m_PulseSpeed * Mathf.PI)) * m_PulseAmplitude;
            rt.localScale = new Vector3(s, s, 1f);
        }

        /// <summary>Covers the whole screen with the top strip (used when there is no target).</summary>
        private void ShowFullDim()
        {
            SetStripsActive(true);
            float hw = m_Rect.rect.width * 0.5f;
            float hh = m_Rect.rect.height * 0.5f;
            SetStrip(m_Top, -hw, hw, -hh, hh);
            SetStrip(m_Bottom, 0f, 0f, 0f, 0f);
            SetStrip(m_Left, 0f, 0f, 0f, 0f);
            SetStrip(m_Right, 0f, 0f, 0f, 0f);
        }
    }
}
