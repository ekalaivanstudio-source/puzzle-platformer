using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using MainGame.UI.Animation;
using MainGame.UI.Feedback;
using MainGame.UI.CinematicEffects;

namespace MainGame.UI.Unified
{
    /// <summary>
    /// Governs the Main Menu button animation style: POWER / ENERGY ACTIVATION.
    /// Replaces slow mechanical arm/chassis movements with a fast, sharp, and responsive
    /// "SNAP + IMPACT + SETTLE" animation sequence.
    /// 
    /// Controller / Keyboard navigation first:
    /// - Strictly ignores mouse hover animations.
    /// - Safely interruptible with zero transform drift or fighting tweens.
    /// </summary>
    [DisallowMultipleComponent]
    public class MainMenuButtonEnergyAnimator : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        [Header("Entrance Snap Settings")]
        [Tooltip("Positional offset when powered down / entering (relative to rest position).")]
        [SerializeField] private Vector2 m_StartOffset = new Vector2(-24f, 0f);

        [Tooltip("Scale modifier in dormant/start state (0.88 - 0.92).")]
        [Range(0.80f, 1.0f)]
        [SerializeField] private float m_StartScale = 0.90f;

        [Tooltip("CanvasGroup alpha when dormant/start state.")]
        [Range(0f, 1.0f)]
        [SerializeField] private float m_StartAlpha = 0.35f;

        [Tooltip("Duration of the fast snap to final position in seconds (0.12 - 0.18s).")]
        [Range(0.08f, 0.25f)]
        [SerializeField] private float m_SnapDuration = 0.14f;

        [Tooltip("Impact overshoot scale upon arrival (1.04 - 1.08).")]
        [Range(1.02f, 1.15f)]
        [SerializeField] private float m_OvershootScale = 1.05f;

        [Tooltip("Duration to settle back to scale 1.0 in seconds (0.06 - 0.10s).")]
        [Range(0.04f, 0.15f)]
        [SerializeField] private float m_SettleDuration = 0.08f;

        [Header("Focus / Controller Navigation Settings")]
        [Tooltip("Duration of focus animation (0.10 - 0.16s).")]
        [Range(0.08f, 0.20f)]
        [SerializeField] private float m_FocusDuration = 0.13f;

        [Tooltip("Anticipation dip scale on focus (0.96).")]
        [SerializeField] private float m_FocusDipScale = 0.96f;

        [Tooltip("Peak punch scale on focus (1.03).")]
        [SerializeField] private float m_FocusPeakScale = 1.03f;

        [Header("Confirm / Select Settings")]
        [Tooltip("Total duration of the confirm punch sequence (0.15 - 0.22s).")]
        [Range(0.12f, 0.26f)]
        [SerializeField] private float m_ConfirmDuration = 0.17f;

        [Tooltip("Anticipation compression scale on confirm (0.94).")]
        [SerializeField] private float m_ConfirmCompressionScale = 0.94f;

        [Tooltip("Impact scale on confirm (1.08).")]
        [SerializeField] private float m_ConfirmImpactScale = 1.08f;

        [Header("Surface Shader & Visual FX")]
        [Tooltip("Peak brightness pulse intensity during impact/activation (1.5 - 2.5).")]
        [Range(1.0f, 4.0f)]
        [SerializeField] private float m_ShaderPulseIntensity = 2.0f;

        [Tooltip("Sparks spawned on entrance arrival and confirm.")]
        [Range(2, 12)]
        [SerializeField] private int m_SparkCount = 5;

        [Tooltip("If true, this button is the destructive Exit action (slightly stronger impact & orange energy).")]
        [SerializeField] private bool m_IsExitButton = false;

        [Header("Audio Identity")]
        [Tooltip("SFX played on button entrance lock impact.")]
        [SerializeField] private UISfxType m_EntranceImpactSfx = UISfxType.Impact;

        [Tooltip("SFX played on button confirmation punch.")]
        [SerializeField] private UISfxType m_ConfirmSfx = UISfxType.Confirm;

        [Header("Visual References")]
        [SerializeField] private RectTransform m_ButtonVisual;
        [SerializeField] private RectTransform m_PointerIcon;
        [SerializeField] private CanvasGroup m_CanvasGroup;

        // Baseline Rest State (strictly captured to eliminate transform drift)
        private Vector2 m_RestAnchoredPos;
        private Vector3 m_RestLocalScale = Vector3.one;
        private Vector3 m_RestEulerAngles = Vector3.zero;
        private Vector2 m_RestPivot = new Vector2(0.5f, 0.5f);
        private bool m_HasCapturedRest = false;

