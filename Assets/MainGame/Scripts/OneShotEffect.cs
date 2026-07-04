using System.Collections;
using UnityEngine;

/// <summary>
/// Plays a sprite-sheet animation once when spawned, then destroys its own GameObject.
/// Drop this on a prefab (with a <see cref="SpriteRenderer"/>) to make a transient VFX
/// such as jump dust, a landing puff, a pickup sparkle, etc. Instantiate the prefab at a
/// world position; it animates and cleans itself up — no manual Destroy needed.
///
/// Uses unscaled time so it plays correctly during slow-motion, matching the project's
/// other sprite animators (<see cref="PlayerAnimator"/>, <see cref="SpriteSheetAnimator"/>).
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class OneShotEffect : MonoBehaviour
{
    [Tooltip("Ordered frames of the effect, played once from first to last.")]
    [SerializeField] private Sprite[] m_Frames;

    [Tooltip("How many frames to display per second.")]
    [SerializeField] private float m_FramesPerSecond = 12f;

    [Tooltip("Extra seconds to hold the last frame on screen before destroying. 0 = destroy as soon as the last frame has shown.")]
    [SerializeField] private float m_LingerAfterLastFrame = 0f;

    private SpriteRenderer m_Renderer;

    private void Awake()
    {
        m_Renderer = GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        if (m_Frames == null || m_Frames.Length == 0)
        {
            Debug.LogWarning($"[OneShotEffect] No frames assigned on '{name}'. Destroying.", this);
            Destroy(gameObject);
            return;
        }

        StartCoroutine(PlayRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        float frameDuration = 1f / Mathf.Max(m_FramesPerSecond, 0.01f);
        var wait = new WaitForSecondsRealtime(frameDuration);

        for (int i = 0; i < m_Frames.Length; i++)
        {
            m_Renderer.sprite = m_Frames[i];
            yield return wait;
        }

        if (m_LingerAfterLastFrame > 0f)
            yield return new WaitForSecondsRealtime(m_LingerAfterLastFrame);

        Destroy(gameObject);
    }
}
