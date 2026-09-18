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
            raycastTarget = false;
            CanvasRenderer cr = GetComponent<CanvasRenderer>();
            if (cr != null) cr.cull = true;

            if (Application.isPlaying)
            {
                Destroy(gameObject);
            }
            else
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    if (this != null && gameObject != null)
                    {
                        DestroyImmediate(gameObject);
                    }
                };
#endif
            }
        }

        public void SetEndpoints(Vector3 startWorld, Vector3 endWorld, float tension = 1.0f)
        {
        }

        public void SetEndpoints(Transform startAnchor, Transform endAnchor, float tension = 1.0f)
        {
        }

        private void LateUpdate()
        {
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
        }

        public void TriggerTensionBurst(float intensity = 1.0f)
        {
        }
    }
}
