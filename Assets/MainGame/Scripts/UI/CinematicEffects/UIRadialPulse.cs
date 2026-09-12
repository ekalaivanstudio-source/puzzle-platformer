using System;
using UnityEngine;
using UnityEngine.UI;

namespace MainGame.UI.CinematicEffects
{
    /// <summary>
    /// Modular controller for concentric radial energy pulses and shockwaves originating from a focal center (e.g. RETRY core).
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    [DisallowMultipleComponent]
    public class UIRadialPulse : MonoBehaviour
    {
        [Header("Radial Pulse Configuration")]
        [SerializeField] private Color m_PulseColor = new Color(0.35f, 0.85f, 1.0f, 1.0f);
        [SerializeField] private Vector2 m_OriginUV = new Vector2(0.5f, 0.5f);
        [SerializeField] private float m_MaxRadius = 1.4f;
        [SerializeField] private float m_Intensity = 3.0f;
        [SerializeField] private float m_Distortion = 0.04f;

        private CinematicUIEffect m_Effect;

        public CinematicUIEffect Effect
        {
            get
            {
                if (m_Effect == null) m_Effect = GetComponent<CinematicUIEffect>() ?? gameObject.AddComponent<CinematicUIEffect>();
                return m_Effect;
            }
        }

        public void PlayPulse(float duration = 0.35f, Color? color = null, Vector2? centerUV = null, float? maxRadius = null, float? intensity = null, Action onComplete = null)
        {
            Color c = color ?? m_PulseColor;
            Vector2 uv = centerUV ?? m_OriginUV;
            float r = maxRadius ?? m_MaxRadius;
            float i = intensity ?? m_Intensity;

            Effect.TriggerRadialPulse(duration, c, uv, r, i, m_Distortion, onComplete);
        }

        public void ResetToIdle()
        {
            Effect.ResetToIdle();
        }
    }
}
