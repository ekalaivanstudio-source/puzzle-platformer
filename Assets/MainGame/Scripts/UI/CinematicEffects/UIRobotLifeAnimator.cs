using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MainGame.UI.CinematicEffects
{
    public enum RobotEnergyPhase
    {
        Idle,
        EnergyBuild,
        Pulse,
        ParticlesRise,
        Fade
    }

    /// <summary>
    /// Living idle animation and energy system for the Robot (Byte).
    /// Operates independently from the Villain:
    /// - Subtle body bob and chassis tilt
    /// - Eye/display pulse
    /// - 6-12s randomized energy charging cycle (Idle -> Build -> Pulse -> Rise -> Fade)
    /// - Upward-moving pixel particles and blue energy fragments via zero-allocation pool
    /// - Zero transform drift: strictly relative to captured baseline coordinates.
    /// </summary>
    [DisallowMultipleComponent]
    public class UIRobotLifeAnimator : MonoBehaviour
    {
        [Header("Idle Bobbing & Chassis Tilt")]
        [SerializeField] private bool m_EnableIdle = true;
        [SerializeField] private float m_BobSpeed = 1.8f;
        [SerializeField] private float m_BobAmplitude = 1.2f;
        [SerializeField] private float m_ChassisTiltSpeed = 1.1f;
        [SerializeField] private float m_ChassisTiltAmplitude = 0.35f;

        [Header("Eye / Core Pulse")]
        [SerializeField] private Vector2 m_EyeOffset = new Vector2(0f, 60f);
        [SerializeField] private Color m_EyeColor = new Color(0.35f, 0.85f, 1f, 1f);
        [SerializeField] private float m_EyePulseInterval = 3.5f;

        [Header("Energy Cycle Configuration")]
        [SerializeField] private bool m_EnableEnergyCycle = true;
        [SerializeField] private float m_MinCycleInterval = 7.0f;
        [SerializeField] private float m_MaxCycleInterval = 13.0f;
        [SerializeField] private Color m_EnergyColor = new Color(0.3f, 0.88f, 1.0f, 1.0f);

        private RectTransform m_RectTransform;
        private Vector2 m_RestPosition;
        private Vector3 m_RestRotation;
        private Vector3 m_RestScale;
        private bool m_HasCapturedRest = false;

        private Coroutine m_IdleRoutine;
        private Coroutine m_EnergyCycleRoutine;
        private Coroutine m_EyePulseRoutine;
        private bool m_IsActive = true;
        private RobotEnergyPhase m_CurrentPhase = RobotEnergyPhase.Idle;
        private UIBackgroundLayerController m_LayerController;

        public Vector3 EyeWorldPosition
        {
            get
            {
                EnsureRestCaptured();
                if (m_RectTransform == null) return transform.position;
                return m_RectTransform.TransformPoint(m_EyeOffset);
            }
        }

        private void Awake()
        {
            m_RectTransform = GetComponent<RectTransform>();
            m_LayerController = GetComponent<UIBackgroundLayerController>();
            EnsureRestCaptured();
        }

        private void Start()
        {
            EnsureRestCaptured();
            if (m_EnableIdle)
            {
                StartIdle();
            }
        }

        private void OnEnable()
        {
            EnsureRestCaptured();
            if (m_EnableIdle)
            {
                StartIdle();
            }
        }

        private void OnDisable()
        {
            Stop();
        }

        public void EnsureRestCaptured()
        {
            if (m_HasCapturedRest) return;

            if (m_RectTransform == null)
            {
                m_RectTransform = GetComponent<RectTransform>();
            }

            if (m_RectTransform != null)
            {
                m_RestPosition = m_RectTransform.anchoredPosition;
                m_RestRotation = m_RectTransform.localEulerAngles;
                m_RestScale = m_RectTransform.localScale;
                m_HasCapturedRest = true;
            }
        }

        public void StartIdle()
        {
            Stop();
            EnsureRestCaptured();
            m_IsActive = true;
            m_IdleRoutine = StartCoroutine(IdleRoutine());
            m_EyePulseRoutine = StartCoroutine(EyePulseRoutine());
            if (m_EnableEnergyCycle)
            {
                m_EnergyCycleRoutine = StartCoroutine(EnergyCycleTimerRoutine());
            }
        }

        private IEnumerator IdleRoutine()
        {
            float time = 0f;

            while (m_IsActive)
            {
                time += Time.unscaledDeltaTime;

                // Subtle body bob (distinct frequency from Villain)
                float bobY = Mathf.Sin(time * m_BobSpeed + 1.2f) * m_BobAmplitude;
                float tiltZ = Mathf.Sin(time * m_ChassisTiltSpeed) * m_ChassisTiltAmplitude;

                if (m_RectTransform != null)
                {
                    m_RectTransform.anchoredPosition = m_RestPosition + new Vector2(0f, bobY);
                    m_RectTransform.localEulerAngles = m_RestRotation + new Vector3(0f, 0f, tiltZ);
                }

                yield return null;
            }
        }

        private IEnumerator EyePulseRoutine()
        {
            while (m_IsActive)
            {
                yield return new WaitForSecondsRealtime(m_EyePulseInterval);

                if (!m_IsActive) yield break;

                // Small eye pulse
                if (m_LayerController != null)
                {
                    m_LayerController.TriggerPulse(0.08f, 0.20f, m_EyeColor);
                }

                if (CinematicUIParticleSystem.Instance != null)
                {
                    CinematicUIParticleSystem.Instance.SpawnEnergyBubble(EyeWorldPosition, m_EyeColor, 0.18f);
                }
            }
        }

        private IEnumerator EnergyCycleTimerRoutine()
        {
            while (m_IsActive)
            {
                float wait = UnityEngine.Random.Range(m_MinCycleInterval, m_MaxCycleInterval);
                yield return new WaitForSecondsRealtime(wait);

                if (!m_IsActive) yield break;

                yield return StartCoroutine(PerformEnergyCycle());
            }
        }

        /// <summary>
        /// IDLE -> ENERGY BUILD -> BLUE PULSE -> PARTICLES RISE -> FADE -> IDLE
        /// </summary>
        public IEnumerator PerformEnergyCycle()
        {
            // 1. ENERGY BUILD (0.35s)
            m_CurrentPhase = RobotEnergyPhase.EnergyBuild;
            float buildDur = 0.35f;
            float elapsed = 0f;
            while (elapsed < buildDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / buildDur;
                float shake = Mathf.Sin(elapsed * 45f) * (0.8f * t);
                if (m_RectTransform != null)
                {
                    m_RectTransform.anchoredPosition = m_RestPosition + new Vector2(shake, 0f);
                }
                yield return null;
            }

            // 2. BLUE ENERGY PULSE (Peak)
            m_CurrentPhase = RobotEnergyPhase.Pulse;
            if (m_LayerController != null)
            {
                m_LayerController.PulseOutline(0.35f, 0.45f, m_EnergyColor);
                m_LayerController.TriggerPulse(0.22f, 0.35f, m_EnergyColor);
            }

            if (CinematicUIParticleSystem.Instance != null)
            {
                CinematicUIParticleSystem.Instance.SpawnSparkBurst(EyeWorldPosition, m_EnergyColor, 6, 18f);
            }

            // 3. PARTICLES RISE
            m_CurrentPhase = RobotEnergyPhase.ParticlesRise;
            if (CinematicUIParticleSystem.Instance != null)
            {
                Vector3 botCenter = transform.position;
                CinematicUIParticleSystem.Instance.SpawnDataFloat(botCenter, m_EnergyColor, 4, 30f);
            }

            yield return new WaitForSecondsRealtime(0.25f);

            // 4. ENERGY FADE
            m_CurrentPhase = RobotEnergyPhase.Fade;
            yield return new WaitForSecondsRealtime(0.20f);

            // 5. RETURN TO IDLE
            m_CurrentPhase = RobotEnergyPhase.Idle;
        }

        public void TriggerSubtleEnergyPulse(float strength = 0.15f)
        {
            if (m_LayerController != null)
            {
                m_LayerController.PulseOutline(strength * 1.2f, 0.28f, m_EnergyColor);
                m_LayerController.TriggerPulse(strength, 0.25f, m_EnergyColor);
            }

            if (CinematicUIParticleSystem.Instance != null)
            {
                CinematicUIParticleSystem.Instance.SpawnSparkBurst(EyeWorldPosition, m_EnergyColor, 3, 12f);
            }
        }

        public void Play()
        {
            StartIdle();
        }

        public void Stop()
        {
            m_IsActive = false;
            if (m_IdleRoutine != null)
            {
                StopCoroutine(m_IdleRoutine);
                m_IdleRoutine = null;
            }
            if (m_EyePulseRoutine != null)
            {
                StopCoroutine(m_EyePulseRoutine);
                m_EyePulseRoutine = null;
            }
            if (m_EnergyCycleRoutine != null)
            {
                StopCoroutine(m_EnergyCycleRoutine);
                m_EnergyCycleRoutine = null;
            }
        }

        public void Reset()
        {
            Stop();
            if (m_HasCapturedRest && m_RectTransform != null)
            {
                m_RectTransform.anchoredPosition = m_RestPosition;
                m_RectTransform.localEulerAngles = m_RestRotation;
                m_RectTransform.localScale = m_RestScale;
            }
            m_CurrentPhase = RobotEnergyPhase.Idle;
        }

        public void SetIdle()
        {
            StartIdle();
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (m_RectTransform == null) m_RectTransform = GetComponent<RectTransform>();
            if (m_RectTransform == null) return;

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(EyeWorldPosition, 6f);
        }
#endif
    }
}
