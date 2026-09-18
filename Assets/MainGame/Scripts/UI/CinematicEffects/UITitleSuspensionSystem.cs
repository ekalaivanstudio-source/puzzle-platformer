using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using MainGame.UI.CinematicEffects.BeatSync;

namespace MainGame.UI.CinematicEffects
{
    /// <summary>
    /// Physical 2-point spring-mass-damper suspension system for the Main Menu Title.
    /// Simulates the title physically hanging from the Villain's two hands via procedural energy links:
    /// - Dual-anchor dynamic tracking
    /// - Deterministic second-order spring physics (inertia, damping, natural return)
    /// - Controlled angular sway (clamped to ±1.0° - 1.5°)
    /// - Reactive attachment sparks and energy string tension modulation
    /// - Zero transform drift: strictly relative to captured baseline coordinates.
    /// </summary>
    [DisallowMultipleComponent]
    public class UITitleSuspensionSystem : MonoBehaviour
    {
        [Header("Villain & Anchor Connections")]
        [SerializeField] private UIVillainAnimator m_VillainAnimator;
        [SerializeField] private UITitleEnergyLink m_LeftEnergyLink;
        [SerializeField] private UITitleEnergyLink m_RightEnergyLink;

        [Header("Title Attachment Points (Local to Title Center)")]
        [Tooltip("Local offset from title center to the left attachment link.")]
        [SerializeField] private Vector2 m_LeftAttachmentOffset = new Vector2(-220f, 120f);

        [Tooltip("Local offset from title center to the right attachment link.")]
        [SerializeField] private Vector2 m_RightAttachmentOffset = new Vector2(220f, 120f);

        [Header("Spring-Mass-Damper Physics")]
        [Tooltip("Natural oscillation frequency (Hz). Higher = tighter, faster spring.")]
        [SerializeField] private float m_Frequency = 2.8f;

        [Tooltip("Damping ratio. ~0.70-0.75 gives natural overshoot and smooth return.")]
        [Range(0.2f, 1.2f)]
        [SerializeField] private float m_DampingRatio = 0.72f;

        [Tooltip("Maximum allowed angular tilt sway in degrees (recommended ±1.0° - 1.5°).")]
        [Range(0.5f, 2.0f)]
        [SerializeField] private float m_MaxAngularTilt = 1.35f;

        [Tooltip("Maximum allowed vertical/horizontal displacement in pixels.")]
        [Range(2f, 24f)]
        [SerializeField] private float m_MaxDisplacement = 12.0f;

        [Header("Attachment Reactive VFX")]
        [SerializeField] private bool m_EnableReactiveSparks = true;
        [SerializeField] private float m_SparkVelocityThreshold = 6.0f;
        [SerializeField] private float m_MinSparkInterval = 1.2f;

        private RectTransform m_RectTransform;
        private Vector2 m_RestPosition;
        private Vector3 m_RestRotation;
        private Vector3 m_RestScale;
        private bool m_HasCapturedRest = false;

        // Physics state
        private Vector2 m_PosDisplacement;
        private Vector2 m_PosVelocity;
        private float m_AngleDisplacement;
        private float m_AngleVelocity;

        private float m_LastSparkTime = -10f;
        private bool m_IsActive = true;
        private UIBackgroundLayerController m_TitleShaderCtrl;

        public UIVillainAnimator VillainAnimator { get => m_VillainAnimator; set => m_VillainAnimator = value; }
        public UITitleEnergyLink LeftEnergyLink { get => m_LeftEnergyLink; set => m_LeftEnergyLink = value; }
        public UITitleEnergyLink RightEnergyLink { get => m_RightEnergyLink; set => m_RightEnergyLink = value; }

        public Vector3 LeftAttachmentWorldPosition
        {
            get
            {
                EnsureRestCaptured();
                if (m_RectTransform == null) return transform.position;
                return m_RectTransform.TransformPoint(m_LeftAttachmentOffset);
            }
        }

        public Vector3 RightAttachmentWorldPosition
        {
            get
            {
                EnsureRestCaptured();
                if (m_RectTransform == null) return transform.position;
                return m_RectTransform.TransformPoint(m_RightAttachmentOffset);
            }
        }

        private void Awake()
        {
            m_RectTransform = GetComponent<RectTransform>();
            m_TitleShaderCtrl = GetComponent<UIBackgroundLayerController>() ?? GetComponentInChildren<UIBackgroundLayerController>();
            EnsureRestCaptured();
            CleanupEnergyLinks();
        }

