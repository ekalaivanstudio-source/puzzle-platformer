using System;
using UnityEngine;

namespace MainGame.UI.Feedback
{
    /// <summary>
    /// Configuration data defining a More Mountains / Feel style tactile feedback moment.
    /// Can be configured in the Inspector or created dynamically in code.
    /// </summary>
    [Serializable]
    public class UIFeedbackPreset
    {
        [Header("Transform Punch")]
        [Tooltip("Relative scale delta added during the punch (e.g. 0.04 for slight pop, -0.06 for compression).")]
        public Vector3 scalePunch = Vector3.zero;

        [Tooltip("Relative position offset added during the punch (e.g. (0, -4, 0) for focus shift, (0, 8, 0) for confirm).")]
        public Vector3 positionShove = Vector3.zero;

        [Tooltip("Angular tilt in degrees around the Z axis.")]
        public float angularTilt = 0f;

        [Tooltip("Total duration of the feedback pulse in unscaled seconds.")]
        [Range(0.02f, 1.0f)]
        public float duration = 0.14f;

        [Header("Pixel FX Particles")]
        [Tooltip("Number of point-filtered pixel particles to burst at the target location.")]
        [Range(0, 30)]
        public int particleCount = 0;

        [Tooltip("Color tint for the spawned particles.")]
        public Color particleColor = Color.white;

        [Header("Screen Shake")]
        [Tooltip("If true, triggers camera/screen micro-shake via FeelService.")]
        public bool triggerScreenShake = false;

        [Range(0.1f, 5.0f)]
        public float shakeIntensity = 1.0f;

        [Range(0.02f, 0.5f)]
        public float shakeDuration = 0.08f;

        [Header("Audio SFX")]
        public bool playSfx = true;
        public UISfxType sfxType = UISfxType.Navigate;
        [Range(0f, 1f)] public float sfxVolume = 1f;
        [Range(0f, 0.2f)] public float pitchVariance = 0.03f;

        public static UIFeedbackPreset CreateFocusPreset()
        {
            return new UIFeedbackPreset
            {
                scalePunch = new Vector3(0.035f, 0.035f, 0f),
                positionShove = new Vector3(0f, -4f, 0f),
                angularTilt = 1.2f,
                duration = 0.12f,
                particleCount = 0,
                playSfx = true,
                sfxType = UISfxType.Navigate,
                sfxVolume = 0.9f,
                pitchVariance = 0.03f
            };
        }

        public static UIFeedbackPreset CreateConfirmPreset()
        {
            return new UIFeedbackPreset
            {
                scalePunch = new Vector3(-0.06f, -0.06f, 0f),
                positionShove = new Vector3(0f, 6f, 0f),
                angularTilt = -1.5f,
                duration = 0.16f,
                particleCount = 6,
                particleColor = new Color(1f, 0.87f, 0.35f, 1f), // Warm amber spark
                playSfx = true,
                sfxType = UISfxType.Confirm,
                sfxVolume = 1f,
                pitchVariance = 0.02f
            };
        }

        public static UIFeedbackPreset CreateBackPreset()
        {
            return new UIFeedbackPreset
            {
                scalePunch = new Vector3(-0.04f, -0.04f, 0f),
                positionShove = new Vector3(0f, -6f, 0f),
                angularTilt = 0f,
                duration = 0.12f,
                particleCount = 0,
                playSfx = true,
                sfxType = UISfxType.Back,
                sfxVolume = 0.9f,
                pitchVariance = 0.02f
            };
        }
    }
}
