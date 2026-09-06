using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using MainGame.UI.Animation;

namespace MainGame.UI.Unified
{
    /// <summary>
    /// Animates the Exit Confirmation Dialog with physical ceiling drop slam and opposing tilted buttons:
    /// - Dialog drops from ceiling (Y = +500px), slams down, overshoots downward by -15px, rebounds, and settles.
    /// - Scale sequence during impact: 0.90 -> 1.05 -> 1.00.
    /// - Micro-shake triggers on slam (1.35f magnitude).
    /// - YES button slides in from left (-300px, -5.5° tilt).
    /// - NO button slides in from right (+300px, +5.5° tilt).
    /// - Buttons straighten on impact.
    /// - Complete out-of-screen exits.
    /// </summary>
    [DisallowMultipleComponent]
    public class ConfirmationPopupAnimator : MonoBehaviour
    {
        [Header("Darkening Overlay")]
        [Tooltip("Background dimmer image or canvas group.")]
        [SerializeField] private CanvasGroup m_DarkOverlay;
        [SerializeField] private float m_OverlayMaxAlpha = 0.70f;

        [Header("Dialog Window")]
        [Tooltip("The main dialog box transform that scales and pops.")]
        [SerializeField] private RectTransform m_DialogWindow;
        [SerializeField] private float m_CeilingDropDistance = 600f;
        [SerializeField] private float m_DialogDuration = 0.28f;

        [Header("Header / Title")]
        [Tooltip("EXIT header image that lands slightly before buttons.")]
        [SerializeField] private RectTransform m_TitleImage;

        [Header("Opposing Buttons")]
        [Tooltip("YES blue button entering from left with tilt.")]
        [SerializeField] private RectTransform m_YesButton;
        [Tooltip("NO red button entering from right with opposing tilt.")]
        [SerializeField] private RectTransform m_NoButton;
        [SerializeField] private float m_ButtonOffset = 450f;

        private Vector2 m_DialogRestPos;
        private Vector3 m_DialogRestScale = Vector3.one;

        private Vector2 m_TitleRestPos;
        private Vector2 m_YesRestPos;
        private Vector2 m_NoRestPos;

        private Coroutine m_ActiveRoutine;
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

            if (m_DarkOverlay == null)
            {
                m_DarkOverlay = GetComponent<CanvasGroup>();
            }

            if (m_DialogWindow == null)
            {
                Transform dialog = transform.Find("Dialog");
                if (dialog != null) m_DialogWindow = dialog as RectTransform;
            }

            if (m_TitleImage == null)
            {
                Transform title = transform.Find("Dialog/Message") ?? transform.Find("Dialog/Title");
                if (title != null) m_TitleImage = title as RectTransform;
            }

            if (m_YesButton == null)
            {
                Transform yes = transform.Find("Dialog/YesButton");
                if (yes != null) m_YesButton = yes as RectTransform;
            }

            if (m_NoButton == null)
            {
                Transform no = transform.Find("Dialog/NoButton");
                if (no != null) m_NoButton = no as RectTransform;
            }

            if (m_DialogWindow != null)
            {
                m_DialogRestPos = m_DialogWindow.anchoredPosition;
                m_DialogRestScale = m_DialogWindow.localScale;
            }

            if (m_TitleImage != null)
            {
                m_TitleRestPos = m_TitleImage.anchoredPosition;
            }

            if (m_YesButton != null)
            {
                m_YesRestPos = m_YesButton.anchoredPosition;
            }

            if (m_NoButton != null)
            {
                m_NoRestPos = m_NoButton.anchoredPosition;
            }

            m_HasCapturedRest = true;
        }

