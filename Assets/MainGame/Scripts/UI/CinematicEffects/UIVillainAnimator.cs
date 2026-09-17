using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MainGame.UI.CinematicEffects
{
    /// <summary>
    /// Organic idle animation system for the Villain (Dr. Glitch).
    /// Provides subtle, believable 2D character life without deforming the pixel artwork:
    /// - Chest/shoulder breathing bob
    /// - Subtle head tilt sway
    /// - Dynamic left/right hand motion that physically drives the Title suspension anchors
    /// - Occasional glasses glint highlight
    /// - Zero transform drift: all movement is relative to captured rest coordinates.
    /// </summary>
    [DisallowMultipleComponent]
    public class UIVillainAnimator : MonoBehaviour
    {
        [Header("Hand Anchor Configuration (Relative to Villain Center)")]
        [Tooltip("Local offset from Villain RectTransform pivot to the left hand.")]
        [SerializeField] private Vector2 m_LeftHandOffset = new Vector2(-210f, 90f);

        [Tooltip("Local offset from Villain RectTransform pivot to the right hand.")]
        [SerializeField] private Vector2 m_RightHandOffset = new Vector2(210f, 90f);

        [Header("Glasses / Head Position for Glint")]
        [SerializeField] private Vector2 m_GlassesOffset = new Vector2(-15f, 280f);

        [Header("Idle Breathing & Motion")]
        [SerializeField] private bool m_EnableIdle = true;
        [SerializeField] private float m_BreathingSpeed = 1.35f;
        [SerializeField] private float m_BreathingAmplitude = 1.5f;
        [SerializeField] private float m_TiltSpeed = 0.85f;
        [SerializeField] private float m_TiltAmplitude = 0.4f;

        [Header("Hand Micro-Motion")]
        [SerializeField] private float m_HandMotionSpeed = 1.6f;
        [SerializeField] private float m_HandMotionAmplitude = 1.8f;

        [Header("Glasses Glint Timing")]
        [SerializeField] private float m_MinGlintInterval = 7.0f;
        [SerializeField] private float m_MaxGlintInterval = 14.0f;

        private RectTransform m_RectTransform;
        private Vector2 m_RestPosition;
        private Vector3 m_RestRotation;
        private Vector3 m_RestScale;
        private bool m_HasCapturedRest = false;

        private Vector2 m_CurrentLeftHandLocal;
        private Vector2 m_CurrentRightHandLocal;

        private Coroutine m_IdleRoutine;
        private Coroutine m_GlintRoutine;
        private bool m_IsActive = true;

        public Vector2 LeftHandOffset { get => m_LeftHandOffset; set => m_LeftHandOffset = value; }
        public Vector2 RightHandOffset { get => m_RightHandOffset; set => m_RightHandOffset = value; }
        public Vector2 GlassesOffset { get => m_GlassesOffset; set => m_GlassesOffset = value; }

        /// <summary>
        /// Real dynamic displacement (in canvas pixels) of the left hand relative to its rest pose.
        /// </summary>
        public Vector2 LeftHandDelta
        {
            get
            {
                EnsureRestCaptured();
                Vector2 bodyDelta = (m_RectTransform != null) ? (m_RectTransform.anchoredPosition - m_RestPosition) : Vector2.zero;
                Vector2 handLocalDelta = m_CurrentLeftHandLocal - m_LeftHandOffset;
                return bodyDelta + handLocalDelta;
            }
        }

        /// <summary>
        /// Real dynamic displacement (in canvas pixels) of the right hand relative to its rest pose.
        /// </summary>
        public Vector2 RightHandDelta
        {
            get
            {
                EnsureRestCaptured();
                Vector2 bodyDelta = (m_RectTransform != null) ? (m_RectTransform.anchoredPosition - m_RestPosition) : Vector2.zero;
                Vector2 handLocalDelta = m_CurrentRightHandLocal - m_RightHandOffset;
                return bodyDelta + handLocalDelta;
            }
        }

        /// <summary>
        /// Current dynamic canvas position of the Villain's left hand.
        /// </summary>
        public Vector2 LeftHandAnchorCanvasPosition
        {
            get
            {
                EnsureRestCaptured();
                if (m_RectTransform == null) return Vector2.zero;
                return m_RectTransform.anchoredPosition + m_CurrentLeftHandLocal;
            }
        }

        /// <summary>
        /// Current dynamic canvas position of the Villain's right hand.
        /// </summary>
        public Vector2 RightHandAnchorCanvasPosition
        {
            get
            {
                EnsureRestCaptured();
                if (m_RectTransform == null) return Vector2.zero;
                return m_RectTransform.anchoredPosition + m_CurrentRightHandLocal;
            }
        }

        public Vector3 LeftHandAnchorWorldPosition
        {
            get
            {
                EnsureRestCaptured();
                if (m_RectTransform == null) return transform.position;
                return m_RectTransform.TransformPoint(m_CurrentLeftHandLocal);
            }
        }

        public Vector3 RightHandAnchorWorldPosition
        {
            get
            {
                EnsureRestCaptured();
                if (m_RectTransform == null) return transform.position;
                return m_RectTransform.TransformPoint(m_CurrentRightHandLocal);
            }
        }

        private void Awake()
        {
            m_RectTransform = GetComponent<RectTransform>();
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
                m_CurrentLeftHandLocal = m_LeftHandOffset;
                m_CurrentRightHandLocal = m_RightHandOffset;
                m_HasCapturedRest = true;
            }
        }

        public void StartIdle()
        {
            Stop();
            EnsureRestCaptured();
            m_IsActive = true;
            m_IdleRoutine = StartCoroutine(IdleMotionRoutine());
            m_GlintRoutine = StartCoroutine(GlintTimerRoutine());
        }

        private IEnumerator IdleMotionRoutine()
        {
            float time = 0f;

            while (m_IsActive)
            {
                time += Time.unscaledDeltaTime;

                // 1. Organic breathing vertical displacement
                float breathY = Mathf.Sin(time * m_BreathingSpeed) * m_BreathingAmplitude;
                float breathX = Mathf.Sin(time * (m_BreathingSpeed * 0.5f)) * (m_BreathingAmplitude * 0.25f);

                // 2. Subtle shoulder/head tilt
                float tiltZ = Mathf.Sin(time * m_TiltSpeed) * m_TiltAmplitude;

                if (m_RectTransform != null)
                {
                    m_RectTransform.anchoredPosition = m_RestPosition + new Vector2(breathX, breathY);
                    m_RectTransform.localEulerAngles = m_RestRotation + new Vector3(0f, 0f, tiltZ);
                }

                // 3. Dynamic Hand motion (slightly out of phase with breathing)
                float handWaveL = Mathf.Sin(time * m_HandMotionSpeed) * m_HandMotionAmplitude;
                float handWaveR = Mathf.Sin(time * m_HandMotionSpeed + 0.85f) * m_HandMotionAmplitude;
                float handDriftX = Mathf.Cos(time * (m_HandMotionSpeed * 0.6f)) * (m_HandMotionAmplitude * 0.4f);

                m_CurrentLeftHandLocal = m_LeftHandOffset + new Vector2(-handDriftX, handWaveL);
                m_CurrentRightHandLocal = m_RightHandOffset + new Vector2(handDriftX, handWaveR);

                yield return null;
            }
        }

        private IEnumerator GlintTimerRoutine()
        {
            while (m_IsActive)
            {
                float waitTime = UnityEngine.Random.Range(m_MinGlintInterval, m_MaxGlintInterval);
                yield return new WaitForSecondsRealtime(waitTime);

                if (m_IsActive)
                {
                    TriggerGlassesGlint();
                }
            }
        }

        public void TriggerGlassesGlint()
        {
            if (m_RectTransform == null) return;

            Vector3 worldGlintPos = m_RectTransform.TransformPoint(m_GlassesOffset);
            if (CinematicUIParticleSystem.Instance != null)
            {
                CinematicUIParticleSystem.Instance.SpawnSparkBurst(worldGlintPos, new Color(1f, 1f, 1f, 0.95f), 4, 10f);
            }

            UIBackgroundLayerController layerCtrl = GetComponent<UIBackgroundLayerController>();
            if (layerCtrl != null)
            {
                layerCtrl.TriggerPulse(0.12f, 0.25f, new Color(1f, 0.95f, 0.8f, 1f));
            }
        }

        public void TriggerVillainEnergyPulse(float strength = 0.2f)
        {
            UIBackgroundLayerController layerCtrl = GetComponent<UIBackgroundLayerController>();
            if (layerCtrl != null)
            {
                layerCtrl.TriggerPulse(strength, 0.4f, new Color(0.95f, 0.2f, 0.2f, 1f));
            }

            if (CinematicUIParticleSystem.Instance != null)
            {
                CinematicUIParticleSystem.Instance.SpawnSparkBurst(LeftHandAnchorWorldPosition, new Color(1f, 0.35f, 0.2f, 1f), 3, 12f);
                CinematicUIParticleSystem.Instance.SpawnSparkBurst(RightHandAnchorWorldPosition, new Color(1f, 0.35f, 0.2f, 1f), 3, 12f);
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
            if (m_GlintRoutine != null)
            {
                StopCoroutine(m_GlintRoutine);
                m_GlintRoutine = null;
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
                m_CurrentLeftHandLocal = m_LeftHandOffset;
                m_CurrentRightHandLocal = m_RightHandOffset;
            }
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
            Gizmos.DrawWireSphere(LeftHandAnchorWorldPosition, 8f);
            Gizmos.DrawWireSphere(RightHandAnchorWorldPosition, 8f);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(m_RectTransform.TransformPoint(m_GlassesOffset), 6f);
        }
#endif
    }
}