        // Active Routines
        private Coroutine m_ActiveRoutine;
        private bool m_IsFocused = false;

        // Cached CinematicUIEffect
        private CinematicUIEffect m_CinematicUI;

        public bool IsExitButton
        {
            get => m_IsExitButton;
            set => m_IsExitButton = value;
        }

        public UISfxType EntranceImpactSfx
        {
            get => m_EntranceImpactSfx;
            set => m_EntranceImpactSfx = value;
        }

        public UISfxType ConfirmSfx
        {
            get => m_ConfirmSfx;
            set => m_ConfirmSfx = value;
        }

        public RectTransform RectComponent
        {
            get
            {
                if (m_ButtonVisual == null) m_ButtonVisual = GetComponent<RectTransform>();
                return m_ButtonVisual;
            }
        }

        public CinematicUIEffect CinematicUI
        {
            get
            {
                if (m_CinematicUI == null && RectComponent != null)
                {
                    Graphic g = RectComponent.GetComponent<Graphic>() ?? RectComponent.GetComponentInChildren<Graphic>();
                    if (g != null)
                    {
                        m_CinematicUI = g.GetComponent<CinematicUIEffect>() ?? g.gameObject.AddComponent<CinematicUIEffect>();
                    }
                }
                return m_CinematicUI;
            }
        }

        private void Awake()
        {
            CaptureRestState();
        }

        private void OnDisable()
        {
            KillMotion();
            ResetToRestState();
        }

        public void CaptureRestState()
        {
            if (m_HasCapturedRest) return;

            if (m_ButtonVisual == null)
            {
                m_ButtonVisual = GetComponent<RectTransform>();
            }

            if (m_ButtonVisual != null)
            {
                m_RestAnchoredPos = m_ButtonVisual.anchoredPosition;
                m_RestLocalScale = m_ButtonVisual.localScale;
                m_RestEulerAngles = m_ButtonVisual.localEulerAngles;
                m_RestPivot = m_ButtonVisual.pivot;
            }

            if (m_CanvasGroup == null)
            {
                m_CanvasGroup = GetComponent<CanvasGroup>();
            }

            if (m_PointerIcon == null)
            {
                Transform icon = transform.Find("Select Icon") ?? transform.Find("pointer") ?? transform.Find("Pointer");
                if (icon != null) m_PointerIcon = icon as RectTransform;
            }

            if (m_PointerIcon != null)
            {
                m_PointerIcon.gameObject.SetActive(false);
            }

            _ = CinematicUI; // Pre-initialize material instance
            m_HasCapturedRest = true;
        }

        public void ResetToRestState()
        {
            KillMotion();
            CaptureRestState();

            if (m_ButtonVisual != null)
            {
                m_ButtonVisual.pivot = m_RestPivot;
                m_ButtonVisual.anchoredPosition = m_RestAnchoredPos;
                m_ButtonVisual.localScale = m_RestLocalScale;
                m_ButtonVisual.localEulerAngles = m_RestEulerAngles;
            }

            if (m_CanvasGroup != null)
            {
                m_CanvasGroup.alpha = 1f;
            }

            if (m_PointerIcon != null)
            {
                m_PointerIcon.gameObject.SetActive(m_IsFocused);
            }

            if (CinematicUI != null)
            {
                CinematicUI.ResetToIdle();
            }
        }

        public void KillMotion()
        {
            if (m_ActiveRoutine != null)
            {
                StopCoroutine(m_ActiveRoutine);
                m_ActiveRoutine = null;
            }

            if (CinematicUI != null)
            {
                CinematicUI.ResetToIdle();
            }
        }

        public Color GetEnergyColor()
        {
            return m_IsExitButton
                ? new Color(1.0f, 0.45f, 0.20f, 1f)  // High-energy orange for Exit
                : new Color(0.35f, 0.88f, 1.0f, 1f); // Vibrant cyan for main items
        }

