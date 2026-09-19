using System;
using UnityEngine;
using UnityEngine.UI;

namespace MainGame.UI.CinematicEffects
{
    public enum UIShaderPreset
    {
        StandardPanel,
        Signboard,
        Button,
        CharacterCore,
        RouteNode
    }

    /// <summary>
    /// Element-level coordinator component for the Cinematic UI Shader.
    /// Configures CinematicUIEffect with standardized presets and provides high-level
    /// activation sweeps, border pulses, impact flashes, and glitch effects.
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    [DisallowMultipleComponent]
    public class CinematicUIShaderController : MonoBehaviour
    {
        [Header("Preset Configuration")]
        [SerializeField] private UIShaderPreset m_Preset = UIShaderPreset.Signboard;
        [SerializeField] private Color m_EnergyColor = new Color(0.35f, 0.85f, 1.0f, 1.0f);
        [SerializeField] private bool m_EnableAmbientBreathing = true;

        private CinematicUIEffect m_UIEffect;

        public CinematicUIEffect UIEffect
        {
            get
            {
                if (m_UIEffect == null)
                {
                    m_UIEffect = GetComponent<CinematicUIEffect>() ?? gameObject.AddComponent<CinematicUIEffect>();
                }
                return m_UIEffect;
            }
        }

        public UIShaderPreset Preset => m_Preset;
        public Color EnergyColor { get => m_EnergyColor; set => m_EnergyColor = value; }

        private void Awake()
        {
            ApplyPreset(m_Preset);
        }

        private void Start()
        {
            if (m_EnableAmbientBreathing && m_Preset != UIShaderPreset.Button)
            {
                UIEffect.StartIdleBreathing(m_EnergyColor);
            }
        }

        public void ApplyPreset(UIShaderPreset preset)
        {
            m_Preset = preset;
            CinematicUIEffect effect = UIEffect;
            if (effect == null) return;

            switch (preset)
            {
                case UIShaderPreset.StandardPanel:
                    effect.BorderColor = new Color(0.25f, 0.55f, 0.85f, 0.6f);
                    effect.BorderIntensity = 0.8f;
                    break;

                case UIShaderPreset.Signboard:
                    effect.BorderColor = m_EnergyColor;
                    effect.BorderIntensity = 1.2f;
                    break;

                case UIShaderPreset.Button:
                    effect.BorderColor = m_EnergyColor;
                    effect.BorderIntensity = 1.0f;
                    break;

                case UIShaderPreset.CharacterCore:
                    effect.BorderColor = new Color(0.3f, 0.95f, 1.0f, 1.0f);
                    effect.BorderIntensity = 2.0f;
                    break;

                case UIShaderPreset.RouteNode:
                    effect.BorderColor = new Color(1.0f, 0.85f, 0.2f, 1.0f);
                    effect.BorderIntensity = 1.8f;
                    break;
            }
        }

        public void PlayActivationSweep(float duration = 0.25f, float angle = 45f, Action onComplete = null)
        {
            UIEffect.PlayActivationSweep(duration, new Color(1f, 1f, 1f, 0.75f), angle, onComplete);
        }

        public void TriggerBorderPulse(float duration = 0.25f, float peakIntensity = 2.2f, Action onComplete = null)
        {
            UIEffect.TriggerBorderPulse(duration, m_EnergyColor, peakIntensity, onComplete);
        }

        public void TriggerImpactFlash(float duration = 0.14f, float peakIntensity = 2.5f, Action onComplete = null)
        {
            UIEffect.TriggerImpactFlash(duration, Color.white, peakIntensity);
            onComplete?.Invoke();
        }

        public void TriggerGlitch(float duration = 0.16f, float intensity = 0.8f, Action onComplete = null)
        {
            UIEffect.TriggerGlitch(duration, intensity, onComplete);
        }

        public void TriggerRadialPulse(float duration = 0.35f, float maxRadius = 1.2f, Action onComplete = null)
        {
            UIEffect.TriggerRadialPulse(duration, m_EnergyColor, maxRadius, 2.5f, onComplete);
        }

        public void ResetToIdle()
        {
            UIEffect.ResetToIdle();
            if (m_EnableAmbientBreathing && m_Preset != UIShaderPreset.Button)
            {
                UIEffect.StartIdleBreathing(m_EnergyColor);
            }
        }
    }
}
