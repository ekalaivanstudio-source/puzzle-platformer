using UnityEngine;

namespace MainGame.UI.Animation
{
    public enum EasingType
    {
        Linear,
        EaseInQuad,
        EaseOutQuad,
        EaseInOutQuad,
        EaseOutCubic,
        EaseInCubic,
        EaseInOutCubic,
        EaseOutBack,
        EaseInBack,
        EaseOutBounce,
        Spring
    }

    /// <summary>
    /// Pure mathematical easing functions with zero runtime GC allocations.
    /// Used across all cinematic UI element transitions.
    /// </summary>
    public static class UIEasing
    {
        public static float Evaluate(EasingType type, float t, float overshoot = 1.2f)
        {
            t = Mathf.Clamp01(t);

            switch (type)
            {
                case EasingType.Linear:
                    return t;

                case EasingType.EaseInQuad:
                    return t * t;

                case EasingType.EaseOutQuad:
                    return 1f - (1f - t) * (1f - t);

                case EasingType.EaseInOutQuad:
                    return t < 0.5f 
                        ? 2f * t * t 
                        : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;

                case EasingType.EaseOutCubic:
                    return 1f - Mathf.Pow(1f - t, 3f);

                case EasingType.EaseInCubic:
                    return t * t * t;

                case EasingType.EaseInOutCubic:
                    return t < 0.5f 
                        ? 4f * t * t * t 
                        : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;

                case EasingType.EaseOutBack:
                {
                    float c1 = overshoot;
                    float c3 = c1 + 1f;
                    return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
                }

                case EasingType.EaseInBack:
                {
                    float c1 = overshoot;
                    float c3 = c1 + 1f;
                    return c3 * t * t * t - c1 * t * t;
                }

                case EasingType.EaseOutBounce:
                {
                    const float n1 = 7.5625f;
                    const float d1 = 2.75f;

                    if (t < 1f / d1)
                    {
                        return n1 * t * t;
                    }
                    if (t < 2f / d1)
                    {
                        t -= 1.5f / d1;
                        return n1 * t * t + 0.75f;
                    }
                    if (t < 2.5f / d1)
                    {
                        t -= 2.25f / d1;
                        return n1 * t * t + 0.9375f;
                    }
                    t -= 2.625f / d1;
                    return n1 * t * t + 0.984375f;
                }

                case EasingType.Spring:
                {
                    // Controlled spring settling without excessive oscillation
                    return 1f - Mathf.Exp(-6f * t) * Mathf.Cos(t * Mathf.PI * 2.5f);
                }

                default:
                    return t;
            }
        }
    }
}
