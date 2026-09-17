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
        Villain,
        Hero,
        Title,
        Custom
    }

    /// <summary>
    /// Controls a UI background layer running the CinematicPixelBackground shader.
    /// Manages an isolated runtime material instance to safely drive subtle environmental motion,
    /// animated stepped noise, machinery pulses, character outlines, and inner rim lighting.
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

        [Header("Luminance Breathing and Color Pulse")]
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

        [Header("Outline Configuration")]
        [SerializeField] private bool m_IsOutlineLayer = false;
        [SerializeField] private Color m_OutlineColor = Color.clear;
        [Range(0f, 10f)]
        [SerializeField] private float m_OutlineWidth = 0f;
        [Range(0f, 5f)]
        [SerializeField] private float m_OutlineGlow = 1.0f;
        [SerializeField] private float m_OutlinePulseSpeed = 2.0f;
        [Range(0f, 1f)]
        [SerializeField] private float m_OutlinePulseAmount = 0.25f;
        [Range(0f, 1f)]
        [SerializeField] private float m_OutlineFlicker = 0.12f;

        [Header("Inner Rim Lighting")]
        [SerializeField] private Color m_InnerRimColor = Color.clear;
        [Range(0f, 3f)]
        [SerializeField] private float m_InnerRimIntensity = 0f;
        [Range(0f, 6f)]
        [SerializeField] private float m_InnerRimWidth = 1.5f;
        [Range(1f, 10f)]
        [SerializeField] private float m_EdgeSharpness = 3.0f;

        [Header("Linked Outline Layer (Behind Foreground)")]
        [SerializeField] private UIBackgroundLayerController m_LinkedOutlineLayer;

        private Image m_TargetImage;
        private Material m_MaterialInstance;
        private Shader m_PixelShader;
        private Coroutine m_PulseRoutine;
        private Coroutine m_GlitchRoutine;
        private Coroutine m_OutlinePulseRoutine;
        private bool m_IsActive = true;

        // Baseline tracking (prevents intensity buildup across multiple pulses)
        private float m_BaselineOutlineGlow;
        private float m_BaselineOutlineWidth;
        private float m_BaselinePulseAmount;
        private float m_BaselineInnerRimIntensity;
        private float m_BaselineBrightnessPulse;
        private float m_BaselinePulseAdditiveIntensity;
        private Color m_BaselineOutlineColor;
        private Color m_BaselinePulseColor;
        private bool m_HasCapturedBaselines = false;

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

        private static readonly int s_OutlineColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int s_OutlineWidthId = Shader.PropertyToID("_OutlineWidth");
        private static readonly int s_OutlineGlowId = Shader.PropertyToID("_OutlineGlow");
        private static readonly int s_OutlinePulseSpeedId = Shader.PropertyToID("_OutlinePulseSpeed");
        private static readonly int s_OutlinePulseAmountId = Shader.PropertyToID("_OutlinePulseAmount");
        private static readonly int s_OutlineFlickerId = Shader.PropertyToID("_OutlineFlicker");
        private static readonly int s_OutlineOnlyId = Shader.PropertyToID("_OutlineOnly");

        private static readonly int s_InnerRimColorId = Shader.PropertyToID("_InnerRimColor");
        private static readonly int s_InnerRimIntensityId = Shader.PropertyToID("_InnerRimIntensity");
        private static readonly int s_InnerRimWidthId = Shader.PropertyToID("_InnerRimWidth");
        private static readonly int s_EdgeSharpnessId = Shader.PropertyToID("_EdgeSharpness");

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

        public bool IsOutlineLayer
        {
            get => m_IsOutlineLayer;
            set
            {
                m_IsOutlineLayer = value;
                SyncShaderProperties();
            }
        }

        public Color OutlineColor { get => m_OutlineColor; set { m_OutlineColor = value; SyncShaderProperties(); } }
        public float OutlineWidth { get => m_OutlineWidth; set { m_OutlineWidth = value; SyncShaderProperties(); } }
        public float OutlineGlow { get => m_OutlineGlow; set { m_OutlineGlow = value; SyncShaderProperties(); } }
        public float OutlinePulseSpeed { get => m_OutlinePulseSpeed; set { m_OutlinePulseSpeed = value; SyncShaderProperties(); } }
        public float OutlinePulseAmount { get => m_OutlinePulseAmount; set { m_OutlinePulseAmount = value; SyncShaderProperties(); } }

        public Color InnerRimColor { get => m_InnerRimColor; set { m_InnerRimColor = value; SyncShaderProperties(); } }
        public float InnerRimIntensity { get => m_InnerRimIntensity; set { m_InnerRimIntensity = value; SyncShaderProperties(); } }
        public float InnerRimWidth { get => m_InnerRimWidth; set { m_InnerRimWidth = value; SyncShaderProperties(); } }

        public UIBackgroundLayerController LinkedOutlineLayer
        {
            get => m_LinkedOutlineLayer;
            set => m_LinkedOutlineLayer = value;
        }

        private void Awake()
        {
            m_TargetImage = GetComponent<Image>();
            InitializeMaterialInstance();
            ApplyDefaultProfileParameters();
            CaptureBaselines();
            SyncShaderProperties();
        }

        private void Start()
        {
            CaptureBaselines();
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
                name = string.Format("{0}_PixelBG_Instance", gameObject.name)
            };

            if (m_TargetImage != null)
            {
                m_TargetImage.material = m_MaterialInstance;
            }
        }

        public void CaptureBaselines()
        {
            if (m_HasCapturedBaselines) return;
            m_BaselineOutlineGlow = m_OutlineGlow;
            m_BaselineOutlineWidth = m_OutlineWidth;
            m_BaselinePulseAmount = m_OutlinePulseAmount;
            m_BaselineInnerRimIntensity = m_InnerRimIntensity;
            m_BaselineBrightnessPulse = m_BrightnessPulse;
            m_BaselinePulseAdditiveIntensity = m_PulseAdditiveIntensity;
            m_BaselineOutlineColor = m_OutlineColor;
            m_BaselinePulseColor = m_PulseColor;
            m_HasCapturedBaselines = true;
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
                    m_OutlineWidth = 0f;
                    m_InnerRimIntensity = 0f;
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
                    m_OutlineWidth = 0f;
                    m_InnerRimIntensity = 0f;
                    break;

                case BackgroundLayerProfile.Villain:
                    // Dr. Glitch: Crimson/electric red silhouette outline + warm gold/orange inner rim
                    m_PixelGridStep = 512f;
                    m_DistortionStrength = 0.001f;
                    m_DistortionSpeed = 0.7f;
                    m_DistortionFrequency = 4.0f;
                    m_NoiseStrength = 0.012f;
                    m_NoiseScale = 48f;
                    m_NoiseSpeed = 2.5f;
                    m_BrightnessPulse = 0.06f;
                    m_PulseColor = new Color(0.95f, 0.2f, 0.25f, 1f);
                    m_PulseSpeed = 1.2f;
                    m_PulseAdditiveIntensity = 0.03f;
                    m_GlitchStrength = 0.008f;
                    m_GlitchBlockSize = 24f;
                    m_GlitchRate = 4f;
                    m_ScanStrength = 0.002f;
                    m_ScanWidth = 0.05f;
                    m_ScanSpeed = 0.3f;

                    m_OutlineColor = new Color(1.0f, 0.165f, 0.28f, 1.0f); // #FF2A47
                    m_OutlineWidth = m_IsOutlineLayer ? 2.8f : 0f;
                    m_OutlineGlow = 1.35f;
                    m_OutlinePulseSpeed = 1.8f;
                    m_OutlinePulseAmount = 0.18f;
                    m_OutlineFlicker = 0.12f;

                    m_InnerRimColor = new Color(1.0f, 0.65f, 0.20f, 1.0f); // warm gold
                    m_InnerRimIntensity = m_IsOutlineLayer ? 0f : 0.70f;
                    m_InnerRimWidth = 1.5f;
                    m_EdgeSharpness = 3.5f;
                    break;

                case BackgroundLayerProfile.Hero:
                    // Byte: Electric cyan / plasma blue outline + lighter cyan inner rim
                    m_PixelGridStep = 512f;
                    m_DistortionStrength = 0.001f;
                    m_DistortionSpeed = 0.8f;
                    m_DistortionFrequency = 4.0f;
                    m_NoiseStrength = 0.010f;
                    m_NoiseScale = 52f;
                    m_NoiseSpeed = 2.8f;
                    m_BrightnessPulse = 0.08f;
                    m_PulseColor = new Color(0.25f, 0.85f, 1.0f, 1f);
                    m_PulseSpeed = 1.6f;
                    m_PulseAdditiveIntensity = 0.04f;
                    m_GlitchStrength = 0.006f;
                    m_GlitchBlockSize = 20f;
                    m_GlitchRate = 3f;
                    m_ScanStrength = 0.002f;
                    m_ScanWidth = 0.05f;
                    m_ScanSpeed = 0.35f;

                    m_OutlineColor = new Color(0.15f, 0.88f, 1.0f, 1.0f); // #26E0FF
                    m_OutlineWidth = m_IsOutlineLayer ? 2.6f : 0f;
                    m_OutlineGlow = 1.45f;
                    m_OutlinePulseSpeed = 2.0f;
                    m_OutlinePulseAmount = 0.25f;
                    m_OutlineFlicker = 0.15f;

                    m_InnerRimColor = new Color(0.48f, 0.92f, 1.0f, 1.0f); // lighter cyan/blue
                    m_InnerRimIntensity = m_IsOutlineLayer ? 0f : 0.85f;
                    m_InnerRimWidth = 1.5f;
                    m_EdgeSharpness = 3.0f;
                    break;

                case BackgroundLayerProfile.Title:
                    // RETRY: Cyber cyan outline + golden yellow inner bevel
                    m_PixelGridStep = 512f;
                    m_DistortionStrength = 0.0005f;
                    m_DistortionSpeed = 0.5f;
                    m_DistortionFrequency = 3.0f;
                    m_NoiseStrength = 0.006f;
                    m_NoiseScale = 40f;
                    m_NoiseSpeed = 2.0f;
                    m_BrightnessPulse = 0.09f;
                    m_PulseColor = new Color(0.35f, 0.90f, 1.0f, 1f);
                    m_PulseSpeed = 1.5f;
                    m_PulseAdditiveIntensity = 0.03f;
                    m_GlitchStrength = 0.005f;
                    m_GlitchBlockSize = 16f;
                    m_GlitchRate = 2f;
                    m_ScanStrength = 0.001f;
                    m_ScanWidth = 0.04f;
                    m_ScanSpeed = 0.25f;

                    m_OutlineColor = new Color(0.22f, 0.88f, 1.0f, 1.0f); // #38E2FF cyber cyan
                    m_OutlineWidth = m_IsOutlineLayer ? 3.2f : 0f;
                    m_OutlineGlow = 1.55f;
                    m_OutlinePulseSpeed = 1.5f;
                    m_OutlinePulseAmount = 0.18f;
                    m_OutlineFlicker = 0.10f;

                    m_InnerRimColor = new Color(1.0f, 0.88f, 0.35f, 1.0f); // #FFE059 golden yellow
                    m_InnerRimIntensity = m_IsOutlineLayer ? 0f : 0.90f;
                    m_InnerRimWidth = 1.8f;
                    m_EdgeSharpness = 4.0f;
                    break;

                case BackgroundLayerProfile.Custom:
                    break;
            }
        }

        public void SyncShaderProperties()
        {
            if (m_MaterialInstance == null)
            {
                InitializeMaterialInstance();
                if (m_MaterialInstance == null) return;
            }

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

            // Outline & Inner Rim
            m_MaterialInstance.SetColor(s_OutlineColorId, m_OutlineColor);
            m_MaterialInstance.SetFloat(s_OutlineWidthId, m_IsActive ? m_OutlineWidth : 0f);
            m_MaterialInstance.SetFloat(s_OutlineGlowId, m_OutlineGlow);
            m_MaterialInstance.SetFloat(s_OutlinePulseSpeedId, m_OutlinePulseSpeed);
            m_MaterialInstance.SetFloat(s_OutlinePulseAmountId, m_OutlinePulseAmount);
            m_MaterialInstance.SetFloat(s_OutlineFlickerId, m_OutlineFlicker);
            m_MaterialInstance.SetFloat(s_OutlineOnlyId, m_IsOutlineLayer ? 1.0f : 0.0f);

            m_MaterialInstance.SetColor(s_InnerRimColorId, m_InnerRimColor);
            m_MaterialInstance.SetFloat(s_InnerRimIntensityId, m_IsActive ? m_InnerRimIntensity : 0f);
            m_MaterialInstance.SetFloat(s_InnerRimWidthId, m_InnerRimWidth);
            m_MaterialInstance.SetFloat(s_EdgeSharpnessId, m_EdgeSharpness);
        }

        public void SetOutline(Color color, float width, float glow, float pulseSpeed = 2f, float pulseAmount = 0.25f)
        {
            m_OutlineColor = color;
            m_OutlineWidth = width;
            m_OutlineGlow = glow;
            m_OutlinePulseSpeed = pulseSpeed;
            m_OutlinePulseAmount = pulseAmount;

            SyncShaderProperties();

            if (m_LinkedOutlineLayer != null)
            {
                m_LinkedOutlineLayer.SetOutline(color, width, glow, pulseSpeed, pulseAmount);
            }
        }

        public void SetInnerRim(Color color, float intensity, float width = 1.5f)
        {
            m_InnerRimColor = color;
            m_InnerRimIntensity = intensity;
            m_InnerRimWidth = width;
            SyncShaderProperties();
        }

        /// <summary>
        /// Pulses outline glow and inner rim intensity temporarily, returning smoothly to baseline.
        /// Strictly avoids intensity accumulation.
        /// </summary>
        public void PulseOutline(float extraIntensity, float duration, Color? overrideColor = null)
        {
            if (!m_IsActive || m_MaterialInstance == null) return;

            CaptureBaselines();

            if (m_OutlinePulseRoutine != null)
            {
                StopCoroutine(m_OutlinePulseRoutine);
                ResetOutlineInternal();
            }

            m_OutlinePulseRoutine = StartCoroutine(OutlinePulseRoutine(extraIntensity, duration, overrideColor));

            if (m_LinkedOutlineLayer != null)
            {
                m_LinkedOutlineLayer.PulseOutline(extraIntensity, duration, overrideColor);
            }
        }

        private IEnumerator OutlinePulseRoutine(float extraIntensity, float duration, Color? overrideColor)
        {
            float baseGlow = m_BaselineOutlineGlow;
            float baseRim = m_BaselineInnerRimIntensity;
            Color baseOutlineCol = m_BaselineOutlineColor;

            if (overrideColor.HasValue)
            {
                m_MaterialInstance.SetColor(s_OutlineColorId, overrideColor.Value);
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float decay = 1f - t;
                decay = decay * decay; // Smooth quadratic falloff

                float curGlow = baseGlow + (extraIntensity * decay);
                float curRim = baseRim + (extraIntensity * 0.6f * decay);

                m_MaterialInstance.SetFloat(s_OutlineGlowId, curGlow);
                m_MaterialInstance.SetFloat(s_InnerRimIntensityId, curRim);
                yield return null;
            }

            ResetOutlineInternal();
            m_OutlinePulseRoutine = null;
        }

        public void ResetOutline()
        {
            if (m_OutlinePulseRoutine != null)
            {
                StopCoroutine(m_OutlinePulseRoutine);
                m_OutlinePulseRoutine = null;
            }
            ResetOutlineInternal();

            if (m_LinkedOutlineLayer != null)
            {
                m_LinkedOutlineLayer.ResetOutline();
            }
        }

        private void ResetOutlineInternal()
        {
            if (!m_HasCapturedBaselines || m_MaterialInstance == null) return;
            m_OutlineGlow = m_BaselineOutlineGlow;
            m_InnerRimIntensity = m_BaselineInnerRimIntensity;
            m_OutlineColor = m_BaselineOutlineColor;
            m_MaterialInstance.SetFloat(s_OutlineGlowId, m_BaselineOutlineGlow);
            m_MaterialInstance.SetFloat(s_InnerRimIntensityId, m_BaselineInnerRimIntensity);
            m_MaterialInstance.SetColor(s_OutlineColorId, m_BaselineOutlineColor);
        }

        public void TriggerPulse(float extraIntensity, float duration, Color? overrideColor = null)
        {
            if (!m_IsActive || m_MaterialInstance == null) return;

            CaptureBaselines();

            if (m_PulseRoutine != null)
            {
                StopCoroutine(m_PulseRoutine);
                ResetPulseInternal();
            }
            m_PulseRoutine = StartCoroutine(PulseRoutine(extraIntensity, duration, overrideColor));

            // Also subtly flare outline when a main pulse triggers
            PulseOutline(extraIntensity * 0.5f, duration, overrideColor);
        }

        private IEnumerator PulseRoutine(float extraIntensity, float duration, Color? overrideColor)
        {
            float baseIntensity = m_BaselinePulseAdditiveIntensity;
            float baseBright = m_BaselineBrightnessPulse;
            Color baseColor = m_BaselinePulseColor;

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

            ResetPulseInternal();
            m_PulseRoutine = null;
        }

        private void ResetPulseInternal()
        {
            if (!m_HasCapturedBaselines || m_MaterialInstance == null) return;
            m_MaterialInstance.SetFloat(s_PulseIntensityId, m_BaselinePulseAdditiveIntensity);
            m_MaterialInstance.SetFloat(s_BrightnessPulseId, m_BaselineBrightnessPulse);
            m_MaterialInstance.SetColor(s_PulseColorId, m_BaselinePulseColor);
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
            if (m_LinkedOutlineLayer != null) m_LinkedOutlineLayer.Play();
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
            if (m_OutlinePulseRoutine != null)
            {
                StopCoroutine(m_OutlinePulseRoutine);
                m_OutlinePulseRoutine = null;
            }
            ResetPulseInternal();
            ResetOutlineInternal();
            SyncShaderProperties();
            if (m_LinkedOutlineLayer != null) m_LinkedOutlineLayer.Stop();
        }

        public void Reset()
        {
            Stop();
            ApplyDefaultProfileParameters();
            CaptureBaselines();
            SyncShaderProperties();
            if (m_LinkedOutlineLayer != null) m_LinkedOutlineLayer.Reset();
        }

        public void SetIdle()
        {
            m_IsActive = true;
            SyncShaderProperties();
            if (m_LinkedOutlineLayer != null) m_LinkedOutlineLayer.SetIdle();
        }

        /// <summary>
        /// Automatically creates or binds a dedicated outline rendering layer behind this image.
        /// Preserves the original image, sprite, and RectTransform completely untouched.
        /// </summary>
        public UIBackgroundLayerController EnsureOutlineLayer()
        {
            if (m_LinkedOutlineLayer != null)
            {
                if (m_LinkedOutlineLayer.GetComponent<RectTransform>() != null)
                {
                    return m_LinkedOutlineLayer;
                }
                m_LinkedOutlineLayer = null;
            }

            Transform parent = transform.parent;
            if (parent == null) return null;

            string outlineName = string.Format("{0}_Outline", gameObject.name);
            Transform existingOutline = parent.Find(outlineName);

            if (existingOutline != null && !(existingOutline is RectTransform))
            {
                if (Application.isPlaying)
                {
                    Destroy(existingOutline.gameObject);
                }
                else
                {
                    DestroyImmediate(existingOutline.gameObject);
                }
                existingOutline = null;
            }

            GameObject outlineGo;
            RectTransform outlineRect;
            Image outlineImage;

            if (existingOutline != null)
            {
                outlineGo = existingOutline.gameObject;
                outlineRect = existingOutline as RectTransform;
                outlineImage = outlineGo.GetComponent<Image>() ?? outlineGo.AddComponent<Image>();
            }
            else
            {
                // CRITICAL: UI GameObjects MUST be initialized with typeof(RectTransform)
                // A default new GameObject() assigns a standard 3D Transform which cannot be converted to RectTransform
                outlineGo = new GameObject(outlineName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                outlineRect = outlineGo.GetComponent<RectTransform>();
                outlineImage = outlineGo.GetComponent<Image>();

                outlineGo.transform.SetParent(parent, false);

                // Place immediately before source image so it renders behind it in the Canvas
                int myIndex = transform.GetSiblingIndex();
                outlineGo.transform.SetSiblingIndex(myIndex);
            }

            // Sync RectTransform
            RectTransform myRect = GetComponent<RectTransform>();
            if (myRect != null && outlineRect != null)
            {
                outlineRect.anchorMin = myRect.anchorMin;
                outlineRect.anchorMax = myRect.anchorMax;
                outlineRect.pivot = myRect.pivot;
                outlineRect.anchoredPosition = myRect.anchoredPosition;
                outlineRect.sizeDelta = myRect.sizeDelta;
                outlineRect.localEulerAngles = myRect.localEulerAngles;
                outlineRect.localScale = myRect.localScale;
            }

            // Setup Image
            Image myImage = GetComponent<Image>();
            if (myImage != null && outlineImage != null)
            {
                outlineImage.sprite = myImage.sprite;
                outlineImage.preserveAspect = myImage.preserveAspect;
                outlineImage.type = myImage.type;
                outlineImage.color = Color.white;
                outlineImage.raycastTarget = false;
            }

            // Setup Mesh Expansion (16px outer geometry padding)
            UIOutlineMeshExpansion expansion = outlineGo.GetComponent<UIOutlineMeshExpansion>() ?? outlineGo.AddComponent<UIOutlineMeshExpansion>();
            expansion.Padding = 16f;

            // Setup Follower
            UIOutlineFollower follower = outlineGo.GetComponent<UIOutlineFollower>() ?? outlineGo.AddComponent<UIOutlineFollower>();
            follower.Target = myRect;

            // Setup Controller
            UIBackgroundLayerController outlineCtrl = outlineGo.GetComponent<UIBackgroundLayerController>() ?? outlineGo.AddComponent<UIBackgroundLayerController>();
            outlineCtrl.m_IsOutlineLayer = true;
            outlineCtrl.Profile = m_Profile;

            m_LinkedOutlineLayer = outlineCtrl;

            // Ensure source foreground layer does not draw redundant outer outline
            m_OutlineWidth = 0f;
            SyncShaderProperties();

            return outlineCtrl;
        }
    }
}
