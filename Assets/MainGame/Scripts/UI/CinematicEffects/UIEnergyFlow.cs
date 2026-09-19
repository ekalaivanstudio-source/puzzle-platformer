using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MainGame.UI.CinematicEffects
{
    /// <summary>
    /// Modular controller for electrical border energy flow.
    /// Supports perimeter clockwise/counter-clockwise as well as directional sweeps
    /// (Left-to-Right, Right-to-Left, Top-to-Bottom, Bottom-to-Top).
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    [DisallowMultipleComponent]
    public class UIEnergyFlow : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private Color m_EnergyColor = new Color(0.35f, 0.85f, 1.0f, 1.0f);
        [SerializeField] private float m_Intensity = 2.0f;
        [SerializeField] private UIBorderDirection m_DefaultDirection = UIBorderDirection.PerimeterClockwise;
        [SerializeField] private bool m_ContinuousFlow = false;

        private CinematicUIEffect m_Effect;

        public CinematicUIEffect Effect
        {
            get
            {
                if (m_Effect == null) m_Effect = GetComponent<CinematicUIEffect>() ?? gameObject.AddComponent<CinematicUIEffect>();
                return m_Effect;
            }
        }

        private void Start()
        {
            if (m_ContinuousFlow)
            {
                StartContinuousFlow(m_EnergyColor, 1.5f, m_Intensity);
            }
        }

        public void PlayEnergyPulse(float duration = 0.28f, Color? color = null, float? intensity = null, UIBorderDirection? direction = null, Action onComplete = null)
        {
            Color c = color ?? m_EnergyColor;
            float pInt = intensity ?? m_Intensity;
            UIBorderDirection dir = direction ?? m_DefaultDirection;
            Effect.TriggerBorderPulse(duration, c, pInt, dir, onComplete);
        }

        public void StartContinuousFlow(Color? color = null, float speed = 1.2f, float intensity = 1.0f)
        {
            Effect.StartContinuousEnergyFlow(color ?? m_EnergyColor, speed, intensity);
        }

        public void StopFlow()
        {
            Effect.StopContinuousEnergyFlow();
        }

        public void ResetToIdle()
        {
            StopFlow();
            Effect.ResetToIdle();
        }
    }
}
