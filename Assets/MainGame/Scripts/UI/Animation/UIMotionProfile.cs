using System;
using UnityEngine;

namespace MainGame.UI.Animation
{
    public enum MotionProfileType
    {
        Custom,
        UI_HeavyImpact,
        UI_MediumImpact,
        UI_LightImpact,
        UI_FastSlide,
        UI_Drop,
        UI_SoftReveal,
        UI_PopupImpact
    }

    /// <summary>
    /// Fully Inspector-exposed physical motion configuration for an individual UI element:
    /// Entry Direction, Entry Distance, Start Delay, Duration, Animation Curve,
    /// Rotation Start, Rotation End, Overshoot Distance, Impact Scale, Impact Duration,
    /// Rebound Amount, Settle Duration, Audio Clip, and Impact Enabled.
    /// </summary>
    [Serializable]
    public class UIElementMotionConfig
    {
        [Header("Identity")]
        public string ElementName = "Element";

        [Header("Trajectory & Screen-Edge Distance")]
        [Tooltip("Direction vector from which the element enters the screen.")]
        public Vector2 EntryDirection = new Vector2(0f, 1f);

        [Tooltip("Genuine screen-edge distance in pixels (> 600-900px) so object feels like it came from outside.")]
        public float EntryDistance = 800f;

        [Header("Timing & Overlap")]
        [Tooltip("Start delay in seconds to allow cascading physical overlap.")]
        public float StartDelay = 0.25f;

        [Tooltip("High-velocity travel duration (0.25s fast to 0.60s slower).")]
        public float Duration = 0.38f;

        [Header("Velocity Curve")]
        [Tooltip("Physical ballistic velocity curve: Fast initial travel (75%) -> Hard deceleration (20%) -> Overshoot (5%).")]
        public AnimationCurve MotionCurve;

        [Header("Physical Rotation")]
        [Tooltip("Rotation during flight (e.g. -7 deg for blue signboards).")]
        public float RotationStart = -7f;

        [Tooltip("Final rested rotation angle (usually 0 deg).")]
        public float RotationEnd = 0f;

        [Header("Impact & Landing Compression")]
        [Tooltip("Whether physical squash and stretch compression is enabled on impact.")]
        public bool ImpactEnabled = true;

        [Tooltip("Overshoot distance past target position in direction of travel (10-25px).")]
        public float OvershootDistance = 18f;

        [Tooltip("Compression scale on impact: X squashes wider (> 1.00), Y compresses down (< 1.00). e.g. (1.07, 0.93)")]
        public Vector2 ImpactScale = new Vector2(1.07f, 0.93f);

        [Tooltip("Duration of the impact squash phase (0.04 - 0.06s).")]
        public float ImpactDuration = 0.05f;

        [Tooltip("Rebound fraction opposite to travel direction (e.g. 0.25).")]
        public float ReboundAmount = 0.25f;

        [Tooltip("Duration of rebound and final settle (0.06 - 0.08s).")]
        public float SettleDuration = 0.07f;

        [Header("Feel & Feedback")]
        [Tooltip("Screen shake kick magnitude on impact.")]
        public float MicroShakeMagnitude = 0f;

        [Tooltip("Sound played on physical landing.")]
        public AudioClip AudioClip;

        public UIElementMotionConfig()
        {
            MotionCurve = UIMotionProfile.FastDrop;
        }

        public UIElementMotionConfig(string name, Vector2 dir, float dist, float delay, float dur, float rotStart, Vector2 squish, float overshoot)
        {
            ElementName = name;
            EntryDirection = dir.normalized;
            EntryDistance = dist;
            StartDelay = delay;
            Duration = dur;
            RotationStart = rotStart;
            RotationEnd = 0f;
            ImpactEnabled = true;
            ImpactScale = squish;
            OvershootDistance = overshoot;
            ImpactDuration = 0.05f;
            ReboundAmount = 0.25f;
            SettleDuration = 0.07f;
            MotionCurve = UIMotionProfile.FastDrop;
        }

        public Vector2 CalculateStartOffset()
        {
            return EntryDirection.normalized * EntryDistance;
        }
    }

    /// <summary>
    /// Reusable physical velocity curves and profile presets for PC/Console physical UI motion.
    /// Implements: Fast initial travel -> Hard deceleration -> Short impact/rebound -> Settle.
    /// </summary>
    [Serializable]
    public class UIMotionProfile
    {
        #region Reusable Authored Animation Curves

