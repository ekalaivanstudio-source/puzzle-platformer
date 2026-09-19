using System;
using UnityEngine;
using UnityEngine.UI;

namespace MainGame.UI.CinematicEffects
{
    /// <summary>
    /// Modular controller for scrolling data stream overlays and telemetry clouds.
    /// Characteristic for Credits and Collection screens.
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    [DisallowMultipleComponent]
    public class UIDataStream : MonoBehaviour
    {
        [Header("Data Stream Configuration")]
        [SerializeField] private Color m_DataColor = new Color(0.30f, 0.95f, 0.60f, 0.85f);
        [SerializeField] private float m_Speed = 6.0f;
        [SerializeField] private float m_Density = 24.0f;

        private CinematicUIEffect m_Effect;

        public CinematicUIEffect Effect
        {
            get
            {
                if (m_Effect == null) m_Effect = GetComponent<CinematicUIEffect>() ?? gameObject.AddComponent<CinematicUIEffect>();
                return m_Effect;
            }
        }

        public void PlayDataStream(float duration = 0.50f, Color? color = null, float? speed = null, float? density = null, Action onComplete = null)
        {
            Color c = color ?? m_DataColor;
            float s = speed ?? m_Speed;
            float d = density ?? m_Density;
            Effect.PlayDataStream(duration, 1.2f, c, s, d, onComplete);
        }

        public void ResetToIdle()
        {
            Effect.ResetToIdle();
        }
    }
}
