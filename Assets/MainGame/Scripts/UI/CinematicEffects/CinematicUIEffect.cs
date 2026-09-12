using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using MainGame.UI.Animation;

namespace MainGame.UI.CinematicEffects
{
    public enum UIBorderDirection
    {
        PerimeterClockwise = 0,
        PerimeterCounterClockwise = 1,
        LeftToRight = 2,
        RightToLeft = 3,
        TopToBottom = 4,
        BottomToTop = 5
    }

    /// <summary>
    /// Master controller component for the Cinematic UI Shader ("MainGame/UI/CinematicUI").
    /// Injects normalized rect coordinates and pixel dimensions into vertices via IMeshModifier.
    /// Drives both surface interactions and perimeter border energy:
    /// - Surface Pixel Reconstruction & Noise Reveal (entry)
    /// - Surface Pixel Dissolve (exit)
    /// - Radial Energy Pulse (origin-centered expanding power surge & distortion)
    /// - Circular Refractive Shockwave Distortion
    /// - Digital Glitch & Chromatic Aberration (RGB Split)
    /// - Surface Scan Sweep & Holographic Flicker
    /// - Data Telemetry Stream Overlays
    /// - Control Segment Signal Flow (sliders)
    /// - Multi-directional perimeter electrical borders
    /// - Deterministic reset without transform or property drift
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Graphic))]
    public class CinematicUIEffect : BaseMeshEffect
    {
        #region Shader Property IDs
        private static readonly int PropBorderColor = Shader.PropertyToID("_BorderColor");
        private static readonly int PropBorderIntensity = Shader.PropertyToID("_BorderIntensity");
        private static readonly int PropBorderWidth = Shader.PropertyToID("_BorderWidth");
        private static readonly int PropBorderSoftness = Shader.PropertyToID("_BorderSoftness");
        private static readonly int PropPulseProgress = Shader.PropertyToID("_PulseProgress");
        private static readonly int PropPulseWidth = Shader.PropertyToID("_PulseWidth");
        private static readonly int PropPulseFalloff = Shader.PropertyToID("_PulseFalloff");
        private static readonly int PropFlowSpeed = Shader.PropertyToID("_FlowSpeed");
        private static readonly int PropClockwise = Shader.PropertyToID("_Clockwise");
        private static readonly int PropEnergyActive = Shader.PropertyToID("_EnergyActive");
        private static readonly int PropBorderPulseIntensity = Shader.PropertyToID("_BorderPulseIntensity");
        private static readonly int PropBorderDirectionMode = Shader.PropertyToID("_BorderDirectionMode");

        private static readonly int PropElecNoiseAmount = Shader.PropertyToID("_ElecNoiseAmount");
        private static readonly int PropElecFreq = Shader.PropertyToID("_ElecFreq");

        private static readonly int PropSweepColor = Shader.PropertyToID("_SweepColor");
        private static readonly int PropSweepIntensity = Shader.PropertyToID("_SweepIntensity");
        private static readonly int PropSweepProgress = Shader.PropertyToID("_SweepProgress");
        private static readonly int PropSweepWidth = Shader.PropertyToID("_SweepWidth");
        private static readonly int PropSweepAngle = Shader.PropertyToID("_SweepAngle");

        private static readonly int PropGlitchIntensity = Shader.PropertyToID("_GlitchIntensity");
        private static readonly int PropGlitchSlices = Shader.PropertyToID("_GlitchSlices");
        private static readonly int PropRgbOffset = Shader.PropertyToID("_RgbOffset");

        private static readonly int PropShockwaveProgress = Shader.PropertyToID("_ShockwaveProgress");
        private static readonly int PropShockwaveOrigin = Shader.PropertyToID("_ShockwaveOrigin");
        private static readonly int PropShockwaveStrength = Shader.PropertyToID("_ShockwaveStrength");
        private static readonly int PropShockwaveThickness = Shader.PropertyToID("_ShockwaveThickness");

        private static readonly int PropRadialPulseRadius = Shader.PropertyToID("_RadialPulseRadius");
        private static readonly int PropRadialPulseCenter = Shader.PropertyToID("_RadialPulseCenter");
        private static readonly int PropRadialPulseThickness = Shader.PropertyToID("_RadialPulseThickness");
        private static readonly int PropRadialPulseIntensity = Shader.PropertyToID("_RadialPulseIntensity");
        private static readonly int PropRadialPulseColor = Shader.PropertyToID("_RadialPulseColor");
        private static readonly int PropRadialPulseDistort = Shader.PropertyToID("_RadialPulseDistort");

        private static readonly int PropReconstructProgress = Shader.PropertyToID("_ReconstructProgress");
        private static readonly int PropReconstructColor = Shader.PropertyToID("_ReconstructColor");
        private static readonly int PropReconstructBlockiness = Shader.PropertyToID("_ReconstructBlockiness");
        private static readonly int PropReconstructEdgeWidth = Shader.PropertyToID("_ReconstructEdgeWidth");

        private static readonly int PropDissolveProgress = Shader.PropertyToID("_DissolveProgress");
        private static readonly int PropDissolveEdgeWidth = Shader.PropertyToID("_DissolveEdgeWidth");
        private static readonly int PropDissolveEdgeColor = Shader.PropertyToID("_DissolveEdgeColor");
        private static readonly int PropDissolveBlockiness = Shader.PropertyToID("_DissolveBlockiness");

        private static readonly int PropNoiseRevealProgress = Shader.PropertyToID("_NoiseRevealProgress");
        private static readonly int PropNoiseRevealColor = Shader.PropertyToID("_NoiseRevealColor");
        private static readonly int PropNoiseRevealEdgeWidth = Shader.PropertyToID("_NoiseRevealEdgeWidth");
        private static readonly int PropNoiseRevealScale = Shader.PropertyToID("_NoiseRevealScale");

        private static readonly int PropHoloFlicker = Shader.PropertyToID("_HoloFlicker");
        private static readonly int PropScanDistortIntensity = Shader.PropertyToID("_ScanDistortIntensity");
        private static readonly int PropScanDistortY = Shader.PropertyToID("_ScanDistortY");
        private static readonly int PropScanDistortWidth = Shader.PropertyToID("_ScanDistortWidth");

        private static readonly int PropDataStreamIntensity = Shader.PropertyToID("_DataStreamIntensity");
        private static readonly int PropDataStreamSpeed = Shader.PropertyToID("_DataStreamSpeed");
        private static readonly int PropDataStreamDensity = Shader.PropertyToID("_DataStreamDensity");
        private static readonly int PropDataStreamColor = Shader.PropertyToID("_DataStreamColor");

        private static readonly int PropSegmentFlowProgress = Shader.PropertyToID("_SegmentFlowProgress");
        private static readonly int PropSegmentFlowIntensity = Shader.PropertyToID("_SegmentFlowIntensity");
        private static readonly int PropSegmentFlowCount = Shader.PropertyToID("_SegmentFlowCount");
        private static readonly int PropSegmentFlowColor = Shader.PropertyToID("_SegmentFlowColor");

        private static readonly int PropFlashColor = Shader.PropertyToID("_FlashColor");
        private static readonly int PropFlashIntensity = Shader.PropertyToID("_FlashIntensity");
        private static readonly int PropSurfaceBrightness = Shader.PropertyToID("_SurfaceBrightness");
        #endregion

        private Graphic m_Graphic;
        private RectTransform m_RectTransform;
        private Material m_MaterialInstance;
        private static Shader s_CinematicShader;

        private Coroutine m_SweepRoutine;
        private Coroutine m_PulseRoutine;
        private Coroutine m_FlashRoutine;
        private Coroutine m_GlitchRoutine;
        private Coroutine m_RadialRoutine;
        private Coroutine m_ShockwaveRoutine;
        private Coroutine m_ReconstructRoutine;
        private Coroutine m_DissolveRoutine;
        private Coroutine m_NoiseRevealRoutine;
        private Coroutine m_ScanRoutine;
        private Coroutine m_DataStreamRoutine;
        private Coroutine m_SegmentFlowRoutine;
        private Coroutine m_IdleRoutine;

        public Graphic GraphicTarget => m_Graphic;
        public Material MaterialInstance => m_MaterialInstance;

        protected override void Awake()
        {
            base.Awake();
            m_Graphic = GetComponent<Graphic>();
            m_RectTransform = GetComponent<RectTransform>();
            EnsureMaterialInstance();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            EnsureMaterialInstance();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            ResetToIdle();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            ResetToIdle();
            if (m_MaterialInstance != null)
            {
                if (m_Graphic != null && m_Graphic.material == m_MaterialInstance)
                {
                    m_Graphic.material = null;
                }
                Destroy(m_MaterialInstance);
                m_MaterialInstance = null;
            }
        }

        private void EnsureMaterialInstance()
        {
            if (m_Graphic == null) m_Graphic = GetComponent<Graphic>();
            if (m_Graphic == null) return;

            if (s_CinematicShader == null)
            {
                s_CinematicShader = Shader.Find("MainGame/UI/CinematicUI");
            }

            if (s_CinematicShader != null && m_MaterialInstance == null)
            {
                m_MaterialInstance = new Material(s_CinematicShader)
                {
                    name = $"{gameObject.name}_CinematicUI_Instance",
                    hideFlags = HideFlags.DontSave
                };
                m_Graphic.material = m_MaterialInstance;
            }
        }

        #region IMeshModifier Implementation

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh == null || vh.currentVertCount == 0) return;

            if (m_RectTransform == null) m_RectTransform = GetComponent<RectTransform>();
            if (m_RectTransform == null) return;

            Rect rect = m_RectTransform.rect;
            float width = Mathf.Max(rect.width, 1f);
            float height = Mathf.Max(rect.height, 1f);

            UIVertex vert = new UIVertex();
            int count = vh.currentVertCount;

            for (int i = 0; i < count; i++)
            {
                vh.PopulateUIVertex(ref vert, i);

                float normX = Mathf.Clamp01((vert.position.x - rect.xMin) / width);
                float normY = Mathf.Clamp01((vert.position.y - rect.yMin) / height);

                // uv1.xy = normalized [0..1] coordinates, uv1.zw = pixel dimensions
                vert.uv1 = new Vector4(normX, normY, width, height);

                vh.SetUIVertex(vert, i);
            }
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            if (m_Graphic != null)
            {
                m_Graphic.SetVerticesDirty();
            }
        }

        #endregion

        #region Public Cinematic Effect APIs

        /// <summary>
        /// Surface Pixel Reconstruction: UI appears from small pixel fragments and consolidates into solid artwork.
        /// </summary>
        public void PlayPixelReconstruction(float duration = 0.35f, Color? edgeColor = null, float blockiness = 28f, Action onComplete = null)
        {
            EnsureMaterialInstance();
            if (m_MaterialInstance == null) { onComplete?.Invoke(); return; }

            if (m_ReconstructRoutine != null) StopCoroutine(m_ReconstructRoutine);
            m_ReconstructRoutine = StartCoroutine(PixelReconstructionRoutine(duration, edgeColor ?? new Color(0.35f, 0.9f, 1f, 1f), blockiness, onComplete));
        }

        private IEnumerator PixelReconstructionRoutine(float duration, Color edgeColor, float blockiness, Action onComplete)
        {
            m_MaterialInstance.SetColor(PropReconstructColor, edgeColor);
            m_MaterialInstance.SetFloat(PropReconstructBlockiness, blockiness);
            m_MaterialInstance.SetFloat(PropReconstructEdgeWidth, 0.08f);

            float elapsed = 0f;
            while (elapsed < duration && m_MaterialInstance != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = UIEasing.Evaluate(EasingType.EaseOutQuad, t);
                m_MaterialInstance.SetFloat(PropReconstructProgress, ease);
                yield return null;
            }

            if (m_MaterialInstance != null)
            {
                m_MaterialInstance.SetFloat(PropReconstructProgress, 1.0f);
            }
            m_ReconstructRoutine = null;
            onComplete?.Invoke();
        }

        /// <summary>
        /// Surface Pixel Dissolve: UI surface breaks into pixel blocks with edge glow.
        /// </summary>
        public void PlayPixelDissolve(float duration = 0.28f, Color? edgeColor = null, float blockiness = 24f, Action onComplete = null)
        {
            EnsureMaterialInstance();
            if (m_MaterialInstance == null) { onComplete?.Invoke(); return; }

            if (m_DissolveRoutine != null) StopCoroutine(m_DissolveRoutine);
            m_DissolveRoutine = StartCoroutine(PixelDissolveRoutine(duration, edgeColor ?? new Color(0.35f, 0.9f, 1f, 1f), blockiness, onComplete));
        }

        private IEnumerator PixelDissolveRoutine(float duration, Color edgeColor, float blockiness, Action onComplete)
        {
            m_MaterialInstance.SetColor(PropDissolveEdgeColor, edgeColor);
            m_MaterialInstance.SetFloat(PropDissolveBlockiness, blockiness);
            m_MaterialInstance.SetFloat(PropDissolveEdgeWidth, 0.06f);

            float elapsed = 0f;
            while (elapsed < duration && m_MaterialInstance != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = UIEasing.Evaluate(EasingType.EaseInQuad, t);
                m_MaterialInstance.SetFloat(PropDissolveProgress, ease);
                yield return null;
            }

            if (m_MaterialInstance != null)
            {
                m_MaterialInstance.SetFloat(PropDissolveProgress, 1.0f);
            }
            m_DissolveRoutine = null;
            onComplete?.Invoke();
        }

        public void SetPixelDissolve(float progress, Color? edgeColor = null)
        {
            EnsureMaterialInstance();
            if (m_MaterialInstance != null)
            {
                m_MaterialInstance.SetFloat(PropDissolveProgress, Mathf.Clamp01(progress));
                if (edgeColor.HasValue) m_MaterialInstance.SetColor(PropDissolveEdgeColor, edgeColor.Value);
            }
        }

        /// <summary>
        /// Surface Noise Reveal: Procedural stepped network/map reveal.
        /// </summary>
        public void PlayNoiseReveal(float duration = 0.36f, Color? edgeColor = null, float scale = 36f, Action onComplete = null)
        {
            EnsureMaterialInstance();
            if (m_MaterialInstance == null) { onComplete?.Invoke(); return; }

            if (m_NoiseRevealRoutine != null) StopCoroutine(m_NoiseRevealRoutine);
            m_NoiseRevealRoutine = StartCoroutine(NoiseRevealRoutine(duration, edgeColor ?? new Color(0.35f, 0.85f, 1f, 1f), scale, onComplete));
        }

        private IEnumerator NoiseRevealRoutine(float duration, Color edgeColor, float scale, Action onComplete)
        {
            m_MaterialInstance.SetColor(PropNoiseRevealColor, edgeColor);
            m_MaterialInstance.SetFloat(PropNoiseRevealScale, scale);
            m_MaterialInstance.SetFloat(PropNoiseRevealEdgeWidth, 0.06f);

            float elapsed = 0f;
            while (elapsed < duration && m_MaterialInstance != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = UIEasing.Evaluate(EasingType.EaseOutQuad, t);
                m_MaterialInstance.SetFloat(PropNoiseRevealProgress, ease);
                yield return null;
            }

            if (m_MaterialInstance != null)
            {
                m_MaterialInstance.SetFloat(PropNoiseRevealProgress, 1.0f);
            }
            m_NoiseRevealRoutine = null;
            onComplete?.Invoke();
        }

        /// <summary>
        /// Surface Noise Dissolve: Procedural stepped network/map dissolve for exit transitions.
        /// </summary>
        public void PlayNoiseDissolve(float duration = 0.28f, Color? edgeColor = null, float scale = 36f, Action onComplete = null)
        {
            EnsureMaterialInstance();
            if (m_MaterialInstance == null) { onComplete?.Invoke(); return; }

            if (m_NoiseRevealRoutine != null) StopCoroutine(m_NoiseRevealRoutine);
            m_NoiseRevealRoutine = StartCoroutine(NoiseDissolveRoutine(duration, edgeColor ?? new Color(0.35f, 0.85f, 1f, 1f), scale, onComplete));
        }

        private IEnumerator NoiseDissolveRoutine(float duration, Color edgeColor, float scale, Action onComplete)
        {
            m_MaterialInstance.SetColor(PropNoiseRevealColor, edgeColor);
            m_MaterialInstance.SetFloat(PropNoiseRevealScale, scale);
            m_MaterialInstance.SetFloat(PropNoiseRevealEdgeWidth, 0.06f);

            float elapsed = 0f;
            while (elapsed < duration && m_MaterialInstance != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = 1f - UIEasing.Evaluate(EasingType.EaseInQuad, t);
                m_MaterialInstance.SetFloat(PropNoiseRevealProgress, ease);
                yield return null;
            }

            if (m_MaterialInstance != null)
            {
                m_MaterialInstance.SetFloat(PropNoiseRevealProgress, 0f);
            }
            m_NoiseRevealRoutine = null;
            onComplete?.Invoke();
        }

        /// <summary>
        /// Directional Scan Sweep across element artwork (Alias with angle/width control).
        /// </summary>
        public void PlayScanSweep(float duration = 0.30f, Color? color = null, float angle = 90f, float width = 0.14f, Action onComplete = null)
        {
            EnsureMaterialInstance();
            if (m_MaterialInstance == null) { onComplete?.Invoke(); return; }

            if (m_SweepRoutine != null) StopCoroutine(m_SweepRoutine);
            m_SweepRoutine = StartCoroutine(SweepRoutine(duration, color ?? new Color(0.85f, 0.96f, 1f, 1f), angle, onComplete));
        }

        /// <summary>
        /// Concentric Radial Energy Pulse: Expands outward from an origin point across the UI surface.
        /// </summary>
        public void TriggerRadialPulse(float duration = 0.35f, Color? color = null, Vector2? centerUV = null, float maxRadius = 1.4f, float intensity = 3.0f, float distortion = 0.03f, Action onComplete = null)
        {
            EnsureMaterialInstance();
            if (m_MaterialInstance == null) { onComplete?.Invoke(); return; }

            if (m_RadialRoutine != null) StopCoroutine(m_RadialRoutine);
            m_RadialRoutine = StartCoroutine(RadialPulseRoutine(duration, color ?? new Color(0.4f, 0.9f, 1f, 1f), centerUV ?? new Vector2(0.5f, 0.5f), maxRadius, intensity, distortion, onComplete));
        }

        private IEnumerator RadialPulseRoutine(float duration, Color pulseColor, Vector2 center, float maxRadius, float intensity, float distortion, Action onComplete)
        {
            m_MaterialInstance.SetColor(PropRadialPulseColor, pulseColor);
            m_MaterialInstance.SetVector(PropRadialPulseCenter, new Vector4(center.x, center.y, 0, 0));
            m_MaterialInstance.SetFloat(PropRadialPulseThickness, 0.07f);
            m_MaterialInstance.SetFloat(PropRadialPulseDistort, distortion);

            float elapsed = 0f;
            while (elapsed < duration && m_MaterialInstance != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                float radius = Mathf.Lerp(0f, maxRadius, t);
                float curIntensity = Mathf.Lerp(intensity, 0f, t);

                m_MaterialInstance.SetFloat(PropRadialPulseRadius, radius);
                m_MaterialInstance.SetFloat(PropRadialPulseIntensity, curIntensity);
                yield return null;
            }

            if (m_MaterialInstance != null)
            {
                m_MaterialInstance.SetFloat(PropRadialPulseIntensity, 0f);
                m_MaterialInstance.SetFloat(PropRadialPulseRadius, 0f);
                m_MaterialInstance.SetFloat(PropRadialPulseDistort, 0f);
            }
            m_RadialRoutine = null;
            onComplete?.Invoke();
        }

        /// <summary>
        /// Circular Refractive Shockwave: Displaces UI surface pixels outward in an expanding ripple.
        /// </summary>
        public void TriggerShockwave(float duration = 0.26f, Vector2? originUV = null, float strength = 0.08f, float thickness = 0.08f, Action onComplete = null)
        {
            EnsureMaterialInstance();
            if (m_MaterialInstance == null) { onComplete?.Invoke(); return; }

            if (m_ShockwaveRoutine != null) StopCoroutine(m_ShockwaveRoutine);
            m_ShockwaveRoutine = StartCoroutine(ShockwaveRoutine(duration, originUV ?? new Vector2(0.5f, 0.5f), strength, thickness, onComplete));
        }

        private IEnumerator ShockwaveRoutine(float duration, Vector2 origin, float strength, float thickness, Action onComplete)
        {
            m_MaterialInstance.SetVector(PropShockwaveOrigin, new Vector4(origin.x, origin.y, 0, 0));
            m_MaterialInstance.SetFloat(PropShockwaveStrength, strength);
            m_MaterialInstance.SetFloat(PropShockwaveThickness, thickness);

            float elapsed = 0f;
            while (elapsed < duration && m_MaterialInstance != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                m_MaterialInstance.SetFloat(PropShockwaveProgress, t);
                yield return null;
            }

            if (m_MaterialInstance != null)
            {
                m_MaterialInstance.SetFloat(PropShockwaveProgress, 0f);
                m_MaterialInstance.SetFloat(PropShockwaveStrength, 0f);
            }
            m_ShockwaveRoutine = null;
            onComplete?.Invoke();
        }

        /// <summary>
        /// Surface Scan Distortion: Sweeps a horizontal scanner line across the surface that refracts and illuminates pixels.
        /// </summary>
        public void PlayCharacterScan(float duration = 0.28f, float intensity = 0.8f, Action onCore = null, Action onComplete = null)
        {
            EnsureMaterialInstance();
            if (m_MaterialInstance == null) { onCore?.Invoke(); onComplete?.Invoke(); return; }

            if (m_ScanRoutine != null) StopCoroutine(m_ScanRoutine);
            m_ScanRoutine = StartCoroutine(ScanDistortRoutine(duration, intensity, onCore, onComplete));
        }

        private IEnumerator ScanDistortRoutine(float duration, float intensity, Action onCore, Action onComplete)
        {
            m_MaterialInstance.SetFloat(PropScanDistortIntensity, intensity);
            m_MaterialInstance.SetFloat(PropScanDistortWidth, 0.06f);

            bool coreHit = false;
            float elapsed = 0f;
            while (elapsed < duration && m_MaterialInstance != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Scan top (1.0) to bottom (0.0)
                float curY = 1.0f - t;
                m_MaterialInstance.SetFloat(PropScanDistortY, curY);

                if (!coreHit && t >= 0.5f)
                {
                    coreHit = true;
                    onCore?.Invoke();
                }
                yield return null;
            }

            if (!coreHit) onCore?.Invoke();

            if (m_MaterialInstance != null)
            {
                m_MaterialInstance.SetFloat(PropScanDistortIntensity, 0f);
            }
            m_ScanRoutine = null;
            onComplete?.Invoke();
        }

        /// <summary>
        /// Data Stream Overlay: Displays horizontal digital data streaks (Credits screen telemetry).
        /// </summary>
        public void PlayDataStream(float duration = 0.50f, float intensity = 1.2f, Color? color = null, float speed = 5.0f, float density = 24.0f, Action onComplete = null)
        {
            EnsureMaterialInstance();
            if (m_MaterialInstance == null) { onComplete?.Invoke(); return; }

            if (m_DataStreamRoutine != null) StopCoroutine(m_DataStreamRoutine);
            m_DataStreamRoutine = StartCoroutine(DataStreamRoutine(duration, intensity, color ?? new Color(0.4f, 0.85f, 1f, 1f), speed, density, onComplete));
        }

        private IEnumerator DataStreamRoutine(float duration, float intensity, Color streamColor, float speed, float density, Action onComplete)
        {
            m_MaterialInstance.SetColor(PropDataStreamColor, streamColor);
            m_MaterialInstance.SetFloat(PropDataStreamSpeed, speed);
            m_MaterialInstance.SetFloat(PropDataStreamDensity, density);

            float elapsed = 0f;
            while (elapsed < duration && m_MaterialInstance != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float curIntensity = Mathf.Sin(t * Mathf.PI) * intensity;
                m_MaterialInstance.SetFloat(PropDataStreamIntensity, curIntensity);
                yield return null;
            }

            if (m_MaterialInstance != null)
            {
                m_MaterialInstance.SetFloat(PropDataStreamIntensity, 0f);
            }
            m_DataStreamRoutine = null;
            onComplete?.Invoke();
        }

        /// <summary>
        /// Segment Flow Signal: Precision pulse along stepped slider segments for Settings.
        /// </summary>
        public void PlaySegmentFlow(float stepProgress, float duration = 0.14f, Color? color = null, float segmentCount = 10f, Action onComplete = null)
        {
            EnsureMaterialInstance();
            if (m_MaterialInstance == null) { onComplete?.Invoke(); return; }

            if (m_SegmentFlowRoutine != null) StopCoroutine(m_SegmentFlowRoutine);
            m_SegmentFlowRoutine = StartCoroutine(SegmentFlowRoutine(stepProgress, duration, color ?? new Color(0.35f, 0.85f, 1f, 1f), segmentCount, onComplete));
        }

        private IEnumerator SegmentFlowRoutine(float targetStep, float duration, Color color, float count, Action onComplete)
        {
            m_MaterialInstance.SetColor(PropSegmentFlowColor, color);
            m_MaterialInstance.SetFloat(PropSegmentFlowCount, count);
            m_MaterialInstance.SetFloat(PropSegmentFlowProgress, targetStep);

            float elapsed = 0f;
            while (elapsed < duration && m_MaterialInstance != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float intensity = Mathf.Sin(t * Mathf.PI) * 2.5f;
                m_MaterialInstance.SetFloat(PropSegmentFlowIntensity, intensity);
                yield return null;
            }

            if (m_MaterialInstance != null)
            {
                m_MaterialInstance.SetFloat(PropSegmentFlowIntensity, 0f);
            }
            m_SegmentFlowRoutine = null;
            onComplete?.Invoke();
        }

        /// <summary>
        /// Multi-Directional Energy Border Pulse.
        /// </summary>
        public void TriggerBorderPulse(float duration = 0.28f, Color? color = null, float peakIntensity = 2.8f, UIBorderDirection direction = UIBorderDirection.PerimeterClockwise, Action onComplete = null)
        {
            EnsureMaterialInstance();
            if (m_MaterialInstance == null) { onComplete?.Invoke(); return; }

            if (m_PulseRoutine != null) StopCoroutine(m_PulseRoutine);
            m_PulseRoutine = StartCoroutine(BorderPulseRoutine(duration, color ?? new Color(0.35f, 0.85f, 1f, 1f), peakIntensity, direction, onComplete));
        }

        private IEnumerator BorderPulseRoutine(float duration, Color pulseColor, float peakIntensity, UIBorderDirection direction, Action onComplete)
        {
            m_MaterialInstance.SetColor(PropBorderColor, pulseColor);
            m_MaterialInstance.SetFloat(PropBorderPulseIntensity, peakIntensity);
            m_MaterialInstance.SetFloat(PropBorderWidth, 2.5f);
            m_MaterialInstance.SetFloat(PropPulseWidth, 0.26f);
            m_MaterialInstance.SetFloat(PropElecNoiseAmount, 0.70f);
            m_MaterialInstance.SetFloat(PropEnergyActive, 0f);

            int dirMode = 0;
            float clockwise = 1f;
            switch (direction)
            {
                case UIBorderDirection.PerimeterCounterClockwise:
                    dirMode = 0; clockwise = -1f; break;
                case UIBorderDirection.LeftToRight:
                    dirMode = 1; break;
                case UIBorderDirection.RightToLeft:
                    dirMode = 2; break;
                case UIBorderDirection.TopToBottom:
                    dirMode = 3; break;
                case UIBorderDirection.BottomToTop:
                    dirMode = 4; break;
                default:
                    dirMode = 0; clockwise = 1f; break;
            }

            m_MaterialInstance.SetFloat(PropBorderDirectionMode, (float)dirMode);
            m_MaterialInstance.SetFloat(PropClockwise, clockwise);

            float elapsed = 0f;
            while (elapsed < duration && m_MaterialInstance != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                m_MaterialInstance.SetFloat(PropPulseProgress, t);
                float baseIntensity = Mathf.Lerp(1.2f, 0.2f, t);
                m_MaterialInstance.SetFloat(PropBorderIntensity, baseIntensity);
                yield return null;
            }

            if (m_MaterialInstance != null)
            {
                m_MaterialInstance.SetFloat(PropPulseProgress, 0f);
                m_MaterialInstance.SetFloat(PropBorderIntensity, 0f);
            }

            m_PulseRoutine = null;
            onComplete?.Invoke();
        }

        /// <summary>
        /// Surface Activation / Scan Sweep across element artwork.
        /// </summary>
        public void PlayActivationSweep(float duration = 0.32f, Color? color = null, float angle = 45f, Action onComplete = null)
        {
            EnsureMaterialInstance();
            if (m_MaterialInstance == null) { onComplete?.Invoke(); return; }

            if (m_SweepRoutine != null) StopCoroutine(m_SweepRoutine);
            m_SweepRoutine = StartCoroutine(SweepRoutine(duration, color ?? new Color(0.85f, 0.96f, 1f, 1f), angle, onComplete));
        }

        private IEnumerator SweepRoutine(float duration, Color sweepColor, float angle, Action onComplete)
        {
            m_MaterialInstance.SetColor(PropSweepColor, sweepColor);
            m_MaterialInstance.SetFloat(PropSweepAngle, angle);
            m_MaterialInstance.SetFloat(PropSweepWidth, 0.14f);

            float elapsed = 0f;
            while (elapsed < duration && m_MaterialInstance != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                float progress = Mathf.Lerp(-0.35f, 1.35f, t);
                m_MaterialInstance.SetFloat(PropSweepProgress, progress);

                float intensity = Mathf.Sin(t * Mathf.PI) * 2.8f;
                m_MaterialInstance.SetFloat(PropSweepIntensity, intensity);
                yield return null;
            }

            if (m_MaterialInstance != null)
            {
                m_MaterialInstance.SetFloat(PropSweepIntensity, 0f);
                m_MaterialInstance.SetFloat(PropSweepProgress, -0.4f);
            }

            m_SweepRoutine = null;
            onComplete?.Invoke();
        }

        /// <summary>
        /// Digital Glitch & RGB Split burst (0.05-0.20s).
        /// </summary>
        public void TriggerDigitalGlitch(float duration = 0.12f, float intensity = 1.0f, float rgbOffset = 0.02f)
        {
            EnsureMaterialInstance();
            if (m_MaterialInstance == null) return;

            if (m_GlitchRoutine != null) StopCoroutine(m_GlitchRoutine);
            m_GlitchRoutine = StartCoroutine(GlitchRoutine(duration, intensity, rgbOffset));
        }

        private IEnumerator GlitchRoutine(float duration, float intensity, float rgbOffset)
        {
            m_MaterialInstance.SetFloat(PropGlitchSlices, 32f);
            m_MaterialInstance.SetFloat(PropRgbOffset, rgbOffset);

            float elapsed = 0f;
            while (elapsed < duration && m_MaterialInstance != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float curIntensity = Mathf.Sin(t * Mathf.PI) * intensity;
                m_MaterialInstance.SetFloat(PropGlitchIntensity, curIntensity);
                yield return null;
            }

            if (m_MaterialInstance != null)
            {
                m_MaterialInstance.SetFloat(PropGlitchIntensity, 0f);
            }
            m_GlitchRoutine = null;
        }

        /// <summary>
        /// Impact Flash on the surface.
        /// </summary>
        public void TriggerImpactFlash(float duration = 0.08f, float intensity = 1.8f, Color? color = null)
        {
            EnsureMaterialInstance();
            if (m_MaterialInstance == null) return;

            if (m_FlashRoutine != null) StopCoroutine(m_FlashRoutine);
            m_FlashRoutine = StartCoroutine(ImpactFlashRoutine(duration, intensity, color ?? Color.white));
        }

        private IEnumerator ImpactFlashRoutine(float duration, float intensity, Color flashColor)
        {
            m_MaterialInstance.SetColor(PropFlashColor, flashColor);

            float elapsed = 0f;
            while (elapsed < duration && m_MaterialInstance != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float curIntensity = Mathf.Lerp(intensity, 0f, t);
                m_MaterialInstance.SetFloat(PropFlashIntensity, curIntensity);
                yield return null;
            }

            if (m_MaterialInstance != null)
            {
                m_MaterialInstance.SetFloat(PropFlashIntensity, 0f);
            }
            m_FlashRoutine = null;
        }

        /// <summary>
        /// Holographic scanline flicker presence.
        /// </summary>
        public void SetHolographicFlicker(float amount)
        {
            EnsureMaterialInstance();
            if (m_MaterialInstance != null)
            {
                m_MaterialInstance.SetFloat(PropHoloFlicker, Mathf.Clamp01(amount));
            }
        }

        /// <summary>
        /// Continuous subtle idle breathing for living interface presence.
        /// </summary>
        public void SetIdleBreathing(bool enabled, float speed = 3.2f, float maxIntensity = 0.20f)
        {
            if (m_IdleRoutine != null)
            {
                StopCoroutine(m_IdleRoutine);
                m_IdleRoutine = null;
            }

            if (enabled)
            {
                EnsureMaterialInstance();
                if (m_MaterialInstance != null)
                {
                    m_IdleRoutine = StartCoroutine(IdleBreathingRoutine(speed, maxIntensity));
                }
            }
            else if (m_MaterialInstance != null)
            {
                m_MaterialInstance.SetFloat(PropBorderIntensity, 0f);
            }
        }

        private IEnumerator IdleBreathingRoutine(float speed, float maxIntensity)
        {
            EnsureMaterialInstance();
            if (m_MaterialInstance == null) yield break;

            m_MaterialInstance.SetFloat(PropBorderWidth, 2.0f);
            m_MaterialInstance.SetColor(PropBorderColor, BorderColor);

            while (true)
            {
                float t = Time.unscaledTime * speed;
                float intensity = (Mathf.Sin(t) * 0.5f + 0.5f) * maxIntensity;
                if (m_MaterialInstance != null)
                {
                    m_MaterialInstance.SetFloat(PropBorderIntensity, intensity);
                }
                yield return null;
            }
        }

        /// <summary>
        /// Deterministically resets all transient shader properties and cancels all active routines.
        /// </summary>
        public void ResetToIdle()
        {
            StopAllCoroutines();
            m_SweepRoutine = null;
            m_PulseRoutine = null;
            m_FlashRoutine = null;
            m_GlitchRoutine = null;
            m_RadialRoutine = null;
            m_ShockwaveRoutine = null;
            m_ReconstructRoutine = null;
            m_DissolveRoutine = null;
            m_NoiseRevealRoutine = null;
            m_ScanRoutine = null;
            m_DataStreamRoutine = null;
            m_SegmentFlowRoutine = null;
            m_IdleRoutine = null;

            if (m_MaterialInstance != null)
            {
                m_MaterialInstance.SetFloat(PropBorderIntensity, 0f);
                m_MaterialInstance.SetFloat(PropPulseProgress, 0f);
                m_MaterialInstance.SetFloat(PropEnergyActive, 0f);
                m_MaterialInstance.SetFloat(PropSweepIntensity, 0f);
                m_MaterialInstance.SetFloat(PropSweepProgress, -0.4f);
                m_MaterialInstance.SetFloat(PropGlitchIntensity, 0f);
                m_MaterialInstance.SetFloat(PropShockwaveProgress, 0f);
                m_MaterialInstance.SetFloat(PropShockwaveStrength, 0f);
                m_MaterialInstance.SetFloat(PropRadialPulseIntensity, 0f);
                m_MaterialInstance.SetFloat(PropRadialPulseRadius, 0f);
                m_MaterialInstance.SetFloat(PropRadialPulseDistort, 0f);
                m_MaterialInstance.SetFloat(PropReconstructProgress, 1.0f);
                m_MaterialInstance.SetFloat(PropDissolveProgress, 0f);
                m_MaterialInstance.SetFloat(PropNoiseRevealProgress, 1.0f);
                m_MaterialInstance.SetFloat(PropScanDistortIntensity, 0f);
                m_MaterialInstance.SetFloat(PropDataStreamIntensity, 0f);
                m_MaterialInstance.SetFloat(PropSegmentFlowIntensity, 0f);
                m_MaterialInstance.SetFloat(PropFlashIntensity, 0f);
                m_MaterialInstance.SetFloat(PropHoloFlicker, 0f);
                m_MaterialInstance.SetFloat(PropSurfaceBrightness, 0f);
            }
        }

        #endregion

        #region Convenience Aliases & Properties

        public Color BorderColor
        {
            get => m_MaterialInstance != null ? m_MaterialInstance.GetColor(PropBorderColor) : Color.white;
            set
            {
                EnsureMaterialInstance();
                if (m_MaterialInstance != null) m_MaterialInstance.SetColor(PropBorderColor, value);
            }
        }

        public float BorderSpeed
        {
            get => m_MaterialInstance != null ? m_MaterialInstance.GetFloat(PropFlowSpeed) : 1f;
            set
            {
                EnsureMaterialInstance();
                if (m_MaterialInstance != null) m_MaterialInstance.SetFloat(PropFlowSpeed, value);
            }
        }

        public float BorderWidth
        {
            get => m_MaterialInstance != null ? m_MaterialInstance.GetFloat(PropBorderWidth) : 2f;
            set
            {
                EnsureMaterialInstance();
                if (m_MaterialInstance != null) m_MaterialInstance.SetFloat(PropBorderWidth, value);
            }
        }

        public float BorderIntensity
        {
            get => m_MaterialInstance != null ? m_MaterialInstance.GetFloat(PropBorderIntensity) : 0f;
            set
            {
                EnsureMaterialInstance();
                if (m_MaterialInstance != null) m_MaterialInstance.SetFloat(PropBorderIntensity, value);
            }
        }

        public float BorderSoftness
        {
            get => m_MaterialInstance != null ? m_MaterialInstance.GetFloat(PropBorderSoftness) : 0.05f;
            set
            {
                EnsureMaterialInstance();
                if (m_MaterialInstance != null) m_MaterialInstance.SetFloat(PropBorderSoftness, value);
            }
        }

        public void TriggerImpactFlash(float duration, Color color, float intensity)
        {
            TriggerImpactFlash(duration, intensity, color);
        }

        public void TriggerRadialPulse(float duration, Color color, float maxRadius, float intensity, Action onComplete = null)
        {
            TriggerRadialPulse(duration, color, new Vector2(0.5f, 0.5f), maxRadius, intensity, 0.02f, onComplete);
        }

        public void TriggerGlitch(float duration, float intensity, Action onComplete = null)
        {
            TriggerDigitalGlitch(duration, intensity);
            onComplete?.Invoke();
        }

        public void TriggerBorderPulse(float duration, Color color, float peakIntensity, Action onComplete = null)
        {
            TriggerBorderPulse(duration, color, peakIntensity, UIBorderDirection.PerimeterClockwise, onComplete);
        }

        public void TriggerBorderPulse(float duration, Color color, float peakIntensity, bool clockwise, Action onComplete = null)
        {
            TriggerBorderPulse(duration, color, peakIntensity, clockwise ? UIBorderDirection.PerimeterClockwise : UIBorderDirection.PerimeterCounterClockwise, onComplete);
        }

        public void TriggerBorderPulse(float duration, Color color, bool clockwise = true, Action onComplete = null)
        {
            TriggerBorderPulse(duration, color, 2.5f, clockwise ? UIBorderDirection.PerimeterClockwise : UIBorderDirection.PerimeterCounterClockwise, onComplete);
        }

        public void StartIdleBreathing(Color color)
        {
            BorderColor = color;
            SetIdleBreathing(true, 3.2f, 0.20f);
        }

        public void StartIdleBreathing(float maxIntensity = 0.20f, float speed = 3.2f)
        {
            SetIdleBreathing(true, speed, maxIntensity);
        }

        public void StopIdleBreathing()
        {
            SetIdleBreathing(false);
        }

        public void TriggerPowerSweep(float duration = 0.32f, float width = 0.14f, float angle = 45f, Color? color = null)
        {
            PlayActivationSweep(duration, color, angle);
        }

        public void SetBorderEnergyActive(bool active, Color? color = null, float speed = 1.2f, float intensity = 1.0f, bool clockwise = true)
        {
            EnsureMaterialInstance();
            if (m_MaterialInstance == null) return;

            m_MaterialInstance.SetFloat(PropEnergyActive, active ? 1f : 0f);
            if (active)
            {
                if (color.HasValue) m_MaterialInstance.SetColor(PropBorderColor, color.Value);
                m_MaterialInstance.SetFloat(PropFlowSpeed, speed);
                m_MaterialInstance.SetFloat(PropClockwise, clockwise ? 1f : -1f);
                m_MaterialInstance.SetFloat(PropBorderIntensity, intensity);
            }
            else
            {
                m_MaterialInstance.SetFloat(PropBorderIntensity, 0f);
            }
        }

        public void StartContinuousEnergyFlow(Color? color = null, float speed = 1.2f, float intensity = 1.0f)
        {
            SetBorderEnergyActive(true, color, speed, intensity);
        }

        public void StopContinuousEnergyFlow()
        {
            SetBorderEnergyActive(false);
        }

        #endregion
    }
}
