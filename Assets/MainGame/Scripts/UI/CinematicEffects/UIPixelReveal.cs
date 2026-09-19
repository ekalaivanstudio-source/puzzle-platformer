using System;
using UnityEngine;
using UnityEngine.UI;

namespace MainGame.UI.CinematicEffects
{
    /// <summary>
    /// Modular controller for surface pixel reconstruction and procedural noise reveals.
    /// Used by entrance transitions (Collection robot portrait reconstruction, Level Selection map noise reveal).
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    [DisallowMultipleComponent]
    public class UIPixelReveal : MonoBehaviour
    {
        [Header("Reveal Configuration")]
        [SerializeField] private Color m_EdgeColor = new Color(0.35f, 0.90f, 1.0f, 1.0f);
        [SerializeField] private float m_Blockiness = 28f;
        [SerializeField] private float m_NoiseScale = 36f;

        private CinematicUIEffect m_Effect;

        public CinematicUIEffect Effect
        {
            get
            {
                if (m_Effect == null) m_Effect = GetComponent<CinematicUIEffect>() ?? gameObject.AddComponent<CinematicUIEffect>();
                return m_Effect;
            }
        }

        public void PlayPixelReconstruction(float duration = 0.35f, Color? edgeColor = null, float? blockiness = null, Action onComplete = null)
        {
            Effect.PlayPixelReconstruction(duration, edgeColor ?? m_EdgeColor, blockiness ?? m_Blockiness, onComplete);
        }

        public void PlayNoiseReveal(float duration = 0.36f, Color? edgeColor = null, float? noiseScale = null, Action onComplete = null)
        {
            Effect.PlayNoiseReveal(duration, edgeColor ?? m_EdgeColor, noiseScale ?? m_NoiseScale, onComplete);
        }

        public void ResetToIdle()
        {
            Effect.ResetToIdle();
        }
    }
}
