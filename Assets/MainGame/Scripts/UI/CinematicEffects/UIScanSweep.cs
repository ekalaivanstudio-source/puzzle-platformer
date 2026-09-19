using System;
using UnityEngine;
using UnityEngine.UI;

namespace MainGame.UI.CinematicEffects
{
    /// <summary>
    /// Modular controller for surface analysis scanner sweeps.
    /// Sweeps a horizontal refractive scanline across artwork with surface illumination boost,
    /// triggering core activation when passing the midpoint.
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    [DisallowMultipleComponent]
    public class UIScanSweep : MonoBehaviour
    {
        [Header("Scan Configuration")]
        [SerializeField] private float m_Intensity = 1.0f;
        [SerializeField] private float m_DefaultDuration = 0.28f;

        private CinematicUIEffect m_Effect;

        public CinematicUIEffect Effect
        {
            get
            {
                if (m_Effect == null) m_Effect = GetComponent<CinematicUIEffect>() ?? gameObject.AddComponent<CinematicUIEffect>();
                return m_Effect;
            }
        }

        public void PlayScan(float? duration = null, float? intensity = null, Action onCore = null, Action onComplete = null)
        {
            float d = duration ?? m_DefaultDuration;
            float i = intensity ?? m_Intensity;
            Effect.PlayCharacterScan(d, i, onCore, onComplete);
        }

        public void ResetToIdle()
        {
            Effect.ResetToIdle();
        }
    }
}
