using System;
using System.Collections;
using UnityEngine;

namespace MainGame.UI.Animation
{
    /// <summary>
    /// Component managing physical transition motion for a single UI RectTransform.
    /// Captures the resting transform state and executes smooth, allocation-free transitions.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    public class UIAnimatedElement : MonoBehaviour
    {
        [Header("Canvas Group")]
        [Tooltip("Optional CanvasGroup to fade alongside movement. Added automatically if FadeAlpha is enabled.")]
        [SerializeField] private CanvasGroup m_CanvasGroup;

        private RectTransform m_RectTransform;
        private Vector2 m_RestAnchoredPosition;
        private Vector3 m_RestScale = Vector3.one;
        private Vector3 m_RestEulerAngles = Vector3.zero;
        private float m_RestAlpha = 1f;

        private bool m_HasCapturedRestState;
        private Coroutine m_ActiveMotionCoroutine;

        public RectTransform RectTransform
        {
            get
            {
                if (m_RectTransform == null) m_RectTransform = GetComponent<RectTransform>();
                return m_RectTransform;
            }
        }

        public CanvasGroup CanvasGroup
        {
            get
            {
                if (m_CanvasGroup == null) m_CanvasGroup = GetComponent<CanvasGroup>();
                return m_CanvasGroup;
            }
        }

        public Vector2 RestPosition => m_RestAnchoredPosition;
        public Vector3 RestScale => m_RestScale;
        public bool IsAnimating => m_ActiveMotionCoroutine != null;

        private void Awake()
        {
            m_RectTransform = GetComponent<RectTransform>();
            if (m_CanvasGroup == null)
            {
                m_CanvasGroup = GetComponent<CanvasGroup>();
            }
            CaptureRestState();
        }

        private void Start()
        {
            // Re-capture in Start if position might have settled after initial layout pass
            CaptureRestState();
        }

        public void CaptureRestState()
        {
            if (m_HasCapturedRestState && m_RectTransform != null) return;
            if (m_RectTransform == null) m_RectTransform = GetComponent<RectTransform>();

            m_RestAnchoredPosition = m_RectTransform.anchoredPosition;
            m_RestScale = m_RectTransform.localScale;
            m_RestEulerAngles = m_RectTransform.localEulerAngles;

            if (CanvasGroup != null)
            {
                m_RestAlpha = m_CanvasGroup.alpha;
            }

            m_HasCapturedRestState = true;
        }

        public void ResetToRestState()
        {
            StopActiveAnimation();
            if (!m_HasCapturedRestState) return;

            if (m_RectTransform != null)
            {
                m_RectTransform.anchoredPosition = m_RestAnchoredPosition;
                m_RectTransform.localScale = m_RestScale;
                m_RectTransform.localEulerAngles = m_RestEulerAngles;
            }

            if (m_CanvasGroup != null)
            {
                m_CanvasGroup.alpha = m_RestAlpha;
            }
        }

        public void SetInstantState(Vector2 positionOffset, float scaleFactor, float rotationOffsetZ, float alpha)
        {
            StopActiveAnimation();
            CaptureRestState();

            if (m_RectTransform != null)
            {
                m_RectTransform.anchoredPosition = m_RestAnchoredPosition + positionOffset;
                m_RectTransform.localScale = m_RestScale * scaleFactor;
                m_RectTransform.localEulerAngles = new Vector3(
                    m_RestEulerAngles.x,
                    m_RestEulerAngles.y,
                    m_RestEulerAngles.z + rotationOffsetZ
                );
            }

            if (m_CanvasGroup != null)
            {
                m_CanvasGroup.alpha = alpha;
            }
        }

        public Coroutine PlayEnter(UITransitionProfile profile, float extraDelay = 0f, Action onComplete = null)
        {
            CaptureRestState();

            Vector2 offset = profile.CalculateOffset();
            float startScale = profile.StartScale > 0f ? profile.StartScale : 1f;
            float endScale = profile.EndScale > 0f ? profile.EndScale : 1f;

            float startAlpha = profile.FadeAlpha ? 0f : (m_CanvasGroup != null ? m_CanvasGroup.alpha : 1f);
            float endAlpha = m_RestAlpha;

            // Ensure CanvasGroup exists if fading
            if (profile.FadeAlpha && m_CanvasGroup == null)
            {
                m_CanvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            // Set initial entering pose
            SetInstantState(offset, startScale, profile.RotationOffset, startAlpha);

            return PlayMotion(
                startPos: m_RestAnchoredPosition + offset,
                targetPos: m_RestAnchoredPosition,
                startScale: m_RestScale * startScale,
                targetScale: m_RestScale * endScale,
                startRotZ: m_RestEulerAngles.z + profile.RotationOffset,
                targetRotZ: m_RestEulerAngles.z,
                startAlpha: startAlpha,
                targetAlpha: endAlpha,
                duration: profile.Duration,
                delay: profile.Delay + extraDelay,
                easing: profile.Easing,
                overshoot: profile.Overshoot,
                onComplete: onComplete
            );
        }

        public Coroutine PlayExit(UITransitionProfile profile, float extraDelay = 0f, Action onComplete = null)
        {
            CaptureRestState();

            Vector2 offset = profile.CalculateOffset();
            float targetScale = profile.EndScale > 0f ? profile.EndScale : 1f;
            float targetAlpha = profile.FadeAlpha ? 0f : (m_CanvasGroup != null ? m_CanvasGroup.alpha : 1f);

            if (profile.FadeAlpha && m_CanvasGroup == null)
            {
                m_CanvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            Vector2 currentPos = m_RectTransform != null ? m_RectTransform.anchoredPosition : m_RestAnchoredPosition;
            Vector3 currentScale = m_RectTransform != null ? m_RectTransform.localScale : m_RestScale;
            float currentRotZ = m_RectTransform != null ? m_RectTransform.localEulerAngles.z : m_RestEulerAngles.z;
            float currentAlpha = m_CanvasGroup != null ? m_CanvasGroup.alpha : 1f;

            return PlayMotion(
                startPos: currentPos,
                targetPos: m_RestAnchoredPosition + offset,
                startScale: currentScale,
                targetScale: m_RestScale * targetScale,
                startRotZ: currentRotZ,
                targetRotZ: m_RestEulerAngles.z + profile.RotationOffset,
                startAlpha: currentAlpha,
                targetAlpha: targetAlpha,
                duration: profile.Duration,
                delay: profile.Delay + extraDelay,
                easing: profile.Easing,
                overshoot: profile.Overshoot,
                onComplete: onComplete
            );
        }

        public Coroutine PlayMotion(
            Vector2 startPos,
            Vector2 targetPos,
            Vector3 startScale,
            Vector3 targetScale,
            float startRotZ,
            float targetRotZ,
            float startAlpha,
            float targetAlpha,
            float duration,
            float delay,
            EasingType easing,
            float overshoot,
            Action onComplete = null)
        {
            StopActiveAnimation();
            m_ActiveMotionCoroutine = StartCoroutine(MotionRoutine(
                startPos, targetPos,
                startScale, targetScale,
                startRotZ, targetRotZ,
                startAlpha, targetAlpha,
                duration, delay,
                easing, overshoot,
                onComplete
            ));
            return m_ActiveMotionCoroutine;
        }

        public void StopActiveAnimation()
        {
            if (m_ActiveMotionCoroutine != null)
            {
                StopCoroutine(m_ActiveMotionCoroutine);
                m_ActiveMotionCoroutine = null;
            }
        }

        private IEnumerator MotionRoutine(
            Vector2 startPos,
            Vector2 targetPos,
            Vector3 startScale,
            Vector3 targetScale,
            float startRotZ,
            float targetRotZ,
            float startAlpha,
            float targetAlpha,
            float duration,
            float delay,
            EasingType easing,
            float overshoot,
            Action onComplete)
        {
            if (delay > 0f)
            {
                float delayElapsed = 0f;
                while (delayElapsed < delay)
                {
                    delayElapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            if (duration <= 0f)
            {
                ApplyState(targetPos, targetScale, targetRotZ, targetAlpha);
                m_ActiveMotionCoroutine = null;
                onComplete?.Invoke();
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = UIEasing.Evaluate(easing, t, overshoot);

                Vector2 currentPos = Vector2.LerpUnclamped(startPos, targetPos, ease);
                Vector3 currentScale = Vector3.LerpUnclamped(startScale, targetScale, ease);
                float currentRotZ = Mathf.LerpUnclamped(startRotZ, targetRotZ, ease);
                float currentAlpha = Mathf.Lerp(startAlpha, targetAlpha, t);

                ApplyState(currentPos, currentScale, currentRotZ, currentAlpha);
                yield return null;
            }

            ApplyState(targetPos, targetScale, targetRotZ, targetAlpha);
            m_ActiveMotionCoroutine = null;
            onComplete?.Invoke();
        }

        private void ApplyState(Vector2 pos, Vector3 scale, float rotZ, float alpha)
        {
            if (m_RectTransform != null)
            {
                m_RectTransform.anchoredPosition = pos;
                m_RectTransform.localScale = scale;
                m_RectTransform.localEulerAngles = new Vector3(
                    m_RestEulerAngles.x,
                    m_RestEulerAngles.y,
                    rotZ
                );
            }

            if (m_CanvasGroup != null)
            {
                m_CanvasGroup.alpha = alpha;
            }
        }

        private void OnDisable()
        {
            StopActiveAnimation();
        }
    }
}
