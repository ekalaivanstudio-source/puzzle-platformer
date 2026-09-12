using System;
using System.Collections;
using UnityEngine;
using MainGame.UI.Feedback;
using MainGame.UI.Unified;

namespace MainGame.UI.CinematicEffects
{
    /// <summary>
    /// Screen transition coordinator driven by CinematicScreenFXProfile.
    /// Eliminates the identical universal diagonal sweep by customizing each transition
    /// according to the source and destination screen profiles.
    /// </summary>
    [DisallowMultipleComponent]
    public class UITransitionFX : MonoBehaviour
    {
        private Coroutine m_TransitionRoutine;

        public void PlayProfileTransition(
            UIScreen fromScreen,
            UIScreen toScreen,
            ScreenFXType fromType,
            ScreenFXType toType,
            Action onMidpoint,
            Action onComplete)
        {
            if (m_TransitionRoutine != null)
            {
                StopCoroutine(m_TransitionRoutine);
            }

            m_TransitionRoutine = StartCoroutine(ProfileTransitionRoutine(
                fromScreen != null ? fromScreen.GetComponent<RectTransform>() : null,
                toScreen != null ? toScreen.GetComponent<RectTransform>() : null,
                CinematicScreenFXProfile.GetProfile(fromType),
                CinematicScreenFXProfile.GetProfile(toType),
                onMidpoint,
                onComplete
            ));
        }

        private IEnumerator ProfileTransitionRoutine(
            RectTransform fromRect,
            RectTransform toRect,
            CinematicScreenFXProfile fromProfile,
            CinematicScreenFXProfile toProfile,
            Action onMidpoint,
            Action onComplete)
        {
            // ─── PHASE 1: SOURCE SCREEN EXIT EFFECT ────────────────────────────
            if (fromProfile != null && fromProfile.PlayExitSfx)
            {
                UIFeedbackAudio.PlaySfx(fromProfile.ExitSfx, fromProfile.SfxVolume, 0.02f);
            }

            if (fromRect != null && fromProfile != null)
            {
                CinematicUIEffect fromFx = fromRect.GetComponent<CinematicUIEffect>() ?? fromRect.GetComponentInChildren<CinematicUIEffect>();
                if (fromFx != null)
                {
                    fromFx.TriggerBorderPulse(fromProfile.ExitDuration * 0.7f, fromProfile.PrimaryEnergyColor, fromProfile.BorderIntensity, fromProfile.BorderDirection);
                    if (fromProfile.GlitchIntensity > 0.3f)
                    {
                        fromFx.TriggerDigitalGlitch(fromProfile.ExitDuration * 0.5f, fromProfile.GlitchIntensity);
                    }
                }

                if (CinematicUIParticleSystem.Instance != null && fromProfile.ParticleBurstCount > 0)
                {
                    CinematicUIParticleSystem.Instance.SpawnSparkBurst(
                        fromRect.position,
                        fromProfile.PrimaryEnergyColor,
                        fromProfile.ParticleBurstCount / 2,
                        fromProfile.ParticleSpreadRadius
                    );
                }
            }

            float exitWait = fromProfile != null ? fromProfile.ExitDuration * 0.6f : 0.12f;
            yield return new WaitForSecondsRealtime(exitWait);

            // ─── PHASE 2: MIDPOINT SWAP ────────────────────────────────────────
            onMidpoint?.Invoke();
            yield return null;

            // ─── PHASE 3: DESTINATION SCREEN ENTRANCE RECEPTION ────────────────
            if (toProfile != null && toProfile.PlayEntranceSfx)
            {
                UIFeedbackAudio.PlaySfx(toProfile.EntranceSfx, toProfile.SfxVolume, 0.02f);
            }

            if (toRect != null && toProfile != null)
            {
                CinematicUIEffect toFx = toRect.GetComponent<CinematicUIEffect>() ?? toRect.GetComponentInChildren<CinematicUIEffect>();
                if (toFx != null)
                {
                    toFx.TriggerBorderPulse(toProfile.EntranceDuration * 0.8f, toProfile.PrimaryEnergyColor, toProfile.BorderIntensity, toProfile.BorderDirection);
                    if (toProfile.ShockwaveStrength > 0.05f)
                    {
                        toFx.TriggerShockwave(toProfile.EntranceDuration * 0.6f, new Vector2(0.5f, 0.5f), toProfile.ShockwaveStrength);
                    }
                }

                if (CinematicUIParticleSystem.Instance != null && toProfile.ParticleBurstCount > 0)
                {
                    CinematicUIParticleSystem.Instance.SpawnSparkBurst(
                        toRect.position,
                        toProfile.SecondaryAccentColor,
                        toProfile.ParticleBurstCount,
                        toProfile.ParticleSpreadRadius
                    );
                }
            }

            float enterWait = toProfile != null ? toProfile.EntranceDuration * 0.7f : 0.18f;
            yield return new WaitForSecondsRealtime(enterWait);

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
