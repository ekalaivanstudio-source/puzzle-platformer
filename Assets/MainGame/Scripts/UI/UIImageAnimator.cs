using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Plays frame-based <see cref="SpriteAnimation"/> clips on a UI <see cref="Image"/>,
/// driven by a <see cref="UIFloatEffect"/>: the clip starts when the element reaches
/// the top and stops as it begins to descend. <see cref="IsPlaying"/> tracks the full
/// reaction so callers can await it.
/// </summary>
public class UIImageAnimator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image m_Image;
    [SerializeField] private UIFloatEffect m_UIFloatEffect;

    [Header("Animations")]
    [SerializeField] private List<SpriteAnimation> m_Animations = new();

    private readonly Dictionary<EvilDoctorAnimationController.DoctorAnimation, SpriteAnimation>
        m_AnimationMap = new();

    private Coroutine m_PlayRoutine;
    private EvilDoctorAnimationController.DoctorAnimation m_PendingAnimation;

    /// <summary>True while the reaction (its driving float cycle) is in progress.</summary>
    public bool IsPlaying => m_UIFloatEffect != null && m_UIFloatEffect.IsPlaying;

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (m_Image == null)
            Debug.LogError($"[{nameof(UIImageAnimator)}] {nameof(m_Image)} is not assigned.", this);

        BuildAnimationMap();

        if (m_UIFloatEffect != null)
        {
            m_UIFloatEffect.OnReachedTop += PlayPendingAnimation;
            m_UIFloatEffect.OnBeforeMoveDown += StopAnimation;
        }
        else
        {
            Debug.LogWarning($"[{nameof(UIImageAnimator)}] {nameof(m_UIFloatEffect)} is not assigned.", this);
        }
    }

    private void OnDestroy()
    {
        if (m_UIFloatEffect != null)
        {
            m_UIFloatEffect.OnReachedTop -= PlayPendingAnimation;
            m_UIFloatEffect.OnBeforeMoveDown -= StopAnimation;
        }
    }

    private void BuildAnimationMap()
    {
        foreach (var animation in m_Animations)
        {
            if (animation == null) continue;

            if (m_AnimationMap.ContainsKey(animation.AnimationType))
            {
                Debug.LogWarning($"[{nameof(UIImageAnimator)}] Duplicate animation '{animation.AnimationType}' ignored.", this);
                continue;
            }

            m_AnimationMap.Add(animation.AnimationType, animation);
        }
    }

    // ─── Public API ───────────────────────────────────────────────────────────

    /// <summary>Queues a reaction and starts the float effect that drives it.</summary>
    public void ShowReaction(EvilDoctorAnimationController.DoctorAnimation animation)
    {
        m_PendingAnimation = animation;

        if (m_UIFloatEffect != null)
        {
            m_UIFloatEffect.Play();
        }
        else
        {
            // No float effect to drive the start/stop — play directly so the reaction
            // is at least visible (it will loop until the next ShowReaction/StopAnimation).
            PlayPendingAnimation();
        }
    }

    public void StopAnimation()
    {
        if (m_PlayRoutine != null)
        {
            StopCoroutine(m_PlayRoutine);
            m_PlayRoutine = null;
        }
    }

    // ─── Playback ─────────────────────────────────────────────────────────────

    private void PlayPendingAnimation() => PlayAnimation(m_PendingAnimation);

    private void PlayAnimation(EvilDoctorAnimationController.DoctorAnimation animationType)
    {
        if (m_Image == null) return;

        if (!m_AnimationMap.TryGetValue(animationType, out SpriteAnimation animation))
        {
            Debug.LogWarning($"[{nameof(UIImageAnimator)}] Animation '{animationType}' not found.", this);
            return;
        }

        if (!animation.HasFrames)
        {
            Debug.LogWarning($"[{nameof(UIImageAnimator)}] Animation '{animationType}' has no frames.", this);
            return;
        }

        if (m_PlayRoutine != null)
            StopCoroutine(m_PlayRoutine);

        m_PlayRoutine = StartCoroutine(PlayRoutine(animation));
    }

    private IEnumerator PlayRoutine(SpriteAnimation animation)
    {
        var wait = new WaitForSecondsRealtime(animation.FrameDuration);

        while (true)
        {
            for (int i = 0; i < animation.Frames.Count; i++)
            {
                m_Image.sprite = animation.Frames[i];
                yield return wait;
            }
        }
    }
}
