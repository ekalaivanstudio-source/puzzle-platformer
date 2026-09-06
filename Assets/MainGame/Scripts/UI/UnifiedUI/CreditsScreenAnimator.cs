using System;
using System.Collections;
using UnityEngine;
using MainGame.UI.Animation;

namespace MainGame.UI.Unified
{
    /// <summary>
    /// Animates the Credits screen entrance and exit with deliberate speed contrast:
    /// Acts as visual rest with a calm, smooth entrance:
    /// - 0.50s smooth panel slide from offscreen right (+850px)
    /// - 0.45s CREDITS signboard physical drop and damped pendulum oscillation
    /// - 0.65s vertical content reveal drift
    /// - Soft back button landing
    /// - Ambient rope breeze sway
    /// - Complete out-of-screen exit (> 850px)
    /// </summary>
    [DisallowMultipleComponent]
    public class CreditsScreenAnimator : MonoBehaviour
    {
        [Header("Panel Background")]
        [SerializeField] private RectTransform m_PanelBackground;
        [SerializeField] private float m_PanelSlideDistance = 850f;
        [SerializeField] private float m_PanelDuration = 0.50f;

        [Header("Header Elements")]
        [Tooltip("CREDITS signboard entering from upper-right with physical pendulum decay.")]
        [SerializeField] private RectTransform m_CreditsSign;

        [Header("Content Container")]
        [Tooltip("Container holding the credits lines/content to reveal vertically.")]
        [SerializeField] private RectTransform m_ContentContainer;

        [Header("Back Button")]
        [SerializeField] private RectTransform m_BackButton;

        private Vector2 m_PanelRestPos;
        private Vector2 m_SignRestPos;
        private Vector3 m_SignRestAngles;
        private Vector2 m_ContentRestPos;
        private Vector2 m_BackRestPos;

        private Coroutine m_ActiveRoutine;
        private Coroutine m_SignBreezeRoutine;
        private bool m_HasCapturedRest = false;

        private void Awake()
        {
            CaptureRestState();
        }

        private void Start()
        {
            CaptureRestState();
        }

        public void CaptureRestState()
        {
            if (m_HasCapturedRest) return;

            if (m_PanelBackground == null)
            {
                Transform bg = transform.Find("BG") ?? transform;
                if (bg != null) m_PanelBackground = bg as RectTransform;
            }

            if (m_CreditsSign == null)
            {
                Transform sign = transform.Find("BG/Holder/Setting IMG") ?? transform.Find("Holder/Setting IMG") ?? transform.Find("Setting IMG");
                if (sign != null) m_CreditsSign = sign as RectTransform;
            }

            if (m_ContentContainer == null)
            {
                Transform content = transform.Find("BG/Holder/Controlles Holder") ?? transform.Find("Holder/Controlles Holder") ?? transform.Find("Controlles Holder");
                if (content != null) m_ContentContainer = content as RectTransform;
            }

            if (m_BackButton == null)
            {
                Transform back = transform.Find("BG/Holder/Back B") ?? transform.Find("Holder/Back B") ?? transform.Find("Back B") ?? transform.Find("BG/Holder/B Back");
                if (back != null) m_BackButton = back as RectTransform;
            }

            if (m_PanelBackground != null)
            {
                m_PanelRestPos = m_PanelBackground.anchoredPosition;
            }

            if (m_CreditsSign != null)
            {
                m_SignRestPos = m_CreditsSign.anchoredPosition;
                m_SignRestAngles = m_CreditsSign.localEulerAngles;
            }

            if (m_ContentContainer != null)
            {
                m_ContentRestPos = m_ContentContainer.anchoredPosition;
            }

            if (m_BackButton != null)
            {
                m_BackRestPos = m_BackButton.anchoredPosition;
            }

            m_HasCapturedRest = true;
        }

        public void ResetToRestState()
        {
            StopActiveAnimation();
            if (!m_HasCapturedRest) return;

            if (m_PanelBackground != null)
            {
                m_PanelBackground.anchoredPosition = m_PanelRestPos;
                m_PanelBackground.localScale = Vector3.one;
            }

            if (m_CreditsSign != null)
            {
                m_CreditsSign.anchoredPosition = m_SignRestPos;
                m_CreditsSign.localEulerAngles = m_SignRestAngles;
                m_CreditsSign.localScale = Vector3.one;
            }

            if (m_ContentContainer != null)
            {
                m_ContentContainer.anchoredPosition = m_ContentRestPos;
                m_ContentContainer.localScale = Vector3.one;
            }

            if (m_BackButton != null)
            {
                m_BackButton.anchoredPosition = m_BackRestPos;
                m_BackButton.localScale = Vector3.one;
            }
        }

