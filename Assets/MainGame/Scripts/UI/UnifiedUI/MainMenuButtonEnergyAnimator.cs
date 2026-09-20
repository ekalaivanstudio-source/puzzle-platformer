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
    public class MainMenuButtonEnergyAnimator : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler
    {
        public static event Action<MainMenuButtonEnergyAnimator, bool> OnAnyButtonFocused;

        [Header("Entrance Snap Settings")]
        [Tooltip("Positional offset when powered down / entering (relative to rest position).")]
        [SerializeField] private Vector2 m_StartOffset = new Vector2(-24f, 0f);

        [Tooltip("Scale modifier in dormant/start state (0.88 - 0.92).")]
        [Range(0.80f, 1.0f)]
        [SerializeField] private float m_StartScale = 0.90f;

        [Tooltip("CanvasGroup alpha when dormant/start state.")]
        [Range(0f, 1.0f)]
        [SerializeField] private float m_StartAlpha = 0.35f;

        [Tooltip("Duration of the fast snap to final position in seconds (0.10 - 0.16s).")]
        [Range(0.08f, 0.20f)]
        [SerializeField] private float m_SnapDuration = 0.13f;

        [Tooltip("Impact overshoot scale upon arrival (1.03 - 1.06).")]
        [Range(1.02f, 1.10f)]
        [SerializeField] private float m_OvershootScale = 1.05f;

        [Tooltip("Duration to settle back to scale 1.0 in seconds (0.05 - 0.09s).")]
        [Range(0.04f, 0.12f)]
        [SerializeField] private float m_SettleDuration = 0.07f;

        [Header("Focus / Controller Navigation Settings")]
        [Tooltip("Duration of focus animation (0.10 - 0.16s).")]
        [Range(0.08f, 0.18f)]
        [SerializeField] private float m_FocusDuration = 0.12f;

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

        [Header("Secondary Motion Settings")]
        [Tooltip("Enable tiny post-impact tactile vibration.")]
        [SerializeField] private bool m_EnableMicroVibration = true;

        [Tooltip("Enable continuous zero-drift floating motion for this button (disabled by default to maintain solid console feel).")]
        [SerializeField] private bool m_EnableFloating = false;

        [Tooltip("Peak vertical floating displacement in pixels.")]
        [SerializeField] private float m_FloatAmplitude = 2.5f;

        [Tooltip("Floating oscillation speed in radians/sec.")]
        [SerializeField] private float m_FloatSpeed = 2.0f;

        [Tooltip("Subtle angular tilt during floating in degrees.")]
        [SerializeField] private float m_FloatTiltAngle = 0.4f;

        [Tooltip("Additional vertical lift when focused/selected.")]
        [SerializeField] private float m_FocusFloatLift = 2.0f;

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
        private Coroutine m_FloatingRoutine;
        private bool m_IsFocused = false;

        // Completion callback owned by whichever routine m_ActiveRoutine currently holds.
        // Held in a field rather than only on the coroutine's stack so that stopping the
        // routine can still release whoever is waiting on it.
        private Action m_PendingCompletion;

        // Cached CinematicUIEffect
        private CinematicUIEffect m_CinematicUI;

        public bool EnableFloating
        {
            get => m_EnableFloating;
            set
            {
                m_EnableFloating = value;
                if (value) StartFloatingIdle();
                else StopFloating();
            }
        }

        public float FloatAmplitude { get => m_FloatAmplitude; set => m_FloatAmplitude = value; }
        public float FloatSpeed { get => m_FloatSpeed; set => m_FloatSpeed = value; }
        public float FloatTiltAngle { get => m_FloatTiltAngle; set => m_FloatTiltAngle = value; }
        public float FocusFloatLift { get => m_FocusFloatLift; set => m_FocusFloatLift = value; }

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

        private void Start()
        {
            CaptureRestState();
            if (m_EnableFloating && m_ActiveRoutine == null && isActiveAndEnabled)
            {
                StartFloatingIdle();
            }
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
            ApplyRestTransform();

            if (m_PointerIcon != null)
            {
                m_PointerIcon.gameObject.SetActive(m_IsFocused);
            }

            if (CinematicUI != null)
            {
                CinematicUI.ResetToIdle();
            }

            if (m_EnableFloating && isActiveAndEnabled)
            {
                StartFloatingIdle();
            }
        }

        /// <summary>
        /// Snaps the visual back to its captured rest pose. Split out of ResetToRestState so
        /// KillMotion can settle an interrupted animation without recursing back through it.
        /// </summary>
        private void ApplyRestTransform()
        {
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
        }

        /// <summary>
        /// Hands the current routine's completion callback to its waiter exactly once.
        /// Cleared before invoking so a callback that starts another animation on this
        /// button cannot re-enter and fire itself twice.
        /// </summary>
        private void CompletePending()
        {
            Action pending = m_PendingCompletion;
            m_PendingCompletion = null;
            pending?.Invoke();
        }

        public void KillMotion()
        {
            bool interrupted = m_ActiveRoutine != null;
            if (m_ActiveRoutine != null)
            {
                StopCoroutine(m_ActiveRoutine);
                m_ActiveRoutine = null;
            }

            StopFloating();

            if (CinematicUI != null)
            {
                CinematicUI.ResetToIdle();
            }

            // A stopped coroutine never reaches its own completion line. Without this the
            // callback is lost, and everything waiting on it waits forever: HomeScreenAnimator
            // counts this button in buttonsPending/pendingRetracts, its entrance and exit
            // sequences block on those counters, and UINavigationManager.IsTransitioning then
            // latches true -- which makes every menu button stop responding. Hover or select
            // during the entrance stagger reaches here via SetFocusState, so this is a normal
            // path, not an edge case.
            if (interrupted)
            {
                ApplyRestTransform();
                CompletePending();
            }
        }

        public void StopFloating()
        {
            if (m_FloatingRoutine != null)
            {
                StopCoroutine(m_FloatingRoutine);
                m_FloatingRoutine = null;
            }
        }

        public void StartFloatingIdle()
        {
            if (!m_EnableFloating || !isActiveAndEnabled || m_ButtonVisual == null) return;

            StopFloating();
            m_FloatingRoutine = StartCoroutine(FloatingIdleRoutine());
        }

        private IEnumerator FloatingIdleRoutine()
        {
            CaptureRestState();
            float phase = (m_RestAnchoredPos.x * 0.015f) + (m_RestAnchoredPos.y * -0.025f) + (transform.GetSiblingIndex() * 0.55f);
            float currentLift = m_IsFocused ? m_FocusFloatLift : 0f;

            while (m_EnableFloating && m_ButtonVisual != null)
            {
                float dt = Time.unscaledDeltaTime;
                float time = Time.unscaledTime;

                float targetLift = m_IsFocused ? m_FocusFloatLift : 0f;
                currentLift = Mathf.Lerp(currentLift, targetLift, dt * 10f);

                // Smooth sinusoidal floating oscillation
                float wave = Mathf.Sin(time * m_FloatSpeed + phase);
                float yBob = wave * m_FloatAmplitude + currentLift;
                float tilt = Mathf.Cos(time * (m_FloatSpeed * 0.85f) + phase) * m_FloatTiltAngle;

                m_ButtonVisual.anchoredPosition = new Vector2(m_RestAnchoredPos.x, m_RestAnchoredPos.y + yBob);
                m_ButtonVisual.localEulerAngles = new Vector3(m_RestEulerAngles.x, m_RestEulerAngles.y, m_RestEulerAngles.z + tilt);

                yield return null;
            }

            m_FloatingRoutine = null;
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
            // Unity refuses StartCoroutine on an inactive GameObject, so the callback would
            // never fire and the caller's pending counter would never drain.
            if (!isActiveAndEnabled)
            {
                onComplete?.Invoke();
                return;
            }
            m_PendingCompletion = onComplete;
            m_ActiveRoutine = StartCoroutine(SnapEntranceRoutine(staggerDelay));
        }

        private IEnumerator SnapEntranceRoutine(float staggerDelay)
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
                    if (m_EnableMicroVibration)
                    {
                        float vibY = Mathf.Sin(t * 36f) * 0.8f * (1f - t);
                        m_ButtonVisual.anchoredPosition = new Vector2(m_RestAnchoredPos.x, m_RestAnchoredPos.y + vibY);
                    }
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
            CompletePending();

            if (m_EnableFloating && isActiveAndEnabled)
            {
                StartFloatingIdle();
            }
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

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (EventSystem.current != null && !PauseMenuScreen.IsPaused)
            {
                EventSystem.current.SetSelectedGameObject(gameObject);
            }
        }

        private void SetFocusState(bool focused)
        {
            CaptureRestState();
            m_IsFocused = focused;

            OnAnyButtonFocused?.Invoke(this, focused);

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

                if (m_EnableFloating && isActiveAndEnabled)
                {
                    StartFloatingIdle();
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

            if (m_EnableFloating && isActiveAndEnabled)
            {
                StartFloatingIdle();
            }
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
            if (!isActiveAndEnabled)
            {
                onComplete?.Invoke();
                return;
            }
            m_PendingCompletion = onComplete;
            m_ActiveRoutine = StartCoroutine(ConfirmPunchRoutine());
        }

        private IEnumerator ConfirmPunchRoutine()
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
            CompletePending();

            if (m_EnableFloating && isActiveAndEnabled)
            {
                StartFloatingIdle();
            }
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
            if (!isActiveAndEnabled)
            {
                onComplete?.Invoke();
                return;
            }
            m_PendingCompletion = onComplete;
            m_ActiveRoutine = StartCoroutine(RetractRoutine());
        }

        private IEnumerator RetractRoutine()
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
            CompletePending();
        }
    }
}
