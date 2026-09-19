using System;
using UnityEngine;
using UnityEngine.UI;

namespace MainGame.UI.CinematicEffects
{
    /// <summary>
    /// Modular controller for kinetic shockwave distortion ripples across the UI surface.
    /// Used for hard impacts, slams, and destructive confirmations.
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    [DisallowMultipleComponent]
    public class UIShockwave : MonoBehaviour
    {
        [Header("Shockwave Configuration")]
        [SerializeField] private Vector2 m_OriginUV = new Vector2(0.5f, 0.5f);
        [SerializeField] private float m_DefaultStrength = 0.08f;
        [SerializeField] private float m_Thickness = 0.08f;

        private CinematicUIEffect m_Effect;

        public CinematicUIEffect Effect
        {
            get
            {
                if (m_Effect == null) m_Effect = GetComponent<CinematicUIEffect>() ?? gameObject.AddComponent<CinematicUIEffect>();
                return m_Effect;
            }
        }

        public void PlayShockwave(float duration = 0.26f, Vector2? originUV = null, float? strength = null, Action onComplete = null)
        {
            Vector2 uv = originUV ?? m_OriginUV;
            float s = strength ?? m_DefaultStrength;
            Effect.TriggerShockwave(duration, uv, s, m_Thickness, onComplete);
        }

        public void ResetToIdle()
        {
            Effect.ResetToIdle();
        }
    }
}
