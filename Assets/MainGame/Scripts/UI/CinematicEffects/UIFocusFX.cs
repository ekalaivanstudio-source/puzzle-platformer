using System;
using UnityEngine;
using MainGame.UI.Feedback;

namespace MainGame.UI.CinematicEffects
{
    /// <summary>
    /// Component coordinating Level 1 Focus / Navigation effects:
    /// - Responsive perimeter energy pulse
    /// - Fast diagonal gleam sweep
    /// - Pointer spark bursts
    /// - Synchronized navigation audio
    /// </summary>
    [DisallowMultipleComponent]
    public class UIFocusFX : MonoBehaviour
    {
        [SerializeField] private Color m_DefaultFocusColor = new Color(0.35f, 0.85f, 1.0f, 1.0f);
        [SerializeField] private Color m_DestructiveFocusColor = new Color(1.0f, 0.45f, 0.2f, 1.0f);

        public void PlayFocus(RectTransform target, RectTransform pointer = null, bool isDestructive = false)
        {
            if (target == null) return;

            Color col = isDestructive ? m_DestructiveFocusColor : m_DefaultFocusColor;

            // 1. Shader pulse on target
            CinematicUIEffect effect = target.GetComponent<CinematicUIEffect>() ?? target.GetComponentInChildren<CinematicUIEffect>();
            if (effect != null)
            {
                effect.TriggerBorderPulse(0.20f, col, 1.9f);
                effect.PlayActivationSweep(0.18f, new Color(1f, 1f, 1f, 0.6f), 45f);
            }

            // 2. Pointer spark burst
            if (pointer != null)
            {
                UIParticleFX.Sparks(pointer.anchoredPosition, target, new Color(1f, 0.88f, 0.35f, 1f), 3, 10f);
            }
            else
            {
                UIParticleFX.Sparks(target.anchoredPosition, target.parent, col, 2, 8f);
            }

            // 3. Audio
            UIFeedbackAudio.PlaySfx(UISfxType.Navigate, 0.85f, 0.03f);
        }

        public void ResetFocus(RectTransform target)
        {
            if (target == null) return;

            CinematicUIEffect effect = target.GetComponent<CinematicUIEffect>() ?? target.GetComponentInChildren<CinematicUIEffect>();
            if (effect != null)
            {
                effect.ResetToIdle();
            }
        }
    }
}
