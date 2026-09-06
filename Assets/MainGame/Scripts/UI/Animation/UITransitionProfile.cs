using System;
using UnityEngine;

namespace MainGame.UI.Animation
{
    public enum TransitionDirection
    {
        None,
        Left,
        Right,
        Top,
        Bottom,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,
        Custom
    }

    /// <summary>
    /// Configuration data describing the physical motion parameters of a UI element or group.
    /// </summary>
    [Serializable]
    public struct UITransitionProfile
    {
        [Header("Motion Vector")]
        public TransitionDirection Direction;
        public float Distance;
        public Vector2 CustomOffset;

        [Header("Scale & Rotation")]
        public float StartScale;
        public float EndScale;
        public float RotationOffset;

        [Header("Timing & Easing")]
        public float Duration;
        public float Delay;
        public float Stagger;
        public float Overshoot;
        public EasingType Easing;
        public bool FadeAlpha;

        public static UITransitionProfile DefaultEnter => new UITransitionProfile
        {
            Direction = TransitionDirection.Right,
            Distance = 200f,
            CustomOffset = Vector2.zero,
            StartScale = 0.95f,
            EndScale = 1f,
            RotationOffset = 0f,
            Duration = 0.35f,
            Delay = 0f,
            Stagger = 0.05f,
            Overshoot = 1.2f,
            Easing = EasingType.EaseOutBack,
            FadeAlpha = true
        };

        public static UITransitionProfile DefaultExit => new UITransitionProfile
        {
            Direction = TransitionDirection.Right,
            Distance = 250f,
            CustomOffset = Vector2.zero,
            StartScale = 1f,
            EndScale = 0.95f,
            RotationOffset = 0f,
            Duration = 0.25f,
            Delay = 0f,
            Stagger = 0.03f,
            Overshoot = 1f,
            Easing = EasingType.EaseInCubic,
            FadeAlpha = true
        };

        public Vector2 CalculateOffset()
        {
            switch (Direction)
            {
                case TransitionDirection.Left:
                    return new Vector2(-Distance, 0f);
                case TransitionDirection.Right:
                    return new Vector2(Distance, 0f);
                case TransitionDirection.Top:
                    return new Vector2(0f, Distance);
                case TransitionDirection.Bottom:
                    return new Vector2(0f, -Distance);
                case TransitionDirection.TopLeft:
                    return new Vector2(-Distance * 0.7071f, Distance * 0.7071f);
                case TransitionDirection.TopRight:
                    return new Vector2(Distance * 0.7071f, Distance * 0.7071f);
                case TransitionDirection.BottomLeft:
                    return new Vector2(-Distance * 0.7071f, -Distance * 0.7071f);
                case TransitionDirection.BottomRight:
                    return new Vector2(Distance * 0.7071f, -Distance * 0.7071f);
                case TransitionDirection.Custom:
                    return CustomOffset;
                case TransitionDirection.None:
                default:
                    return Vector2.zero;
            }
        }
    }
}
