using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MainGame.UI.RoboticEffects
{
    /// <summary>
    /// Robotic panel effect controller attached to main UI panel backgrounds:
    /// - Manages edge illumination, inner shadow depth, and power-up sequences (0.28s - 0.35s).
    /// - Adds procedural corner mechanical brackets ([+], [ ]).
    /// - Zero continuous distractions: settles to crisp resting visuals with subtle ambient edge glow.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Graphic))]
    public class RoboticUIPanelEffect : MonoBehaviour
    {
        [Header("Robotic Edge Styling")]
        [SerializeField] private Color m_EdgeColor = new Color(0.35f, 0.78f, 1.0f, 1.0f);
        [SerializeField] private float m_EdgeIntensity = 1.15f;
        [SerializeField] private float m_EdgeWidth = 2.0f;
        [SerializeField] private float m_InnerShadowDarkness = 0.20f;
        [SerializeField] private float m_InnerShadowWidth = 24.0f;

        [Header("Corner Mechanical Brackets")]
        [SerializeField] private bool m_EnableCornerBrackets = true;
        [SerializeField] private Color m_BracketColor = new Color(0.35f, 0.78f, 1.0f, 0.50f);

        [Header("Selective Features")]
        [Tooltip("Only enabled for specific inspection screens (e.g. Collection). Always false for Level Selection.")]
        [SerializeField] private bool m_EnableScanShimmer = false;
        [SerializeField] private float m_ScanIntensity = 0.25f;

        private Graphic m_Graphic;
        private Material m_InstanceMaterial;
        private RoboticUIMeshModifier m_MeshModifier;
        private RectTransform[] m_CornerBrackets;
        private Coroutine m_ActiveAnimationRoutine;

        private static readonly int PropEdgeColor = Shader.PropertyToID("_EdgeColor");
        private static readonly int PropEdgeIntensity = Shader.PropertyToID("_EdgeIntensity");
        private static readonly int PropEdgeWidth = Shader.PropertyToID("_EdgeWidth");
        private static readonly int PropInnerShadowDarkness = Shader.PropertyToID("_InnerShadowDarkness");
        private static readonly int PropInnerShadowWidth = Shader.PropertyToID("_InnerShadowWidth");
        private static readonly int PropActivation = Shader.PropertyToID("_Activation");
        private static readonly int PropEdgeSweep = Shader.PropertyToID("_EdgeSweepProgress");
        private static readonly int PropScanIntensity = Shader.PropertyToID("_ScanIntensity");
        private static readonly int PropGlitchAmount = Shader.PropertyToID("_GlitchAmount");

        public Color EdgeColor
        {
            get => m_EdgeColor;
            set
            {
                m_EdgeColor = value;
                if (m_InstanceMaterial != null) m_InstanceMaterial.SetColor(PropEdgeColor, value);
            }
        }

        private void Awake()
        {
            InitializeMaterial();
            if (m_EnableCornerBrackets)
            {
                CreateCornerBrackets();
            }
        }

        private void InitializeMaterial()
        {
            m_Graphic = GetComponent<Graphic>();
            if (m_Graphic == null) return;

            // Ensure mesh modifier is present to feed uv1 (normalized coordinates)
            m_MeshModifier = GetComponent<RoboticUIMeshModifier>();
            if (m_MeshModifier == null)
            {
                m_MeshModifier = gameObject.AddComponent<RoboticUIMeshModifier>();
            }

            Shader shader = Shader.Find("MainGame/UI/RoboticPanel");
            if (shader != null)
            {
                m_InstanceMaterial = new Material(shader);
                ApplyMaterialProperties();
                m_Graphic.material = m_InstanceMaterial;
            }
        }

        private void ApplyMaterialProperties()
        {
            if (m_InstanceMaterial == null) return;

            m_InstanceMaterial.SetColor(PropEdgeColor, m_EdgeColor);
            m_InstanceMaterial.SetFloat(PropEdgeIntensity, m_EdgeIntensity);
            m_InstanceMaterial.SetFloat(PropEdgeWidth, m_EdgeWidth);
            m_InstanceMaterial.SetFloat(PropInnerShadowDarkness, m_InnerShadowDarkness);
            m_InstanceMaterial.SetFloat(PropInnerShadowWidth, m_InnerShadowWidth);
            m_InstanceMaterial.SetFloat(PropScanIntensity, m_EnableScanShimmer ? m_ScanIntensity : 0f);
            m_InstanceMaterial.SetFloat(PropActivation, 1f);
            m_InstanceMaterial.SetFloat(PropEdgeSweep, -0.5f);
            m_InstanceMaterial.SetFloat(PropGlitchAmount, 0f);
        }

        private void CreateCornerBrackets()
        {
            if (m_CornerBrackets != null && m_CornerBrackets.Length == 4) return;

            // Create procedural 10x10 pixel L-bracket sprite
            Texture2D tex = new Texture2D(10, 10, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            Color transparent = new Color(0, 0, 0, 0);
            for (int x = 0; x < 10; x++)
                for (int y = 0; y < 10; y++)
                    tex.SetPixel(x, y, transparent);

            // Draw top-left L bracket (2px thick)
            for (int x = 0; x < 10; x++)
            {
                tex.SetPixel(x, 9, Color.white);
                tex.SetPixel(x, 8, Color.white);
            }
            for (int y = 0; y < 10; y++)
            {
                tex.SetPixel(0, y, Color.white);
                tex.SetPixel(1, y, Color.white);
            }
            tex.Apply();

            Sprite bracketSprite = Sprite.Create(tex, new Rect(0, 0, 10, 10), new Vector2(0.5f, 0.5f), 1f);

            m_CornerBrackets = new RectTransform[4];
            Vector2[] anchors = new Vector2[]
            {
                new Vector2(0f, 1f), // Top-Left
                new Vector2(1f, 1f), // Top-Right
                new Vector2(1f, 0f), // Bottom-Right
                new Vector2(0f, 0f)  // Bottom-Left
            };
            Vector3[] rotations = new Vector3[]
            {
                Vector3.zero,
                new Vector3(0f, 0f, -90f),
                new Vector3(0f, 0f, -180f),
                new Vector3(0f, 0f, 90f)
            };
            Vector2[] offsets = new Vector2[]
            {
                new Vector2(16f, -16f),
                new Vector2(-16f, -16f),
                new Vector2(-16f, 16f),
                new Vector2(16f, 16f)
            };

            for (int i = 0; i < 4; i++)
            {
                GameObject bObj = new GameObject($"Bracket_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                bObj.transform.SetParent(transform, false);

                RectTransform rt = bObj.GetComponent<RectTransform>();
                rt.anchorMin = anchors[i];
                rt.anchorMax = anchors[i];
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(14f, 14f);
                rt.anchoredPosition = offsets[i];
                rt.localEulerAngles = rotations[i];

                Image img = bObj.GetComponent<Image>();
                img.sprite = bracketSprite;
                img.color = m_BracketColor;
                img.raycastTarget = false;

                m_CornerBrackets[i] = rt;
            }
        }

        /// <summary>
        /// Snappy Robotic Power-Up Sequence (0.28s - 0.35s):
        /// 1. Edge light sweeps across perimeter (0..1)
        /// 2. Digital signal flicker (1-2 quick frames)
        /// 3. Settles into crisp resting edge illumination
        /// </summary>
        public void PlayPowerUp(float duration = 0.28f, Action onComplete = null)
        {
            if (m_InstanceMaterial == null)
            {
                onComplete?.Invoke();
                return;
            }

            if (m_ActiveAnimationRoutine != null)
            {
                StopCoroutine(m_ActiveAnimationRoutine);
            }

            m_ActiveAnimationRoutine = StartCoroutine(PowerUpRoutine(duration, onComplete));
        }

        private IEnumerator PowerUpRoutine(float duration, Action onComplete)
        {
            m_InstanceMaterial.SetFloat(PropActivation, 0f);
            m_InstanceMaterial.SetFloat(PropEdgeSweep, -0.4f);

            float sweepDuration = duration * 0.65f;
            float flickerDuration = duration * 0.35f;

            // Phase 1: Edge light activation sweep
            float elapsed = 0f;
            while (elapsed < sweepDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / sweepDuration);

                m_InstanceMaterial.SetFloat(PropActivation, t);
                m_InstanceMaterial.SetFloat(PropEdgeSweep, Mathf.Lerp(-0.4f, 1.3f, t));
                yield return null;
            }

            // Phase 2: Micro digital signal flicker (1-2 quick pulses)
            float flickerElapsed = 0f;
            while (flickerElapsed < flickerDuration)
            {
                flickerElapsed += Time.unscaledDeltaTime;
                float ft = Mathf.Clamp01(flickerElapsed / flickerDuration);

                // Subtle brightness pulse: 1.25 -> 0.92 -> 1.05 -> 1.00
                float flicker = 1.0f + Mathf.Sin(ft * Mathf.PI * 4f) * 0.22f * (1f - ft);
                m_InstanceMaterial.SetFloat(PropEdgeIntensity, m_EdgeIntensity * flicker);
                yield return null;
            }

            // Phase 3: Settle into resting state
            m_InstanceMaterial.SetFloat(PropActivation, 1f);
            m_InstanceMaterial.SetFloat(PropEdgeIntensity, m_EdgeIntensity);
            m_InstanceMaterial.SetFloat(PropEdgeSweep, -0.5f);

            m_ActiveAnimationRoutine = null;
            onComplete?.Invoke();
        }

        /// <summary>
        /// Reverse power-down on panel exit (0.16s).
        /// </summary>
        public void PlayPowerDown(float duration = 0.16f, Action onComplete = null)
        {
            if (m_InstanceMaterial == null)
            {
                onComplete?.Invoke();
                return;
            }

            if (m_ActiveAnimationRoutine != null)
            {
                StopCoroutine(m_ActiveAnimationRoutine);
            }

            m_ActiveAnimationRoutine = StartCoroutine(PowerDownRoutine(duration, onComplete));
        }

        private IEnumerator PowerDownRoutine(float duration, Action onComplete)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                m_InstanceMaterial.SetFloat(PropActivation, 1f - t);
                yield return null;
            }

            m_InstanceMaterial.SetFloat(PropActivation, 0f);
            m_ActiveAnimationRoutine = null;
            onComplete?.Invoke();
        }

        /// <summary>
        /// Momentary subtle signal jitter (0.08s - 0.12s) used for alert moments or logo stabilization.
        /// </summary>
        public void TriggerGlitch(float duration = 0.10f)
        {
            if (m_InstanceMaterial == null) return;
            StartCoroutine(GlitchRoutine(duration));
        }

        private IEnumerator GlitchRoutine(float duration)
        {
            m_InstanceMaterial.SetFloat(PropGlitchAmount, 1f);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                m_InstanceMaterial.SetFloat(PropGlitchAmount, 1f - t);
                yield return null;
            }

            m_InstanceMaterial.SetFloat(PropGlitchAmount, 0f);
        }

        private void OnDestroy()
        {
            if (m_InstanceMaterial != null)
            {
                Destroy(m_InstanceMaterial);
            }
        }
    }
}
