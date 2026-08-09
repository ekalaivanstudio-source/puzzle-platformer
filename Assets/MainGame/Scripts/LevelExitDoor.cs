using System.Collections;
using UnityEngine;

/// <summary>
/// The level's exit door. Touching it while the key is placed wins the level, but the
/// player does NOT stop on contact — they keep walking to <see cref="InteractionPosition"/>
/// first, so the level-complete sequence always plays with the player standing in the
/// doorway instead of frozen wherever their collider happened to clip the door's edge.
///
/// It also owns how the doorway LOOKS, the mirror of <see cref="LevelEntryDoor"/>: the open
/// animation <see cref="KeySlot"/> asks for when the key is placed, and the close
/// <see cref="PlayerController"/> awaits once the player has spun into it. Both live here
/// rather than on the slot so the frames, the renderer and the doorway effect are configured
/// in exactly one place — on the door they belong to.
///
/// Put this on the object tagged "Door" (the one carrying the door's trigger collider)
/// and point it at an empty child placed on the ground in the middle of the doorway.
/// </summary>
public class LevelExitDoor : MonoBehaviour
{
    [Tooltip("Where the player walks to after touching the open door. Normally an empty " +
             "child sitting on the ground at the centre of the doorway — the player's " +
             "pivot is at their feet, so place it at floor level. Falls back to this " +
             "object's own position when left empty.")]
    [SerializeField] private Transform m_InteractionPoint;

    [Header("Door Animation")]
    [Tooltip("Doorway sprite renderer whose sprite the open and close animations drive.")]
    [SerializeField] private SpriteRenderer m_DoorRenderer;

    [Tooltip("Door open animation frames. Frame 0 is the closed state; the last frame is " +
             "fully open. Closing plays the same frames backwards.")]
    [SerializeField] private Sprite[] m_DoorOpenFrames;

    [Tooltip("Seconds each frame of the door animation is shown.")]
    [SerializeField] private float m_FrameTime = 0.06f;

    [Header("Door Open Effect")]
    [Tooltip("Looping effect sitting in the doorway, shown once the door has finished " +
             "opening and hidden again when it closes or the slot resets. Optional. A child " +
             "of the door authored in place — it is only toggled here, never spawned, so its " +
             "position, scale and sorting are whatever the scene shows.")]
    [SerializeField] private GameObject m_DoorOpenEffect;

    private Coroutine m_Animation;

    /// <summary>World position the player walks to before the level completes.</summary>
    public Vector2 InteractionPosition =>
        m_InteractionPoint != null ? (Vector2)m_InteractionPoint.position : (Vector2)transform.position;

    /// <summary>
    /// Shut, instantly and silently: any animation in flight is dropped, the doorway goes
    /// back to its closed frame and the doorway effect goes out. The slot's own set-up and
    /// reset use this — it is the state a level starts in.
    /// </summary>
    public void SetClosed()
    {
        StopAnimation();
        SetFrame(0);
        ShowEffect(false);
    }

    /// <summary>
    /// Plays the open animation and holds on the last frame, revealing the doorway effect as
    /// it finishes — the doorway only reads as open at that point, so the effect would
    /// otherwise glow through a still-shut door. Called by <see cref="KeySlot"/> when the key
    /// goes in. A door authored with no frames simply lights up straight away.
    /// </summary>
    public void Open()
    {
        if (!HasFrames())
        {
            ShowEffect(true);
            return;
        }

        StopAnimation();
        m_Animation = StartCoroutine(AnimateRoutine(opening: true));
    }

    /// <summary>
    /// Plays the open animation backwards, shutting the doorway behind the player who has
    /// just spun into it, and awaited by <see cref="PlayerController"/>'s win sequence so the
    /// level's outro only carries on once the door is closed. The doorway effect goes out
    /// first: it belongs to an open doorway, and would otherwise glow through the shut door.
    /// </summary>
    public IEnumerator CloseRoutine()
    {
        ShowEffect(false);

        if (!HasFrames())
        {
            SetFrame(0);
            yield break;
        }

        StopAnimation();
        yield return AnimateRoutine(opening: false);
    }

    // Plays m_DoorOpenFrames start to finish, or finish to start when closing, and holds on
    // the frame it ends on.
    private IEnumerator AnimateRoutine(bool opening)
    {
        int count = m_DoorOpenFrames.Length;

        for (int i = 0; i < count; i++)
        {
            SetFrame(opening ? i : count - 1 - i);

            if (i < count - 1 && m_FrameTime > 0f)
                yield return new WaitForSeconds(m_FrameTime);
        }

        if (opening)
            ShowEffect(true);

        m_Animation = null;
    }

    private void StopAnimation()
    {
        if (m_Animation == null) return;

        StopCoroutine(m_Animation);
        m_Animation = null;
    }

    private bool HasFrames() =>
        m_DoorRenderer != null && m_DoorOpenFrames != null && m_DoorOpenFrames.Length > 0;

    private void SetFrame(int index)
    {
        if (!HasFrames()) return;

        m_DoorRenderer.sprite = m_DoorOpenFrames[Mathf.Clamp(index, 0, m_DoorOpenFrames.Length - 1)];
    }

    private void ShowEffect(bool visible)
    {
        if (m_DoorOpenEffect != null)
            m_DoorOpenEffect.SetActive(visible);
    }

    // Draws the walk destination so the point can be placed without entering play mode.
    private void OnDrawGizmosSelected()
    {
        Vector2 point = InteractionPosition;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(point, 0.2f);
        Gizmos.DrawLine(point + Vector2.left * 0.35f, point + Vector2.right * 0.35f);
    }
}
