using System;
using UnityEngine;
using UnityEngine.UI;

namespace MainGame.UI.CinematicEffects
{
    /// <summary>
    /// Procedural pixel-art energy string connecting the Villain's hand to the Main Menu Title.
    /// Implemented as an optimized MaskableGraphic that generates segmented digital energy cables
    /// with stepped pixel jitter, tension reaction, and zero runtime GC allocations.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    [DisallowMultipleComponent]
    public class UITitleEnergyLink : MaskableGraphic
    {
        [Header("Anchor Points")]
        [SerializeField] private Transform m_StartAnchor;
        [SerializeField] private Transform m_EndAnchor;
        [SerializeField] private Vector3 m_StartWorldPos;
        [SerializeField] private Vector3 m_EndWorldPos;
        [SerializeField] private bool m_UseExplicitWorldPositions = false;

        [Header("Pixel Energy Cable Styling")]
        [SerializeField] private float m_LineWidth = 3.2f;
        [SerializeField] private int m_SegmentCount = 12;
        [SerializeField] private Color m_CoreColor = new Color(0.92f, 0.98f, 1f, 1f);
        [SerializeField] private Color m_GlowColor = new Color(0.25f, 0.82f, 1f, 0.85f);

        [Header("Dynamic Energy Motion")]
        [SerializeField] private float m_FlickerSpeed = 16.0f;
        [SerializeField] private float m_EnergyPulseSpeed = 3.5f;
        [SerializeField] private float m_SagAmplitude = 5.0f;
        [SerializeField] private float m_JitterAmplitude = 1.4f;

        private float m_TensionFactor = 1.0f;
        private RectTransform m_RectTransform;

        public Transform StartAnchor { get => m_StartAnchor; set => m_StartAnchor = value; }
        public Transform EndAnchor { get => m_EndAnchor; set => m_EndAnchor = value; }
        public float TensionFactor { get => m_TensionFactor; set { m_TensionFactor = Mathf.Clamp(value, 0.5f, 2.5f); SetVerticesDirty(); } }

        protected override void Awake()
        {
            base.Awake();
            m_RectTransform = GetComponent<RectTransform>();
            raycastTarget = false;
            CanvasRenderer cr = GetComponent<CanvasRenderer>();
            if (cr != null) cr.cull = false;
        }

        public void SetEndpoints(Vector3 startWorld, Vector3 endWorld, float tension = 1.0f)
        {
            m_UseExplicitWorldPositions = true;
            m_StartWorldPos = startWorld;
            m_EndWorldPos = endWorld;
            m_TensionFactor = tension;
            SetVerticesDirty();
        }

        public void SetEndpoints(Transform startAnchor, Transform endAnchor, float tension = 1.0f)
        {
            m_UseExplicitWorldPositions = false;
            m_StartAnchor = startAnchor;
            m_EndAnchor = endAnchor;
            m_TensionFactor = tension;
            SetVerticesDirty();
        }

        private void LateUpdate()
        {
            // Redraw dynamically as anchors move
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            Vector3 startW = m_UseExplicitWorldPositions ? m_StartWorldPos : (m_StartAnchor != null ? m_StartAnchor.position : transform.position);
            Vector3 endW = m_UseExplicitWorldPositions ? m_EndWorldPos : (m_EndAnchor != null ? m_EndAnchor.position : transform.position + Vector3.down * 50f);

            if (m_RectTransform == null) m_RectTransform = rectTransform;

            Vector2 pStart = m_RectTransform.InverseTransformPoint(startW);
            Vector2 pEnd = m_RectTransform.InverseTransformPoint(endW);

            Vector2 diff = pEnd - pStart;
            float totalDist = diff.magnitude;
            if (totalDist < 1.0f) return;

            Vector2 dir = diff / totalDist;
            Vector2 normal = new Vector2(-dir.y, dir.x);

            int segments = Mathf.Clamp(m_SegmentCount, 4, 32);
            float step = 1.0f / segments;
            float time = Time.unscaledTime;

            // Tension modulates catenary sag (higher tension = straighter line)
            float currentSag = m_SagAmplitude / Mathf.Max(0.1f, m_TensionFactor);

            Vector2 prevPos = pStart;
            for (int i = 1; i <= segments; i++)
            {
                float t = i * step;
                Vector2 basePos = Vector2.Lerp(pStart, pEnd, t);

                // Catenary sag curve
                float sagCurve = Mathf.Sin(t * Mathf.PI);
                float sagOffset = -sagCurve * currentSag;

                // High-frequency digital energy jitter
                float jitterPhase = Mathf.Floor((time * m_FlickerSpeed) + i * 2.1f);
                float pseudoRand = Mathf.Repeat(Mathf.Sin(jitterPhase * 91.34f) * 43758.54f, 1f) - 0.5f;
                float jitter = pseudoRand * m_JitterAmplitude;

                Vector2 curPos = basePos + (normal * jitter) + new Vector2(0f, sagOffset);

                // Energy traveling wave pulse
                float pulse = Mathf.Repeat(t * 3f - time * m_EnergyPulseSpeed, 1f);
                float pulseGlow = Mathf.Pow(Mathf.Sin(pulse * Mathf.PI), 2f);
                Color segColor = Color.Lerp(m_GlowColor, m_CoreColor, pulseGlow * m_TensionFactor);

                AddLineSegment(vh, prevPos, curPos, m_LineWidth, segColor);
                prevPos = curPos;
            }
        }

        private void AddLineSegment(VertexHelper vh, Vector2 start, Vector2 end, float width, Color col)
        {
            Vector2 dir = (end - start).normalized;
            Vector2 perp = new Vector2(-dir.y, dir.x) * (width * 0.5f);

            int startIndex = vh.currentVertCount;

            UIVertex v1 = UIVertex.simpleVert;
            v1.position = start + perp;
            v1.color = col;

            UIVertex v2 = UIVertex.simpleVert;
            v2.position = start - perp;
            v2.color = col;

            UIVertex v3 = UIVertex.simpleVert;
            v3.position = end - perp;
            v3.color = col;

            UIVertex v4 = UIVertex.simpleVert;
            v4.position = end + perp;
            v4.color = col;

            vh.AddVert(v1);
            vh.AddVert(v2);
            vh.AddVert(v3);
            vh.AddVert(v4);

            vh.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
            vh.AddTriangle(startIndex + 2, startIndex + 3, startIndex);
        }
    }
}