        private static AnimationCurve s_HeavyImpact;
        private static AnimationCurve s_MediumImpact;
        private static AnimationCurve s_LightImpact;
        private static AnimationCurve s_FastDrop;
        private static AnimationCurve s_FastSlide;
        private static AnimationCurve s_SoftReveal;

        /// <summary>
        /// Heavy physical slam curve: 75% of travel spent at maximum velocity, then aggressive braking.
        /// </summary>
        public static AnimationCurve HeavyImpact
        {
            get
            {
                if (s_HeavyImpact == null)
                {
                    s_HeavyImpact = new AnimationCurve(
                        new Keyframe(0f, 0f, 2.6f, 2.6f),
                        new Keyframe(0.70f, 0.88f, 1.2f, 0.5f),
                        new Keyframe(1f, 1f, 0.05f, 0f)
                    );
                }
                return s_HeavyImpact;
            }
        }

        /// <summary>
        /// Medium physical impact curve with fast travel and crisp deceleration.
        /// </summary>
        public static AnimationCurve MediumImpact
        {
            get
            {
                if (s_MediumImpact == null)
                {
                    s_MediumImpact = new AnimationCurve(
                        new Keyframe(0f, 0f, 2.4f, 2.4f),
                        new Keyframe(0.72f, 0.86f, 1.1f, 0.6f),
                        new Keyframe(1f, 1f, 0.05f, 0f)
                    );
                }
                return s_MediumImpact;
            }
        }

        /// <summary>
        /// Light impact curve for lower-mass elements or sub-labels.
        /// </summary>
        public static AnimationCurve LightImpact
        {
            get
            {
                if (s_LightImpact == null)
                {
                    s_LightImpact = new AnimationCurve(
                        new Keyframe(0f, 0f, 2.1f, 2.1f),
                        new Keyframe(0.75f, 0.85f, 0.9f, 0.5f),
                        new Keyframe(1f, 1f, 0.05f, 0f)
                    );
                }
                return s_LightImpact;
            }
        }

        /// <summary>
        /// Fast fall from sky curve: high gravity acceleration and hard braking.
        /// </summary>
        public static AnimationCurve FastDrop
        {
            get
            {
                if (s_FastDrop == null)
                {
                    s_FastDrop = new AnimationCurve(
                        new Keyframe(0f, 0f, 2.8f, 2.8f),
                        new Keyframe(0.68f, 0.90f, 1.3f, 0.4f),
                        new Keyframe(1f, 1f, 0.02f, 0f)
                    );
                }
                return s_FastDrop;
            }
        }

        /// <summary>
        /// Fast horizontal/diagonal slide from far outside screen edges.
        /// </summary>
        public static AnimationCurve FastSlide
        {
            get
            {
                if (s_FastSlide == null)
                {
                    s_FastSlide = new AnimationCurve(
                        new Keyframe(0f, 0f, 3.0f, 3.0f),
                        new Keyframe(0.70f, 0.89f, 1.2f, 0.4f),
                        new Keyframe(1f, 1f, 0.02f, 0f)
                    );
                }
                return s_FastSlide;
            }
        }

        /// <summary>
        /// Smooth, calm reveal curve for visual rest screens (e.g. Credits).
        /// </summary>
        public static AnimationCurve SoftReveal
        {
            get
            {
                if (s_SoftReveal == null)
                {
                    s_SoftReveal = new AnimationCurve(
                        new Keyframe(0f, 0f, 1.5f, 1.5f),
                        new Keyframe(0.80f, 0.92f, 0.6f, 0.4f),
                        new Keyframe(1f, 1f, 0f, 0f)
                    );
                }
                return s_SoftReveal;
            }
        }

        #endregion

        #region Legacy Compatibility Presets

        [Header("Trajectory & Distance")]
        public TransitionDirection Direction = TransitionDirection.Right;
        public float Distance = 800f;
        public Vector2 CustomOffset = Vector2.zero;

        [Header("Timing")]
        public float StartDelay = 0f;
        public float Duration = 0.38f;

        [Header("Velocity Curve")]
        public AnimationCurve MotionCurve;

        [Header("Rotation")]
        public float RotationStart = 0f;
        public float RotationEnd = 0f;

        [Header("Overshoot & Physical Impact")]
        public float OvershootDistance = 20f;
        public bool ImpactEnabled = true;
        public Vector2 ImpactScale = new Vector2(1.07f, 0.93f);
        public float ImpactDuration = 0.05f;
        public float SettleDuration = 0.07f;
        public float ScreenKickStrength = 0f;

        [Header("Audio")]
        public AudioClip ImpactAudio;

