using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MainGame.UI.CinematicEffects
{
    /// <summary>
    /// Coordinates Level 0 Idle Atmosphere across menu screens:
    /// - Low-frequency breathing cycle (3.5 - 5.0s interval)
    /// - Subtle dormant energy pulses on signboards and characters
    /// - Sporadic tiny pixel dust or micro-spark emission
    /// - Clean pause/resume during active transitions
    /// </summary>
    [DisallowMultipleComponent]
    public class UIAmbientFX : MonoBehaviour
    {
        [SerializeField] private float m_MinCycleInterval = 3.5f;
        [SerializeField] private float m_MaxCycleInterval = 5.0f;
        [SerializeField] private bool m_IsActive = true;

        private readonly List<CinematicUIEffect> m_RegisteredEffects = new List<CinematicUIEffect>(16);
        private Coroutine m_AmbientRoutine;

        private void OnEnable()
        {
            if (m_IsActive && m_AmbientRoutine == null)
            {
                m_AmbientRoutine = StartCoroutine(AmbientLivingLoop());
            }
        }

        private void OnDisable()
        {
            StopAmbient();
        }

        public void Register(CinematicUIEffect effect)
        {
            if (effect != null && !m_RegisteredEffects.Contains(effect))
            {
                m_RegisteredEffects.Add(effect);
            }
        }

        public void Unregister(CinematicUIEffect effect)
        {
            if (effect != null)
            {
                m_RegisteredEffects.Remove(effect);
            }
        }

        public void SetPaused(bool paused)
        {
            m_IsActive = !paused;
            if (paused)
            {
                StopAmbient();
            }
            else if (m_AmbientRoutine == null && gameObject.activeInHierarchy)
            {
                m_AmbientRoutine = StartCoroutine(AmbientLivingLoop());
            }
        }

        private void StopAmbient()
        {
            if (m_AmbientRoutine != null)
            {
                StopCoroutine(m_AmbientRoutine);
                m_AmbientRoutine = null;
            }
        }

        private IEnumerator AmbientLivingLoop()
        {
            // Initial breathing offset
            yield return new WaitForSecondsRealtime(1.5f);

            while (m_IsActive)
            {
                float delay = UnityEngine.Random.Range(m_MinCycleInterval, m_MaxCycleInterval);
                yield return new WaitForSecondsRealtime(delay);

                // Clean up null references
                m_RegisteredEffects.RemoveAll(e => e == null);

                if (m_RegisteredEffects.Count > 0)
                {
                    // Select 1-2 random elements to emit a subtle living pulse
                    int pickCount = Mathf.Min(2, m_RegisteredEffects.Count);
                    for (int i = 0; i < pickCount; i++)
                    {
                        int idx = UnityEngine.Random.Range(0, m_RegisteredEffects.Count);
                        CinematicUIEffect fx = m_RegisteredEffects[idx];
                        if (fx != null && fx.gameObject.activeInHierarchy)
                        {
                            fx.TriggerBorderPulse(0.45f, fx.BorderColor, 1.3f);
                            if (UnityEngine.Random.value < 0.4f)
                            {
                                RectTransform rt = fx.GetComponent<RectTransform>();
                                if (rt != null)
                                {
                                    UIParticleFX.GenericBurst(UIParticleType.TinyDust, rt.anchoredPosition, rt.parent, fx.BorderColor, 2, 20f);
                                }
                            }
                        }
                    }
                }
            }

            m_AmbientRoutine = null;
        }

        public void Clear()
        {
            StopAmbient();
            m_RegisteredEffects.Clear();
        }
    }
}
