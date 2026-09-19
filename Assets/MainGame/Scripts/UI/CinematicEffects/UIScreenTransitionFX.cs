using System;
using System.Collections;
using UnityEngine;
using MainGame.UI.Feedback;

namespace MainGame.UI.CinematicEffects
{
    /// <summary>
    /// Coordinates Level 4 Screen Transformation & Mode Change Transition Bridges (0.4 - 0.8s):
    /// - Phase 1: Energy Accumulation & Edge Wave
    /// - Phase 2: Directional Flash, Glitch Pulse & Pixel Fragments
    /// - Phase 3: Screen Midpoint Switch
    /// - Phase 4: Mode Transformation Sweep across destination
    /// - Phase 5: Panel Unfold & Settle Lock
    /// </summary>
    [DisallowMultipleComponent]
    public class UIScreenTransitionFX : MonoBehaviour
    {
        [Header("Transition Visuals")]
        [SerializeField] private Color m_EnergyColor = new Color(0.35f, 0.85f, 1.0f, 1.0f);
        [SerializeField] private Color m_AccentColor = new Color(1.0f, 0.85f, 0.2f, 1.0f);

        private Coroutine m_TransitionRoutine;

        /// <summary>
        /// Plays the high-energy Mode Change Transition Bridge between HomeScreen and LevelSelectionScreen.
        /// </summary>
        public void PlayModeChangeBridge(RectTransform fromScreen, RectTransform toScreen, Action onMidpoint, Action onComplete)
        {
            if (m_TransitionRoutine != null)
            {
                StopCoroutine(m_TransitionRoutine);
            }

            m_TransitionRoutine = StartCoroutine(ModeChangeBridgeRoutine(fromScreen, toScreen, onMidpoint, onComplete));
        }

        private IEnumerator ModeChangeBridgeRoutine(RectTransform fromScreen, RectTransform toScreen, Action onMidpoint, Action onComplete)
        {
            // ─── PHASE 1: ENERGY ACCUMULATION (0.14s) ───────────────────────────
            UIFeedbackAudio.PlaySfx(UISfxType.RobotBoot, 0.80f, 0.02f);

            if (fromScreen != null)
            {
                UIParticleFX.ScreenEdgeWave(fromScreen, m_EnergyColor, 3);
                CinematicUIEffect fromFx = fromScreen.GetComponent<CinematicUIEffect>() ?? fromScreen.GetComponentInChildren<CinematicUIEffect>();
                if (fromFx != null)
                {
                    fromFx.TriggerBorderPulse(0.18f, m_EnergyColor, 2.0f);
                }
            }

            yield return new WaitForSecondsRealtime(0.14f);

            // ─── PHASE 2: DIRECTIONAL FLASH & GLITCH (0.12s) ───────────────────
            UIFeedbackAudio.PlaySfx(UISfxType.Deploy, 0.85f, 0.04f);

            if (fromScreen != null)
            {
                UIParticleFX.PixelDissolve(fromScreen, m_AccentColor, 12);
                CinematicUIEffect fromFx = fromScreen.GetComponent<CinematicUIEffect>() ?? fromScreen.GetComponentInChildren<CinematicUIEffect>();
                if (fromFx != null)
                {
                    fromFx.TriggerGlitch(0.12f, 0.6f);
                    fromFx.PlayActivationSweep(0.14f, Color.white, 45f);
                }
            }

            yield return new WaitForSecondsRealtime(0.10f);

            // ─── PHASE 3: MIDPOINT SCREEN SWITCH ───────────────────────────────
            onMidpoint?.Invoke();
            yield return null;

            // ─── PHASE 4: MODE TRANSFORMATION SWEEP (0.22s) ───────────────────
            UIFeedbackAudio.PlaySfx(UISfxType.Deploy, 0.90f, 0.02f);

            if (toScreen != null)
            {
                UIParticleFX.ScreenEdgeWave(toScreen, m_AccentColor, 4);
                CinematicUIEffect toFx = toScreen.GetComponent<CinematicUIEffect>() ?? toScreen.GetComponentInChildren<CinematicUIEffect>();
                if (toFx != null)
                {
                    toFx.PlayActivationSweep(0.24f, new Color(1f, 1f, 1f, 0.8f), 45f);
                    toFx.TriggerBorderPulse(0.26f, m_AccentColor, 2.2f);
                }
            }

            yield return new WaitForSecondsRealtime(0.22f);

            // ─── PHASE 5: FINAL IMPACT & SETTLE ───────────────────────────────
            if (toScreen != null)
            {
                UIParticleFX.Sparks(Vector2.zero, toScreen, m_AccentColor, 5, 28f);
            }

            m_TransitionRoutine = null;
            onComplete?.Invoke();
        }

        public void StopActiveTransition()
        {
            if (m_TransitionRoutine != null)
            {
                StopCoroutine(m_TransitionRoutine);
                m_TransitionRoutine = null;
            }
        }
    }
}