        public UIMotionProfile()
        {
            MotionCurve = FastDrop;
        }

        public Vector2 CalculateStartOffset()
        {
            if (Direction == TransitionDirection.Custom) return CustomOffset;
            switch (Direction)
            {
                case TransitionDirection.Left: return new Vector2(-Distance, 0f);
                case TransitionDirection.Right: return new Vector2(Distance, 0f);
                case TransitionDirection.Top: return new Vector2(0f, Distance);
                case TransitionDirection.Bottom: return new Vector2(0f, -Distance);
                case TransitionDirection.TopLeft: return new Vector2(-Distance * 0.7071f, Distance * 0.7071f);
                case TransitionDirection.TopRight: return new Vector2(Distance * 0.7071f, Distance * 0.7071f);
                case TransitionDirection.BottomLeft: return new Vector2(-Distance * 0.7071f, -Distance * 0.7071f);
                case TransitionDirection.BottomRight: return new Vector2(Distance * 0.7071f, -Distance * 0.7071f);
                case TransitionDirection.None:
                default:
                    return Vector2.zero;
            }
        }

        public static UIMotionProfile GetPreset(MotionProfileType type)
        {
            UIMotionProfile p = new UIMotionProfile();
            switch (type)
            {
                case MotionProfileType.UI_HeavyImpact:
                    p.Duration = 0.52f;
                    p.OvershootDistance = 25f;
                    p.ImpactEnabled = true;
                    p.ImpactScale = new Vector2(1.08f, 0.92f);
                    p.ImpactDuration = 0.06f;
                    p.SettleDuration = 0.08f;
                    p.ScreenKickStrength = 0.8f;
                    p.MotionCurve = HeavyImpact;
                    break;

                case MotionProfileType.UI_MediumImpact:
                    p.Duration = 0.38f;
                    p.OvershootDistance = 18f;
                    p.ImpactEnabled = true;
                    p.ImpactScale = new Vector2(1.06f, 0.94f);
                    p.ImpactDuration = 0.05f;
                    p.SettleDuration = 0.07f;
                    p.ScreenKickStrength = 0.4f;
                    p.MotionCurve = MediumImpact;
                    break;

                case MotionProfileType.UI_LightImpact:
                    p.Duration = 0.42f;
                    p.OvershootDistance = 12f;
                    p.ImpactEnabled = true;
                    p.ImpactScale = new Vector2(1.03f, 0.97f);
                    p.ImpactDuration = 0.05f;
                    p.SettleDuration = 0.06f;
                    p.ScreenKickStrength = 0f;
                    p.MotionCurve = LightImpact;
                    break;

                case MotionProfileType.UI_FastSlide:
                    p.Duration = 0.28f;
                    p.OvershootDistance = 18f;
                    p.ImpactEnabled = true;
                    p.ImpactScale = new Vector2(1.07f, 0.93f);
                    p.ImpactDuration = 0.05f;
                    p.SettleDuration = 0.06f;
                    p.ScreenKickStrength = 0.3f;
                    p.MotionCurve = FastSlide;
                    break;

                case MotionProfileType.UI_Drop:
                    p.Direction = TransitionDirection.Top;
                    p.Duration = 0.44f;
                    p.OvershootDistance = 25f;
                    p.ImpactEnabled = true;
                    p.ImpactScale = new Vector2(1.10f, 0.90f);
                    p.ImpactDuration = 0.06f;
                    p.SettleDuration = 0.07f;
                    p.ScreenKickStrength = 0.9f;
                    p.MotionCurve = FastDrop;
                    break;

                case MotionProfileType.UI_SoftReveal:
                    p.Duration = 0.65f;
                    p.OvershootDistance = 8f;
                    p.ImpactEnabled = false;
                    p.ImpactScale = Vector2.one;
                    p.ImpactDuration = 0.04f;
                    p.SettleDuration = 0.05f;
                    p.ScreenKickStrength = 0f;
                    p.MotionCurve = SoftReveal;
                    break;

                case MotionProfileType.UI_PopupImpact:
                    p.Direction = TransitionDirection.Top;
                    p.Duration = 0.28f;
                    p.OvershootDistance = 20f;
                    p.ImpactEnabled = true;
                    p.ImpactScale = new Vector2(1.08f, 0.90f);
                    p.ImpactDuration = 0.06f;
                    p.SettleDuration = 0.08f;
                    p.ScreenKickStrength = 1.35f;
                    p.MotionCurve = HeavyImpact;
                    break;
            }
            return p;
        }

        #endregion
    }
}
