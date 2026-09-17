using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using MainGame.UI.Animation;
using MainGame.UI.RoboticEffects;
using MainGame.UI.Feedback;
using MainGame.UI.CinematicEffects;

namespace MainGame.UI.Unified
{
    /// <summary>
    /// Animates buttons on focus/selection and submit state. Handles physical positioning shift,
    /// living heartbeat pulse, kinetic indicator ping-pong, and screen micro-shake on submit punch.
    /// Works seamlessly with Keyboard, Gamepad, and Mouse.
    /// </summary>
    [DisallowMultipleComponent]
    public class UIAnimatedButton : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Selection Animation Settings")]
        [Tooltip("Position shift when selected (0 -> -4px).")]
        [SerializeField] private float m_SelectedOffsetX = -4f;

        [Tooltip("Scale modifier when selected (1.00 -> 1.035).")]
        [SerializeField] private float m_SelectedScale = 1.035f;

        [Tooltip("Angular tilt in degrees when selected (+1.2 deg).")]
        [SerializeField] private float m_SelectedTilt = 1.2f;

        [Tooltip("Duration of focus in transition (0.12 - 0.16s).")]
        [SerializeField] private float m_FocusInDuration = 0.14f;

        [Tooltip("Duration of focus out transition (seconds).")]
        [SerializeField] private float m_FocusOutDuration = 0.12f;

        [Header("Floating Effect Settings")]
        [Tooltip("Enable continuous floating motion for this button.")]
        [SerializeField] private bool m_EnableFloating = true;

        [Tooltip("Vertical floating bob amplitude in pixels.")]
        [SerializeField] private float m_FloatAmplitude = 4f;

        [Tooltip("Floating oscillation speed in radians/sec.")]
        [SerializeField] private float m_FloatSpeed = 2.2f;

        [Tooltip("Subtle angular tilt during floating in degrees.")]
        [SerializeField] private float m_FloatTiltAngle = 0.8f;

        [Tooltip("Additional vertical lift when focused/selected.")]
        [SerializeField] private float m_FocusFloatLift = 3.5f;

        [Header("Physical Confirmation Punch")]
        [Tooltip("Duration of the physical compression and rebound when submitted/pressed.")]
        [SerializeField] private float m_ConfirmDuration = 0.14f;

        [Tooltip("Compression scale during submit punch.")]
        [SerializeField] private float m_ConfirmPressedScale = 0.94f;

        [Tooltip("Positional push depth during submit punch.")]
        [SerializeField] private float m_ConfirmPunchOffset = 5f;

        [Header("Red Signboard / Back Button Settings")]
        [Tooltip("If true, applies red signboard styling and punch behavior tailored for Back / Exit.")]
        [SerializeField] private bool m_IsDestructiveOrBack = false;

        [Header("Audio Customization (Optional)")]
        [Tooltip("Sound played on button selection focus. If null, falls back to AudioManager if configured.")]
        [SerializeField] private AudioClip m_SelectAudioClip;
        [Tooltip("Sound played on button confirmation. If null, falls back to AudioManager.PlayButton().")]
        [SerializeField] private AudioClip m_ConfirmAudioClip;

        [Header("Visual References")]
        [Tooltip("The main visual container transform of the button that will be offset/scaled.")]
        [SerializeField] private RectTransform m_ButtonVisual;

        [Tooltip("The left selection pointer (e.g. blue triangle).")]
        [SerializeField] private RectTransform m_LeftPointer;

        private Vector2 m_OriginalVisualPos;
        private Vector2 m_OriginalPointerPos;
        private Coroutine m_AnimationCoroutine;
        private Coroutine m_LivingIdleCoroutine;
        private Coroutine m_ConfirmCoroutine;
        private bool m_IsFocused = false;
        private bool m_HasCapturedRestState = false;

        public bool EnableFloating
        {
            get => m_EnableFloating;
            set
            {
                m_EnableFloating = value;
                if (value) StartFloating();
                else StopFloating();
            }
        }
        public float FloatAmplitude { get => m_FloatAmplitude; set => m_FloatAmplitude = value; }
        public float FloatSpeed { get => m_FloatSpeed; set => m_FloatSpeed = value; }
        public float FloatTiltAngle { get => m_FloatTiltAngle; set => m_FloatTiltAngle = value; }
        public float FocusFloatLift { get => m_FocusFloatLift; set => m_FocusFloatLift = value; }

        public bool IsFocused => m_IsFocused;
        public RectTransform ButtonVisual => m_ButtonVisual;

