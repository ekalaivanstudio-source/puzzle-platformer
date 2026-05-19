using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Plays through a sprite array as a frame-by-frame animation.
/// Works on both <see cref="SpriteRenderer"/> (world-space) and UI <see cref="Image"/> components.
/// Uses unscaled time so the animation runs correctly even during slow-motion.
/// </summary>
public class SpriteSheetAnimator : MonoBehaviour
{
    [Tooltip("Ordered array of sprites to cycle through (frames of the animation).")]
    [SerializeField] private Sprite[] m_Frames;

    [Tooltip("How many frames to display per second.")]
    [SerializeField] private float m_FramesPerSecond = 4f;

    [Tooltip("If true, the animation loops continuously; otherwise it stops on the last frame.")]
    [SerializeField] private bool m_Loop = true;

    private SpriteRenderer m_SpriteRenderer;
    private Image m_Image;
    private float m_Timer;
    private int m_CurrentFrame;

    // ─── Lifecycle ───────────────────────────────────────────────────────────

    private void Awake()
    {
        m_SpriteRenderer = GetComponent<SpriteRenderer>();
        m_Image = GetComponent<Image>();
    }

    private void OnEnable()
    {
        // Restart from frame 0 each time the object becomes active
        m_CurrentFrame = 0;
        m_Timer = 0f;
        ApplyFrame();
    }

    private void Update()
    {
        if (m_Frames == null || m_Frames.Length <= 1) return;

        // Unscaled so animation plays correctly during slow-motion
        m_Timer += Time.unscaledDeltaTime;

        float frameDuration = 1f / Mathf.Max(m_FramesPerSecond, 0.01f);

        if (m_Timer < frameDuration) return;

        // Absorb all accumulated overflow so startup lag doesn't cause a burst of frame advances
        m_Timer %= frameDuration;
        m_CurrentFrame++;

        if (m_CurrentFrame >= m_Frames.Length)
        {
            if (m_Loop)
                m_CurrentFrame = 0;
            else
            {
                m_CurrentFrame = m_Frames.Length - 1;
                enabled = false; // stop updating once the last frame is reached
                return;
            }
        }

        ApplyFrame();
    }

    // ─── Public API ──────────────────────────────────────────────────────────

    /// <summary>Restarts the animation from frame 0.</summary>
    public void Restart()
    {
        m_CurrentFrame = 0;
        m_Timer = 0f;
        enabled = true;
        ApplyFrame();
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private void ApplyFrame()
    {
        if (m_Frames == null || m_Frames.Length == 0) return;

        Sprite frame = m_Frames[m_CurrentFrame];
        if (m_SpriteRenderer != null) m_SpriteRenderer.sprite = frame;
        // Use overrideSprite for UI Image: swaps the visual without affecting layout
        if (m_Image != null) m_Image.overrideSprite = frame;
    }
}
