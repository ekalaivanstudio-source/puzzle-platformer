using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class SpriteAnimation
{
    public EvilDoctorAnimationController.DoctorAnimation AnimationType;

    public List<Sprite> Frames = new();

    [Min(1)]
    public float FPS = 12f;
}

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

    private void Awake()
    {
        foreach (var animation in m_Animations)
        {
            if (!m_AnimationMap.ContainsKey(animation.AnimationType))
            {
                m_AnimationMap.Add(animation.AnimationType, animation);
            }
        }

        if (m_UIFloatEffect != null)
        {
            m_UIFloatEffect.OnReachedTop += PlayPendingAnimation;
            m_UIFloatEffect.OnBeforeMoveDown += StopAnimation;
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

    public void ShowReaction(EvilDoctorAnimationController.DoctorAnimation animation)
    {
        m_PendingAnimation = animation;
        m_UIFloatEffect.Play();
    }

    private void PlayPendingAnimation()
    {
        PlayAnimation(m_PendingAnimation);
    }

    private void PlayAnimation(EvilDoctorAnimationController.DoctorAnimation animationType)
    {
        if (!m_AnimationMap.TryGetValue(animationType, out SpriteAnimation animation))
        {
            Debug.LogWarning($"Animation {animationType} not found.");
            return;
        }

        if (m_PlayRoutine != null)
            StopCoroutine(m_PlayRoutine);

        m_PlayRoutine = StartCoroutine(PlayRoutine(animation));
    }

    private IEnumerator PlayRoutine(SpriteAnimation animation)
    {
        if (animation.Frames == null || animation.Frames.Count == 0)
            yield break;

        float frameDuration = 1f / animation.FPS;

        while (true)
        {
            for (int i = 0; i < animation.Frames.Count; i++)
            {
                m_Image.sprite = animation.Frames[i];

                yield return new WaitForSecondsRealtime(frameDuration);
            }
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
}