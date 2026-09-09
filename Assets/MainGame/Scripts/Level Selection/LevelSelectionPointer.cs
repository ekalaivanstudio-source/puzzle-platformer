using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace LevelSelection
{
    /// <summary>
    /// Single authoritative level selection pointer / indicator for the Level Selection screen.
    /// Replaces the per-node duplicate arrows with one managed instance that flies smoothly
    /// between nodes on navigation, drops from above with a physical spring bounce upon map entrance,
    /// and resets cleanly without transform or tween drift.
    /// </summary>
    [DisallowMultipleComponent]
    public class LevelSelectionPointer : MonoBehaviour
    {
        #region Inspector Fields

        [Header("Visual Components")]
        [SerializeField] private Image pointerImage;
        [SerializeField] private RectTransform rectTransform;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Offset & Positioning")]
        [Tooltip("Vertical pixel offset above the target node center.")]
        [SerializeField] private float verticalOffset = 52f;

        [Header("Bounce & Bob")]
        [Tooltip("Idle floating bob amplitude in local units.")]
        [SerializeField] private float bobAmplitude = 5f;
        [Tooltip("Idle floating bob frequency in cycles/sec.")]
        [SerializeField] private float bobSpeed = 5.5f;

        [Header("Motion Settings")]
        [Tooltip("Glide duration when navigating between nodes.")]
        [SerializeField] private float glideDuration = 0.10f;

        #endregion

        #region Private Fields

        private LevelNodeUI m_CurrentTargetNode;
        private Coroutine m_MotionCoroutine;
        private Coroutine m_BobCoroutine;
        private Vector3 m_RestLocalPos;
        private Vector3 m_BaseScale = Vector3.one;
        private bool m_IsVisible = false;

        #endregion

        #region Properties

        public LevelNodeUI CurrentTargetNode => m_CurrentTargetNode;
        public bool IsVisible => m_IsVisible;
        public RectTransform RectTransform
        {
            get
            {
                if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
                return rectTransform;
            }
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            EnsureComponents();
        }

        private void OnDisable()
        {
            ResetPointerState();
        }

        #endregion

        #region Initialization & Setup

        public void EnsureComponents()
        {
            if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null && gameObject != null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
            if (pointerImage == null) pointerImage = GetComponent<Image>();
            if (pointerImage == null && gameObject != null) pointerImage = GetComponentInChildren<Image>(true);

            if (rectTransform != null)
            {
                rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                rectTransform.pivot = new Vector2(0.5f, 0.5f);
            }

            if (pointerImage != null)
            {
                pointerImage.raycastTarget = false;
            }
        }

        public void SetSprite(Sprite sprite)
        {
            EnsureComponents();
            if (pointerImage != null && sprite != null)
            {
                pointerImage.sprite = sprite;
                pointerImage.SetNativeSize();
            }
        }

        #endregion

        #region State Reset

        /// <summary>
        /// Instantly hides the pointer, stops all running animations, and clears target references.
        /// Guaranteed clean slate for screen opening, reopening, and arc transitions.
        /// </summary>
        public void ResetPointerState()
        {
            if (m_MotionCoroutine != null)
            {
                StopCoroutine(m_MotionCoroutine);
                m_MotionCoroutine = null;
            }

            if (m_BobCoroutine != null)
            {
                StopCoroutine(m_BobCoroutine);
                m_BobCoroutine = null;
            }

            m_CurrentTargetNode = null;
            m_IsVisible = false;

            if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }

            if (rectTransform != null)
            {
                rectTransform.localScale = Vector3.zero;
                rectTransform.localRotation = Quaternion.identity;
            }

            gameObject.SetActive(false);
        }

        #endregion

        #region Positioning Calculation

        /// <summary>
        /// Computes the precise local position for the pointer within its parent RectTransform,
        /// relative to the given node's world center + verticalOffset.
        /// Eliminates drift across different anchors, canvas scalers, and screen resolutions.
        /// </summary>
        public Vector3 CalculateTargetLocalPos(LevelNodeUI node)
        {
            if (node == null) return Vector3.zero;

            RectTransform nodeRect = node.RectTransform;
            RectTransform parentRect = transform.parent as RectTransform;

            if (nodeRect == null) return Vector3.zero;

            // Compute center of the node in world space
            Vector3 worldCenter = nodeRect.TransformPoint(nodeRect.rect.center);

            // Convert to parent local space
            Vector3 localPos = parentRect != null ? parentRect.InverseTransformPoint(worldCenter) : node.transform.localPosition;

            return new Vector3(localPos.x, localPos.y + verticalOffset, 0f);
        }

        #endregion

        #region Entrance & Drop

        /// <summary>
        /// Drops the pointer from above (+48px) onto the target node with a spring bounce.
        /// Called at the end of the Map Network Activation sequence.
        /// </summary>
        public void PlayEntranceDrop(LevelNodeUI targetNode, Action onDone = null)
        {
            if (targetNode == null)
            {
                onDone?.Invoke();
                return;
            }

            EnsureComponents();
            ResetPointerState();

            m_CurrentTargetNode = targetNode;
            m_IsVisible = true;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();

            Vector3 targetLocalPos = CalculateTargetLocalPos(targetNode);
            m_RestLocalPos = targetLocalPos;

            if (m_MotionCoroutine != null) StopCoroutine(m_MotionCoroutine);
            m_MotionCoroutine = StartCoroutine(EntranceDropRoutine(targetLocalPos, onDone));
        }

        private IEnumerator EntranceDropRoutine(Vector3 targetLocalPos, Action onDone)
        {
            Vector3 startPos = targetLocalPos + new Vector3(0f, 48f, 0f);
            Vector3 startScale = m_BaseScale * 1.35f;
            Quaternion startRot = Quaternion.Euler(0f, 0f, -8f);

            rectTransform.localPosition = startPos;
            rectTransform.localScale = startScale;
            rectTransform.localRotation = startRot;

            if (canvasGroup != null) canvasGroup.alpha = 0f;

            float duration = 0.28f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Alpha fade in quickly over first 35% of duration
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = Mathf.Clamp01(t / 0.35f);
                }

                // Spring bounce on Y
                float yOffset;
                if (t < 0.5f)
                {
                    yOffset = Mathf.Lerp(48f, -4f, EaseInQuad(t / 0.5f));
                }
                else if (t < 0.75f)
                {
                    yOffset = Mathf.Lerp(-4f, 2f, EaseOutQuad((t - 0.5f) / 0.25f));
                }
                else
                {
                    yOffset = Mathf.Lerp(2f, 0f, EaseInOutQuad((t - 0.75f) / 0.25f));
                }

                rectTransform.localPosition = new Vector3(targetLocalPos.x, targetLocalPos.y + yOffset, targetLocalPos.z);
                rectTransform.localScale = Vector3.Lerp(startScale, m_BaseScale, EaseOutQuad(t));
                rectTransform.localRotation = Quaternion.Slerp(startRot, Quaternion.identity, EaseOutQuad(t));

                yield return null;
            }

            rectTransform.localPosition = targetLocalPos;
            rectTransform.localScale = m_BaseScale;
            rectTransform.localRotation = Quaternion.identity;
            if (canvasGroup != null) canvasGroup.alpha = 1f;

            m_MotionCoroutine = null;

            // Start continuous subtle bob
            StartBobbing(targetLocalPos);

            onDone?.Invoke();
        }

        #endregion

        #region Navigation Flight / Glide

        /// <summary>
        /// Smoothly glides the pointer from its current position to the newly selected node.
        /// </summary>
        public void MoveToNode(LevelNodeUI targetNode, float customDuration = -1f)
        {
            if (targetNode == null) return;
            if (m_CurrentTargetNode == targetNode && m_IsVisible) return;

            EnsureComponents();

            // If not currently visible, perform clean drop instead of gliding from far away
            if (!m_IsVisible || !gameObject.activeSelf)
            {
                PlayEntranceDrop(targetNode);
                return;
            }

            m_CurrentTargetNode = targetNode;
            transform.SetAsLastSibling();

            Vector3 newTargetPos = CalculateTargetLocalPos(targetNode);
            m_RestLocalPos = newTargetPos;

            if (m_BobCoroutine != null)
            {
                StopCoroutine(m_BobCoroutine);
                m_BobCoroutine = null;
            }

            if (m_MotionCoroutine != null) StopCoroutine(m_MotionCoroutine);
            float duration = customDuration > 0f ? customDuration : glideDuration;
            m_MotionCoroutine = StartCoroutine(GlideToNodeRoutine(newTargetPos, duration));
        }

        private IEnumerator GlideToNodeRoutine(Vector3 targetPos, float duration)
        {
            Vector3 startPos = rectTransform.localPosition;
            Vector3 squashScale = new Vector3(m_BaseScale.x * 1.15f, m_BaseScale.y * 0.88f, m_BaseScale.z);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = EaseOutQuad(t);

                rectTransform.localPosition = Vector3.Lerp(startPos, targetPos, ease);

                // Subtle squash midway through flight, restoring at destination
                if (t < 0.5f)
                {
                    rectTransform.localScale = Vector3.Lerp(m_BaseScale, squashScale, t / 0.5f);
                }
                else
                {
                    rectTransform.localScale = Vector3.Lerp(squashScale, m_BaseScale, (t - 0.5f) / 0.5f);
                }

                yield return null;
            }

            rectTransform.localPosition = targetPos;
            rectTransform.localScale = m_BaseScale;
            rectTransform.localRotation = Quaternion.identity;

            m_MotionCoroutine = null;
            StartBobbing(targetPos);
        }

        #endregion

        #region Idle Bobbing

        private void StartBobbing(Vector3 basePos)
        {
            if (m_BobCoroutine != null) StopCoroutine(m_BobCoroutine);
            m_RestLocalPos = basePos;
            if (isActiveAndEnabled && m_IsVisible)
            {
                m_BobCoroutine = StartCoroutine(BobRoutine());
            }
        }

        private IEnumerator BobRoutine()
        {
            float elapsed = 0f;
            while (m_IsVisible && gameObject.activeInHierarchy)
            {
                elapsed += Time.unscaledDeltaTime;
                float wave = Mathf.Sin(elapsed * bobSpeed);
                float yBob = wave * bobAmplitude;

                // Subtle scale pulse in sync with bob
                float scalePulse = 1f + wave * 0.03f;

                if (rectTransform != null)
                {
                    rectTransform.localPosition = new Vector3(m_RestLocalPos.x, m_RestLocalPos.y + yBob, m_RestLocalPos.z);
                    rectTransform.localScale = m_BaseScale * scalePulse;
                }

                yield return null;
            }
            m_BobCoroutine = null;
        }

        #endregion

        #region Easing Helpers

        private static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);
        private static float EaseInQuad(float t) => t * t;
        private static float EaseInOutQuad(float t) => t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;

        #endregion
    }
}
