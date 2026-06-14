using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TutorialSystem
{
    /// <summary>
    /// The instructional popup: a character/mascot icon on the left, a speech bubble with the
    /// message, and an optional "Next" button. Plays a springy scale-in when shown.
    ///
    /// Placement is driven by the step's <see cref="TutorialPopupAnchor"/>:
    ///   • <b>RelativeToTarget</b> — the popup sits at the target's on-screen position plus
    ///     <see cref="m_TargetOffset"/> and <b>follows the target every frame</b>, so it moves to a
    ///     new spot for each step / target (and tracks moving targets). Optionally clamped on-screen.
    ///   • Top / Bottom / Center — fixed positions.
    ///   • Auto — the screen half opposite the target, so the bubble never covers it.
    ///
    /// It is a passive "view": the <see cref="TutorialManager"/> calls <see cref="Initialize"/> once,
    /// then <see cref="Show"/> / <see cref="Hide"/> per step, and listens to <see cref="OnNextClicked"/>.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(CanvasGroup))]
    public class TutorialPopupUI : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Root that gets scaled during the pop-in animation (usually this object).")]
        [SerializeField] private RectTransform m_Content;
        [SerializeField] private Image m_CharacterImage;
        [SerializeField] private TMP_Text m_MessageText;
        [SerializeField] private Button m_NextButton;

        [Header("Animation")]
        [Tooltip("Pop-in duration in seconds (uses unscaled time so it works while paused).")]
        [SerializeField] private float m_PopDuration = 0.28f;

        [Tooltip("How far the scale overshoots 1.0 before settling (springiness).")]
        [SerializeField] private float m_Overshoot = 0.12f;

        [Header("Fixed Anchor Positions (canvas px, from center)")]
        [SerializeField] private Vector2 m_TopPosition = new Vector2(0f, 620f);
        [SerializeField] private Vector2 m_BottomPosition = new Vector2(0f, -620f);
        [SerializeField] private Vector2 m_CenterPosition = Vector2.zero;

        [Header("Relative-To-Target Placement")]
        [Tooltip("When a step's Popup Anchor is RelativeToTarget, the popup is placed at the target's " +
                 "screen position plus this offset (canvas px), and follows the target. Negative Y " +
                 "puts the popup below the target; positive Y above it.")]
        [SerializeField] private Vector2 m_TargetOffset = new Vector2(0f, -300f);

        [Tooltip("Keep the popup fully inside the screen even when the target is near an edge.")]
        [SerializeField] private bool m_ClampToScreen = true;

        [Tooltip("Margin (canvas px) kept between the popup and the screen edge when clamping.")]
        [SerializeField] private float m_ClampMargin = 24f;

        private RectTransform m_Rect;
        private CanvasGroup m_Group;
        private RectTransform m_CanvasRect;
        private Camera m_WorldCamera;
        private TutorialTarget m_Target;
        private TutorialPopupAnchor m_Anchor;
        private Coroutine m_Anim;

        /// <summary>Raised when the Next button is clicked.</summary>
        public event Action OnNextClicked;

        private void Awake()
        {
            m_Rect = (RectTransform)transform;
            m_Group = GetComponent<CanvasGroup>();
            if (m_Content == null) m_Content = m_Rect;
            if (m_NextButton != null)
                m_NextButton.onClick.AddListener(() => OnNextClicked?.Invoke());
            HideImmediate();
        }

        /// <summary>Wires the canvas + world camera so the popup can position relative to targets.</summary>
        public void Initialize(RectTransform canvasRect, Camera worldCamera)
        {
            m_CanvasRect = canvasRect;
            m_WorldCamera = worldCamera;
        }

        /// <summary>
        /// Shows the popup with the given content and plays the pop-in.
        /// </summary>
        /// <param name="character">Mascot sprite (null hides the character image).</param>
        /// <param name="message">Body text (TMP rich text supported).</param>
        /// <param name="anchor">Where to place the popup.</param>
        /// <param name="target">The step's target (used by RelativeToTarget / Auto), or null.</param>
        /// <param name="showNextButton">Whether the Next button is shown for this step.</param>
        public void Show(Sprite character, string message, TutorialPopupAnchor anchor,
                         TutorialTarget target, bool showNextButton)
        {
            gameObject.SetActive(true);
            m_Target = target;
            m_Anchor = anchor;

            if (m_CharacterImage != null)
            {
                m_CharacterImage.gameObject.SetActive(character != null);
                m_CharacterImage.sprite = character;
            }
            if (m_MessageText != null) m_MessageText.text = message;
            if (m_NextButton != null) m_NextButton.gameObject.SetActive(showNextButton);

            Reposition(); // place it for THIS target before the pop-in starts.

            m_Group.interactable = true;
            m_Group.blocksRaycasts = true;

            if (m_Anim != null) StopCoroutine(m_Anim);
            m_Anim = StartCoroutine(PopIn());
        }

        /// <summary>Hides the popup instantly.</summary>
        public void Hide() => HideImmediate();

        private void LateUpdate()
        {
            // Follow the target every frame when placed relative to it (handles moving/animating
            // targets; the per-step Show() already re-anchors when the target changes).
            if (m_Anchor == TutorialPopupAnchor.RelativeToTarget && m_Target != null)
                Reposition();
        }

        private void Reposition() => m_Rect.anchoredPosition = ResolvePosition();

        private Vector2 ResolvePosition()
        {
            if (m_Anchor == TutorialPopupAnchor.RelativeToTarget)
            {
                if (m_Target == null || m_CanvasRect == null) return m_CenterPosition;
                Vector2 screen = TutorialScreenPositioner.GetScreenPoint(m_Target, m_WorldCamera);
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        m_CanvasRect, screen, null, out Vector2 local))
                    return m_CenterPosition;
                Vector2 pos = local + m_TargetOffset;
                return m_ClampToScreen ? Clamp(pos) : pos;
            }

            switch (m_Anchor)
            {
                case TutorialPopupAnchor.Top:    return m_TopPosition;
                case TutorialPopupAnchor.Bottom: return m_BottomPosition;
                case TutorialPopupAnchor.Center: return m_CenterPosition;
                default: // Auto: opposite half from the target so the bubble never covers it.
                    return TargetScreenY() < Screen.height * 0.5f ? m_TopPosition : m_BottomPosition;
            }
        }

        private float TargetScreenY()
        {
            if (m_Target == null) return Screen.height * 0.5f;
            return TutorialScreenPositioner.GetScreenPoint(m_Target, m_WorldCamera).y;
        }

        /// <summary>Keeps the popup fully inside the canvas, accounting for its size.</summary>
        private Vector2 Clamp(Vector2 pos)
        {
            if (m_CanvasRect == null) return pos;
            Vector2 half = new Vector2(m_CanvasRect.rect.width, m_CanvasRect.rect.height) * 0.5f;
            Vector2 size = (m_Content != null ? m_Content.rect.size : m_Rect.rect.size);

            float minX = -half.x + m_ClampMargin + size.x * 0.5f;
            float maxX =  half.x - m_ClampMargin - size.x * 0.5f;
            float minY = -half.y + m_ClampMargin + size.y * 0.5f;
            float maxY =  half.y - m_ClampMargin - size.y * 0.5f;

            // If the popup is larger than the screen on an axis, just center it there.
            if (minX > maxX) minX = maxX = 0f;
            if (minY > maxY) minY = maxY = 0f;
            return new Vector2(Mathf.Clamp(pos.x, minX, maxX), Mathf.Clamp(pos.y, minY, maxY));
        }

        private IEnumerator PopIn()
        {
            float t = 0f;
            m_Group.alpha = 0f;
            while (t < m_PopDuration)
            {
                t += Time.unscaledDeltaTime;
                float n = Mathf.Clamp01(t / m_PopDuration);
                float eased = EaseOutBack(n, m_Overshoot); // ease-out-back for a little bounce.
                m_Content.localScale = Vector3.one * eased;
                m_Group.alpha = Mathf.Clamp01(n * 1.5f);
                yield return null;
            }
            m_Content.localScale = Vector3.one;
            m_Group.alpha = 1f;
            m_Anim = null;
        }

        private static float EaseOutBack(float x, float overshoot)
        {
            float c1 = 1.70158f * (1f + overshoot * 6f);
            float c3 = c1 + 1f;
            float inv = x - 1f;
            return 1f + c3 * inv * inv * inv + c1 * inv * inv;
        }

        private void HideImmediate()
        {
            if (m_Anim != null) { StopCoroutine(m_Anim); m_Anim = null; }
            if (m_Group == null) m_Group = GetComponent<CanvasGroup>();
            m_Group.alpha = 0f;
            m_Group.interactable = false;
            m_Group.blocksRaycasts = false;
            m_Target = null;
            gameObject.SetActive(false);
        }
    }
}