        // ═════════════════════════════════════════════════════════════════════════════
        // 1. FAST "SNAP + IMPACT + SETTLE" ENTRANCE
        // ═════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Instantly primes the button in its dormant, powered-down state before snap entrance.
        /// </summary>
        public void PrepareDormantState()
        {
            KillMotion();
            CaptureRestState();

            if (m_ButtonVisual != null)
            {
                m_ButtonVisual.pivot = m_RestPivot;
                m_ButtonVisual.anchoredPosition = m_RestAnchoredPos + m_StartOffset;
                m_ButtonVisual.localScale = m_RestLocalScale * m_StartScale;
                m_ButtonVisual.localEulerAngles = Vector3.zero;
            }

            if (m_CanvasGroup != null)
            {
                m_CanvasGroup.alpha = m_StartAlpha;
            }

            if (m_PointerIcon != null)
            {
                m_PointerIcon.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Launches the fast snap + impact + settle animation with optional stagger delay.
        /// </summary>
        public void PlaySnapEntrance(float staggerDelay = 0f, Action onComplete = null)
        {
            KillMotion();
            CaptureRestState();
            m_ActiveRoutine = StartCoroutine(SnapEntranceRoutine(staggerDelay, onComplete));
        }

        private IEnumerator SnapEntranceRoutine(float staggerDelay, Action onComplete)
        {
            if (staggerDelay > 0f)
            {
                yield return new WaitForSecondsRealtime(staggerDelay);
            }

            Vector2 startPos = m_RestAnchoredPos + m_StartOffset;
            Vector2 targetPos = m_RestAnchoredPos;
            Vector3 startScale = m_RestLocalScale * m_StartScale;
            float overshootVal = m_IsExitButton ? (m_OvershootScale + 0.02f) : m_OvershootScale;
            Vector3 impactScale = m_RestLocalScale * overshootVal;
            Color energyColor = GetEnergyColor();

            // ─── PHASE 1: FAST SNAP (0.12 - 0.18s) ──────────────────────────────
            float snapDur = m_SnapDuration;
            float elapsed = 0f;

            while (elapsed < snapDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / snapDur);
                // Aggressive ease-out curve (fast explosive snap, deceleration on arrival)
                float posEase = UIEasing.Evaluate(EasingType.EaseOutCubic, t);

                if (m_ButtonVisual != null)
                {
                    m_ButtonVisual.anchoredPosition = Vector2.LerpUnclamped(startPos, targetPos, posEase);
                    m_ButtonVisual.localScale = Vector3.LerpUnclamped(startScale, impactScale, posEase);
                }

                if (m_CanvasGroup != null)
                {
                    // Alpha recovers within the first 60ms
                    m_CanvasGroup.alpha = Mathf.Lerp(m_StartAlpha, 1.0f, Mathf.Clamp01(t * 2.5f));
                }

                yield return null;
            }

            // Lock precisely to target position
            if (m_ButtonVisual != null)
            {
                m_ButtonVisual.anchoredPosition = targetPos;
                m_ButtonVisual.localScale = impactScale;
            }
            if (m_CanvasGroup != null) m_CanvasGroup.alpha = 1f;

            // ─── PHASE 2: IMPACT & SURFACE SHADER ACTIVATION (0.08 - 0.15s) ──────
            // Surface brightness pulse + energy sweep + border pulse + digital glitch
            if (CinematicUI != null)
            {
                CinematicUI.TriggerImpactFlash(0.10f, m_ShaderPulseIntensity, energyColor);
                CinematicUI.PlayActivationSweep(0.12f, energyColor, 45f);
                CinematicUI.TriggerBorderPulse(0.14f, energyColor, 2.2f);
                CinematicUI.TriggerDigitalGlitch(0.08f, 0.5f, 0.008f);
            }

            // Audio: Bespoke impact SFX
            float sfxVol = m_IsExitButton ? 0.95f : 0.75f;
            UIFeedbackAudio.PlaySfx(m_EntranceImpactSfx, sfxVol, 0.02f);

            // Screen micro-shake
            float shakeImpulse = m_IsExitButton ? 0.8f : 0.25f;
            float shakeDuration = m_IsExitButton ? 0.06f : 0.035f;
            UIMicroShake.Shake(shakeImpulse, shakeDuration);

            // Tiny pixel fragments/sparks
            if (CinematicUIParticleSystem.Instance != null && m_ButtonVisual != null)
            {
                int count = m_IsExitButton ? (m_SparkCount + 3) : m_SparkCount;
                CinematicUIParticleSystem.Instance.SpawnSparkBurst(Vector2.zero, m_ButtonVisual, energyColor, count, 20f);
            }

            // ─── PHASE 3: QUICK SETTLE TO 1.0 (0.06 - 0.10s) ────────────────────
            float settleDur = m_SettleDuration;
            elapsed = 0f;

            while (elapsed < settleDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / settleDur);
                float settleEase = UIEasing.Evaluate(EasingType.EaseOutQuad, t);

                if (m_ButtonVisual != null)
                {
                    m_ButtonVisual.localScale = Vector3.Lerp(impactScale, m_RestLocalScale, settleEase);
                }

                yield return null;
            }

            // Restore exact rest state
            if (m_ButtonVisual != null)
            {
                m_ButtonVisual.anchoredPosition = m_RestAnchoredPos;
                m_ButtonVisual.localScale = m_RestLocalScale;
                m_ButtonVisual.localEulerAngles = m_RestEulerAngles;
            }

            m_ActiveRoutine = null;
            onComplete?.Invoke();
        }

