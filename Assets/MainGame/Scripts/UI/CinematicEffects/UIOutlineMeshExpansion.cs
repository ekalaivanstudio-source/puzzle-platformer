using UnityEngine;
using UnityEngine.UI;

namespace MainGame.UI.CinematicEffects
{
    /// <summary>
    /// Expands the vertex geometry and UV coordinates of a UI graphic quad.
    /// Provides the outer pixel margin necessary for shader-driven outlines to render
    /// completely outside the original sprite silhouette without clipping or scaling the artwork.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(Graphic))]
    [ExecuteAlways]
    public class UIOutlineMeshExpansion : BaseMeshEffect
    {
        [Tooltip("Geometry padding in canvas pixels added around the sprite quad for outer outline rendering.")]
        [SerializeField] private float m_Padding = 16f;

        public float Padding
        {
            get => m_Padding;
            set
            {
                if (!Mathf.Approximately(m_Padding, value))
                {
                    m_Padding = value;
                    if (graphic != null)
                    {
                        graphic.SetVerticesDirty();
                    }
                }
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (graphic != null)
            {
                graphic.SetVerticesDirty();
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (graphic != null)
            {
                graphic.SetVerticesDirty();
            }
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            if (graphic != null)
            {
                graphic.SetVerticesDirty();
            }
        }
#endif

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0 || m_Padding <= 0.001f)
            {
                return;
            }

            UIVertex vert = new UIVertex();

            // 1. Calculate vertex and UV bounds
            float minX = float.MaxValue;
            float maxX = float.MinValue;
            float minY = float.MaxValue;
            float maxY = float.MinValue;

            float minU = float.MaxValue;
            float maxU = float.MinValue;
            float minV = float.MaxValue;
            float maxV = float.MinValue;

            int count = vh.currentVertCount;
            for (int i = 0; i < count; i++)
            {
                vh.PopulateUIVertex(ref vert, i);
                if (vert.position.x < minX) minX = vert.position.x;
                if (vert.position.x > maxX) maxX = vert.position.x;
                if (vert.position.y < minY) minY = vert.position.y;
                if (vert.position.y > maxY) maxY = vert.position.y;

                if (vert.uv0.x < minU) minU = vert.uv0.x;
                if (vert.uv0.x > maxU) maxU = vert.uv0.x;
                if (vert.uv0.y < minV) minV = vert.uv0.y;
                if (vert.uv0.y > maxV) maxV = vert.uv0.y;
            }

            float width = maxX - minX;
            float height = maxY - minY;
            if (width <= 0.001f || height <= 0.001f) return;

            float uSpan = maxU - minU;
            float vSpan = maxV - minV;

            float uPadding = (m_Padding / width) * uSpan;
            float vPadding = (m_Padding / height) * vSpan;

            // 2. Expand vertices and UVs proportionally outward
            float midX = (minX + maxX) * 0.5f;
            float midY = (minY + maxY) * 0.5f;

            for (int i = 0; i < count; i++)
            {
                vh.PopulateUIVertex(ref vert, i);

                if (vert.position.x < midX)
                {
                    vert.position.x -= m_Padding;
                    vert.uv0.x -= uPadding;
                }
                else
                {
                    vert.position.x += m_Padding;
                    vert.uv0.x += uPadding;
                }

                if (vert.position.y < midY)
                {
                    vert.position.y -= m_Padding;
                    vert.uv0.y -= vPadding;
                }
                else
                {
                    vert.position.y += m_Padding;
                    vert.uv0.y += vPadding;
                }

                vh.SetUIVertex(vert, i);
            }
        }
    }
}
