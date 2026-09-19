using System;
using UnityEngine;

namespace MainGame.UI.CinematicEffects
{
    /// <summary>
    /// Modular controller for electrical arcs and spark bursts.
    /// Drives sharp electrical discharge between components and corner latches.
    /// </summary>
    public class UIElectricArc : MonoBehaviour
    {
        [Header("Arc Configuration")]
        [SerializeField] private Color m_ArcColor = new Color(0.35f, 0.85f, 1.0f, 1.0f);
        [SerializeField] private int m_DefaultCount = 6;
        [SerializeField] private float m_Radius = 24f;

        public void TriggerArc(Vector2 position, Transform parent = null, Color? color = null, int? count = null)
        {
            Color c = color ?? m_ArcColor;
            int n = count ?? m_DefaultCount;
            Transform p = parent != null ? parent : transform;

            if (CinematicUIParticleSystem.Instance != null)
            {
                CinematicUIParticleSystem.Instance.SpawnSparkBurst(position, p, c, n, m_Radius);
            }
        }

        public void TriggerDirectionalArc(Vector2 position, Vector2 direction, Transform parent = null, Color? color = null, int? count = null)
        {
            Color c = color ?? m_ArcColor;
            int n = count ?? m_DefaultCount;
            Transform p = parent != null ? parent : transform;

            if (CinematicUIParticleSystem.Instance != null)
            {
                CinematicUIParticleSystem.Instance.SpawnDirectionalBurst(position, p, c, direction, n, 45f, m_Radius * 1.5f);
            }
        }
    }
}
