using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MainGame.UI.CinematicEffects
{
    /// <summary>
    /// Animates the Spark/FX layer and coordinates ambient pixel atmosphere:
    /// - Subtle alpha breathing and micro-flicker on the static Spark artwork
    /// - Occasional localized electrical spark bursts near machinery points
    /// - Sparse ambient pixel particles drifting slowly upward
    /// - Object-pooled zero-allocation lifecycle management.
    /// </summary>
    [DisallowMultipleComponent]
    public class UISparkAtmosphereSystem : MonoBehaviour
    {
        [Header("Spark Artwork Modulation")]
        [SerializeField] private bool m_EnableArtworkBreathing = true;
        [SerializeField] private float m_BaseAlpha = 0.85f;
        [SerializeField] private float m_AlphaPulseSpeed = 2.0f;
        [SerializeField] private float m_AlphaPulseAmplitude = 0.15f;

        [Header("Localized Spark Bursts")]
        [SerializeField] private bool m_EnableSparkBursts = true;
        [SerializeField] private float m_MinBurstInterval = 3.5f;
        [SerializeField] private float m_MaxBurstInterval = 7.0f;
        [SerializeField] private Color m_SparkColor = new Color(1f, 0.85f, 0.35f, 0.95f);

        [Header("Ambient Pixel Particles")]
        [SerializeField] private bool m_EnableAmbientParticles = true;
        [SerializeField] private float m_AmbientParticleInterval = 1.8f;
        [SerializeField] private Color m_AmbientParticleColor = new Color(0.65f, 0.85f, 1f, 0.6f);

        private CanvasGroup m_CanvasGroup;
        private Image m_Image;
        private RectTransform m_RectTransform;
        private Coroutine m_BreathingRoutine;
        private Coroutine m_SparkBurstRoutine;
        private Coroutine m_AmbientRoutine;
        private bool m_IsActive = true;

        private void Awake()
        {
            m_RectTransform = GetComponent<RectTransform>();
            m_Image = GetComponent<Image>();
            m_CanvasGroup = GetComponent<CanvasGroup>();
            if (m_CanvasGroup == null)
            {
                m_CanvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
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

            if (m_EnableArtworkBreathing)
            {
                m_BreathingRoutine = StartCoroutine(ArtworkBreathingRoutine());
            }
            if (m_EnableSparkBursts)
            {
                m_SparkBurstRoutine = StartCoroutine(SparkBurstRoutine());
            }
            if (m_EnableAmbientParticles)
            {
                m_AmbientRoutine = StartCoroutine(AmbientParticlesRoutine());
            }
        }

        private IEnumerator ArtworkBreathingRoutine()
        {
            float time = 0f;
            while (m_IsActive)
            {
                time += Time.unscaledDeltaTime;

                // Subtle sine breathing with occasional rapid micro-flicker
                float sine = Mathf.Sin(time * m_AlphaPulseSpeed) * m_AlphaPulseAmplitude;
                float flicker = (Mathf.Sin(time * 33f) > 0.95f) ? -0.1f : 0f;

                if (m_CanvasGroup != null)
                {
                    m_CanvasGroup.alpha = Mathf.Clamp01(m_BaseAlpha + sine + flicker);
                }

                yield return null;
            }
        }

        private IEnumerator SparkBurstRoutine()
        {
            while (m_IsActive)
            {
                float wait = UnityEngine.Random.Range(m_MinBurstInterval, m_MaxBurstInterval);
                yield return new WaitForSecondsRealtime(wait);

                if (!m_IsActive) yield break;

                // Pick a localized hotspot across the machinery / canvas area
                if (CinematicUIParticleSystem.Instance != null && m_RectTransform != null)
                {
                    Vector2 randomLocal = new Vector2(
                        UnityEngine.Random.Range(-500f, 500f),
                        UnityEngine.Random.Range(-200f, 350f)
                    );
                    Vector3 worldPos = m_RectTransform.TransformPoint(randomLocal);
                    CinematicUIParticleSystem.Instance.SpawnSparkBurst(worldPos, m_SparkColor, UnityEngine.Random.Range(3, 6), 14f);
                }
            }
        }

        private IEnumerator AmbientParticlesRoutine()
        {
            while (m_IsActive)
            {
                yield return new WaitForSecondsRealtime(m_AmbientParticleInterval);

                if (!m_IsActive) yield break;

                if (CinematicUIParticleSystem.Instance != null && m_RectTransform != null)
                {
                    // Spawn a tiny floating dust / pixel fragment rising slowly
                    Vector2 spawnLocal = new Vector2(
                        UnityEngine.Random.Range(-800f, 800f),
                        UnityEngine.Random.Range(-450f, -150f)
                    );
                    Vector3 worldPos = m_RectTransform.TransformPoint(spawnLocal);
                    CinematicUIParticleSystem.Instance.SpawnDataFloat(worldPos, m_AmbientParticleColor, 1, 15f);
                }
            }
        }

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
            if (m_SparkBurstRoutine != null)
            {
                StopCoroutine(m_SparkBurstRoutine);
                m_SparkBurstRoutine = null;
            }
            if (m_AmbientRoutine != null)
            {
                StopCoroutine(m_AmbientRoutine);
                m_AmbientRoutine = null;
            }
        }

        public void Reset()
        {
            Stop();
            if (m_CanvasGroup != null)
            {
                m_CanvasGroup.alpha = m_BaseAlpha;
            }
        }

        public void SetIdle()
        {
            StartAtmosphere();
        }
    }
}