        private void Start()
        {
            EnsureRestCaptured();
            ResolveVillainReference();
            CleanupEnergyLinks();
        }

        private void OnEnable()
        {
            EnsureRestCaptured();
            CleanupEnergyLinks();
            m_IsActive = true;
            MusicBeatManager.OnBeat += HandleBeat;
        }

        private void OnValidate()
        {
            CleanupEnergyLinks();
        }

        private void OnDisable()
        {
            MusicBeatManager.OnBeat -= HandleBeat;
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
                // Enforce y = 0 if it was captured around 125 (prevent covering villain face)
                if (Mathf.Abs(m_RestPosition.y) > 0.01f && Mathf.Abs(m_RestPosition.y - 125f) < 50f)
                {
                    m_RestPosition.y = 0f;
                    m_RectTransform.anchoredPosition = m_RestPosition;
                }

                m_RestRotation = m_RectTransform.localEulerAngles;
                m_RestScale = m_RectTransform.localScale;
                m_HasCapturedRest = true;
            }
        }

        public void ResolveVillainReference()
        {
            if (m_VillainAnimator != null) return;
            m_VillainAnimator = FindAnyObjectByType<UIVillainAnimator>();
        }

        public void CleanupEnergyLinks()
        {
            Transform linkContainer = transform.parent != null ? transform.parent : transform;
            if (linkContainer != null)
            {
                Transform l = linkContainer.Find("TitleEnergyLink_Left");
                if (l != null)
                {
                    if (Application.isPlaying) Destroy(l.gameObject);
                    else DestroyImmediate(l.gameObject);
                }
                Transform r = linkContainer.Find("TitleEnergyLink_Right");
                if (r != null)
                {
                    if (Application.isPlaying) Destroy(r.gameObject);
                    else DestroyImmediate(r.gameObject);
                }
            }

            Transform childL = transform.Find("TitleEnergyLink_Left");
            if (childL != null)
            {
                if (Application.isPlaying) Destroy(childL.gameObject);
                else DestroyImmediate(childL.gameObject);
            }
            Transform childR = transform.Find("TitleEnergyLink_Right");
            if (childR != null)
            {
                if (Application.isPlaying) Destroy(childR.gameObject);
                else DestroyImmediate(childR.gameObject);
            }

            m_LeftEnergyLink = null;
            m_RightEnergyLink = null;
        }

        public void CreateEnergyLinksIfMissing()
        {
            CleanupEnergyLinks();
        }

        private void HandleBeat(BeatEvent evt)
        {
            OnBeatImpulse(evt.IsDownbeat);
        }

        public void OnBeatImpulse(bool isDownbeat)
        {
            if (!m_IsActive) return;
            float kickY = isDownbeat ? -3.5f : -1.8f;
            float torque = UnityEngine.Random.Range(-0.45f, 0.45f);
            Impulse(new Vector2(0f, kickY), torque);

            if (m_TitleShaderCtrl != null && isDownbeat)
            {
                m_TitleShaderCtrl.PulseOutlineImmediate(1.8f, 0.02f, 0.04f, 0.16f, null, "TitleBeatImpulse");
            }

            if (isDownbeat && UnityEngine.Random.value < 0.35f)
            {
                TriggerAttachmentSparks();
            }
        }

        private void Update()
        {
            if (!m_IsActive || m_RectTransform == null) return;

            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f); // Prevent physics explosion on lag spike
            if (dt <= 0.0001f) return;

            float time = Time.unscaledTime;

            // ─── 1. COMPUTE DRIVING FORCES FROM VILLAIN HANDS & AMBIENT DRIFT ──
            Vector2 handDeltaAvg = Vector2.zero;
            float differentialY = 0f;

            if (m_VillainAnimator != null)
            {
                Vector2 deltaL = m_VillainAnimator.LeftHandDelta;
                Vector2 deltaR = m_VillainAnimator.RightHandDelta;

                handDeltaAvg = (deltaL + deltaR) * 0.5f;
                differentialY = (deltaL.y - deltaR.y);
            }

            // Continuous ambient physical hanging bob and pendulum sway (signboard suspended in air)
            float ambientBobY = Mathf.Sin(time * 1.8f) * 3.5f;
            float ambientDriftX = Mathf.Cos(time * 0.9f) * 1.5f;
            float ambientSwayAngle = Mathf.Sin(time * 1.15f + 0.4f) * 1.25f;