        public void PlayEntrance(Action onComplete)
        {
            StopActiveAnimation();
            CaptureRestState();
            m_ActiveRoutine = StartCoroutine(EntranceRoutine(onComplete));
        }

        public void PlayExit(Action onComplete)
        {
            StopActiveAnimation();
            CaptureRestState();
            m_ActiveRoutine = StartCoroutine(ExitRoutine(onComplete));
        }

        public void StopActiveAnimation()
        {
            if (m_ActiveRoutine != null)
            {
                StopCoroutine(m_ActiveRoutine);
                m_ActiveRoutine = null;
            }

            if (m_SignBreezeRoutine != null)
            {
                StopCoroutine(m_SignBreezeRoutine);
                m_SignBreezeRoutine = null;
            }
        }

        private IEnumerator EntranceRoutine(Action onComplete)
        {
            // 1. Panel slides smoothly from offscreen right (+850px) over 0.50s (calm speed contrast)
            if (m_PanelBackground != null)
            {
                Vector2 startPanel = new Vector2(m_PanelRestPos.x + m_PanelSlideDistance, m_PanelRestPos.y);
                m_PanelBackground.anchoredPosition = startPanel;
                StartCoroutine(AnimateMotion(m_PanelBackground, startPanel, m_PanelRestPos, 0f, 0f, m_PanelDuration, 0f, EasingType.EaseOutCubic, 1f, () =>
                {
                    UIMicroShake.Shake(0.35f, 0.05f);
                }));
            }

            // 2. CREDITS signboard enters over 0.45s from upper-right with decaying pendulum oscillation
            if (m_CreditsSign != null)
            {
                StartCoroutine(SignEntranceRoutine());
            }

            // 3. Back button smoothly arrives from bottom (-150px)
            if (m_BackButton != null)
            {
                Vector2 startBack = new Vector2(m_BackRestPos.x, m_BackRestPos.y - 150f);
                m_BackButton.anchoredPosition = startBack;
                StartCoroutine(AnimateMotion(
                    m_BackButton,
                    startBack, m_BackRestPos,
                    0f, 0f,
                    0.42f, 0.18f,
                    EasingType.EaseOutCubic, 1f
                ));
            }

            // 4. Content elements enter calmly from below with smooth vertical drift (0.65s)
            if (m_ContentContainer != null)
            {
                Vector2 startContent = new Vector2(m_ContentRestPos.x, m_ContentRestPos.y - 120f);
                m_ContentContainer.anchoredPosition = startContent;

                yield return StartCoroutine(AnimateMotion(
                    m_ContentContainer,
                    startContent, m_ContentRestPos,
                    0f, 0f,
                    0.65f, 0.14f,
                    EasingType.EaseOutCubic, 1f
                ));
            }
            else
            {
                float timer = 0f;
                while (timer < m_PanelDuration)
                {
                    timer += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            m_ActiveRoutine = null;
            onComplete?.Invoke();
        }

        private IEnumerator SignEntranceRoutine()
        {
            Vector2 startSign = new Vector2(m_SignRestPos.x + 280f, m_SignRestPos.y + 200f);
            m_CreditsSign.anchoredPosition = startSign;
            m_CreditsSign.localEulerAngles = new Vector3(0f, 0f, 8f);

            float dropDuration = 0.45f;
            float elapsed = 0f;
            while (elapsed < dropDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / dropDuration);
                float ease = UIEasing.Evaluate(EasingType.EaseOutQuad, t);

                if (m_CreditsSign != null)
                {
                    m_CreditsSign.anchoredPosition = Vector2.Lerp(startSign, m_SignRestPos, ease);
                }
                yield return null;
            }

            if (m_CreditsSign != null) m_CreditsSign.anchoredPosition = m_SignRestPos;

            // Damped pendulum decay (swings -8° -> +4.5° -> -2° -> 0°)
            elapsed = 0f;
            float swingDuration = 0.70f;
            while (elapsed < swingDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float decay = Mathf.Exp(-elapsed * 3.5f);
                float angle = -decay * Mathf.Sin(elapsed * 10f) * 8f;

                if (m_CreditsSign != null)
                {
                    m_CreditsSign.localEulerAngles = new Vector3(0f, 0f, m_SignRestAngles.z + angle);
                }
                yield return null;
            }

            if (m_CreditsSign != null) m_CreditsSign.localEulerAngles = m_SignRestAngles;

            // Ambient rope breeze sway
            m_SignBreezeRoutine = StartCoroutine(SignAmbientSwayRoutine());
        }

        private IEnumerator SignAmbientSwayRoutine()
        {
            while (m_CreditsSign != null)
            {
                float angle = Mathf.Sin(Time.unscaledTime * 1.2f) * 0.55f;
                m_CreditsSign.localEulerAngles = new Vector3(0f, 0f, m_SignRestAngles.z + angle);
                yield return null;
            }
        }

        private IEnumerator ExitRoutine(Action onComplete)
        {
            float duration = 0.25f;

            if (m_SignBreezeRoutine != null)
            {
                StopCoroutine(m_SignBreezeRoutine);
                m_SignBreezeRoutine = null;
            }

            // Content retracts upward
            if (m_ContentContainer != null)
            {
                Vector2 targetContent = new Vector2(m_ContentRestPos.x, m_ContentRestPos.y + 400f);
                StartCoroutine(AnimateMotion(m_ContentContainer, m_ContentContainer.anchoredPosition, targetContent, 0f, 0f, duration, 0f, EasingType.EaseInBack, 1.1f));
            }

            // Sign exits upper-right (> 850px)
            if (m_CreditsSign != null)
            {
                Vector2 targetSign = new Vector2(m_SignRestPos.x + 500f, m_SignRestPos.y + 300f);
                StartCoroutine(AnimateMotion(m_CreditsSign, m_CreditsSign.anchoredPosition, targetSign, 0f, 6f, duration, 0f, EasingType.EaseInBack, 1.1f));
            }

            // Back button exits downward (-500px)
            if (m_BackButton != null)
            {
                Vector2 targetBack = new Vector2(m_BackRestPos.x, m_BackRestPos.y - 500f);
                StartCoroutine(AnimateMotion(m_BackButton, m_BackButton.anchoredPosition, targetBack, 0f, 0f, duration, 0f, EasingType.EaseInBack, 1.1f));
            }

            // Panel exits right (+950px)
            if (m_PanelBackground != null)
            {
                Vector2 targetPanel = new Vector2(m_PanelRestPos.x + m_PanelSlideDistance, m_PanelRestPos.y);
                StartCoroutine(AnimateMotion(m_PanelBackground, m_PanelBackground.anchoredPosition, targetPanel, 0f, 0f, duration, 0.03f, EasingType.EaseInBack, 1.1f));
            }

            float timer = 0f;
            while (timer < duration)
            {
                timer += Time.unscaledDeltaTime;
                yield return null;
            }

            m_ActiveRoutine = null;
            onComplete?.Invoke();
        }

        private IEnumerator AnimateMotion(
            RectTransform target,
            Vector2 startPos, Vector2 targetPos,
            float startRotZ, float targetRotZ,
            float duration, float delay,
            EasingType easing, float overshoot,
            Action onDone = null)
        {
            if (delay > 0f)
            {
                float delayTimer = 0f;
                while (delayTimer < delay)
                {
                    delayTimer += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = UIEasing.Evaluate(easing, t, overshoot);

                if (target != null)
                {
                    target.anchoredPosition = Vector2.LerpUnclamped(startPos, targetPos, ease);
                    if (Mathf.Abs(startRotZ - targetRotZ) > 0.01f)
                    {
                        target.localEulerAngles = new Vector3(0f, 0f, Mathf.LerpUnclamped(startRotZ, targetRotZ, ease));
                    }
                }
                yield return null;
            }

            if (target != null)
            {
                target.anchoredPosition = targetPos;
                target.localEulerAngles = new Vector3(0f, 0f, targetRotZ);
            }

            onDone?.Invoke();
        }
    }
}
