using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MainGame.UI.CinematicEffects
{
    /// <summary>
    /// Animates the Spark/FX layer with vibrant flying sparks and dynamic chromatic color cycling:
    /// - Continuous airborne flying sparks floating upward and swaying organically across the screen
    /// - Smooth real-time chromatic color cycling (HSV hue progression) across particles and spark artwork
    /// - Kinetic floating drift motion on the Spark artwork layer
    /// - Luminous flying spark bursts (zero glitch, zero scan distortion)
    /// - Object-pooled zero-allocation lifecycle management.
    /// </summary>
    [DisallowMultipleComponent]
    public class UISparkAtmosphereSystem : MonoBehaviour
    {
        [Header("Dynamic Chromatic Color Cycling")]
        [Tooltip("Enables real-time smooth color morphing across all flying sparks and spark artwork.")]
        [SerializeField] private bool m_EnableColorCycling = true;
        [Tooltip("Speed of the chromatic hue cycle.")]
        [SerializeField] private float m_ColorCycleSpeed = 0.18f;
        [Tooltip("Saturation of the cycling colors.")]
        [SerializeField] private float m_ColorSaturation = 0.88f;
        [Tooltip("Brightness value of the cycling colors.")]
        [SerializeField] private float m_ColorValue = 1.0f;
        [SerializeField] private Color m_BaseSparkColor = new Color(0.35f, 0.85f, 1f, 1f);

        [Header("Continuous Flying Sparks")]
        [Tooltip("Spawns airborne sparks that float upward and drift across the screen.")]
        [SerializeField] private bool m_EnableFlyingSparks = true;
        [Tooltip("Interval between individual flying sparks.")]
        [SerializeField] private float m_FlyingSparkInterval = 0.12f;
        [Tooltip("Average vertical flight speed of rising sparks.")]
        [SerializeField] private float m_FlightSpeedY = 180f;
        [Tooltip("Maximum horizontal wind drift velocity.")]
        [SerializeField] private float m_WindDriftX = 40f;
        [Tooltip("Flight duration of flying sparks in seconds.")]
        [SerializeField] private float m_SparkLifetime = 3.0f;
        [Tooltip("Pixel size of flying sparks.")]
        [SerializeField] private float m_SparkPixelSize = 5f;
        [Tooltip("Sinusoidal wind wobble amplitude.")]
        [SerializeField] private float m_WobbleAmplitude = 22f;

        [Header("Spark Artwork Floating Motion")]
        [Tooltip("Gentle floating sway applied to the Spark artwork transform.")]
        [SerializeField] private bool m_EnableArtworkFloating = true;
        [SerializeField] private float m_FloatingAmplitude = 7.0f;
        [SerializeField] private float m_FloatingSpeed = 1.2f;

        [Header("Spark Artwork Modulation")]
        [SerializeField] private bool m_EnableArtworkBreathing = true;
        [SerializeField] private float m_BaseAlpha = 0.85f;
        [SerializeField] private float m_AlphaPulseSpeed = 2.0f;
        [SerializeField] private float m_AlphaPulseAmplitude = 0.15f;

        [Header("Luminous Flying Bursts")]
        [SerializeField] private bool m_EnableSparkBursts = true;
        [SerializeField] private float m_MinBurstInterval = 3.5f;
        [SerializeField] private float m_MaxBurstInterval = 6.5f;

        private CanvasGroup m_CanvasGroup;
        private Image m_Image;
        private RectTransform m_RectTransform;
        private UIBackgroundLayerController m_LayerController;

        private Coroutine m_BreathingRoutine;
        private Coroutine m_FlyingSparksRoutine;
        private Coroutine m_FloatingMotionRoutine;
        private Coroutine m_ColorCycleRoutine;
        private Coroutine m_SparkBurstRoutine;

        private bool m_IsActive = true;
        private float m_CurrentHue = 0.55f; // Initial vibrant cyan/plasma hue
        private Vector2 m_InitialAnchoredPosition;
        private bool m_HasCapturedInitialPos = false;

        public Color CurrentLiveColor => Color.HSVToRGB(m_CurrentHue, m_ColorSaturation, m_ColorValue);
        public Color NextMorphColor => Color.HSVToRGB(Mathf.Repeat(m_CurrentHue + 0.22f, 1f), m_ColorSaturation * 0.9f, m_ColorValue);

        private void Awake()
        {
            m_RectTransform = GetComponent<RectTransform>();
            if (m_RectTransform != null && !m_HasCapturedInitialPos)
            {
                m_InitialAnchoredPosition = m_RectTransform.anchoredPosition;
                m_HasCapturedInitialPos = true;
            }

            m_Image = GetComponent<Image>();
            m_CanvasGroup = GetComponent<CanvasGroup>();
            if (m_CanvasGroup == null)
            {
                m_CanvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
            m_LayerController = GetComponent<UIBackgroundLayerController>();
        }

        private void Start()
        {
            StartAtmosphere();
        }

        private void OnEnable()
        {
            StartAtmosphere();
        }

        private void OnDisable()
        {
            Stop();
        }

        public void StartAtmosphere()
        {
            Stop();
            m_IsActive = true;

            if (m_EnableColorCycling)
            {
                m_ColorCycleRoutine = StartCoroutine(ColorCyclingRoutine());
            }
            if (m_EnableArtworkBreathing)
            {
                m_BreathingRoutine = StartCoroutine(ArtworkBreathingRoutine());
            }
            if (m_EnableArtworkFloating)
            {
                m_FloatingMotionRoutine = StartCoroutine(ArtworkFloatingMotionRoutine());
            }
            if (m_EnableFlyingSparks)
            {
                m_FlyingSparksRoutine = StartCoroutine(FlyingSparksEmitterRoutine());
            }
            if (m_EnableSparkBursts)
            {
                m_SparkBurstRoutine = StartCoroutine(SparkBurstRoutine());
            }
        }

        #region Dynamic Color Cycling

        private IEnumerator ColorCyclingRoutine()
        {
            while (m_IsActive)
            {
                m_CurrentHue = Mathf.Repeat(m_CurrentHue + Time.unscaledDeltaTime * m_ColorCycleSpeed, 1f);
                Color liveColor = CurrentLiveColor;

                // Tint spark artwork image
                if (m_Image != null)
                {
                    m_Image.color = new Color(liveColor.r, liveColor.g, liveColor.b, m_Image.color.a);
                }

                // Sync pulse color with live dynamic chromatic color
                if (m_LayerController != null)
                {
                    m_LayerController.PulseColor = liveColor;
                }

                yield return null;
            }
        }

        #endregion

        #region Artwork Motion & Breathing

        private IEnumerator ArtworkBreathingRoutine()
        {
            float time = 0f;
            while (m_IsActive)
            {
                time += Time.unscaledDeltaTime;

                // Subtle sine breathing with gentle micro-twinkle
                float sine = Mathf.Sin(time * m_AlphaPulseSpeed) * m_AlphaPulseAmplitude;
                float twinkle = (Mathf.Sin(time * 28f) > 0.94f) ? -0.08f : 0f;

                if (m_CanvasGroup != null)
                {
                    m_CanvasGroup.alpha = Mathf.Clamp01(m_BaseAlpha + sine + twinkle);
                }

                yield return null;
            }
        }

        private IEnumerator ArtworkFloatingMotionRoutine()
        {
            if (m_RectTransform == null) yield break;
            if (!m_HasCapturedInitialPos)
            {
                m_InitialAnchoredPosition = m_RectTransform.anchoredPosition;
                m_HasCapturedInitialPos = true;
            }

            float time = 0f;
            while (m_IsActive)
            {
                time += Time.unscaledDeltaTime * m_FloatingSpeed;

                // Lissajous figure-8 floating sway
                float offsetX = Mathf.Sin(time) * m_FloatingAmplitude;
                float offsetY = Mathf.Cos(time * 1.3f) * (m_FloatingAmplitude * 0.75f);

                m_RectTransform.anchoredPosition = m_InitialAnchoredPosition + new Vector2(offsetX, offsetY);
                yield return null;
            }
        }

        #endregion

        #region Flying Sparks Emitter

        private IEnumerator FlyingSparksEmitterRoutine()
        {
            while (m_IsActive)
            {
                yield return new WaitForSecondsRealtime(m_FlyingSparkInterval);

                if (!m_IsActive) yield break;

                if (CinematicUIParticleSystem.Instance != null && m_RectTransform != null)
                {
                    // Spawn across screen width in the lower/mid region
                    Vector2 spawnLocal = new Vector2(
                        UnityEngine.Random.Range(-850f, 850f),
                        UnityEngine.Random.Range(-520f, -120f)
                    );
                    Vector3 worldPos = m_RectTransform.TransformPoint(spawnLocal);

                    // Upward velocity with wind drift
                    Vector2 velocity = new Vector2(
                        UnityEngine.Random.Range(-m_WindDriftX, m_WindDriftX),
                        UnityEngine.Random.Range(m_FlightSpeedY * 0.8f, m_FlightSpeedY * 1.35f)
                    );

                    float duration = UnityEngine.Random.Range(m_SparkLifetime * 0.8f, m_SparkLifetime * 1.3f);
                    float size = UnityEngine.Random.Range(m_SparkPixelSize * 0.75f, m_SparkPixelSize * 1.35f);
                    float wobble = UnityEngine.Random.Range(m_WobbleAmplitude * 0.8f, m_WobbleAmplitude * 1.3f);

                    // Chromatic color transition: starts at current cycling hue, morphs towards target hue
                    Color startCol = CurrentLiveColor;
                    Color endCol = NextMorphColor;

                    CinematicUIParticleSystem.Instance.SpawnFloatingSpark(
                        worldPos,
                        startCol,
                        endCol,
                        velocity,
                        duration,
                        size,
                        wobble
                    );
                }
            }
        }

        private IEnumerator SparkBurstRoutine()
        {
            while (m_IsActive)
            {
                float wait = UnityEngine.Random.Range(m_MinBurstInterval, m_MaxBurstInterval);
                yield return new WaitForSecondsRealtime(wait);

                if (!m_IsActive) yield break;

                // Luminous flying spark burst: zero scan, zero glitch!
                TriggerLuminousFlyingBurst(UnityEngine.Random.Range(7, 12));
            }
        }

        /// <summary>
        /// Emits a burst of luminous flying sparks that radiate outward and float upward while morphing colors.
        /// </summary>
        public void TriggerLuminousFlyingBurst(int count = 10)
        {
            if (CinematicUIParticleSystem.Instance == null || m_RectTransform == null) return;

            // Pick a localized hotspot across the machinery/canvas
            Vector2 centerLocal = new Vector2(
                UnityEngine.Random.Range(-550f, 550f),
                UnityEngine.Random.Range(-250f, 250f)
            );

            Color startCol = CurrentLiveColor;
            Color endCol = NextMorphColor;

            for (int i = 0; i < count; i++)
            {
                Vector2 spawnPos = centerLocal + new Vector2(UnityEngine.Random.Range(-30f, 30f), UnityEngine.Random.Range(-30f, 30f));
                Vector3 worldPos = m_RectTransform.TransformPoint(spawnPos);

                // Fan outward and rise
                float angle = UnityEngine.Random.Range(30f, 150f) * Mathf.Deg2Rad;
                float speed = UnityEngine.Random.Range(180f, 360f);
                Vector2 velocity = new Vector2(Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed);

                float duration = UnityEngine.Random.Range(1.8f, 3.2f);
                float size = UnityEngine.Random.Range(4.5f, 7.5f);
                float wobble = UnityEngine.Random.Range(15f, 30f);

                CinematicUIParticleSystem.Instance.SpawnFloatingSpark(
                    worldPos,
                    startCol,
                    endCol,
                    velocity,
                    duration,
                    size,
                    wobble
                );
            }
        }

        /// <summary>
        /// Backwards-compatible method: triggers a luminous flying burst instead of digital glitches.
        /// </summary>
        public void TriggerSparkGlitch(ControlledGlitchType type = ControlledGlitchType.TypeB_DigitalTear)
        {
            TriggerLuminousFlyingBurst(8);
        }

        #endregion

        public void Play()
        {
            StartAtmosphere();
        }

        public void Stop()
        {
            m_IsActive = false;
            if (m_BreathingRoutine != null)
            {
                StopCoroutine(m_BreathingRoutine);
                m_BreathingRoutine = null;
            }
            if (m_FlyingSparksRoutine != null)
            {
                StopCoroutine(m_FlyingSparksRoutine);
                m_FlyingSparksRoutine = null;
            }
            if (m_FloatingMotionRoutine != null)
            {
                StopCoroutine(m_FloatingMotionRoutine);
                m_FloatingMotionRoutine = null;
            }
            if (m_ColorCycleRoutine != null)
            {
                StopCoroutine(m_ColorCycleRoutine);
                m_ColorCycleRoutine = null;
            }
            if (m_SparkBurstRoutine != null)
            {
                StopCoroutine(m_SparkBurstRoutine);
                m_SparkBurstRoutine = null;
            }

            if (m_RectTransform != null && m_HasCapturedInitialPos)
            {
                m_RectTransform.anchoredPosition = m_InitialAnchoredPosition;
            }
        }

        public void Reset()
        {
            Stop();
            if (m_CanvasGroup != null)
            {
                m_CanvasGroup.alpha = m_BaseAlpha;
            }
            if (m_RectTransform != null && m_HasCapturedInitialPos)
            {
                m_RectTransform.anchoredPosition = m_InitialAnchoredPosition;
            }
        }

        public void SetIdle()
        {
            StartAtmosphere();
        }
    }
}
