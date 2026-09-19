using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MainGame.UI.Animation;
using MainGame.UI.RoboticEffects;

namespace MainGame.UI.Feedback
{
    /// <summary>
    /// Central UI Feedback execution engine.
    /// Applies More Mountains / Unity Feel style micro-interactions to UI elements cleanly:
    /// - Scale punches & compressions
    /// - Positional shoves (e.g. forward punch, depth shift towards player)
    /// - Angular tilts (signboard wobbles)
    /// - Point-filtered spark/pixel particle bursts
    /// - Camera/screen micro-shake
    /// - Synchronized SFX routing through SFX AudioMixerGroup
    ///
    /// Preserves base positions/rotations/scales so UI animations never drift on repeated triggers.
    /// </summary>
    [DisallowMultipleComponent]
    public class UIFeedbackController : MonoBehaviour
    {
        private static UIFeedbackController s_Instance;

        public static UIFeedbackController Instance
        {
            get
            {
                if (s_Instance != null) return s_Instance;
                s_Instance = FindAnyObjectByType<UIFeedbackController>();
                if (s_Instance != null) return s_Instance;

                GameObject go = new GameObject("[UIFeedbackController]");
                s_Instance = go.AddComponent<UIFeedbackController>();
                DontDestroyOnLoad(go);
                return s_Instance;
            }
        }

        private class ActiveUIFeedback
        {
            public RectTransform target;
            public Coroutine coroutine;
            public Vector3 baseLocalPosition;
            public Vector3 baseLocalScale;
            public Quaternion baseLocalRotation;
            public bool hasBase;
        }

        private readonly Dictionary<RectTransform, ActiveUIFeedback> m_ActiveFeedbacks = new Dictionary<RectTransform, ActiveUIFeedback>();

        private void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            s_Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// Executes a feedback preset on the specified target RectTransform.
        /// </summary>
        public void PlayFeedback(RectTransform target, UIFeedbackPreset preset)
        {
            if (target == null || preset == null) return;

            // Audio
            if (preset.playSfx)
            {
                UIFeedbackAudio.PlaySfx(preset.sfxType, preset.sfxVolume, preset.pitchVariance);
            }

            // Particles
            if (preset.particleCount > 0)
            {
                SpawnParticles(target, preset);
            }

            // Screen Shake
            if (preset.triggerScreenShake)
            {
                UIMicroShake.Shake(preset.shakeIntensity, preset.shakeDuration);
            }

            // Transform Punch
            bool hasTransformMotion = preset.scalePunch != Vector3.zero ||
                                      preset.positionShove != Vector3.zero ||
                                      Mathf.Abs(preset.angularTilt) > 0.01f;

            if (hasTransformMotion && gameObject.activeInHierarchy)
            {
                if (!m_ActiveFeedbacks.TryGetValue(target, out ActiveUIFeedback feedback))
                {
                    feedback = new ActiveUIFeedback
                    {
                        target = target,
                        baseLocalPosition = target.localPosition,
                        baseLocalScale = target.localScale,
                        baseLocalRotation = target.localRotation,
                        hasBase = true
                    };
                    m_ActiveFeedbacks[target] = feedback;
                }
                else
                {
                    if (feedback.coroutine != null)
                    {
                        StopCoroutine(feedback.coroutine);
                        // Restore base before starting new punch
                        target.localPosition = feedback.baseLocalPosition;
                        target.localScale = feedback.baseLocalScale;
                        target.localRotation = feedback.baseLocalRotation;
                    }
                    else
                    {
                        // Update base to current resting state
                        feedback.baseLocalPosition = target.localPosition;
                        feedback.baseLocalScale = target.localScale;
                        feedback.baseLocalRotation = target.localRotation;
                    }
                }

                feedback.coroutine = StartCoroutine(AnimateFeedbackRoutine(feedback, preset));
            }
        }

        private IEnumerator AnimateFeedbackRoutine(ActiveUIFeedback feedback, UIFeedbackPreset preset)
        {
            RectTransform rt = feedback.target;
            Vector3 basePos = feedback.baseLocalPosition;
            Vector3 baseScale = feedback.baseLocalScale;
            Quaternion baseRot = feedback.baseLocalRotation;

            float duration = Mathf.Max(0.02f, preset.duration);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);

                // Spring rebound curve: fast attack (0 -> 1), elastic overshoot/rebound back to 0
                float curve;
                if (progress < 0.35f)
                {
                    // Attack
                    curve = Mathf.Sin((progress / 0.35f) * Mathf.PI * 0.5f);
                }
                else
                {
                    // Decay with slight rebound
                    float decayProgress = (progress - 0.35f) / 0.65f;
                    curve = Mathf.Cos(decayProgress * Mathf.PI * 0.5f) * Mathf.Cos(decayProgress * Mathf.PI * 1.5f);
                }

                if (rt != null)
                {
                    rt.localPosition = basePos + preset.positionShove * curve;
                    rt.localScale = baseScale + Vector3.Scale(baseScale, preset.scalePunch * curve);
                    rt.localRotation = baseRot * Quaternion.Euler(0f, 0f, preset.angularTilt * curve);
                }

                yield return null;
            }

            // Perfectly restore base state
            if (rt != null)
            {
                rt.localPosition = basePos;
                rt.localScale = baseScale;
                rt.localRotation = baseRot;
            }

            feedback.coroutine = null;
        }

        private void SpawnParticles(RectTransform target, UIFeedbackPreset preset)
        {
            RoboticPixelFXPool pool = RoboticPixelFXPool.Instance;
            if (pool != null)
            {
                Vector2 center = target.anchoredPosition;
                Transform parent = target.parent != null ? target.parent : target;
                pool.SpawnSparkBurst(center, parent, preset.particleColor, preset.particleCount, 22f);
            }
        }

        public static void Trigger(RectTransform target, UIFeedbackPreset preset)
        {
            if (Instance != null)
            {
                Instance.PlayFeedback(target, preset);
            }
        }
    }
}
