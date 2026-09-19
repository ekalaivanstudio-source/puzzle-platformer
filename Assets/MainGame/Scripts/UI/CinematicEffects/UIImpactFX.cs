using System;
using System.Collections;
using UnityEngine;
using MainGame.UI.Feedback;

namespace MainGame.UI.CinematicEffects
{
    /// <summary>
    /// Component coordinating UI physical landing impact effects:
    /// - Kinetic deceleration micro-shake
    /// - High-intensity shader impact flash
    /// - Directional pixel spark & shard bursts
    /// - Synchronized kinetic audio lock
    /// </summary>
    [DisallowMultipleComponent]
    public class UIImpactFX : MonoBehaviour
    {
        private Coroutine m_ShakeRoutine;

        /// <summary>
        /// Plays a complete physical landing impact on the specified UI element.
        /// </summary>
        public void PlayImpact(RectTransform target, float intensity = 1.0f, Vector2? direction = null, Color? sparkColor = null, Action onComplete = null)
        {
            if (target == null)
            {
                onComplete?.Invoke();
                return;
            }

            Color pColor = sparkColor ?? new Color(1f, 0.88f, 0.35f, 1f);

            // 1. Shader flash
            CinematicUIEffect effect = target.GetComponent<CinematicUIEffect>() ?? target.GetComponentInChildren<CinematicUIEffect>();
            if (effect != null)
            {
                effect.TriggerImpactFlash(0.12f, Color.white, 2.2f * intensity);
            }

            // 2. Particle burst
            Vector2 dir = direction ?? Vector2.down;
            UIParticleFX.DirectionalSparks(target.anchoredPosition, target.parent, dir, pColor, Mathf.RoundToInt(4 * intensity), 140f * intensity);
            UIParticleFX.Shards(target.anchoredPosition, target.parent, pColor, Mathf.RoundToInt(3 * intensity), 20f * intensity);

            // 3. Audio feedback
            UIFeedbackAudio.PlaySfx(UISfxType.Impact, Mathf.Clamp01(0.75f * intensity), 0.03f);

            // 4. Micro-shake
            if (m_ShakeRoutine != null)
            {
                StopCoroutine(m_ShakeRoutine);
            }
            m_ShakeRoutine = StartCoroutine(MicroShakeRoutine(target, intensity, onComplete));
        }

        private IEnumerator MicroShakeRoutine(RectTransform target, float intensity, Action onComplete)
        {
            Vector2 originalPos = target.anchoredPosition;
            float duration = 0.12f;
            float elapsed = 0f;
            float shakeMagnitude = 3.5f * intensity;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;
                float decay = 1f - t;
                float offset = Mathf.Sin(t * Mathf.PI * 6f) * shakeMagnitude * decay;

                target.anchoredPosition = new Vector2(originalPos.x, originalPos.y + offset);
                yield return null;
            }

            target.anchoredPosition = originalPos;
            m_ShakeRoutine = null;
            onComplete?.Invoke();
        }

        public void StopActiveShake()
        {
            if (m_ShakeRoutine != null)
            {
                StopCoroutine(m_ShakeRoutine);
                m_ShakeRoutine = null;
            }
        }
    }
}