        // ═════════════════════════════════════════════════════════════════════════════
        // 2. FOCUS / CONTROLLER NAVIGATION (NO MOUSE HOVER)
        // ═════════════════════════════════════════════════════════════════════════════

        public void OnSelect(BaseEventData eventData)
        {
            SetFocusState(true);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            SetFocusState(false);
        }

        private void SetFocusState(bool focused)
        {
            CaptureRestState();
            m_IsFocused = focused;

            // Instantly kill any ongoing focus or idle animation
            KillMotion();

            if (!focused)
            {
                // Immediately remove focus effect and restore rest scale
                if (m_ButtonVisual != null)
                {
                    m_ButtonVisual.anchoredPosition = m_RestAnchoredPos;
                    m_ButtonVisual.localScale = m_RestLocalScale;
                }
                if (m_PointerIcon != null)
                {
                    m_PointerIcon.gameObject.SetActive(false);
                }
                if (CinematicUI != null)
                {
                    CinematicUI.ResetToIdle();
                }
                return;
            }

            // NEW BUTTON FOCUSED:
            // Fast 2-phase scale: 0.96 -> 1.03 -> 1.0 (target: 0.10 - 0.16s)
            m_ActiveRoutine = StartCoroutine(FocusInRoutine());
        }

        private IEnumerator FocusInRoutine()
        {
            Color energyColor = GetEnergyColor();

            // Audio: Navigate focus click (debounced in UIFeedbackAudio)
            UIFeedbackAudio.PlaySfx(UISfxType.Navigate, 0.85f, 0.02f);

            // Activate pointer icon immediately
            if (m_PointerIcon != null)
            {
                m_PointerIcon.gameObject.SetActive(true);
            }

            // Surface brightness flash & edge highlight
            if (CinematicUI != null)
            {
                CinematicUI.TriggerImpactFlash(0.08f, 1.4f, energyColor);
                CinematicUI.TriggerBorderPulse(0.12f, energyColor, 1.8f);
            }

            // Tiny pixel burst on pointer
            if (CinematicUIParticleSystem.Instance != null && m_PointerIcon != null)
            {
                CinematicUIParticleSystem.Instance.SpawnSparkBurst(Vector2.zero, m_PointerIcon, energyColor, 3, 14f);
            }

            float totalDur = m_FocusDuration;
            float halfDur = totalDur * 0.45f;
            float settleDur = totalDur - halfDur;

            Vector3 restScale = m_RestLocalScale;
            Vector3 dipScale = restScale * m_FocusDipScale;
            Vector3 peakScale = restScale * m_FocusPeakScale;

            // Phase 1: 0.96 -> 1.03
            float elapsed = 0f;
            while (elapsed < halfDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / halfDur);
                float ease = UIEasing.Evaluate(EasingType.EaseOutQuad, t);

                if (m_ButtonVisual != null)
                {
                    m_ButtonVisual.localScale = Vector3.Lerp(dipScale, peakScale, ease);
                }
                yield return null;
            }

            // Phase 2: 1.03 -> 1.00
            elapsed = 0f;
            while (elapsed < settleDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / settleDur);
                float ease = UIEasing.Evaluate(EasingType.EaseOutQuad, t);

