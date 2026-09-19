using UnityEngine;
using UnityEngine.UI;

namespace MainGame.UI.RoboticEffects
{
    /// <summary>
    /// Injects normalized RectTransform coordinates (0..1) and pixel dimensions into UIVertex.uv1.
    /// This enables uGUI shaders to calculate pixel-accurate edge outlines, inner shadows, and sweeps
    /// that are 100% immune to 9-slicing and sprite atlas packing.
    /// Zero runtime GC allocations; only recalculates on geometry rebuild.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Graphic))]
    public class RoboticUIMeshModifier : BaseMeshEffect
    {
        private Graphic m_TargetGraphic;
        private RectTransform m_RectTransform;

        protected override void Awake()
        {
            base.Awake();
            m_TargetGraphic = GetComponent<Graphic>();
            m_RectTransform = GetComponent<RectTransform>();
        }

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

                // uv1.xy = normalized [0..1] across panel, uv1.zw = pixel size
                vert.uv1 = new Vector4(normX, normY, width, height);

                vh.SetUIVertex(vert, i);
            }
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            if (m_TargetGraphic != null)
            {
                m_TargetGraphic.SetVerticesDirty();
            }
        }
    }
}
