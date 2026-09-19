using System;
using System.Collections;
using UnityEngine;
using MainGame.UI.Feedback;

namespace MainGame.UI.CinematicEffects
{
    /// <summary>
    /// Component coordinating Level 2 Action Confirmation effects:
    /// - Physical compression punch and snappy spring rebound
    /// - Radial energy shockwave ring expanding outward
    /// - Impact flash and particle fragment ejection
    /// - Synchronized confirmation / back audio
    /// </summary>
    [DisallowMultipleComponent]
    public class UIConfirmFX : MonoBehaviour
    {
        private Coroutine m_PunchRoutine;

        public void PlayConfirm(RectTransform target, bool isDestructive = false, Action onComplete = null)
        {
            if (target == null)
            {
                onComplete?.Invoke();
                return;
            }

            Color pColor = isDestructive ? new Color(1.0f, 0.45f, 0.2f) : new Color(0.35f, 0.85f, 1.0f);

            // 1. Shader shockwave and impact flash
            CinematicUIEffect effect = target.GetComponent<CinematicUIEffect>() ?? target.GetComponentInChildren<CinematicUIEffect>();
            if (effect != null)
            {
                effect.TriggerImpactFlash(0.12f, Color.white, 2.0f);
                effect.TriggerRadialPulse(0.32f, pColor, 1.4f, 2.5f);
            }

            // 2. Particle ejection
            UIParticleFX.Sparks(target.anchoredPosition, target.parent, pColor, 6, 28f);
            UIParticleFX.Shards(target.anchoredPosition, target.parent, pColor, 4, 32f);

            // 3. Audio feedback
            if (isDestructive)
            {
                UIFeedbackAudio.PlaySfx(UISfxType.Back, 0.90f, 0.02f);
            }
            else
            {
                UIFeedbackAudio.PlaySfx(UISfxType.Confirm, 1.0f, 0.02f);
            }

            // 4. Punch motion
            if (m_PunchRoutine != null)
            {
                StopCoroutine(m_PunchRoutine);
            }
            m_PunchRoutine = StartCoroutine(PunchMotionRoutine(target, onComplete));
        }

        private IEnumerator PunchMotionRoutine(RectTransform target, Action onComplete)
        {
            Vector3 originalScale = target.localScale;
            Vector2 originalPos = target.anchoredPosition;
            float duration = 0.14f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Compression punch curve: 1.00 -> 0.94 -> 1.02 -> 1.00
                float scaleMod = 1f;
                if (t < 0.35f)
                {
                    float p = t / 0.35f;
                    scaleMod = Mathf.Lerp(1.0f, 0.94f, p);
                }
                else
                {
                    float p = (t - 0.35f) / 0.65f;
                    scaleMod = Mathf.Lerp(0.94f, 1.0f, Mathf.Sin(p * Mathf.PI * 0.5f));
                }

                target.localScale = originalScale * scaleMod;
                yield return null;
            }

            target.localScale = originalScale;
            target.anchoredPosition = originalPos;
            m_PunchRoutine = null;
            onComplete?.Invoke();
        }

        public void StopActivePunch()
        {
            if (m_PunchRoutine != null)
            {
                StopCoroutine(m_PunchRoutine);
                m_PunchRoutine = null;
            }
        }
    }
}
