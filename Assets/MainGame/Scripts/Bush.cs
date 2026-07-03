using System.Collections;
using UnityEngine;

/// <summary>
/// Rustles a bush by playing a sprite-sheet animation when the player touches it.
/// The bush sits on its first (idle) frame until the player enters the trigger, then
/// plays through <see cref="m_Frames"/> once. Uses unscaled time so it animates correctly
/// during slow-motion, matching <see cref="SpriteSheetAnimator"/>.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Bush : MonoBehaviour
{
    [Header("Animation")]
    [Tooltip("Ordered frames of the rustle. Frame 0 is the idle/resting sprite.")]
    [SerializeField] private Sprite[] m_Frames;

    [Tooltip("How many frames to display per second.")]
    [SerializeField] private float m_FramesPerSecond = 12f;

    [Tooltip("Sprite renderer that shows the bush. Auto-fetched from this object if left empty.")]
    [SerializeField] private SpriteRenderer m_Renderer;

    [Header("Behaviour")]
    [Tooltip("Tag of the object that makes the bush rustle.")]
    [SerializeField] private string m_PlayerTag = "Player";

    [Tooltip("If true, the bush returns to its idle frame after the rustle finishes; otherwise it holds on the last frame.")]
    [SerializeField] private bool m_ReturnToIdleWhenDone = true;

    [Tooltip("If true, the bush can rustle every time the player enters; otherwise it rustles only once.")]
    [SerializeField] private bool m_Repeatable = true;

    private Coroutine m_Rustle;
    private bool m_HasPlayed;

    private void Awake()
    {
        if (m_Renderer == null)
            m_Renderer = GetComponent<SpriteRenderer>();

        if (m_Renderer == null)
            Debug.LogWarning($"[Bush] No SpriteRenderer found on '{name}'. Assign one or put the Bush on the object that has the SpriteRenderer.", this);

        if (m_Frames == null || m_Frames.Length == 0)
            Debug.LogWarning($"[Bush] No frames assigned on '{name}'. Fill the Frames array with the bush sprite-sheet slices.", this);

        SetIdle();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log($"[Bush] Trigger entered by '{other.name}' (tag '{other.tag}'). Looking for tag '{m_PlayerTag}'.", this);

        if (!other.CompareTag(m_PlayerTag))
            return;

        if (m_HasPlayed && !m_Repeatable)
            return;

        Play();
    }

    private void Play()
    {
        if (m_Renderer == null || m_Frames == null || m_Frames.Length == 0)
            return;

        m_HasPlayed = true;

        if (m_Rustle != null)
            StopCoroutine(m_Rustle);

        m_Rustle = StartCoroutine(RustleRoutine());
    }

    private IEnumerator RustleRoutine()
    {
        float frameDuration = 1f / Mathf.Max(m_FramesPerSecond, 0.01f);
        var wait = new WaitForSecondsRealtime(frameDuration);

        for (int i = 0; i < m_Frames.Length; i++)
        {
            m_Renderer.sprite = m_Frames[i];

            if (i < m_Frames.Length - 1)
                yield return wait;
        }

        if (m_ReturnToIdleWhenDone)
            SetIdle();

        m_Rustle = null;
    }

    private void SetIdle()
    {
        if (m_Renderer != null && m_Frames != null && m_Frames.Length > 0)
            m_Renderer.sprite = m_Frames[0];
    }
}
