using System;
using UnityEngine;
using UnityEngine.UI;

namespace MainGame.UI.CinematicEffects
{
    /// <summary>
    /// Modular controller for digital glitch, horizontal slice tear, and RGB split aberration.
    /// Particularly characteristic for Credits data transmission and high-impact transition spikes.
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    [DisallowMultipleComponent]
    public class UIDigitalGlitch : MonoBehaviour
    {
        [Header("Glitch Configuration")]
        [SerializeField] private float m_DefaultIntensity = 0.8f;
        [SerializeField] private float m_RgbOffset = 0.02f;

        private CinematicUIEffect m_Effect;

        public CinematicUIEffect Effect
        {
            get
            {
                if (m_Effect == null) m_Effect = GetComponent<CinematicUIEffect>() ?? gameObject.AddComponent<CinematicUIEffect>();
                return m_Effect;
            }
        }

        public void PlayGlitch(float duration = 0.14f, float? intensity = null, float? rgbOffset = null, Action onComplete = null)
        {
            float i = intensity ?? m_DefaultIntensity;
            float o = rgbOffset ?? m_RgbOffset;
            Effect.TriggerDigitalGlitch(duration, i, o);
            if (onComplete != null)
            {
                StartCoroutine(InvokeAfterTime(duration, onComplete));
            }
        }

        public void SetHolographicFlicker(float amount)
        {
            Effect.SetHolographicFlicker(amount);
        }

        public void ResetToIdle()
        {
            Effect.SetHolographicFlicker(0f);
            Effect.ResetToIdle();
        }

        private System.Collections.IEnumerator InvokeAfterTime(float delay, Action callback)
        {
            yield return new WaitForSecondsRealtime(delay);
            callback?.Invoke();
        }
    }
}