                if (m_ButtonVisual != null)
                {
                    m_ButtonVisual.localScale = Vector3.Lerp(peakScale, restScale, ease);
                }
                yield return null;
            }

            if (m_ButtonVisual != null)
            {
                m_ButtonVisual.localScale = restScale;
                m_ButtonVisual.anchoredPosition = m_RestAnchoredPos;
            }

            m_ActiveRoutine = null;
        }

        // ═════════════════════════════════════════════════════════════════════════════
        // 3. CONFIRM / SELECT ANIMATION
        // ═════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Fast impact animation when user presses Confirm / Enter:
        /// 1. scale 1.0 -> 0.94
        /// 2. impact scale 0.94 -> 1.08
        /// 3. return to 1.0
        /// Total duration: ~0.15 - 0.22s.
        /// </summary>
        public void PlayConfirmPunch(Action onComplete = null)
        {
            KillMotion();
            CaptureRestState();
            m_ActiveRoutine = StartCoroutine(ConfirmPunchRoutine(onComplete));
        }

        private IEnumerator ConfirmPunchRoutine(Action onComplete)
        {
            Color energyColor = GetEnergyColor();
            Vector3 restScale = m_RestLocalScale;
            Vector3 compressScale = restScale * m_ConfirmCompressionScale;
            Vector3 impactScale = restScale * m_ConfirmImpactScale;

            float totalDur = m_ConfirmDuration;
            float compressDur = totalDur * 0.28f;
            float impactDur = totalDur * 0.36f;
            float settleDur = totalDur - compressDur - impactDur;

            // 1. Compression (1.0 -> 0.94)
            float elapsed = 0f;
            while (elapsed < compressDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / compressDur);
                float ease = UIEasing.Evaluate(EasingType.EaseInQuad, t);

                if (m_ButtonVisual != null)
                {
                    m_ButtonVisual.localScale = Vector3.Lerp(restScale, compressScale, ease);
                }
                yield return null;
            }

            // 2. Explosive Impact Punch (0.94 -> 1.08)
            // Trigger impact FX on impact frame
            UIMicroShake.Shake(1.2f, 0.08f);
            UIFeedbackAudio.PlaySfx(m_ConfirmSfx, 0.95f, 0.02f);

            if (CinematicUI != null)
            {
                CinematicUI.TriggerImpactFlash(0.10f, m_ShaderPulseIntensity * 1.2f, energyColor);
                CinematicUI.TriggerBorderPulse(0.18f, energyColor, 2.8f);
                CinematicUI.TriggerDigitalGlitch(0.09f, 0.7f, 0.012f);
            }

            if (CinematicUIParticleSystem.Instance != null && m_ButtonVisual != null)
            {
                CinematicUIParticleSystem.Instance.SpawnSparkBurst(Vector2.zero, m_ButtonVisual, energyColor, m_SparkCount + 2, 24f);
            }

            elapsed = 0f;
            while (elapsed < impactDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / impactDur);
                float ease = UIEasing.Evaluate(EasingType.EaseOutBack, t, 1.4f);

                if (m_ButtonVisual != null)
                {
                    m_ButtonVisual.localScale = Vector3.LerpUnclamped(compressScale, impactScale, ease);
                }
                yield return null;
            }

            // 3. Settle back to 1.0
            elapsed = 0f;
            while (elapsed < settleDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / settleDur);
                float ease = UIEasing.Evaluate(EasingType.EaseOutQuad, t);

                if (m_ButtonVisual != null)
                {
                    m_ButtonVisual.localScale = Vector3.Lerp(impactScale, restScale, ease);
                }
                yield return null;
            }

            if (m_ButtonVisual != null)
            {
                m_ButtonVisual.localScale = restScale;
                m_ButtonVisual.anchoredPosition = m_RestAnchoredPos;
            }

            m_ActiveRoutine = null;
            onComplete?.Invoke();
        }

        // ═════════════════════════════════════════════════════════════════════════════
        // 4. RETRACT (SCREEN EXIT RECONFIGURATION)
        // ═════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Fast power-down retract when leaving screen (0.12s snap back to offset and low alpha).
        /// </summary>
        public void PlayRetract(Action onComplete = null)
        {
            KillMotion();
            CaptureRestState();
            m_ActiveRoutine = StartCoroutine(RetractRoutine(onComplete));
        }

        private IEnumerator RetractRoutine(Action onComplete)
        {
            Vector2 startPos = m_ButtonVisual != null ? m_ButtonVisual.anchoredPosition : m_RestAnchoredPos;
            Vector2 targetPos = m_RestAnchoredPos + m_StartOffset;
            Vector3 startScale = m_ButtonVisual != null ? m_ButtonVisual.localScale : m_RestLocalScale;
            Vector3 targetScale = m_RestLocalScale * m_StartScale;

            if (CinematicUI != null)
            {
                CinematicUI.TriggerBorderPulse(0.12f, GetEnergyColor(), 1.5f, clockwise: false);
            }

            float duration = 0.12f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = UIEasing.Evaluate(EasingType.EaseInQuad, t);

                if (m_ButtonVisual != null)
                {
                    m_ButtonVisual.anchoredPosition = Vector2.Lerp(startPos, targetPos, ease);
                    m_ButtonVisual.localScale = Vector3.Lerp(startScale, targetScale, ease);
                }

                if (m_CanvasGroup != null)
                {
                    m_CanvasGroup.alpha = Mathf.Lerp(1.0f, m_StartAlpha, ease);
                }

                yield return null;
            }

            if (m_PointerIcon != null)
            {
                m_PointerIcon.gameObject.SetActive(false);
            }

            if (CinematicUI != null)
            {
                CinematicUI.ResetToIdle();
            }

            m_ActiveRoutine = null;
            onComplete?.Invoke();
        }
    }
}