            // Target spring targets
            Vector2 targetPos = (handDeltaAvg * 0.85f) + new Vector2(ambientDriftX, ambientBobY);
            targetPos.x = Mathf.Clamp(targetPos.x, -m_MaxDisplacement, m_MaxDisplacement);
            targetPos.y = Mathf.Clamp(targetPos.y, -m_MaxDisplacement, m_MaxDisplacement);

            float targetAngle = (differentialY * 0.65f) + ambientSwayAngle;
            targetAngle = Mathf.Clamp(targetAngle, -m_MaxAngularTilt, m_MaxAngularTilt);

            // ─── 2. SECOND-ORDER SPRING DYNAMICS (POSITION) ────────────────────
            float omega = 2.0f * Mathf.PI * m_Frequency;
            float dampingCoeff = 2.0f * m_DampingRatio * omega;
            float stiffness = omega * omega;

            Vector2 posForce = (targetPos - m_PosDisplacement) * stiffness - (m_PosVelocity * dampingCoeff);
            m_PosVelocity += posForce * dt;
            m_PosDisplacement += m_PosVelocity * dt;

            // ─── 3. SECOND-ORDER SPRING DYNAMICS (ROTATION) ────────────────────
            float angleOmega = omega * 1.15f;
            float angleDampingCoeff = 2.0f * m_DampingRatio * angleOmega;
            float angleStiffness = angleOmega * angleOmega;

            float angleForce = (targetAngle - m_AngleDisplacement) * angleStiffness - (m_AngleVelocity * angleDampingCoeff);
            m_AngleVelocity += angleForce * dt;
            m_AngleDisplacement += m_AngleVelocity * dt;

            m_AngleDisplacement = Mathf.Clamp(m_AngleDisplacement, -m_MaxAngularTilt, m_MaxAngularTilt);

            // ─── 4. APPLY TO RECTTRANSFORM (ZERO TRANSFORM DRIFT) ──────────────
            m_RectTransform.anchoredPosition = m_RestPosition + m_PosDisplacement;
            m_RectTransform.localEulerAngles = m_RestRotation + new Vector3(0f, 0f, m_AngleDisplacement);

            // ─── 5. REACTIVE ATTACHMENT SPARKS & ENERGY ────────────────────────
            float kineticSpeed = m_PosVelocity.magnitude + Mathf.Abs(m_AngleVelocity) * 2.0f;
            if (m_EnableReactiveSparks && kineticSpeed > m_SparkVelocityThreshold)
            {
                if (Time.unscaledTime - m_LastSparkTime > m_MinSparkInterval)
                {
                    m_LastSparkTime = Time.unscaledTime;
                    TriggerAttachmentSparks();
                }
            }
        }

        public void TriggerAttachmentSparks()
        {
            if (m_TitleShaderCtrl != null)
            {
                m_TitleShaderCtrl.PulseOutlineImmediate(1.9f, 0.02f, 0.05f, 0.18f, new Color(0.22f, 0.88f, 1f, 1f), "TitleSparks");
            }
        }

        public void Impulse(Vector2 force, float angularTorque = 0f)
        {
            m_PosVelocity += force;
            m_AngleVelocity += angularTorque;
        }

        public void TriggerTitleGlitch(float duration = 0.15f)
        {
            if (MainMenuBackgroundDirector.Instance != null && MainMenuBackgroundDirector.Instance.BgRedController != null)
            {
                MainMenuBackgroundDirector.Instance.BgRedController.TriggerGlitch(duration);
            }
        }

        public void Play()
        {
            m_IsActive = true;
        }

        public void Stop()
        {
            m_IsActive = false;
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
            m_PosDisplacement = Vector2.zero;
            m_PosVelocity = Vector2.zero;
            m_AngleDisplacement = 0f;
            m_AngleVelocity = 0f;
        }

        public void SetIdle()
        {
            m_IsActive = true;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (m_RectTransform == null) m_RectTransform = GetComponent<RectTransform>();
            if (m_RectTransform == null) return;

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(LeftAttachmentWorldPosition, 6f);
            Gizmos.DrawWireSphere(RightAttachmentWorldPosition, 6f);
        }
#endif
    }
}
