using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class SpriteAnimation
{
    public string AnimationName;
    public List<Sprite> Frames;
}

public class UIImageAnimator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image m_Image;
    [SerializeField] private UIFloatEffect m_UIFloatEffect;

    [Header("Animations")]
    [SerializeField] private List<SpriteAnimation> m_Animations = new();

    [Header("Settings")]
    [SerializeField] private float m_FramesPerSecond = 10f;
    [SerializeField] private bool m_Loop = true;

    private SpriteAnimation m_CurrentAnimation;
    private int m_CurrentFrame;
    private float m_Timer;
    private bool m_IsPlaying;

    private void Update()
    {
        if (!m_IsPlaying || m_CurrentAnimation == null)
            return;

        if (m_CurrentAnimation.Frames == null || m_CurrentAnimation.Frames.Count <= 1)
            return;

        m_Timer += Time.unscaledDeltaTime;

        float frameDuration = 1f / Mathf.Max(m_FramesPerSecond, 0.01f);

        if (m_Timer < frameDuration)
            return;

        m_Timer %= frameDuration;
        m_CurrentFrame++;

        if (m_CurrentFrame >= m_CurrentAnimation.Frames.Count)
        {
            if (m_Loop)
            {
                m_CurrentFrame = 0;
            }
            else
            {
                m_CurrentFrame = m_CurrentAnimation.Frames.Count - 1;
                m_IsPlaying = false;
                return;
            }
        }

        ApplyFrame();
    }

    public void PlayAnimation(string animationName)
    {
        SpriteAnimation animation = m_Animations.Find(a => a.AnimationName == animationName);

        if (animation == null)
        {
            Debug.LogWarning($"Animation '{animationName}' not found.");
            return;
        }

        m_CurrentAnimation = animation;
        m_CurrentFrame = 0;
        m_Timer = 0f;
        m_IsPlaying = true;

        ApplyFrame();
        m_UIFloatEffect?.Play();
    }

    public void StopAnimation()
    {
        m_IsPlaying = false;
    }

    private void ApplyFrame()
    {
        if (m_Image == null ||
            m_CurrentAnimation == null ||
            m_CurrentAnimation.Frames.Count == 0)
            return;

        m_Image.sprite = m_CurrentAnimation.Frames[m_CurrentFrame];
    }
}