        private CinematicUIEffect m_CinematicUI;
        public CinematicUIEffect CinematicUI
        {
            get
            {
                if (m_CinematicUI == null && m_ButtonVisual != null)
                {
                    Graphic g = m_ButtonVisual.GetComponent<Graphic>() ?? m_ButtonVisual.GetComponentInChildren<Graphic>();
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
            if (m_ButtonVisual == null)
            {
                m_ButtonVisual = GetComponent<RectTransform>();
            }

            if (m_LeftPointer == null)
            {
                Transform pointer = transform.Find("Select Icon") ?? transform.Find("pointer");
                if (pointer != null)
                {
                    m_LeftPointer = pointer as RectTransform;
                }
            }

            if (m_LeftPointer != null)
            {
                m_LeftPointer.gameObject.SetActive(false);
            }

            CaptureRestState();
        }

        private void Start()
        {
            CaptureRestState();
            if (m_EnableFloating && m_AnimationCoroutine == null && m_ConfirmCoroutine == null && isActiveAndEnabled)
            {
                StartFloating();
            }
        }

        public void CaptureRestState()
        {
            if (m_HasCapturedRestState || m_ButtonVisual == null) return;

            m_OriginalVisualPos = m_ButtonVisual.anchoredPosition;
            if (m_LeftPointer != null)
            {
                m_OriginalPointerPos = m_LeftPointer.anchoredPosition;
            }
            m_HasCapturedRestState = true;
        }

        private void OnDisable()
        {
            ResetToNormalState();
        }

        public void OnSelect(BaseEventData eventData)
        {
            SetFocused(true);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            SetFocused(false);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(gameObject);
            }
            else
            {
                SetFocused(true);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            // Keeps focus on the last hovered button for controller/keyboard parity
        }


        public Coroutine PlayConfirmPunch(Action onComplete)
        {
            CaptureRestState();

            if (m_LivingIdleCoroutine != null)
            {
                StopCoroutine(m_LivingIdleCoroutine);
                m_LivingIdleCoroutine = null;
            }

            if (m_ConfirmCoroutine != null)
            {
                StopCoroutine(m_ConfirmCoroutine);
            }

            // Audio feedback
            PlayConfirmAudio();

            m_ConfirmCoroutine = StartCoroutine(ConfirmPunchRoutine(onComplete));
            return m_ConfirmCoroutine;
        }

        private void PlaySelectAudio()
        {
            if (m_SelectAudioClip != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayUi(m_SelectAudioClip);
            }
            else
            {
                UIFeedbackAudio.PlaySfx(UISfxType.Navigate, 0.85f, 0.03f);
            }
        }

        private void PlayConfirmAudio()
        {
            if (m_ConfirmAudioClip != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayUi(m_ConfirmAudioClip);
            }
            else if (m_IsDestructiveOrBack)
            {
                UIFeedbackAudio.PlaySfx(UISfxType.Back, 0.90f, 0.02f);
            }
            else
            {
                UIFeedbackAudio.PlaySfx(UISfxType.Confirm, 1.0f, 0.02f);
            }
        }

        private void SetFocused(bool focused)
        {
            CaptureRestState();

            if (m_IsFocused == focused) return;
            m_IsFocused = focused;

            if (focused)
            {
                PlaySelectAudio();
            }

            if (m_LivingIdleCoroutine != null)
            {
                StopCoroutine(m_LivingIdleCoroutine);
                m_LivingIdleCoroutine = null;
            }

            if (m_AnimationCoroutine != null)
            {
                StopCoroutine(m_AnimationCoroutine);
            }

            m_AnimationCoroutine = StartCoroutine(AnimateTransition(focused));
        }

        private IEnumerator AnimateTransition(bool focusIn)
        {
            float elapsed = 0f;
            float duration = focusIn ? m_FocusInDuration : m_FocusOutDuration;

            Vector2 startPos = m_ButtonVisual.anchoredPosition;
            Vector2 targetPos = focusIn 
                ? new Vector2(m_OriginalVisualPos.x + m_SelectedOffsetX, m_OriginalVisualPos.y) 
                : m_OriginalVisualPos;

            Vector3 startScale = m_ButtonVisual.localScale;
            Vector3 targetScale = focusIn 
                ? new Vector3(m_SelectedScale, m_SelectedScale, 1f) 
                : Vector3.one;

            Vector3 startRot = m_ButtonVisual.localEulerAngles;
            Vector3 targetRot = focusIn ? new Vector3(0f, 0f, m_SelectedTilt) : Vector3.zero;

            if (focusIn)
            {
                if (m_LeftPointer != null) m_LeftPointer.gameObject.SetActive(true);

                if (CinematicUIFXManager.Instance != null)
                {
                    CinematicUIFXManager.Instance.PlayFocusFX(m_ButtonVisual, m_LeftPointer, m_IsDestructiveOrBack);
                }
                else
                {
                    Color pulseCol = m_IsDestructiveOrBack ? new Color(1f, 0.45f, 0.2f) : new Color(0.35f, 0.85f, 1f);
                    if (CinematicUI != null)
                    {
                        CinematicUI.TriggerBorderPulse(0.24f, pulseCol, 1.8f);
                        CinematicUI.PlayActivationSweep(0.22f, new Color(1f, 1f, 1f, 0.6f), 45f);
                    }

                    if (CinematicUIParticleSystem.Instance != null && m_LeftPointer != null)
                    {
                        CinematicUIParticleSystem.Instance.SpawnSparkBurst(m_LeftPointer.anchoredPosition, m_ButtonVisual, new Color(1f, 0.88f, 0.35f), 3, 10f);
                    }
                }
            }
            else
            {
                if (m_LeftPointer != null) m_LeftPointer.gameObject.SetActive(false);
                if (CinematicUIFXManager.Instance != null)
                {
                    CinematicUIFXManager.Instance.FocusFX.ResetFocus(m_ButtonVisual);
                }
                else if (CinematicUI != null)
                {
                    CinematicUI.ResetToIdle();
                }
            }

            Vector2 startPointerPos = m_LeftPointer != null ? m_LeftPointer.anchoredPosition : Vector2.zero;
            Vector2 targetPointerPos = m_OriginalPointerPos;
            Vector2 slidePointerFrom = new Vector2(m_OriginalPointerPos.x - 14f, m_OriginalPointerPos.y);
            Vector2 pointerStart = focusIn ? slidePointerFrom : startPointerPos;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                if (m_ButtonVisual != null)
                {
                    if (focusIn)
                    {
                        // Small physical accent pulse (1.00 -> 1.06 -> 1.035, 0 -> -5px -> -4px)
                        float pulseFactor = Mathf.Sin(t * Mathf.PI);
                        Vector3 currentScale = Vector3.Lerp(startScale, targetScale, t) + new Vector3(0.02f * pulseFactor, 0.02f * pulseFactor, 0f);
                        Vector2 currentPos = Vector2.Lerp(startPos, targetPos, t) + new Vector2(-1f * pulseFactor, 0f);
                        m_ButtonVisual.localScale = currentScale;
                        m_ButtonVisual.anchoredPosition = currentPos;
                        m_ButtonVisual.localEulerAngles = Vector3.Lerp(startRot, targetRot, t);
                    }
                    else
                    {
                        float ease = UIEasing.Evaluate(EasingType.EaseOutQuad, t);
                        m_ButtonVisual.anchoredPosition = Vector2.Lerp(startPos, targetPos, ease);
                        m_ButtonVisual.localScale = Vector3.Lerp(startScale, targetScale, ease);
                        m_ButtonVisual.localEulerAngles = Vector3.Lerp(startRot, targetRot, ease);
                    }
                }

                if (m_LeftPointer != null && focusIn)
                {
                    float pointerEase = UIEasing.Evaluate(EasingType.EaseOutBack, t, 1.35f);
                    m_LeftPointer.anchoredPosition = Vector2.LerpUnclamped(pointerStart, targetPointerPos, pointerEase);
                }

                yield return null;
            }

            m_ButtonVisual.anchoredPosition = targetPos;
            m_ButtonVisual.localScale = targetScale;
            m_ButtonVisual.localEulerAngles = targetRot;
            if (m_LeftPointer != null && focusIn) m_LeftPointer.anchoredPosition = targetPointerPos;
            m_AnimationCoroutine = null;

            if (focusIn && m_IsFocused)
            {
                if (m_EnableFloating && isActiveAndEnabled)
                {
                    StartFloating();
                }
                else
                {
                    m_LivingIdleCoroutine = StartCoroutine(LivingIdleRoutine());
                }
            }
            else if (m_EnableFloating && isActiveAndEnabled)
            {
                StartFloating();
            }
        }

        public void StartFloating()
        {
            if (!m_EnableFloating || !isActiveAndEnabled || m_ButtonVisual == null) return;

            StopFloating();
            m_LivingIdleCoroutine = StartCoroutine(LivingIdleRoutine());
        }

        public void StopFloating()
        {
            if (m_LivingIdleCoroutine != null)
            {
                StopCoroutine(m_LivingIdleCoroutine);
                m_LivingIdleCoroutine = null;
            }
        }

        private IEnumerator LivingIdleRoutine()
        {
            CaptureRestState();
            float phase = (m_OriginalVisualPos.x * 0.015f) + (m_OriginalVisualPos.y * -0.025f) + (transform.GetSiblingIndex() * 0.55f);
            float currentLift = m_IsFocused ? m_FocusFloatLift : 0f;
            float currentXOffset = m_IsFocused ? m_SelectedOffsetX : 0f;
            float currentBaseScale = m_IsFocused ? m_SelectedScale : 1.0f;
            float currentBaseTilt = m_IsFocused ? m_SelectedTilt : 0f;

            while (m_EnableFloating && m_ButtonVisual != null)
            {
                float dt = Time.unscaledDeltaTime;
                float time = Time.unscaledTime;

                float targetLift = m_IsFocused ? m_FocusFloatLift : 0f;
                float targetX = m_IsFocused ? m_SelectedOffsetX : 0f;
                float targetScale = m_IsFocused ? m_SelectedScale : 1.0f;
                float targetTilt = m_IsFocused ? m_SelectedTilt : 0f;

                currentLift = Mathf.Lerp(currentLift, targetLift, dt * 10f);
                currentXOffset = Mathf.Lerp(currentXOffset, targetX, dt * 10f);
                currentBaseScale = Mathf.Lerp(currentBaseScale, targetScale, dt * 10f);
                currentBaseTilt = Mathf.Lerp(currentBaseTilt, targetTilt, dt * 10f);

                // Floating oscillation
                float wave = Mathf.Sin(time * m_FloatSpeed + phase);
                float yBob = wave * (m_IsFocused ? m_FloatAmplitude * 1.15f : m_FloatAmplitude) + currentLift;
                float rock = Mathf.Cos(time * (m_FloatSpeed * 0.85f) + phase) * m_FloatTiltAngle;

                // Heartbeat pulse when focused
                float pulse = m_IsFocused ? Mathf.Sin(time * 4.5f) * 0.016f : 0f;
                float finalScale = currentBaseScale + pulse;

                m_ButtonVisual.anchoredPosition = new Vector2(m_OriginalVisualPos.x + currentXOffset, m_OriginalVisualPos.y + yBob);
                m_ButtonVisual.localScale = new Vector3(finalScale, finalScale, 1f);
                m_ButtonVisual.localEulerAngles = new Vector3(0f, 0f, currentBaseTilt + rock);

                // Pointer bobbing ping-pong when pointer is active and focused
                if (m_LeftPointer != null && m_IsFocused)
                {
                    float pointerBob = Mathf.Sin(time * 7f) * 4.5f;
                    m_LeftPointer.anchoredPosition = new Vector2(m_OriginalPointerPos.x + pointerBob, m_OriginalPointerPos.y);
                }

                yield return null;
            }

            m_LivingIdleCoroutine = null;
        }

        private IEnumerator ConfirmPunchRoutine(Action onComplete)
        {
            // Immediate micro-shake impulse for physical punch impact
            UIMicroShake.Shake(1.2f, 0.09f);

            Color sparkCol = m_IsDestructiveOrBack ? new Color(1.0f, 0.45f, 0.2f) : new Color(1.0f, 0.87f, 0.35f);

            if (CinematicUI != null)
            {
                CinematicUI.TriggerImpactFlash(0.08f, 2.2f, sparkCol);
                CinematicUI.TriggerBorderPulse(0.18f, sparkCol, 3.5f);
            }

            if (CinematicUIFXManager.Instance != null && m_ButtonVisual != null)
            {
                CinematicUIFXManager.Instance.TriggerParticleBurst(m_ButtonVisual, Vector2.zero, UIParticleType.ElectricalSpark, 8, sparkCol);
                CinematicUIFXManager.Instance.TriggerParticleBurst(m_ButtonVisual, Vector2.zero, UIParticleType.DigitalShard, 4, sparkCol);
            }
            else if (CinematicUIParticleSystem.Instance != null && m_ButtonVisual != null)
            {
                CinematicUIParticleSystem.Instance.SpawnDirectionalBurst(
                    Vector2.zero, m_ButtonVisual, sparkCol,
                    m_IsDestructiveOrBack ? Vector2.left : Vector2.right, 8, 45f, 32f);
            }
            else if (RoboticPixelFXPool.Instance != null && m_ButtonVisual != null)
            {
                RoboticPixelFXPool.Instance.SpawnSparkBurst(Vector2.zero, m_ButtonVisual, sparkCol, 6, 16f);
            }

            float duration = m_ConfirmDuration;
            float halfDuration = duration * 0.45f;
            float returnDuration = duration - halfDuration;

            Vector2 basePos = m_IsFocused 
                ? new Vector2(m_OriginalVisualPos.x + m_SelectedOffsetX, m_OriginalVisualPos.y) 
                : m_OriginalVisualPos;
            Vector2 punchPos = new Vector2(basePos.x + (m_IsDestructiveOrBack ? -m_ConfirmPunchOffset : m_ConfirmPunchOffset), basePos.y);

            Vector3 baseScale = m_IsFocused ? new Vector3(m_SelectedScale, m_SelectedScale, 1f) : Vector3.one;
            Vector3 pressedScale = new Vector3(m_ConfirmPressedScale, m_ConfirmPressedScale, 1f);
            Vector3 baseRot = m_IsFocused ? new Vector3(0f, 0f, m_SelectedTilt) : Vector3.zero;
            Vector3 pressedRot = new Vector3(0f, 0f, -1.5f);

            // Phase 1: Physical compression (1.035 -> 0.94)
            float elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / halfDuration);
                float ease = 1f - Mathf.Pow(1f - t, 2f);

                m_ButtonVisual.anchoredPosition = Vector2.Lerp(basePos, punchPos, ease);
                m_ButtonVisual.localScale = Vector3.Lerp(baseScale, pressedScale, ease);
                m_ButtonVisual.localEulerAngles = Vector3.Lerp(baseRot, pressedRot, ease);
                yield return null;
            }

