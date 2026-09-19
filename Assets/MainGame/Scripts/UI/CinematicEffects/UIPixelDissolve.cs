using System;
using UnityEngine;
using UnityEngine.UI;

namespace MainGame.UI.CinematicEffects
{
    /// <summary>
    /// Modular controller for surface pixel dissolves on UI exits.
    /// Dissolves the Graphic surface using blocky dissolve noise while emitting scattering pixel fragments.
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    [DisallowMultipleComponent]
    public class UIPixelDissolve : MonoBehaviour
    {
        [Header("Dissolve Configuration")]
        [SerializeField] private Color m_EdgeColor = new Color(0.35f, 0.90f, 1.0f, 1.0f);
        [SerializeField] private float m_Blockiness = 24f;
        [SerializeField] private int m_ScatterShardCount = 14;

        private CinematicUIEffect m_Effect;
        private RectTransform m_RectTransform;

        public CinematicUIEffect Effect
        {
            get
            {
                if (m_Effect == null) m_Effect = GetComponent<CinematicUIEffect>() ?? gameObject.AddComponent<CinematicUIEffect>();
                return m_Effect;
            }
        }

        private void Awake()
        {
            m_RectTransform = GetComponent<RectTransform>();
        }

        public void PlayDissolve(float duration = 0.28f, Color? edgeColor = null, float? blockiness = null, int? shardCount = null, Action onComplete = null)
        {
            Color c = edgeColor ?? m_EdgeColor;
            float b = blockiness ?? m_Blockiness;
            int shards = shardCount ?? m_ScatterShardCount;

            if (shards > 0 && CinematicUIParticleSystem.Instance != null && m_RectTransform != null)
            {
                CinematicUIParticleSystem.Instance.SpawnPixelDissolveShards(m_RectTransform, transform.parent, c, shards);
            }

            Effect.PlayPixelDissolve(duration, c, b, onComplete);
        }

        public void SetDissolveProgress(float progress, Color? edgeColor = null)
        {
            Effect.SetPixelDissolve(progress, edgeColor ?? m_EdgeColor);
        }

        public void ResetToIdle()
        {
            Effect.SetPixelDissolve(0f);
            Effect.ResetToIdle();
        }
    }
}
