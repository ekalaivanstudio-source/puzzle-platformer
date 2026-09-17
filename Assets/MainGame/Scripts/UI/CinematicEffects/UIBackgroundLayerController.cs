using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MainGame.UI.CinematicEffects
{
    public enum BackgroundLayerProfile
    {
        BackgroundRed,
        RedYellowTransition,
        Custom
    }

    /// <summary>
    /// Controls a UI background layer running the CinematicPixelBackground shader.
    /// Manages an isolated runtime material instance to safely drive subtle environmental motion,
    /// animated stepped noise, machinery pulses, and localized glitches without altering disk assets.
    /// </summary>
    [RequireComponent(typeof(Image))]
    [DisallowMultipleComponent]
    public class UIBackgroundLayerController : MonoBehaviour
    {
        [Header("Profile Configuration")]
        [SerializeField] private BackgroundLayerProfile m_Profile = BackgroundLayerProfile.BackgroundRed;

        [Header("Pixel Grid Snapping")]
        [Tooltip("Texel dimension for discrete pixel-grid coordinate snapping.")]
        [SerializeField] private float m_PixelGridStep = 512f;

        [Header("Wave Displacement")]
        [Range(0f, 0.05f)]
        [SerializeField] private float m_DistortionStrength = 0.003f;
        [SerializeField] private float m_DistortionSpeed = 0.8f;
        [SerializeField] private float m_DistortionFrequency = 6.0f;

        [Header("Stepped Pixel Noise")]
        [Range(0f, 0.2f)]
        [SerializeField] private float m_NoiseStrength = 0.025f;
        [SerializeField] private float m_NoiseScale = 48.0f;
        [SerializeField] private float m_NoiseSpeed = 3.0f;

        [Header("Luminance Breathing & Color Pulse")]
        [Range(0f, 0.5f)]
        [SerializeField] private float m_BrightnessPulse = 0.08f;
        [SerializeField] private Color m_PulseColor = new Color(0.9f, 0.25f, 0.2f, 1f);
        [SerializeField] private float m_PulseSpeed = 1.2f;
        [Range(0f, 1f)]
        [SerializeField] private float m_PulseAdditiveIntensity = 0.05f;

        [Header("Occasional Glitch Blocks")]
        [Range(0f, 0.05f)]
        [SerializeField] private float m_GlitchStrength = 0.012f;
        [SerializeField] private float m_GlitchBlockSize = 20.0f;
        [SerializeField] private float m_GlitchRate = 8.0f;

        [Header("Subtle Scanline Distortion")]
        [Range(0f, 0.03f)]
        [SerializeField] private float m_ScanStrength = 0.005f;
        [SerializeField] private float m_ScanWidth = 0.08f;
        [SerializeField] private float m_ScanSpeed = 0.4f;

        private Image m_TargetImage;
        private Material m_MaterialInstance;
        private Shader m_PixelShader;
        private Coroutine m_PulseRoutine;
        private Coroutine m_GlitchRoutine;
        private bool m_IsActive = true;

        // Shader property IDs (zero-allocation)
        private static readonly int s_PixelGridStepId = Shader.PropertyToID("_PixelGridStep");
        private static readonly int s_DistortionStrengthId = Shader.PropertyToID("_DistortionStrength");
        private static readonly int s_DistortionSpeedId = Shader.PropertyToID("_DistortionSpeed");
        private static readonly int s_DistortionFreqId = Shader.PropertyToID("_DistortionFrequency");
        private static readonly int s_NoiseStrengthId = Shader.PropertyToID("_NoiseStrength");
        private static readonly int s_NoiseScaleId = Shader.PropertyToID("_NoiseScale");
        private static readonly int s_NoiseSpeedId = Shader.PropertyToID("_NoiseSpeed");
        private static readonly int s_BrightnessPulseId = Shader.PropertyToID("_BrightnessPulse");
        private static readonly int s_PulseColorId = Shader.PropertyToID("_PulseColor");
        private static readonly int s_PulseSpeedId = Shader.PropertyToID("_PulseSpeed");
        private static readonly int s_PulseIntensityId = Shader.PropertyToID("_PulseIntensity");
        private static readonly int s_GlitchStrengthId = Shader.PropertyToID("_GlitchStrength");
        private static readonly int s_GlitchBlockSizeId = Shader.PropertyToID("_GlitchBlockSize");
        private static readonly int s_GlitchRateId = Shader.PropertyToID("_GlitchRate");
        private static readonly int s_ScanStrengthId = Shader.PropertyToID("_ScanStrength");
        private static readonly int s_ScanWidthId = Shader.PropertyToID("_ScanWidth");
        private static readonly int s_ScanSpeedId = Shader.PropertyToID("_ScanSpeed");

        public BackgroundLayerProfile Profile
        {
            get => m_Profile;
            set
            {
                m_Profile = value;
                ApplyDefaultProfileParameters();
                SyncShaderProperties();
            }
        }

        private void Awake()
        {
            m_TargetImage = GetComponent<Image>();
            InitializeMaterialInstance();
            ApplyDefaultProfileParameters();
            SyncShaderProperties();
        }

        private void OnDestroy()
        {
            if (m_MaterialInstance != null)
            {
                Destroy(m_MaterialInstance);
                m_MaterialInstance = null;
            }
        }

        private void InitializeMaterialInstance()
        {
            if (m_MaterialInstance != null) return;

            m_PixelShader = Shader.Find("MainGame/UI/CinematicPixelBackground");
            if (m_PixelShader == null)
            {
                Debug.LogWarning("[UIBackgroundLayerController] CinematicPixelBackground shader not found! Falling back to Image material.");
                return;
            }

            // Create an isolated runtime material instance so disk assets are never modified
            m_MaterialInstance = new Material(m_PixelShader)
            {
                name = $"{gameObject.name}_PixelBG_Instance"
            };

            if (m_TargetImage != null)
            {
                m_TargetImage.material = m_MaterialInstance;
            }
        }

        public void ApplyDefaultProfileParameters()
        {
            switch (m_Profile)
            {
                case BackgroundLayerProfile.BackgroundRed:
                    m_PixelGridStep = 512f;
                    m_DistortionStrength = 0.002f;
                    m_DistortionSpeed = 0.6f;
                    m_DistortionFrequency = 5.0f;
                    m_NoiseStrength = 0.02f;
                    m_NoiseScale = 42f;
                    m_NoiseSpeed = 2.5f;
                    m_BrightnessPulse = 0.07f;
                    m_PulseColor = new Color(0.95f, 0.22f, 0.18f, 1f);
                    m_PulseSpeed = 1.0f;
                    m_PulseAdditiveIntensity = 0.04f;
                    m_GlitchStrength = 0.008f;
                    m_GlitchBlockSize = 24f;
                    m_GlitchRate = 6f;
                    m_ScanStrength = 0.004f;
                    m_ScanWidth = 0.07f;
                    m_ScanSpeed = 0.35f;
                    break;

                case BackgroundLayerProfile.RedYellowTransition:
                    m_PixelGridStep = 512f;
                    m_DistortionStrength = 0.004f;
                    m_DistortionSpeed = 0.9f;
                    m_DistortionFrequency = 8.0f;
                    m_NoiseStrength = 0.035f;
                    m_NoiseScale = 56f;
                    m_NoiseSpeed = 3.5f;
                    m_BrightnessPulse = 0.12f;
                    m_PulseColor = new Color(1.0f, 0.78f, 0.25f, 1f);
                    m_PulseSpeed = 1.4f;
                    m_PulseAdditiveIntensity = 0.06f;
                    m_GlitchStrength = 0.012f;
                    m_GlitchBlockSize = 20f;
                    m_GlitchRate = 8f;
                    m_ScanStrength = 0.006f;
                    m_ScanWidth = 0.06f;
                    m_ScanSpeed = 0.5f;
                    break;

                case BackgroundLayerProfile.Custom:
                    break;
            }
        }

        public void SyncShaderProperties()
        {
            if (m_MaterialInstance == null) return;

            m_MaterialInstance.SetFloat(s_PixelGridStepId, m_PixelGridStep);
            m_MaterialInstance.SetFloat(s_DistortionStrengthId, m_IsActive ? m_DistortionStrength : 0f);
            m_MaterialInstance.SetFloat(s_DistortionSpeedId, m_DistortionSpeed);
            m_MaterialInstance.SetFloat(s_DistortionFreqId, m_DistortionFrequency);

            m_MaterialInstance.SetFloat(s_NoiseStrengthId, m_IsActive ? m_NoiseStrength : 0f);
            m_MaterialInstance.SetFloat(s_NoiseScaleId, m_NoiseScale);
            m_MaterialInstance.SetFloat(s_NoiseSpeedId, m_NoiseSpeed);

            m_MaterialInstance.SetFloat(s_BrightnessPulseId, m_IsActive ? m_BrightnessPulse : 0f);
            m_MaterialInstance.SetColor(s_PulseColorId, m_PulseColor);
            m_MaterialInstance.SetFloat(s_PulseSpeedId, m_PulseSpeed);
            m_MaterialInstance.SetFloat(s_PulseIntensityId, m_IsActive ? m_PulseAdditiveIntensity : 0f);

            m_MaterialInstance.SetFloat(s_GlitchStrengthId, m_IsActive ? m_GlitchStrength : 0f);
            m_MaterialInstance.SetFloat(s_GlitchBlockSizeId, m_GlitchBlockSize);
            m_MaterialInstance.SetFloat(s_GlitchRateId, m_GlitchRate);

            m_MaterialInstance.SetFloat(s_ScanStrengthId, m_IsActive ? m_ScanStrength : 0f);
            m_MaterialInstance.SetFloat(s_ScanWidthId, m_ScanWidth);
            m_MaterialInstance.SetFloat(s_ScanSpeedId, m_ScanSpeed);
        }

        public void TriggerPulse(float extraIntensity, float duration, Color? overrideColor = null)
        {
            if (!m_IsActive || m_MaterialInstance == null) return;

            if (m_PulseRoutine != null)
            {
                StopCoroutine(m_PulseRoutine);
            }
            m_PulseRoutine = StartCoroutine(PulseRoutine(extraIntensity, duration, overrideColor));
        }

        private IEnumerator PulseRoutine(float extraIntensity, float duration, Color? overrideColor)
        {
            float baseIntensity = m_PulseAdditiveIntensity;
            float baseBright = m_BrightnessPulse;
            Color baseColor = m_PulseColor;

            Color targetColor = overrideColor ?? m_PulseColor;
            m_MaterialInstance.SetColor(s_PulseColorId, targetColor);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float decay = 1f - t;
                decay = decay * decay; // Smooth quadratic decay

                float curIntensity = baseIntensity + (extraIntensity * decay);
                float curBright = baseBright + (extraIntensity * 0.35f * decay);

                m_MaterialInstance.SetFloat(s_PulseIntensityId, curIntensity);
                m_MaterialInstance.SetFloat(s_BrightnessPulseId, curBright);
                yield return null;
            }

            m_MaterialInstance.SetFloat(s_PulseIntensityId, baseIntensity);
            m_MaterialInstance.SetFloat(s_BrightnessPulseId, baseBright);
            m_MaterialInstance.SetColor(s_PulseColorId, baseColor);
            m_PulseRoutine = null;
        }

        public void TriggerGlitch(float duration)
        {
            if (!m_IsActive || m_MaterialInstance == null) return;

            if (m_GlitchRoutine != null)
            {
                StopCoroutine(m_GlitchRoutine);
            }
            m_GlitchRoutine = StartCoroutine(GlitchRoutine(duration));
        }

        private IEnumerator GlitchRoutine(float duration)
        {
            float baseGlitch = m_GlitchStrength;
            m_MaterialInstance.SetFloat(s_GlitchStrengthId, baseGlitch * 2.5f);

            yield return new WaitForSecondsRealtime(duration);

            m_MaterialInstance.SetFloat(s_GlitchStrengthId, baseGlitch);
            m_GlitchRoutine = null;
        }

        public void Play()
        {
            m_IsActive = true;
            SyncShaderProperties();
        }

        public void Stop()
        {
            m_IsActive = false;
            if (m_PulseRoutine != null)
            {
                StopCoroutine(m_PulseRoutine);
                m_PulseRoutine = null;
            }
            if (m_GlitchRoutine != null)
            {
                StopCoroutine(m_GlitchRoutine);
                m_GlitchRoutine = null;
            }
            SyncShaderProperties();
        }

        public void Reset()
        {
            Stop();
            ApplyDefaultProfileParameters();
            SyncShaderProperties();
        }

        public void SetIdle()
        {
            m_IsActive = true;
            SyncShaderProperties();
        }
    }
}
