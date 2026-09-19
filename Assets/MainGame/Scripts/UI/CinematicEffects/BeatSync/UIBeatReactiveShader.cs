using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MainGame.UI.CinematicEffects.BeatSync
{
    public enum BeatTriggerMode
    {
        AllBeats,
        StrongBeatsOnly,
        DownbeatsOnly,
        EveryNthBar
    }

    /// <summary>
    /// Connects UI materials and CinematicUIEffect shaders to the master music rhythm.
    /// Drives subtle, rapidly-decaying intensity boosts on border energy, surface brightness,
    /// or radial pulses without distorting or overwhelming the core UI.
    /// </summary>
    [DisallowMultipleComponent]
    public class UIBeatReactiveShader : MonoBehaviour
    {
        [Header("Target Effect")]
        [Tooltip("The CinematicUIEffect component on this or a child GameObject.")]
        [SerializeField] private CinematicUIEffect m_TargetEffect;

        [Header("Rhythm Response Settings")]
        [Tooltip("Which musical beats should trigger a visual reaction.")]
        [SerializeField] private BeatTriggerMode m_TriggerMode = BeatTriggerMode.StrongBeatsOnly;

        [Tooltip("When set to EveryNthBar, fires only on bars whose index is a multiple of this value.")]
        [SerializeField] private int m_BarInterval = 2;

        [Tooltip("Energy border pulse intensity boost on beat (0.0 to 1.0).")]
        [Range(0f, 1f)]
        [SerializeField] private float m_BorderBoost = 0.25f;

        [Tooltip("Core brightness boost on beat (0.0 to 0.5).")]
        [Range(0f, 0.5f)]
        [SerializeField] private float m_BrightnessBoost = 0.12f;

        [Tooltip("Color of the rhythmic energy surge.")]
        [SerializeField] private Color m_EnergyColor = new Color(0.35f, 0.85f, 1.0f, 0.8f);

        [Tooltip("Duration in seconds for the pulse to smoothly decay back to baseline.")]
        [Range(0.05f, 0.40f)]
        [SerializeField] private float m_DecayDuration = 0.18f;

        [Header("Subtle Idle Mode")]
        [Tooltip("If true, reactions only execute when the menu is in an idle state.")]
        [SerializeField] private bool m_OnlyWhenIdle = true;

        private Coroutine m_DecayRoutine;
        private bool m_IsIdle = true;

        public bool IsIdle
        {
            get => m_IsIdle;
            set => m_IsIdle = value;
        }

        public CinematicUIEffect TargetEffect
        {
            get
            {
                if (m_TargetEffect == null)
                {
                    m_TargetEffect = GetComponent<CinematicUIEffect>() ?? GetComponentInChildren<CinematicUIEffect>();
                }
                return m_TargetEffect;
            }
            set => m_TargetEffect = value;
        }

        private void Awake()
        {
            if (m_TargetEffect == null)
            {
                m_TargetEffect = GetComponent<CinematicUIEffect>() ?? GetComponentInChildren<CinematicUIEffect>();
            }
        }

        private void OnEnable()
        {
            MusicBeatManager.OnBeat += HandleBeat;
        }

        private void OnDisable()
        {
            MusicBeatManager.OnBeat -= HandleBeat;
            StopDecay();
        }

        private void HandleBeat(BeatEvent evt)
        {
            if (m_OnlyWhenIdle && !m_IsIdle) return;
            if (TargetEffect == null) return;

            bool shouldFire = false;
            switch (m_TriggerMode)
            {
                case BeatTriggerMode.AllBeats:
                    shouldFire = true;
                    break;
                case BeatTriggerMode.StrongBeatsOnly:
                    shouldFire = evt.IsStrongBeat;
                    break;
                case BeatTriggerMode.DownbeatsOnly:
                    shouldFire = evt.IsDownbeat;
                    break;
                case BeatTriggerMode.EveryNthBar:
                    shouldFire = evt.IsDownbeat && (evt.BarIndex % Mathf.Max(1, m_BarInterval) == 0);
                    break;
            }

            if (shouldFire)
            {
                TriggerPulse(evt.Strength);
            }
        }

        public void TriggerPulse(float strengthMultiplier = 1.0f)
        {
            if (TargetEffect == null) return;

            float borderVal = m_BorderBoost * strengthMultiplier;
            float brightVal = m_BrightnessBoost * strengthMultiplier;

            StopDecay();
            m_DecayRoutine = StartCoroutine(DecayPulseRoutine(borderVal, brightVal));
        }

        private IEnumerator DecayPulseRoutine(float maxBorder, float maxBright)
        {
            CinematicUIEffect fx = TargetEffect;
            if (fx == null) yield break;

            fx.BorderColor = m_EnergyColor;

            float elapsed = 0f;
            float dur = Mathf.Max(0.04f, m_DecayDuration);

            while (elapsed < dur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / dur);
                // Fast decay curve (ease out quadratic / exponential)
                float decay = (1f - t) * (1f - t);

                fx.BorderIntensity = decay * maxBorder;

                yield return null;
            }

            fx.BorderIntensity = 0f;
            m_DecayRoutine = null;
        }

        private void StopDecay()
        {
            if (m_DecayRoutine != null)
            {
                StopCoroutine(m_DecayRoutine);
                m_DecayRoutine = null;
            }
        }
    }
}