            // Phase 2: Rebound with punch overshoot
            elapsed = 0f;
            while (elapsed < returnDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / returnDuration);
                
                // Back ease for snappy physical rebound
                float ease = 1f + 1.25f * Mathf.Pow(t - 1f, 3f) + 0.6f * Mathf.Pow(t - 1f, 2f);

                m_ButtonVisual.anchoredPosition = Vector2.LerpUnclamped(punchPos, basePos, ease);
                m_ButtonVisual.localScale = Vector3.LerpUnclamped(pressedScale, baseScale, ease);
                m_ButtonVisual.localEulerAngles = Vector3.LerpUnclamped(pressedRot, baseRot, ease);
                yield return null;
            }

            m_ButtonVisual.anchoredPosition = basePos;
            m_ButtonVisual.localScale = baseScale;
            m_ButtonVisual.localEulerAngles = baseRot;
            m_ConfirmCoroutine = null;

            if (m_EnableFloating && isActiveAndEnabled)
            {
                StartFloating();
            }
            else if (m_IsFocused)
            {
                m_LivingIdleCoroutine = StartCoroutine(LivingIdleRoutine());
            }

            onComplete?.Invoke();
        }

        public void ResetToNormalState()
        {
            if (m_LivingIdleCoroutine != null)
            {
                StopCoroutine(m_LivingIdleCoroutine);
                m_LivingIdleCoroutine = null;
            }

            if (m_AnimationCoroutine != null)
            {
                StopCoroutine(m_AnimationCoroutine);
                m_AnimationCoroutine = null;
            }

            if (m_ConfirmCoroutine != null)
            {
                StopCoroutine(m_ConfirmCoroutine);
                m_ConfirmCoroutine = null;
            }

            m_IsFocused = false;

            if (!m_HasCapturedRestState) return;

            if (m_ButtonVisual != null)
            {
                m_ButtonVisual.anchoredPosition = m_OriginalVisualPos;
                m_ButtonVisual.localScale = Vector3.one;
                m_ButtonVisual.localEulerAngles = Vector3.zero;
            }

            if (m_LeftPointer != null)
            {
                m_LeftPointer.anchoredPosition = m_OriginalPointerPos;
                m_LeftPointer.gameObject.SetActive(false);
            }

            if (m_EnableFloating && isActiveAndEnabled)
            {
                StartFloating();
            }
        }

        private IEnumerator ButtonHolographicFlashRoutine(Image img, float duration)
        {
            if (img == null) yield break;
            Color baseColor = img.color;
            Color flashColor = new Color(
                Mathf.Min(1f, baseColor.r * 1.25f + 0.1f),
                Mathf.Min(1f, baseColor.g * 1.25f + 0.1f),
                Mathf.Min(1f, baseColor.b * 1.25f + 0.1f),
                baseColor.a
            );

            float elapsed = 0f;
            while (elapsed < duration && img != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float s = Mathf.Sin(t * Mathf.PI);
                img.color = Color.Lerp(baseColor, flashColor, s);
                yield return null;
            }
            if (img != null) img.color = baseColor;
        }
    }
}
