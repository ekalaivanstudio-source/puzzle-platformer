using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MainGame.UI.CinematicEffects
{
    public enum BackgroundLayerProfile
    {
        Custom = 0,
        BackgroundRed = 1,
        RedYellowTransition = 2,
        Villain = 3,
        Hero = 4,
        Title = 5,
        Spark = 6
    }

    public enum OutlinePulseState
    {
        Idle,
        Delay,
        Attack,
        Hold,
        Release,
        Charging
    }

    /// <summary>
    /// Controls runtime parameters of the CinematicPixelBackground shader on a specific UI Image.
    /// Provides low-latency event-driven outline attack-hold-release envelope, full theme integration,
    /// and baseline tracking without permanent disk material mutation.
    /// </summary>
    [RequireComponent(typeof(Image))]
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public class UIBackgroundLayerController : MonoBehaviour
    {
        [Header("Profile Configuration")]
        [SerializeField] private BackgroundLayerProfile m_Profile = BackgroundLayerProfile.Custom;

        [Header("Cinematic Color Theme")]
        [Tooltip("Reusable ScriptableObject color palette. Overrides individual colors unless custom overrides are enabled.")]
        [SerializeField] private CinematicUIColorTheme m_Theme;
        [Tooltip("Fallback preset if no Theme asset is assigned.")]
        [SerializeField] private CinematicThemePreset m_ThemePreset = CinematicThemePreset.CyberCyan;
        [SerializeField] private bool m_UseTheme = true;

        [Header("Color Overrides (Per-Object)")]
        [SerializeField] private bool m_OverrideOutlineColor = false;
        [SerializeField] private Color m_CustomOutlineColor = Color.white;
        [SerializeField] private bool m_OverrideInnerRimColor = false;
        [SerializeField] private Color m_CustomInnerRimColor = Color.white;
        [SerializeField] private bool m_OverrideGlowColor = false;
        [SerializeField] private Color m_CustomGlowColor = Color.white;
        [SerializeField] private bool m_OverrideEnergyColor = false;
        [SerializeField] private Color m_CustomEnergyColor = Color.white;
        [SerializeField] private bool m_OverridePulseColor = false;
        [SerializeField] private Color m_CustomPulseColor = Color.white;

        [Header("Color Intensity Multipliers")]
        [Range(0f, 3f)] [SerializeField] private float m_OutlineIntensity = 1.0f;
        [Range(0f, 3f)] [SerializeField] private float m_InnerRimMultiplier = 1.0f;
        [Range(0f, 3f)] [SerializeField] private float m_GlowMultiplier = 1.0f;
        [Range(0f, 3f)] [SerializeField] private float m_EnergyMultiplier = 1.0f;
        [Range(0f, 3f)] [SerializeField] private float m_PulseMultiplier = 1.0f;

        [Header("Outline Response & Envelope (AHR)")]
        [Tooltip("Response delay in seconds before outline attack begins (0.00 for instant response).")]
        [SerializeField] private float m_OutlineResponseDelay = 0.00f;
        [Tooltip("Attack duration in seconds to reach peak intensity.")]
        [SerializeField] private float m_OutlineAttackTime = 0.04f;
        [Tooltip("Hold duration in seconds at peak intensity.")]
        [SerializeField] private float m_OutlineHoldTime = 0.05f;
        [Tooltip("Release duration in seconds returning to baseline.")]
        [SerializeField] private float m_OutlineReleaseTime = 0.14f;
        [Tooltip("Baseline glow multiplier at rest.")]
        [SerializeField] private float m_OutlineBaseGlow = 1.0f;
        [Tooltip("Peak glow multiplier during pulse impact.")]
        [SerializeField] private float m_OutlinePeakGlow = 2.0f;

        [Header("Pixel Grid Snapping")]
        [SerializeField] private float m_PixelGridStep = 512.0f;

        [Header("Subtle Wave Displacement")]
        [Range(0f, 0.02f)]
        [SerializeField] private float m_DistortionStrength = 0.003f;
        [SerializeField] private float m_DistortionSpeed = 0.8f;
        [SerializeField] private float m_DistortionFrequency = 6.0f;

        [Header("Animated Pixel Noise")]
        [Range(0f, 0.1f)]
        [SerializeField] private float m_NoiseStrength = 0.025f;
        [SerializeField] private float m_NoiseScale = 48.0f;
        [SerializeField] private float m_NoiseSpeed = 3.0f;

        [Header("Brightness & Color Breathing")]
        [Range(0f, 0.5f)]
        [SerializeField] private float m_BrightnessPulse = 0.08f;
        [SerializeField] private Color m_PulseColor = new Color(0.9f, 0.2f, 0.2f, 1f);
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
        [SerializeField] private float m_OutlineFlicker = 0f;

        [Header("Inner Rim Lighting")]
        [SerializeField] private Color m_InnerRimColor = Color.clear;
        [Range(0f, 3f)]
        [SerializeField] private float m_InnerRimIntensity = 0f;
        [Range(0f, 6f)]
        [SerializeField] private float m_InnerRimWidth = 1.5f;
        [Range(1f, 10f)]
        [SerializeField] private float m_EdgeSharpness = 3.0f;

        [Header("Glitch Permissions")]
        [Tooltip("Controls whether this layer is allowed to undergo digital glitch effects. Only Red BG and Spark layers are permitted.")]
        [SerializeField] private bool m_AllowGlitch = false;

        [Header("Linked Outline Layer (Behind Foreground)")]
        [SerializeField] private UIBackgroundLayerController m_LinkedOutlineLayer;

        [Header("Debug & Status (Editor Only)")]
        [SerializeField] private bool m_DebugMode = false;
        [SerializeField] private OutlinePulseState m_CurrentPulseState = OutlinePulseState.Idle;
        [SerializeField] private string m_LastEvent = "None";
        [SerializeField] private float m_LastEventTime = 0f;
        [SerializeField] private float m_DebugEffectiveGlow = 0f;
        [SerializeField] private float m_DebugEffectiveRim = 0f;

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
        [SerializeField] private bool m_HasInitializedDefaults = false;

        // Shader property IDs (zero-allocation)
        private static readonly int s_PixelGridStepId = Shader.PropertyToID("_PixelGridStep");
        private static readonly int s_DistortionStrengthId = Shader.PropertyToID("_DistortionStrength");
        private static readonly int s_EnergyStrengthId = Shader.PropertyToID("_EnergyStrength");
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
                ApplyThemeColors();
                SyncShaderProperties();
            }
        }

        public Material MaterialInstance => m_MaterialInstance;

        public CinematicUIColorTheme Theme
        {
            get => m_Theme;
            set
            {
                m_Theme = value;
                ApplyThemeColors();
            }
        }

        public CinematicThemePreset ThemePreset
        {
            get => m_ThemePreset;
            set
            {
                ApplyThemePreset(value);
            }
        }

        public bool UseTheme
        {
            get => m_UseTheme;
            set
            {
                m_UseTheme = value;
                ApplyThemeColors();
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

        public float ResponseDelay { get => m_OutlineResponseDelay; set => m_OutlineResponseDelay = value; }
        public float AttackTime { get => m_OutlineAttackTime; set => m_OutlineAttackTime = value; }
        public float HoldTime { get => m_OutlineHoldTime; set => m_OutlineHoldTime = value; }
        public float ReleaseTime { get => m_OutlineReleaseTime; set => m_OutlineReleaseTime = value; }
        public float BaseGlow { get => m_OutlineBaseGlow; set => m_OutlineBaseGlow = value; }
        public float PeakGlow { get => m_OutlinePeakGlow; set => m_OutlinePeakGlow = value; }

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

        public bool AllowGlitch
        {
            get => m_AllowGlitch;
            set => m_AllowGlitch = value;
        }

        public bool IsGlitchPermitted()
        {
            if (m_Profile == BackgroundLayerProfile.BackgroundRed || m_Profile == BackgroundLayerProfile.Spark)
            {
                return m_AllowGlitch;
            }
            return false;
        }

        public void ApplyThemePreset(CinematicThemePreset preset)
        {
            m_ThemePreset = preset;
            m_UseTheme = true;
            m_Theme = null;
            ApplyThemeColors();
            SyncShaderProperties();

            if (m_LinkedOutlineLayer != null)
            {
                m_LinkedOutlineLayer.m_ThemePreset = preset;
                m_LinkedOutlineLayer.m_UseTheme = true;
                m_LinkedOutlineLayer.m_Theme = null;
                m_LinkedOutlineLayer.ApplyThemeColors();
                m_LinkedOutlineLayer.SyncShaderProperties();
            }
        }

        private Coroutine m_ThemeTransitionRoutine;

        /// <summary>
        /// Smoothly transitions outline, rim, and pulse colors to a target preset over duration seconds.
        /// </summary>
        public void TransitionToTheme(CinematicThemePreset targetPreset, float duration = 1.0f)
        {
            if (!m_IsActive || !gameObject.activeInHierarchy)
            {
                ApplyThemePreset(targetPreset);
                return;
            }

            if (m_ThemeTransitionRoutine != null)
            {
                StopCoroutine(m_ThemeTransitionRoutine);
            }
            m_ThemeTransitionRoutine = StartCoroutine(ThemeTransitionRoutine(targetPreset, duration));

            if (m_LinkedOutlineLayer != null)
            {
                m_LinkedOutlineLayer.TransitionToTheme(targetPreset, duration);
            }
        }

        private IEnumerator ThemeTransitionRoutine(CinematicThemePreset targetPreset, float duration)
        {
            CinematicUIColorTheme targetTheme = CinematicUIColorTheme.CreateRuntimePreset(targetPreset);
            if (targetTheme == null) yield break;

            Color startOutline = m_OutlineColor;
            Color startRim = m_InnerRimColor;
            Color startPulse = m_PulseColor;

            Color targetOutline = targetTheme.OutlineColor;
            Color targetRim = targetTheme.InnerRimColor;
            Color targetPulse = targetTheme.PulseColor;

            m_ThemePreset = targetPreset;
            m_UseTheme = true;
            m_Theme = null;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float smoothT = Mathf.SmoothStep(0f, 1f, t);

                m_OutlineColor = Color.Lerp(startOutline, targetOutline, smoothT);
                m_InnerRimColor = Color.Lerp(startRim, targetRim, smoothT);
                m_PulseColor = Color.Lerp(startPulse, targetPulse, smoothT);

                if (m_MaterialInstance != null)
                {
                    m_MaterialInstance.SetColor(s_OutlineColorId, m_OutlineColor);
                    m_MaterialInstance.SetColor(s_InnerRimColorId, m_InnerRimColor);
                    m_MaterialInstance.SetColor(s_PulseColorId, m_PulseColor);
                }

                yield return null;
            }

            m_OutlineColor = targetOutline;
            m_InnerRimColor = targetRim;
            m_PulseColor = targetPulse;
            SyncShaderProperties();
            m_ThemeTransitionRoutine = null;
        }

        /// <summary>
        /// Picks a random theme preset from the 12 available presets.
        /// </summary>
        public void ApplyRandomTheme(bool smooth = true, float duration = 1.0f)
        {
            var values = (CinematicThemePreset[])Enum.GetValues(typeof(CinematicThemePreset));
            int index = UnityEngine.Random.Range(0, 12);
            CinematicThemePreset chosen = values[index];

            if (smooth && Application.isPlaying)
            {
                TransitionToTheme(chosen, duration);
            }
            else
            {
                ApplyThemePreset(chosen);
            }
        }

        private void Awake()
        {
            m_TargetImage = GetComponent<Image>();
            InitializeMaterialInstance();
            if (!m_HasInitializedDefaults)
            {
                ApplyDefaultProfileParameters();
                m_HasInitializedDefaults = true;
            }
            ApplyThemeColors();
            CaptureBaselines();
            SyncShaderProperties();
        }

        private void Start()
        {
            CaptureBaselines();
            ApplyThemeColors();
            SyncShaderProperties();
        }

        private void OnEnable()
        {
            if (m_TargetImage == null) m_TargetImage = GetComponent<Image>();
            if (m_MaterialInstance == null) InitializeMaterialInstance();
            ApplyThemeColors();
            SyncShaderProperties();
        }

        private void OnDisable()
        {
            if (!Application.isPlaying && m_MaterialInstance != null)
            {
                DestroyImmediate(m_MaterialInstance);
                m_MaterialInstance = null;
            }
        }

        private void OnDestroy()
        {
            if (m_MaterialInstance != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(m_MaterialInstance);
                }
                else
                {
                    DestroyImmediate(m_MaterialInstance);
                }
                m_MaterialInstance = null;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (m_TargetImage == null) m_TargetImage = GetComponent<Image>();
            if (m_MaterialInstance == null) InitializeMaterialInstance();
            ApplyThemeColors();
            SyncShaderProperties();
        }
#endif

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
                name = string.Format("{0}_PixelBG_Instance", gameObject.name),
                hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild
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

        /// <summary>
        /// Applies theme palette colors, respecting individual color overrides and intensity multipliers.
        /// </summary>
        public void ApplyThemeColors()
        {
            CinematicUIColorTheme theme = m_Theme;
            if (theme == null && m_UseTheme)
            {
                theme = CinematicUIColorTheme.CreateRuntimePreset(m_ThemePreset);
            }

            if (theme != null && m_UseTheme)
            {
                m_OutlineColor = m_OverrideOutlineColor ? m_CustomOutlineColor : theme.OutlineColor;
                m_InnerRimColor = m_OverrideInnerRimColor ? m_CustomInnerRimColor : theme.InnerRimColor;
                m_PulseColor = m_OverridePulseColor ? m_CustomPulseColor : theme.PulseColor;
            }

            if (m_MaterialInstance != null)
            {
                m_MaterialInstance.SetColor(s_OutlineColorId, m_OutlineColor);
                m_MaterialInstance.SetFloat(s_OutlineGlowId, m_OutlineGlow * m_GlowMultiplier);
                m_MaterialInstance.SetColor(s_InnerRimColorId, m_InnerRimColor);
                m_MaterialInstance.SetFloat(s_InnerRimIntensityId, m_InnerRimIntensity * m_InnerRimMultiplier);
                m_MaterialInstance.SetColor(s_PulseColorId, m_PulseColor);
                m_MaterialInstance.SetFloat(s_PulseIntensityId, m_PulseAdditiveIntensity * m_PulseMultiplier);
            }

            if (m_LinkedOutlineLayer != null)
            {
                m_LinkedOutlineLayer.m_Theme = m_Theme;
                m_LinkedOutlineLayer.m_ThemePreset = m_ThemePreset;
                m_LinkedOutlineLayer.m_UseTheme = m_UseTheme;
                m_LinkedOutlineLayer.ApplyThemeColors();
                m_LinkedOutlineLayer.SyncShaderProperties();
            }
        }

        public void ApplyDefaultProfileParameters()
        {
            switch (m_Profile)
            {
                case BackgroundLayerProfile.BackgroundRed:
                    m_ThemePreset = CinematicThemePreset.Industrial;
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
                    m_GlitchStrength = 0f;
                    m_GlitchBlockSize = 24f;
                    m_GlitchRate = 6f;
                    m_ScanStrength = 0f;
                    m_ScanWidth = 0.07f;
                    m_ScanSpeed = 0.35f;
                    m_OutlineWidth = 0f;
                    m_InnerRimIntensity = 0f;
                    m_AllowGlitch = true;
                    break;

                case BackgroundLayerProfile.RedYellowTransition:
                    m_ThemePreset = CinematicThemePreset.GoldenPower;
                    m_PixelGridStep = 0f;
                    m_DistortionStrength = 0f;
                    m_DistortionSpeed = 0f;
                    m_DistortionFrequency = 0f;
                    m_NoiseStrength = 0f;
                    m_NoiseScale = 0f;
                    m_NoiseSpeed = 0f;
                    m_BrightnessPulse = 0f;
                    m_PulseColor = new Color(1.0f, 0.78f, 0.25f, 1f);
                    m_PulseSpeed = 1.4f;
                    m_PulseAdditiveIntensity = 0f;
                    m_GlitchStrength = 0f;
                    m_GlitchBlockSize = 20f;
                    m_GlitchRate = 8f;
                    m_ScanStrength = 0f;
                    m_ScanWidth = 0.06f;
                    m_ScanSpeed = 0.5f;
                    m_OutlineWidth = 0f;
                    m_InnerRimIntensity = 0f;
                    m_AllowGlitch = false;
                    break;

                case BackgroundLayerProfile.Villain:
                    m_ThemePreset = CinematicThemePreset.VillainCrimson;
                    m_PixelGridStep = 0f;
                    m_DistortionStrength = 0f;
                    m_DistortionSpeed = 0f;
                    m_DistortionFrequency = 0f;
                    m_NoiseStrength = 0f;
                    m_NoiseScale = 0f;
                    m_NoiseSpeed = 0f;
                    m_BrightnessPulse = 0f;
                    m_PulseColor = new Color(0.95f, 0.2f, 0.25f, 1f);
                    m_PulseSpeed = 1.2f;
                    m_PulseAdditiveIntensity = 0f;
                    m_GlitchStrength = 0f;
                    m_GlitchBlockSize = 24f;
                    m_GlitchRate = 4f;
                    m_ScanStrength = 0f;
                    m_ScanWidth = 0.05f;
                    m_ScanSpeed = 0.3f;

                    m_OutlineColor = new Color(1.0f, 0.165f, 0.28f, 1.0f); // #FF2A47
                    m_OutlineWidth = m_IsOutlineLayer ? 2.8f : 0f;
                    m_OutlineGlow = 1.35f;
                    m_OutlinePulseSpeed = 1.8f;
                    m_OutlinePulseAmount = 0.18f;
                    m_OutlineFlicker = 0f;

                    m_InnerRimColor = new Color(1.0f, 0.65f, 0.20f, 1.0f); // warm gold
                    m_InnerRimIntensity = m_IsOutlineLayer ? 0f : 0.70f;
                    m_InnerRimWidth = 1.5f;
                    m_EdgeSharpness = 3.5f;

                    m_OutlineAttackTime = 0.04f;
                    m_OutlineHoldTime = 0.06f;
                    m_OutlineReleaseTime = 0.16f;
                    m_AllowGlitch = false;
                    break;

                case BackgroundLayerProfile.Hero:
                    m_ThemePreset = CinematicThemePreset.CyberCyan;
                    m_PixelGridStep = 0f;
                    m_DistortionStrength = 0f;
                    m_DistortionSpeed = 0f;
                    m_DistortionFrequency = 0f;
                    m_NoiseStrength = 0f;
                    m_NoiseScale = 0f;
                    m_NoiseSpeed = 0f;
                    m_BrightnessPulse = 0f;
                    m_PulseColor = new Color(0.25f, 0.85f, 1.0f, 1f);
                    m_PulseSpeed = 1.6f;
                    m_PulseAdditiveIntensity = 0f;
                    m_GlitchStrength = 0f;
                    m_GlitchBlockSize = 20f;
                    m_GlitchRate = 3f;
                    m_ScanStrength = 0f;
                    m_ScanWidth = 0.05f;
                    m_ScanSpeed = 0.35f;

                    m_OutlineColor = new Color(0.15f, 0.88f, 1.0f, 1.0f); // #26E0FF
                    m_OutlineWidth = m_IsOutlineLayer ? 2.6f : 0f;
                    m_OutlineGlow = 1.45f;
                    m_OutlinePulseSpeed = 2.0f;
                    m_OutlinePulseAmount = 0.25f;
                    m_OutlineFlicker = 0f;

                    m_InnerRimColor = new Color(0.48f, 0.92f, 1.0f, 1.0f); // plasma cyan
                    m_InnerRimIntensity = m_IsOutlineLayer ? 0f : 0.85f;
                    m_InnerRimWidth = 1.5f;
                    m_EdgeSharpness = 3.0f;

                    m_OutlineAttackTime = 0.03f;
                    m_OutlineHoldTime = 0.05f;
                    m_OutlineReleaseTime = 0.14f;
                    m_AllowGlitch = false;
                    break;

                case BackgroundLayerProfile.Title:
                    m_ThemePreset = CinematicThemePreset.RetroArcade;
                    m_PixelGridStep = 0f;
                    m_DistortionStrength = 0f;
                    m_DistortionSpeed = 0f;
                    m_DistortionFrequency = 0f;
                    m_NoiseStrength = 0f;
                    m_NoiseScale = 0f;
                    m_NoiseSpeed = 0f;
                    m_BrightnessPulse = 0f;
                    m_PulseColor = new Color(0.35f, 0.90f, 1.0f, 1f);
                    m_PulseSpeed = 1.5f;
                    m_PulseAdditiveIntensity = 0f;
                    m_GlitchStrength = 0f;
                    m_GlitchBlockSize = 16f;
                    m_GlitchRate = 2f;
                    m_ScanStrength = 0f;
                    m_ScanWidth = 0.04f;
                    m_ScanSpeed = 0.25f;

                    m_OutlineColor = new Color(0.22f, 0.88f, 1.0f, 1.0f); // #38E2FF cyber cyan
                    m_OutlineWidth = m_IsOutlineLayer ? 3.2f : 0f;
                    m_OutlineGlow = 1.55f;
                    m_OutlinePulseSpeed = 1.5f;
                    m_OutlinePulseAmount = 0.18f;
                    m_OutlineFlicker = 0f;

                    m_InnerRimColor = new Color(1.0f, 0.88f, 0.35f, 1.0f); // gold bevel
                    m_InnerRimIntensity = m_IsOutlineLayer ? 0f : 0.90f;
                    m_InnerRimWidth = 1.8f;
                    m_EdgeSharpness = 3.2f;

                    m_OutlineAttackTime = 0.03f;
                    m_OutlineHoldTime = 0.05f;
                    m_OutlineReleaseTime = 0.15f;
                    m_AllowGlitch = false;
                    break;

                case BackgroundLayerProfile.Spark:
                    m_ThemePreset = CinematicThemePreset.PlasmaBlue;
                    m_PixelGridStep = 384f;
                    m_DistortionStrength = 0.003f;
                    m_DistortionSpeed = 1.2f;
                    m_DistortionFrequency = 10.0f;
                    m_NoiseStrength = 0.035f;
                    m_NoiseScale = 64f;
                    m_NoiseSpeed = 4.0f;
                    m_BrightnessPulse = 0.15f;
                    m_PulseColor = new Color(0.35f, 0.85f, 1f, 1f);
                    m_PulseSpeed = 2.0f;
                    m_PulseAdditiveIntensity = 0.08f;
                    m_GlitchStrength = 0f;
                    m_GlitchBlockSize = 16f;
                    m_GlitchRate = 12f;
                    m_ScanStrength = 0f;
                    m_ScanWidth = 0.05f;
                    m_ScanSpeed = 0.6f;
                    m_OutlineWidth = 0f;
                    m_InnerRimIntensity = 0f;
                    m_AllowGlitch = true;
                    break;

                default:
                    break;
            }
        }

        public void SyncShaderProperties()
        {
            if (m_MaterialInstance == null)
            {
                InitializeMaterialInstance();
            }
            if (m_MaterialInstance == null) return;

            bool allowGlitchScan = IsGlitchPermitted();
            float effectiveGlitch = allowGlitchScan ? m_GlitchStrength : 0f;
            float effectiveScan = allowGlitchScan ? m_ScanStrength : 0f;
            float effectiveDistortion = allowGlitchScan ? m_DistortionStrength : 0f;
            float effectiveNoise = allowGlitchScan ? m_NoiseStrength : 0f;
            float effectiveBrightnessPulse = allowGlitchScan ? m_BrightnessPulse : 0f;
            float effectivePulseIntensity = allowGlitchScan ? (m_PulseAdditiveIntensity * m_PulseMultiplier) : 0f;
            float effectiveOutlineFlicker = allowGlitchScan ? m_OutlineFlicker : 0f;

            m_MaterialInstance.SetFloat(s_PixelGridStepId, allowGlitchScan ? m_PixelGridStep : 0f);
            m_MaterialInstance.SetFloat(s_DistortionStrengthId, effectiveDistortion);
            m_MaterialInstance.SetFloat(s_DistortionSpeedId, allowGlitchScan ? m_DistortionSpeed : 0f);
            m_MaterialInstance.SetFloat(s_DistortionFreqId, allowGlitchScan ? m_DistortionFrequency : 0f);
            m_MaterialInstance.SetFloat(s_NoiseStrengthId, effectiveNoise);
            m_MaterialInstance.SetFloat(s_NoiseScaleId, allowGlitchScan ? m_NoiseScale : 0f);
            m_MaterialInstance.SetFloat(s_NoiseSpeedId, allowGlitchScan ? m_NoiseSpeed : 0f);
            m_MaterialInstance.SetFloat(s_BrightnessPulseId, effectiveBrightnessPulse);
            m_MaterialInstance.SetColor(s_PulseColorId, m_PulseColor);
            m_MaterialInstance.SetFloat(s_PulseSpeedId, m_PulseSpeed);
            m_MaterialInstance.SetFloat(s_PulseIntensityId, effectivePulseIntensity);
            m_MaterialInstance.SetFloat(s_GlitchStrengthId, effectiveGlitch);
            m_MaterialInstance.SetFloat(s_GlitchBlockSizeId, m_GlitchBlockSize);
            m_MaterialInstance.SetFloat(s_GlitchRateId, m_GlitchRate);
            m_MaterialInstance.SetFloat(s_ScanStrengthId, effectiveScan);
            m_MaterialInstance.SetFloat(s_ScanWidthId, m_ScanWidth);
            m_MaterialInstance.SetFloat(s_ScanSpeedId, m_ScanSpeed);
            m_MaterialInstance.SetFloat(s_EnergyStrengthId, 0f);

            m_MaterialInstance.SetColor(s_OutlineColorId, m_OutlineColor);
            m_MaterialInstance.SetFloat(s_OutlineWidthId, m_OutlineWidth * m_OutlineIntensity);
            m_MaterialInstance.SetFloat(s_OutlineGlowId, m_OutlineGlow * m_GlowMultiplier);
            m_MaterialInstance.SetFloat(s_OutlinePulseSpeedId, m_OutlinePulseSpeed);
            m_MaterialInstance.SetFloat(s_OutlinePulseAmountId, m_OutlinePulseAmount);
            m_MaterialInstance.SetFloat(s_OutlineFlickerId, effectiveOutlineFlicker);
            m_MaterialInstance.SetFloat(s_OutlineOnlyId, m_IsOutlineLayer ? 1.0f : 0.0f);

            m_MaterialInstance.SetColor(s_InnerRimColorId, m_InnerRimColor);
            m_MaterialInstance.SetFloat(s_InnerRimIntensityId, m_InnerRimIntensity * m_InnerRimMultiplier);
            m_MaterialInstance.SetFloat(s_InnerRimWidthId, m_InnerRimWidth);
            m_MaterialInstance.SetFloat(s_EdgeSharpnessId, m_EdgeSharpness);
        }

        /// <summary>
        /// Immediately triggers a responsive, event-driven outline attack-hold-release pulse in 0-1 frames.
        /// Cancels any previous pulse immediately (no queuing, no competing coroutines).
        /// </summary>
        public void PulseOutlineImmediate(float peakMultiplier = 1.0f, float attack = -1f, float hold = -1f, float release = -1f, Color? overrideColor = null, string eventName = "Event")
        {
            if (!m_IsActive || m_MaterialInstance == null) return;

            CaptureBaselines();

            if (m_OutlinePulseRoutine != null)
            {
                StopCoroutine(m_OutlinePulseRoutine);
                m_OutlinePulseRoutine = null;
            }

            m_LastEvent = eventName;
            m_LastEventTime = Time.unscaledTime;

            float attackDur = attack >= 0f ? attack : m_OutlineAttackTime;
            float holdDur = hold >= 0f ? hold : m_OutlineHoldTime;
            float releaseDur = release >= 0f ? release : m_OutlineReleaseTime;
            float delayDur = m_OutlineResponseDelay;

            m_OutlinePulseRoutine = StartCoroutine(OutlineEnvelopeRoutine(peakMultiplier, delayDur, attackDur, holdDur, releaseDur, overrideColor));

            if (m_LinkedOutlineLayer != null)
            {
                m_LinkedOutlineLayer.PulseOutlineImmediate(peakMultiplier, attack, hold, release, overrideColor, eventName);
            }
        }

        private IEnumerator OutlineEnvelopeRoutine(float peakMultiplier, float delay, float attack, float hold, float release, Color? overrideColor)
        {
            float baseGlow = m_BaselineOutlineGlow * m_OutlineBaseGlow;
            float targetPeakGlow = m_BaselineOutlineGlow * m_OutlinePeakGlow * peakMultiplier;
            float baseRim = m_BaselineInnerRimIntensity;
            float targetPeakRim = m_BaselineInnerRimIntensity * (1f + 0.8f * peakMultiplier);

            Color targetColor = overrideColor ?? (m_OverrideOutlineColor ? m_CustomOutlineColor : m_OutlineColor);
            m_MaterialInstance.SetColor(s_OutlineColorId, targetColor);

            if (delay > 0.001f)
            {
                m_CurrentPulseState = OutlinePulseState.Delay;
                yield return new WaitForSecondsRealtime(delay);
            }

            // 1. ATTACK (0 to peak in attack time)
            m_CurrentPulseState = OutlinePulseState.Attack;
            float startGlow = m_MaterialInstance.GetFloat(s_OutlineGlowId);
            float startRim = m_MaterialInstance.GetFloat(s_InnerRimIntensityId);

            if (attack > 0.002f)
            {
                float elapsedAttack = 0f;
                while (elapsedAttack < attack)
                {
                    elapsedAttack += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsedAttack / attack);
                    float curvedT = 1f - (1f - t) * (1f - t); // Punchy ease-out attack

                    float g = Mathf.Lerp(startGlow, targetPeakGlow, curvedT);
                    float r = Mathf.Lerp(startRim, targetPeakRim, curvedT);

                    m_MaterialInstance.SetFloat(s_OutlineGlowId, g);
                    m_MaterialInstance.SetFloat(s_InnerRimIntensityId, r);
                    m_DebugEffectiveGlow = g;
                    m_DebugEffectiveRim = r;
                    yield return null;
                }
            }

            m_MaterialInstance.SetFloat(s_OutlineGlowId, targetPeakGlow);
            m_MaterialInstance.SetFloat(s_InnerRimIntensityId, targetPeakRim);
            m_DebugEffectiveGlow = targetPeakGlow;
            m_DebugEffectiveRim = targetPeakRim;

            // 2. HOLD
            m_CurrentPulseState = OutlinePulseState.Hold;
            if (hold > 0.002f)
            {
                float elapsedHold = 0f;
                while (elapsedHold < hold)
                {
                    elapsedHold += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            // 3. RELEASE
            m_CurrentPulseState = OutlinePulseState.Release;
            if (release > 0.002f)
            {
                float elapsedRel = 0f;
                while (elapsedRel < release)
                {
                    elapsedRel += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsedRel / release);
                    float decay = (1f - t) * (1f - t); // Smooth quadratic decay

                    float g = Mathf.Lerp(baseGlow, targetPeakGlow, decay);
                    float r = Mathf.Lerp(baseRim, targetPeakRim, decay);

                    m_MaterialInstance.SetFloat(s_OutlineGlowId, g);
                    m_MaterialInstance.SetFloat(s_InnerRimIntensityId, r);
                    m_DebugEffectiveGlow = g;
                    m_DebugEffectiveRim = r;
                    yield return null;
                }
            }

            ResetOutlineInternal();
            m_CurrentPulseState = OutlinePulseState.Idle;
            m_OutlinePulseRoutine = null;
        }

        /// <summary>
        /// Sets a continuous charge buildup level (0 to 1). Ramps outline glow and inner rim smoothly
        /// from baseline to charged level without fighting envelope coroutines.
        /// </summary>
        public void SetOutlineCharge(float normalizedCharge, Color? color = null)
        {
            if (!m_IsActive || m_MaterialInstance == null) return;

            CaptureBaselines();

            if (m_OutlinePulseRoutine != null)
            {
                StopCoroutine(m_OutlinePulseRoutine);
                m_OutlinePulseRoutine = null;
            }

            m_CurrentPulseState = normalizedCharge > 0.01f ? OutlinePulseState.Charging : OutlinePulseState.Idle;

            float charge = Mathf.Clamp01(normalizedCharge);
            float baseGlow = m_BaselineOutlineGlow * m_OutlineBaseGlow;
            float targetGlow = Mathf.Lerp(baseGlow, baseGlow * m_OutlinePeakGlow * 1.5f, charge);

            float baseRim = m_BaselineInnerRimIntensity;
            float targetRim = Mathf.Lerp(baseRim, baseRim * 1.8f, charge);

            m_MaterialInstance.SetFloat(s_OutlineGlowId, targetGlow);
            m_MaterialInstance.SetFloat(s_InnerRimIntensityId, targetRim);

            if (color.HasValue)
            {
                m_MaterialInstance.SetColor(s_OutlineColorId, color.Value);
            }

            m_DebugEffectiveGlow = targetGlow;
            m_DebugEffectiveRim = targetRim;

            if (m_LinkedOutlineLayer != null)
            {
                m_LinkedOutlineLayer.SetOutlineCharge(normalizedCharge, color);
            }
        }

        /// <summary>
        /// Backwards-compatible pulse method. Automatically maps to the instant AHR envelope.
        /// </summary>
        public void PulseOutline(float extraIntensity, float duration, Color? overrideColor = null)
        {
            float peak = 1f + (extraIntensity * 2.5f);
            float release = duration > 0f ? duration : m_OutlineReleaseTime;
            PulseOutlineImmediate(peak, m_OutlineAttackTime, m_OutlineHoldTime, release, overrideColor, "Pulse");
        }

        public void ResetOutline()
        {
            if (m_OutlinePulseRoutine != null)
            {
                StopCoroutine(m_OutlinePulseRoutine);
                m_OutlinePulseRoutine = null;
            }
            ResetOutlineInternal();
            m_CurrentPulseState = OutlinePulseState.Idle;

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
            m_DebugEffectiveGlow = m_BaselineOutlineGlow;
            m_DebugEffectiveRim = m_BaselineInnerRimIntensity;
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
            PulseOutlineImmediate(1f + extraIntensity * 1.5f, m_OutlineAttackTime, m_OutlineHoldTime, duration, overrideColor, "MainPulse");
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
            if (!m_IsActive || m_MaterialInstance == null || !IsGlitchPermitted()) return;

            if (m_GlitchRoutine != null)
            {
                StopCoroutine(m_GlitchRoutine);
            }
            m_GlitchRoutine = StartCoroutine(GlitchRoutine(duration));
        }

        private IEnumerator GlitchRoutine(float duration)
        {
            float glitchVal = (m_Profile == BackgroundLayerProfile.Spark) ? 0.045f : 0.035f;
            m_MaterialInstance.SetFloat(s_GlitchStrengthId, glitchVal);
            m_MaterialInstance.SetFloat(s_ScanStrengthId, 0.02f);

            yield return new WaitForSecondsRealtime(duration);

            m_MaterialInstance.SetFloat(s_GlitchStrengthId, 0f);
            m_MaterialInstance.SetFloat(s_ScanStrengthId, 0f);
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
            ApplyThemeColors();
            SyncShaderProperties();
        }

        /// <summary>
        /// Ensures a dedicated outline child/sibling GameObject exists with expanded mesh geometry.
        /// Guaranteed to instantiate with typeof(RectTransform) and cleanly replace any legacy Transform objects.
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
            outlineCtrl.m_Theme = m_Theme;
            outlineCtrl.m_ThemePreset = m_ThemePreset;
            outlineCtrl.m_UseTheme = m_UseTheme;
            outlineCtrl.Profile = m_Profile;
            outlineCtrl.m_AllowGlitch = false;
            outlineCtrl.ApplyThemeColors();
            outlineCtrl.SyncShaderProperties();

            m_LinkedOutlineLayer = outlineCtrl;

            // Ensure source foreground layer does not draw redundant outer outline
            m_OutlineWidth = 0f;
            SyncShaderProperties();

            return outlineCtrl;
        }

        /// <summary>
        /// Triggers an intentional, controlled glitch pulse through MainMenuGlitchController.
        /// </summary>
        public void TriggerControlledGlitch(ControlledGlitchType type, float durationMultiplier = 1f, Action onComplete = null)
        {
            if (!IsGlitchPermitted())
            {
                onComplete?.Invoke();
                return;
            }

            MainMenuGlitchController glitcher = GetComponent<MainMenuGlitchController>() ?? gameObject.AddComponent<MainMenuGlitchController>();
            glitcher.TriggerGlitch(type, durationMultiplier, onComplete);

            if (m_LinkedOutlineLayer != null && m_LinkedOutlineLayer.IsGlitchPermitted())
            {
                MainMenuGlitchController linkedGlitcher = m_LinkedOutlineLayer.GetComponent<MainMenuGlitchController>() ?? m_LinkedOutlineLayer.gameObject.AddComponent<MainMenuGlitchController>();
                linkedGlitcher.TriggerGlitch(type, durationMultiplier);
            }
        }
    }
}