        public void ResetToRestState()
        {
            StopActiveAnimation();
            if (!m_HasCapturedRest) return;

            if (m_DarkOverlay != null)
            {
                m_DarkOverlay.alpha = 0f;
            }

            if (m_DialogWindow != null)
            {
                m_DialogWindow.anchoredPosition = m_DialogRestPos;
                m_DialogWindow.localScale = m_DialogRestScale;
            }

            if (m_TitleImage != null)
            {
                m_TitleImage.anchoredPosition = m_TitleRestPos;
                m_TitleImage.localScale = Vector3.one;
            }

            if (m_YesButton != null)
            {
                m_YesButton.anchoredPosition = m_YesRestPos;
                m_YesButton.localScale = Vector3.one;
                m_YesButton.localEulerAngles = Vector3.zero;
            }

            if (m_NoButton != null)
            {
                m_NoButton.anchoredPosition = m_NoRestPos;
                m_NoButton.localScale = Vector3.one;
                m_NoButton.localEulerAngles = Vector3.zero;
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
        }

        private IEnumerator EntranceRoutine(Action onComplete)
        {
            // 1. Dark overlay fade in rapidly
            if (m_DarkOverlay != null)
            {
                m_DarkOverlay.alpha = 0f;
                StartCoroutine(AnimateOverlayAlpha(0f, m_OverlayMaxAlpha, 0.18f));
            }

            // 2. Dialog ceiling drop slam (Y = +500px -> -15px overshoot -> rebound -> settle)
            if (m_DialogWindow != null)
            {
                yield return StartCoroutine(DialogCeilingSlamRoutine());
            }

            // 3. Title image micro bounce
            if (m_TitleImage != null)
            {
                Vector2 startTitle = new Vector2(m_TitleRestPos.x, m_TitleRestPos.y + 40f);
                m_TitleImage.anchoredPosition = startTitle;
                StartCoroutine(AnimateMotion(m_TitleImage, startTitle, m_TitleRestPos, 0f, 0f, 0.20f, 0f, EasingType.EaseOutBack, 1.25f));
            }

            // 4. Opposing YES and NO buttons:
            // YES from left (-300px, -5.5° tilt)
            if (m_YesButton != null)
            {
                Vector2 startYes = new Vector2(m_YesRestPos.x - m_ButtonOffset, m_YesRestPos.y);
                m_YesButton.anchoredPosition = startYes;
                m_YesButton.localScale = new Vector3(0.90f, 0.90f, 1f);
                m_YesButton.localEulerAngles = new Vector3(0f, 0f, -5.5f);

                StartCoroutine(AnimateButtonPhysicalLanding(
                    m_YesButton,
                    startYes, m_YesRestPos,
                    -5.5f, 0f,
                    0.26f, 0.02f,
                    new Vector2(18f, 0f)
                ));
            }

            // NO from right (+450px, +5.5° tilt, distinct duration: 0.34s)
            if (m_NoButton != null)
            {
                Vector2 startNo = new Vector2(m_NoRestPos.x + m_ButtonOffset, m_NoRestPos.y);
                m_NoButton.anchoredPosition = startNo;
                m_NoButton.localScale = new Vector3(0.90f, 0.90f, 1f);
                m_NoButton.localEulerAngles = new Vector3(0f, 0f, 5.5f);

                yield return StartCoroutine(AnimateButtonPhysicalLanding(
                    m_NoButton,
                    startNo, m_NoRestPos,
                    5.5f, 0f,
                    0.34f, 0.04f,
                    new Vector2(-18f, 0f)
                ));
            }
            else
            {
                float timer = 0f;
                while (timer < 0.28f)
                {
                    timer += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            m_ActiveRoutine = null;
            onComplete?.Invoke();
        }

        private IEnumerator DialogCeilingSlamRoutine()
        {
            // Drops from ceiling (+500px)
            Vector2 startPos = new Vector2(m_DialogRestPos.x, m_DialogRestPos.y + m_CeilingDropDistance);
            Vector2 overshootPos = new Vector2(m_DialogRestPos.x, m_DialogRestPos.y - 15f);
            m_DialogWindow.anchoredPosition = startPos;
            m_DialogWindow.localScale = new Vector3(0.92f, 0.92f, 1f);

            // Phase 1: Rapid drop down from ceiling (0.22s)
            float dropDuration = m_DialogDuration * 0.78f;
            float elapsed = 0f;
            while (elapsed < dropDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / dropDuration);
                float ease = UIEasing.Evaluate(EasingType.EaseInQuad, t);

                if (m_DialogWindow != null)
                {
                    m_DialogWindow.anchoredPosition = Vector2.Lerp(startPos, overshootPos, ease);
                }
                yield return null;
            }

            // Slam impact!
            if (m_DialogWindow != null) m_DialogWindow.anchoredPosition = overshootPos;
            UIMicroShake.Shake(1.35f, 0.09f);

            // Phase 2: Landing compression sequence:
            // Scale: 0.90 -> 1.05 -> 0.98 -> 1.00
            // Position: -15px -> +4px -> 0px
            Vector2 reboundPos = new Vector2(m_DialogRestPos.x, m_DialogRestPos.y + 4f);
            Vector3 squishScale = new Vector3(m_DialogRestScale.x * 1.08f, m_DialogRestScale.y * 0.90f, 1f);
            Vector3 stretchScale = new Vector3(m_DialogRestScale.x * 0.96f, m_DialogRestScale.y * 1.05f, 1f);
            Vector3 reboundScale = new Vector3(m_DialogRestScale.x * 1.01f, m_DialogRestScale.y * 0.98f, 1f);

            // Squish on overshoot to 0.90 (0.05s)
            elapsed = 0f;
            float s1 = 0.05f;
            while (elapsed < s1)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / s1);
                if (m_DialogWindow != null)
                {
                    m_DialogWindow.localScale = Vector3.Lerp(m_DialogRestScale, squishScale, t);
                }
                yield return null;
            }

            // Rebound up to +4px and stretch to 1.05 (0.07s)
            elapsed = 0f;
            float s2 = 0.07f;
            while (elapsed < s2)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / s2);
                if (m_DialogWindow != null)
                {
                    m_DialogWindow.anchoredPosition = Vector2.Lerp(overshootPos, reboundPos, t);
                    m_DialogWindow.localScale = Vector3.Lerp(squishScale, stretchScale, t);
                }
                yield return null;
            }

            // Rebound settle to 0.98 (0.05s)
            elapsed = 0f;
            float s3 = 0.05f;
            while (elapsed < s3)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / s3);
                if (m_DialogWindow != null)
                {
                    m_DialogWindow.anchoredPosition = Vector2.Lerp(reboundPos, m_DialogRestPos, t);
                    m_DialogWindow.localScale = Vector3.Lerp(stretchScale, reboundScale, t);
                }
                yield return null;
            }

            // Settle to rest position and scale 1.00 (0.04s)
            elapsed = 0f;
            float s4 = 0.04f;
            while (elapsed < s4)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / s4);
                if (m_DialogWindow != null)
                {
                    m_DialogWindow.localScale = Vector3.Lerp(reboundScale, m_DialogRestScale, t);
                }
                yield return null;
            }

            if (m_DialogWindow != null)
            {
                m_DialogWindow.anchoredPosition = m_DialogRestPos;
                m_DialogWindow.localScale = m_DialogRestScale;
            }
        }

        private IEnumerator AnimateButtonPhysicalLanding(
            RectTransform target,
            Vector2 startPos, Vector2 restPos,
            float startRotZ, float targetRotZ,
            float duration, float delay,
            Vector2 overshootOffset)
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

            if (target == null) yield break;

            Vector2 overshootPos = restPos + overshootOffset;
            float travelDur = duration * 0.70f;
            float elapsed = 0f;

            // Travel & straighten
            while (elapsed < travelDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / travelDur);
                float ease = UIEasing.Evaluate(EasingType.EaseOutQuad, t);

                if (target != null)
                {
                    target.anchoredPosition = Vector2.Lerp(startPos, overshootPos, ease);
                    target.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(startRotZ, targetRotZ, ease));
                }
                yield return null;
            }

            // Settle sequence
            Vector2 reboundPos = restPos - (overshootOffset * 0.25f);
            float settleDur = duration * 0.30f;
            elapsed = 0f;
            while (elapsed < settleDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / settleDur);
                float ease = UIEasing.Evaluate(EasingType.EaseOutQuad, t);

                if (target != null)
                {
                    target.anchoredPosition = Vector2.Lerp(overshootPos, restPos, ease);
                    target.localScale = Vector3.Lerp(new Vector3(0.94f, 1.04f, 1f), Vector3.one, ease);
                }
                yield return null;
            }

            if (target != null)
            {
                target.anchoredPosition = restPos;
                target.localScale = Vector3.one;
                target.localEulerAngles = new Vector3(0f, 0f, targetRotZ);
            }
        }

        private IEnumerator ExitRoutine(Action onComplete)
        {
            float duration = 0.20f;

            // Overlay fade out
            if (m_DarkOverlay != null)
            {
                StartCoroutine(AnimateOverlayAlpha(m_DarkOverlay.alpha, 0f, duration));
            }

            // YES flings left (-600px)
            if (m_YesButton != null)
            {
                Vector2 targetYes = new Vector2(m_YesRestPos.x - 600f, m_YesRestPos.y);
                StartCoroutine(AnimateMotion(m_YesButton, m_YesButton.anchoredPosition, targetYes, 0f, -8f, duration, 0f, EasingType.EaseInBack, 1.15f));
            }

            // NO flings right (+600px)
            if (m_NoButton != null)
            {
                Vector2 targetNo = new Vector2(m_NoRestPos.x + 600f, m_NoRestPos.y);
                StartCoroutine(AnimateMotion(m_NoButton, m_NoButton.anchoredPosition, targetNo, 0f, 8f, duration, 0f, EasingType.EaseInBack, 1.15f));
            }

            // Dialog flings down into floor (-650px)
            if (m_DialogWindow != null)
            {
                Vector2 exitPos = new Vector2(m_DialogRestPos.x, m_DialogRestPos.y - 650f);
                Vector3 exitScale = m_DialogRestScale * 0.85f;

                StartCoroutine(AnimateMotion(
                    m_DialogWindow,
                    m_DialogWindow.anchoredPosition, exitPos,
                    0f, 0f,
                    duration, 0.02f,
                    EasingType.EaseInBack, 1.15f
                ));
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

        private IEnumerator AnimateOverlayAlpha(float from, float to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                if (m_DarkOverlay != null)
                {
                    m_DarkOverlay.alpha = Mathf.Lerp(from, to, t);
                }
                yield return null;
            }

            if (m_DarkOverlay != null)
            {
                m_DarkOverlay.alpha = to;
            }
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
