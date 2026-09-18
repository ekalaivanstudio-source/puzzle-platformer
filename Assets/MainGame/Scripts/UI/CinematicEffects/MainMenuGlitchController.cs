using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MainGame.UI.CinematicEffects
{
    public enum ControlledGlitchType
    {
        TypeA_MicroGlitch = 0,     // 0.03-0.07s: 1-3 small blocks, subtle RGB offset, quick brightness flicker. Subtle punctuation.
        TypeB_DigitalTear = 1,     // 0.04-0.10s: Localized horizontal slice displacement, controlled rectangular segments. Rare.
        TypeC_SignalBreak = 2,     // 0.06-0.12s: Short horizontal segments, stepped displacement, quick reassembly. Transitions.
        TypeD_MajorImpactGlitch = 3 // 0.08-0.15s: Stronger displacement, pixel fragments, brightness flash, RGB split, instant recovery.
    }

    [Serializable]
    public struct ControlledGlitchProfile
    {
        public float duration;
        public float displacementStrength;
        public float rgbOffset;
        public float brightnessBoost;
        public float sliceCount;
    }

    /// <summary>
    /// Governs intentional, controlled digital glitch punctuation for Main Menu elements.
    /// Implements a strict 6-phase lifecycle:
    /// NORMAL -> PRE-GLITCH -> DISTORTION -> IMPACT -> RECONSTRUCT -> NORMAL.
    /// Eliminates random, uncontrolled constant glitches and guarantees deterministic reset.
    /// </summary>
    [DisallowMultipleComponent]
    public class MainMenuGlitchController : MonoBehaviour
    {
        private static readonly int s_GlitchStrengthId = Shader.PropertyToID("_GlitchStrength");
        private static readonly int s_GlitchBlockSizeId = Shader.PropertyToID("_GlitchBlockSize");
        private static readonly int s_GlitchIntensityId = Shader.PropertyToID("_GlitchIntensity");
        private static readonly int s_GlitchSlicesId = Shader.PropertyToID("_GlitchSlices");
        private static readonly int s_RgbOffsetId = Shader.PropertyToID("_RgbOffset");
        private static readonly int s_ScanStrengthId = Shader.PropertyToID("_ScanStrength");
        private static readonly int s_SurfaceBrightnessId = Shader.PropertyToID("_SurfaceBrightness");

        [Header("Profile Configurations")]
        [SerializeField] private ControlledGlitchProfile m_TypeAMicro = new ControlledGlitchProfile
        {
            duration = 0.05f,
            displacementStrength = 0.015f,
            rgbOffset = 0.005f,
            brightnessBoost = 0.15f,
            sliceCount = 48f
        };

        [SerializeField] private ControlledGlitchProfile m_TypeBDigitalTear = new ControlledGlitchProfile
        {
            duration = 0.07f,
            displacementStrength = 0.035f,
            rgbOffset = 0.009f,
            brightnessBoost = 0.25f,
            sliceCount = 28f
        };

        [SerializeField] private ControlledGlitchProfile m_TypeCSignalBreak = new ControlledGlitchProfile
        {
            duration = 0.09f,
            displacementStrength = 0.055f,
            rgbOffset = 0.014f,
            brightnessBoost = 0.35f,
            sliceCount = 20f
        };

        [SerializeField] private ControlledGlitchProfile m_TypeDMajorImpact = new ControlledGlitchProfile
        {
            duration = 0.12f,
            displacementStrength = 0.085f,
            rgbOffset = 0.022f,
            brightnessBoost = 0.60f,
            sliceCount = 16f
        };

        private Material m_TargetMaterial;
        private UIBackgroundLayerController m_LayerController;
        private CinematicUIEffect m_CinematicUIEffect;
        private Coroutine m_ActiveGlitchRoutine;

        public bool IsGlitching => m_ActiveGlitchRoutine != null;

        private void Awake()
        {
            ResolveTarget();
            if (!IsGlitchAllowed())
            {
                ResetToNormal();
            }
        }

        public bool IsGlitchAllowed()
        {
            if (m_LayerController != null)
            {
                return m_LayerController.IsGlitchPermitted();
            }

            string goName = gameObject.name.ToLowerInvariant();
            return goName.Contains("bg red") || goName.Contains("backgroundred") || goName.Contains("bg_red");
        }

        public void ResolveTarget()
        {
            if (m_LayerController == null) m_LayerController = GetComponent<UIBackgroundLayerController>();
            if (m_CinematicUIEffect == null) m_CinematicUIEffect = GetComponent<CinematicUIEffect>();

            if (m_TargetMaterial == null)
            {
                if (m_LayerController != null && m_LayerController.MaterialInstance != null)
                {
                    m_TargetMaterial = m_LayerController.MaterialInstance;
                }
                else if (m_CinematicUIEffect != null && m_CinematicUIEffect.MaterialInstance != null)
                {
                    m_TargetMaterial = m_CinematicUIEffect.MaterialInstance;
                }
                else
                {
                    Graphic g = GetComponent<Graphic>();
                    if (g != null) m_TargetMaterial = g.material;
                }
            }
        }

        public void TriggerGlitch(ControlledGlitchType type, float durationMultiplier = 1f, Action onComplete = null)
        {
            ResolveTarget();
            if (m_TargetMaterial == null || !IsGlitchAllowed())
            {
                ResetToNormal();
                onComplete?.Invoke();
                return;
            }

            ControlledGlitchProfile profile = GetProfile(type);
            profile.duration *= Mathf.Max(0.1f, durationMultiplier);

            if (m_ActiveGlitchRoutine != null)
            {
                StopCoroutine(m_ActiveGlitchRoutine);
                ResetToNormal();
            }

            m_ActiveGlitchRoutine = StartCoroutine(GlitchLifecycleRoutine(profile, onComplete));
        }

        public ControlledGlitchProfile GetProfile(ControlledGlitchType type)
        {
            switch (type)
            {
                case ControlledGlitchType.TypeA_MicroGlitch: return m_TypeAMicro;
                case ControlledGlitchType.TypeB_DigitalTear: return m_TypeBDigitalTear;
                case ControlledGlitchType.TypeC_SignalBreak: return m_TypeCSignalBreak;
                case ControlledGlitchType.TypeD_MajorImpactGlitch: return m_TypeDMajorImpact;
                default: return m_TypeAMicro;
            }
        }

        private IEnumerator GlitchLifecycleRoutine(ControlledGlitchProfile profile, Action onComplete)
        {
            float totalDur = Mathf.Max(0.02f, profile.duration);
            // 6-Phase Lifecycle division:
            // 1. Pre-glitch anticipation (15%)
            // 2. Distortion peak (35%)
            // 3. Impact strike (20%)
            // 4. Reconstruct & reassemble (30%)
            float preDur = totalDur * 0.15f;
            float distDur = totalDur * 0.35f;
            float impactDur = totalDur * 0.20f;
            float reconDur = totalDur * 0.30f;

            // Apply slice density
            if (m_TargetMaterial.HasProperty(s_GlitchSlicesId))
                m_TargetMaterial.SetFloat(s_GlitchSlicesId, profile.sliceCount);
            if (m_TargetMaterial.HasProperty(s_GlitchBlockSizeId))
                m_TargetMaterial.SetFloat(s_GlitchBlockSizeId, profile.sliceCount);

            // ─── PHASE 1: PRE-GLITCH (TINY FLICKER ANTICIPATION) ───────────────
            float elapsed = 0f;
            while (elapsed < preDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / preDur;
                float flicker = t * profile.brightnessBoost * 0.35f;
                ApplyParameters(profile.displacementStrength * 0.2f * t, profile.rgbOffset * 0.25f * t, flicker);
                yield return null;
            }

            // ─── PHASE 2: PEAK DISTORTION (MAX HORIZONTAL SLICE DISPLACEMENT) ───
            elapsed = 0f;
            while (elapsed < distDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / distDur;
                float intensity = Mathf.Lerp(0.5f, 1.0f, Mathf.Sin(t * Mathf.PI));
                ApplyParameters(profile.displacementStrength * intensity, profile.rgbOffset * intensity, profile.brightnessBoost * intensity);
                yield return null;
            }

            // ─── PHASE 3: IMPACT (BRIGHT FLASH & COMPRESSION SNAP) ─────────────
            elapsed = 0f;
            while (elapsed < impactDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / impactDur;
                float flash = Mathf.Lerp(profile.brightnessBoost, 0f, t);
                ApplyParameters(profile.displacementStrength * 0.4f * (1f - t), profile.rgbOffset * 0.4f * (1f - t), flash);
                yield return null;
            }

            // ─── PHASE 4: RECONSTRUCT & REASSEMBLE (PIXELS LOCK BACK TO NORMAL) ─
            elapsed = 0f;
            while (elapsed < reconDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / reconDur;
                float decay = 1f - t;
                ApplyParameters(profile.displacementStrength * 0.1f * decay, profile.rgbOffset * 0.1f * decay, 0f);
                yield return null;
            }

            // ─── PHASE 5: DETERMINISTIC NORMAL STATE RESTORE ────────────────────
            ResetToNormal();
            m_ActiveGlitchRoutine = null;
            onComplete?.Invoke();
        }

        private void ApplyParameters(float displacement, float rgbOffset, float brightness)
        {
            if (m_TargetMaterial == null) return;

            if (m_TargetMaterial.HasProperty(s_GlitchStrengthId))
                m_TargetMaterial.SetFloat(s_GlitchStrengthId, displacement);

            if (m_TargetMaterial.HasProperty(s_GlitchIntensityId))
                m_TargetMaterial.SetFloat(s_GlitchIntensityId, displacement * 10f);

            if (m_TargetMaterial.HasProperty(s_RgbOffsetId))
                m_TargetMaterial.SetFloat(s_RgbOffsetId, rgbOffset);

            if (m_TargetMaterial.HasProperty(s_SurfaceBrightnessId))
                m_TargetMaterial.SetFloat(s_SurfaceBrightnessId, brightness);
        }

        public void ResetToNormal()
        {
            if (m_TargetMaterial != null)
            {
                if (m_TargetMaterial.HasProperty(s_GlitchStrengthId))
                    m_TargetMaterial.SetFloat(s_GlitchStrengthId, 0f);

                if (m_TargetMaterial.HasProperty(s_GlitchIntensityId))
                    m_TargetMaterial.SetFloat(s_GlitchIntensityId, 0f);

                if (m_TargetMaterial.HasProperty(s_RgbOffsetId))
                    m_TargetMaterial.SetFloat(s_RgbOffsetId, 0f);

                if (m_TargetMaterial.HasProperty(s_ScanStrengthId))
                    m_TargetMaterial.SetFloat(s_ScanStrengthId, 0f);

                if (m_TargetMaterial.HasProperty(s_SurfaceBrightnessId))
                    m_TargetMaterial.SetFloat(s_SurfaceBrightnessId, 0f);
            }
        }

        private void OnDisable()
        {
            if (m_ActiveGlitchRoutine != null)
            {
                StopCoroutine(m_ActiveGlitchRoutine);
                m_ActiveGlitchRoutine = null;
            }
            ResetToNormal();
        }
    }
}
