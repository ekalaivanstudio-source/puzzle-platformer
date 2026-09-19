using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MainGame.UI.CinematicEffects
{
    /// <summary>
    /// Component driving the CinematicRoute shader ("MainGame/UI/CinematicRoute") on level path segments.
    /// Simulates a directional traveling electrical current (Node A -> Node B) with a bright head,
    /// fading tail, and quantized pixel-art steps, rather than a static filled bar.
    /// </summary>
    [RequireComponent(typeof(Image))]
    [DisallowMultipleComponent]
    public class UIPathRouteEffect : MonoBehaviour
    {
        private static readonly int PropPulseProgress = Shader.PropertyToID("_PulseProgress");
        private static readonly int PropPulseLength = Shader.PropertyToID("_PulseLength");
        private static readonly int PropPulseHeadWidth = Shader.PropertyToID("_PulseHeadWidth");
        private static readonly int PropHeadColor = Shader.PropertyToID("_HeadColor");
        private static readonly int PropTailColor = Shader.PropertyToID("_TailColor");
        private static readonly int PropBaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int PropPulseIntensity = Shader.PropertyToID("_PulseIntensity");
        private static readonly int PropIsActive = Shader.PropertyToID("_IsActive");
        private static readonly int PropQuantizeSteps = Shader.PropertyToID("_QuantizeSteps");

        [Header("Route Visuals")]
        [SerializeField] private Color m_HeadColor = new Color(1.0f, 1.0f, 1.0f, 1.0f);
        [SerializeField] private Color m_TailColor = new Color(1.0f, 0.85f, 0.2f, 1.0f);
        [SerializeField] private Color m_BaseColor = new Color(0.2f, 0.25f, 0.35f, 0.5f);
        [SerializeField] private float m_PulseLength = 0.35f;
        [SerializeField] private float m_PulseIntensity = 2.2f;
        [SerializeField] private float m_QuantizeSteps = 8.0f;

        private Image m_Image;
        private Material m_RuntimeMaterial;
        private Shader m_RouteShader;
        private Coroutine m_FlowRoutine;
        private bool m_IsActive = false;

        public bool IsActive => m_IsActive;

        private void Awake()
        {
            EnsureMaterial();
        }

        private void EnsureMaterial()
        {
            if (m_Image == null) m_Image = GetComponent<Image>();
            if (m_RouteShader == null) m_RouteShader = Shader.Find("MainGame/UI/CinematicRoute");

            if (m_RuntimeMaterial == null && m_RouteShader != null && m_Image != null)
            {
                m_RuntimeMaterial = new Material(m_RouteShader)
                {
                    name = $"{gameObject.name}_CinematicRouteMat"
                };
                m_Image.material = m_RuntimeMaterial;
                ApplyDefaultShaderProperties();
            }
        }

        private void ApplyDefaultShaderProperties()
        {
            if (m_RuntimeMaterial == null) return;

            m_RuntimeMaterial.SetColor(PropHeadColor, m_HeadColor);
            m_RuntimeMaterial.SetColor(PropTailColor, m_TailColor);
            m_RuntimeMaterial.SetColor(PropBaseColor, m_BaseColor);
            m_RuntimeMaterial.SetFloat(PropPulseLength, m_PulseLength);
            m_RuntimeMaterial.SetFloat(PropPulseIntensity, m_PulseIntensity);
            m_RuntimeMaterial.SetFloat(PropQuantizeSteps, m_QuantizeSteps);
            m_RuntimeMaterial.SetFloat(PropIsActive, m_IsActive ? 1.0f : 0.0f);
            m_RuntimeMaterial.SetFloat(PropPulseProgress, 0.0f);
        }

        public void SetRouteActive(bool active)
        {
            m_IsActive = active;
            EnsureMaterial();
            if (m_RuntimeMaterial != null)
            {
                m_RuntimeMaterial.SetFloat(PropIsActive, active ? 1.0f : 0.0f);
            }
        }

        /// <summary>
        /// Animates a single traveling pulse from start node (0) to end node (1).
        /// When progress reaches 1.0, invokes onReachTarget callback (e.g. to trigger node ignition).
        /// </summary>
        public void PlayTravelPulse(float duration, Action onReachTarget = null, Action onComplete = null)
        {
            EnsureMaterial();
            SetRouteActive(true);

            if (m_FlowRoutine != null)
            {
                StopCoroutine(m_FlowRoutine);
            }

            m_FlowRoutine = StartCoroutine(TravelPulseRoutine(duration, onReachTarget, onComplete));
        }

        private IEnumerator TravelPulseRoutine(float duration, Action onReachTarget, Action onComplete)
        {
            float elapsed = 0f;
            float totalTarget = 1.0f + m_PulseLength;
            bool reachedTarget = false;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float progress = t * totalTarget;

                if (m_RuntimeMaterial != null)
                {
                    m_RuntimeMaterial.SetFloat(PropPulseProgress, progress);
                }

                if (!reachedTarget && progress >= 1.0f)
                {
                    reachedTarget = true;
                    onReachTarget?.Invoke();
                }

                yield return null;
            }

            if (!reachedTarget)
            {
                onReachTarget?.Invoke();
            }

            if (m_RuntimeMaterial != null)
            {
                m_RuntimeMaterial.SetFloat(PropPulseProgress, totalTarget);
            }

            m_FlowRoutine = null;
            onComplete?.Invoke();
        }

        /// <summary>
        /// Starts continuous looping energy pulse along this route segment.
        /// </summary>
        public void StartContinuousFlow(float cycleDuration = 1.2f, float delay = 0f)
        {
            EnsureMaterial();
            SetRouteActive(true);

            if (m_FlowRoutine != null)
            {
                StopCoroutine(m_FlowRoutine);
            }

            m_FlowRoutine = StartCoroutine(ContinuousFlowRoutine(cycleDuration, delay));
        }

        private IEnumerator ContinuousFlowRoutine(float cycleDuration, float initialDelay)
        {
            if (initialDelay > 0f)
            {
                yield return new WaitForSecondsRealtime(initialDelay);
            }

            float totalTarget = 1.0f + m_PulseLength;

            while (true)
            {
                float elapsed = 0f;
                while (elapsed < cycleDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / cycleDuration);
                    float progress = t * totalTarget;

                    if (m_RuntimeMaterial != null)
                    {
                        m_RuntimeMaterial.SetFloat(PropPulseProgress, progress);
                    }

                    yield return null;
                }

                // Short rest between pulses
                yield return new WaitForSecondsRealtime(0.25f);
            }
        }

        public void StopFlow()
        {
            if (m_FlowRoutine != null)
            {
                StopCoroutine(m_FlowRoutine);
                m_FlowRoutine = null;
            }

            if (m_RuntimeMaterial != null)
            {
                m_RuntimeMaterial.SetFloat(PropPulseProgress, 0.0f);
            }
        }

        public void ResetToDefault()
        {
            StopFlow();
            SetRouteActive(false);
        }

        private void OnDisable()
        {
            StopFlow();
        }

        private void OnDestroy()
        {
            StopFlow();
            if (m_RuntimeMaterial != null)
            {
                Destroy(m_RuntimeMaterial);
                m_RuntimeMaterial = null;
            }
        }
    }
}
